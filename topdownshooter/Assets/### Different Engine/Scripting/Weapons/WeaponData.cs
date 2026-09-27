using System.Collections.Generic;
using UnityEngine;

// what kind of harm a weapon does. every weapon is one or the other; StatContext keeps a damage
// multiplier per class, the hook for class passives, characters and resistances later
public enum AttackClass { Physical, Magical }

// a weapon's level up card and all of its settings in one asset. because it's an UpgradeData,
// the level up pool finds it on its own: the first pick adds the weapon to the player and every
// pick after levels it up. the card text is written from the level table, so it can't drift.
// the inherited Value, Additive, Max Level and Overcharge fields don't apply to weapons
public abstract class WeaponData : UpgradeData
{
    [Header("Evolution")]
    [Tooltip("the weapon the player must also hold before the evolution is offered: the Bow, the Electrical Aura, the Meteorite or any weapon asset. empty = any other weapon will do")]
    public UpgradeData evolutionPartner;
    [Tooltip("the icon on the evolution's level up card. empty keeps the weapon's own")]
    public Sprite evolvedIcon;
    public Sprite[] evolvedIconFrames = System.Array.Empty<Sprite>();
    [Tooltip("the evolution's name in the run's stats. empty = \"Evolved\" and the weapon's name")]
    public string evolvedTitle;

    [Header("Worn on the back (the starting weapon of whoever carries it; see BackWeapon)")]
    [Tooltip("the weapon slung across a character's back, at the characters' pixel size, looping. Tools > VFX > Build Weapon FX sets these")]
    public Sprite[] backFrames = System.Array.Empty<Sprite>();
    [Min(0.1f)] public float backFps = 6f;

    public abstract int LevelCount { get; }
    public override UpgradeCategory Category => UpgradeCategory.Weapon;

    // its attack class, set by each weapon in code: arrows and blades are Physical, talismans,
    // spells and summoned things Magical
    public virtual AttackClass AttackClass => AttackClass.Physical;
    public override string CategoryLabel => $"{Category} ({AttackClass})";
    public override Sprite CardIcon => NextPickEvolves && evolvedIcon != null ? evolvedIcon : icon;
    public override Sprite[] CardIconFrames => NextPickEvolves && evolvedIcon != null ? evolvedIconFrames : iconFrames;
    public override int MaxLevel => Mathf.Max(1, LevelCount);

    // the level that is the evolution, or 0 for a weapon that doesn't evolve
    public virtual int EvolutionLevel => 0;
    public bool NextPickEvolves => EvolutionLevel > 0 && Level + 1 == EvolutionLevel;

    public string GetEvolvedTitle() => string.IsNullOrEmpty(evolvedTitle) ? "Evolved " + GetBaseTitle() : evolvedTitle;

    public abstract Weapon AddTo(GameObject owner);

    // one line of a weapon's numbers, as the attribute tooltip shows it (ItemTooltip)
    public readonly struct Attribute
    {
        public readonly string label, value;
        public Attribute(string label, string value) { this.label = label; this.value = value; }
    }

    // the weapon's numbers at a level, with the player's stats applied (Might, its attack class,
    // Cooldown, Area) when there's a StatContext: what it actually does right now
    public virtual void Attributes(int level, StatContext stats, List<Attribute> into) { }

    protected static float Hurt(StatContext s, float damage, AttackClass c) => s != null ? damage * s.MightMul * s.ClassMul(c) : damage;
    protected static float Every(StatContext s, float seconds, UpgradeType type = UpgradeType.Weapon) => s != null ? s.CooldownFor(type, seconds) : seconds;
    protected static float Wide(StatContext s, float radius) => s != null ? radius * s.AreaMul : radius;
    protected static string N(float v) => v.ToString(v >= 100f ? "0" : "0.#");
    protected static string Sec(float v) => v.ToString("0.##") + "s";
    protected static void Add(List<Attribute> into, string label, string value) => into.Add(new Attribute(label, value));

    public override string GetLevelProgress() => NextPickEvolves ? "Evolution" : Level == 0 ? "New" : $"Lv. {Level + 1}";

    // card text for a level, 1 being the pick that unlocks the weapon
    public abstract string Describe(int level);

    // the last card before the evolution says how it evolves, so it can be planned for
    public override string GetDisplayDescription()
    {
        string text = Describe(Level + 1);
        if (EvolutionLevel > 0 && Level + 2 == EvolutionLevel)
        {
            string partner = evolutionPartner != null ? evolutionPartner.GetBaseTitle() : "another weapon";
            text += PlayerInventory.EvolvesFromEnvelopes
                ? $"\nMaxed, it evolves from a fortune envelope while you hold {partner}."
                : $"\nEvolves while you hold {partner}.";
        }
        return text;
    }

    protected virtual void OnEnable()
    {
        type = UpgradeType.Weapon;
        overcharge = false;
    }
}

// what a weapon's settings inherit: names the behaviour to put on the player
public abstract class WeaponData<TWeapon> : WeaponData where TWeapon : Weapon
{
    public override Weapon AddTo(GameObject owner)
    {
        var weapon = owner.AddComponent<TWeapon>();
        weapon.Init(this);
        return weapon;
    }
}
