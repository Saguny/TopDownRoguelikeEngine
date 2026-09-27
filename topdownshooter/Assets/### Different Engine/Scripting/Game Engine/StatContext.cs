using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class StatContext : MonoBehaviour
{
    [FormerlySerializedAs("bulletSpeedMul")] public float arrowSpeedMul = 1f;
    public float pickupRadiusMul = 1f;
    [FormerlySerializedAs("bulletDamageMul")] public float arrowDamageMul = 1f;
    public float healthMul = 1f;   // 🩸 NEU

    [FormerlySerializedAs("bulletCountAdd")] public int arrowCountAdd = 0;

    public float critChance = 0f;
    public float critMultiplier = 2f;
    public int pierceAdd = 0;
    public float moveSpeedMul = 1f;

    // overcharge is kept apart from the level multipliers and recomputed from its stack count,
    // never compounded, which is what keeps it bounded
    private readonly Dictionary<UpgradeType, float> overcharge = new Dictionary<UpgradeType, float>();
    private float baseMaxHealth = -1f;

    // cooldowns come in two layers: the global Cooldown stat and each weapon's own cooldown
    // upgrade. picks inside a layer add up (8% + 8% = 16%), so every pick is worth the same and
    // reads honestly in the menu; the two layers multiply, and each is capped so nothing can
    // reach a zero cooldown
    public const float MaxCooldownReduction = 0.6f;
    private readonly Dictionary<UpgradeType, float> cooldownReduction = new Dictionary<UpgradeType, float>();

    // global Area and Weapon Speed, same shape as the cooldown layer: picks add up (+10% + +10%)
    // and multiply with each weapon's own radius or speed upgrades
    private readonly Dictionary<UpgradeType, float> globalBonus = new Dictionary<UpgradeType, float>();

    // bonuses from the picked character and the global upgrade shop, set once at spawn from the
    // StatSheet. ResetStats leaves these alone: they belong to the run, not to a level up
    private readonly float[] sheetBonus = new float[StatSheet.Count];

    public float SheetBonus(StatId id) => sheetBonus[(int)id];

    // area and weapon speed from the sheet join the in-run bucket, so +10% there and +10% from
    // picks read as +20%
    public float AreaMul => 1f + GlobalBonus(UpgradeType.Area) + SheetBonus(StatId.Area);
    public float WeaponSpeedMul => 1f + GlobalBonus(UpgradeType.WeaponSpeed) + SheetBonus(StatId.WeaponSpeed);

    // the shop's Might and the Might passive add up; in endless the passive keeps going as overcharge
    public float MightMul => Mathf.Max(0f, 1f + SheetBonus(StatId.Might) + GlobalBonus(UpgradeType.Might)) * OC(UpgradeType.Might);
    public int ArrowCountTotal => arrowCountAdd + Mathf.RoundToInt(SheetBonus(StatId.ArrowCount));
    public int PierceTotal => pierceAdd + Mathf.RoundToInt(SheetBonus(StatId.Pierce));
    public float CritChanceTotal => Mathf.Clamp01(critChance + SheetBonus(StatId.CritChance));
    public float CritMultiplierTotal => critMultiplier + OCBonus(UpgradeType.CritDamage) + SheetBonus(StatId.CritDamage);
    public float MoveSpeedTotal => moveSpeedMul * Mathf.Max(0f, 1f + SheetBonus(StatId.MoveSpeed));
    public float PickupRadiusTotal => pickupRadiusMul * Mathf.Max(0f, 1f + SheetBonus(StatId.Magnet));
    public float GrowthMul => Mathf.Max(0f, 1f + SheetBonus(StatId.Growth));

    private float GlobalBonus(UpgradeType type) => globalBonus.TryGetValue(type, out var b) ? b : 0f;

    public float Reduction(UpgradeType type) =>
        cooldownReduction.TryGetValue(type, out var r) ? Mathf.Min(r, MaxCooldownReduction) : 0f;

    // weaponCooldown is the weapon's own layer: ArrowCooldown, AuraCooldown or AOEAttackCooldown.
    // endless overcharge on the global stat works as extra attack rate on top. the sheet's
    // cooldown is a third layer with its own cap, so it never eats into the in-run 60%
    public float CooldownFor(UpgradeType weaponCooldown, float baseCooldown)
    {
        return baseCooldown
            * (1f - Reduction(UpgradeType.Cooldown))
            * (1f - Reduction(weaponCooldown))
            * (1f - Mathf.Min(SheetBonus(StatId.Cooldown), MaxCooldownReduction))
            / OC(UpgradeType.Cooldown);
    }

    // any weapon's hit can crit: the Steady Hands and Executioner passives plus the sheet's crit
    // stats. returns the damage to deal
    public float WithCrit(float damage, out bool crit)
    {
        float chance = CritChanceTotal;
        crit = chance > 0f && Random.value < chance;
        return crit ? damage * CritMultiplierTotal : damage;
    }

    // what the stat panel shows mid run: the sheet plus everything picked so far. with a pick,
    // the value as it would be after taking it, for the level up screen's preview. false for
    // stats nothing in the run changes; those keep the sheet's value
    public bool TryLive(StatId id, UpgradeData pick, out float value)
    {
        bool Takes(UpgradeType t) => pick != null && pick.type == t && !pick.IsAtCap;
        float Mul(UpgradeType t) => Takes(t) ? (pick.additive ? 1f + pick.value : pick.value) : 1f;
        float Add(UpgradeType t) => Takes(t) ? pick.value : 0f;
        bool Overcharges(UpgradeType t) => pick != null && pick.type == t && pick.IsOvercharging;
        float Oc(UpgradeType t) => Overcharges(t) ? 1f + pick.OverchargeBonus(pick.OverchargeStacks + 1) : OC(t);
        var health = GetComponent<PlayerHealth>();

        switch (id)
        {
            case StatId.MaxHealth when health != null:
                value = health.Max * Mul(UpgradeType.MaxHealth) / OC(UpgradeType.MaxHealth) * Oc(UpgradeType.MaxHealth);
                return true;
            case StatId.Recovery when health != null: value = health.regenPerSecond; return true;
            case StatId.Armor when health != null: value = health.armor; return true;
            case StatId.Revival when health != null: value = health.revivals; return true;
            case StatId.MoveSpeed:
                value = moveSpeedMul * Mul(UpgradeType.MoveSpeed) * Mathf.Max(0f, 1f + SheetBonus(StatId.MoveSpeed)) - 1f;
                return true;
            case StatId.Might:
                value = Mathf.Max(0f, 1f + SheetBonus(StatId.Might) + GlobalBonus(UpgradeType.Might) + Add(UpgradeType.Might)) * Oc(UpgradeType.Might) - 1f;
                return true;
            case StatId.Cooldown:
                cooldownReduction.TryGetValue(UpgradeType.Cooldown, out var taken);
                value = 1f - (1f - Mathf.Min(taken + Add(UpgradeType.Cooldown), MaxCooldownReduction))
                           * (1f - Mathf.Min(SheetBonus(StatId.Cooldown), MaxCooldownReduction)) / Oc(UpgradeType.Cooldown);
                return true;
            case StatId.Area: value = GlobalBonus(UpgradeType.Area) + Add(UpgradeType.Area) + SheetBonus(StatId.Area); return true;
            case StatId.WeaponSpeed: value = GlobalBonus(UpgradeType.WeaponSpeed) + Add(UpgradeType.WeaponSpeed) + SheetBonus(StatId.WeaponSpeed); return true;
            case StatId.ArrowCount: value = ArrowCountTotal; return true;
            case StatId.Pierce: value = PierceTotal + Mathf.RoundToInt(Add(UpgradeType.Pierce)); return true;
            case StatId.CritChance: value = Mathf.Clamp01(CritChanceTotal + Add(UpgradeType.CritChance)); return true;
            case StatId.CritDamage:
                value = critMultiplier + Add(UpgradeType.CritDamage) + SheetBonus(StatId.CritDamage)
                        + (Overcharges(UpgradeType.CritDamage) ? pick.OverchargeBonus(pick.OverchargeStacks + 1) : OCBonus(UpgradeType.CritDamage));
                return true;
            case StatId.Magnet:
                value = pickupRadiusMul * Mul(UpgradeType.PickupRadius) * Mathf.Max(0f, 1f + SheetBonus(StatId.Magnet)) - 1f;
                return true;
            case StatId.Growth: value = GrowthMul - 1f; return true;
        }
        value = 0f;
        return false;
    }

    private void Start() => ApplySheet(StatSheet.ForRun());

    // who this run is played as; weapons read their signature perks off it
    public CharacterData Character { get; private set; }

    public void ApplySheet(StatSheet sheet)
    {
        for (int i = 0; i < sheetBonus.Length; i++) sheetBonus[i] = sheet.Bonus((StatId)i);

        var health = GetComponent<PlayerHealth>();
        if (health != null)
        {
            // max health is the one stat the sheet sets outright; the rest ride on the scene's values
            if (sheet.Catalog.Get(StatId.MaxHealth) != null) health.SetMax(sheet[StatId.MaxHealth], true);
            health.regenPerSecond = Mathf.Max(0f, sheet[StatId.Recovery]);
            health.armor = Mathf.Max(0f, sheet[StatId.Armor]);
            health.revivals = Mathf.Max(0, Mathf.RoundToInt(sheet[StatId.Revival]));
        }

        var character = sheet.Character;
        Character = character;
        if (character != null && character.animations != null && TryGetComponent(out Animator animator))
            animator.runtimeAnimatorController = character.animations;

        var inventory = GetComponent<PlayerInventory>();
        if (inventory != null)
            inventory.GrantStartingWeapon(character != null ? character.startingWeapon : null);
    }

    // multiplier for multiplicative stats, e.g. 1.33 for +33% damage
    public float OC(UpgradeType type) => overcharge.TryGetValue(type, out var b) ? 1f + b : 1f;

    // raw bonus for additive stats, e.g. +0.4 on the crit multiplier
    public float OCBonus(UpgradeType type) => overcharge.TryGetValue(type, out var b) ? b : 0f;

    public void SetOvercharge(UpgradeType type, float bonus)
    {
        overcharge[type] = bonus;
        if (type == UpgradeType.MaxHealth) ApplyHealthUpgrade(false);
    }

    public void ResetStats()
    {
        cooldownReduction.Clear();
        globalBonus.Clear();
        arrowSpeedMul = 1f;
        pickupRadiusMul = 1f;
        arrowDamageMul = 1f;
        healthMul = 1f;  // 🩸 NEU
        arrowCountAdd = 0;
        critChance = 0f;
        critMultiplier = 2f;
        pierceAdd = 0;
        moveSpeedMul = 1f;
        overcharge.Clear();
    }

    public void Apply(UpgradeData u)
    {
        switch (u.type)
        {
            case UpgradeType.Area:
            case UpgradeType.WeaponSpeed:
            case UpgradeType.Might:
                globalBonus.TryGetValue(u.type, out var bonusHad);
                globalBonus[u.type] = bonusHad + u.value;
                break;
            case UpgradeType.Cooldown:
            case UpgradeType.ArrowCooldown:
            case UpgradeType.AuraCooldown:
            case UpgradeType.AOEAttackCooldown:
                cooldownReduction.TryGetValue(u.type, out var had);
                cooldownReduction[u.type] = had + u.value;
                break;
            case UpgradeType.ArrowSpeed:
                arrowSpeedMul *= u.additive ? (1f + u.value) : u.value;
                break;
            case UpgradeType.ArrowCount:
                arrowCountAdd += Mathf.RoundToInt(u.value);
                break;
            case UpgradeType.PickupRadius:
                pickupRadiusMul *= u.additive ? (1f + u.value) : u.value;
                break;
            case UpgradeType.ArrowDamage:
                arrowDamageMul *= u.additive ? (1f + u.value) : u.value;
                break;
            case UpgradeType.MaxHealth: // 🩸 NEU
                healthMul *= u.additive ? (1f + u.value) : u.value;
                ApplyHealthUpgrade(true);
                break;
            case UpgradeType.AuraUnlock:
            case UpgradeType.AuraDamage:
            case UpgradeType.AuraRadius:
                break;
            case UpgradeType.CritChance:
                critChance = Mathf.Clamp01(critChance + u.value);
                break;
            case UpgradeType.CritDamage:
                critMultiplier += u.value;
                break;
            case UpgradeType.Pierce:
                pierceAdd += Mathf.RoundToInt(u.value);
                break;
            case UpgradeType.MoveSpeed:
                moveSpeedMul *= u.additive ? (1f + u.value) : u.value;
                break;
            case UpgradeType.AOEAttack:
                // Dieses Upgrade wird im PlayerInventory direkt gehandhabt.
                break;


        }
    }

    private void ApplyHealthUpgrade(bool fillToMax)
    {
        var playerHealth = GetComponent<PlayerHealth>();
        if (playerHealth == null) return;

        // capture the unupgraded max once. this used to multiply the already upgraded Max by the
        // cumulative multiplier, so three x1.2 picks gave 2.99x instead of 1.73x
        if (baseMaxHealth < 0f) baseMaxHealth = playerHealth.Max;

        float newMax = baseMaxHealth * healthMul * OC(UpgradeType.MaxHealth);
        float gained = newMax - playerHealth.Max;

        playerHealth.SetMax(newMax, fillToMax);

        // overcharge doesn't full heal, but raising max shouldn't read as losing health either
        if (!fillToMax && gained > 0f) playerHealth.Heal(gained, false);
    }
}
