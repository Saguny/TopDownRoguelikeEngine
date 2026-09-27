using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using System.Collections.Generic;

public class PlayerInventory : MonoBehaviour
{
    [FormerlySerializedAs("gearCount")] public int wenCount = 0;
    [FormerlySerializedAs("gearsForUpgrade")] public int wenForUpgrade = 5;
    [FormerlySerializedAs("gearIncreaseMultiplier")] public float wenIncreaseMultiplier = 1.3f;

    public List<UpgradeData> allUpgrades;
    public UpgradeMenuUI upgradeMenuUI;

    [SerializeField] private ProgressBarGradient progressBarGradient;
    [SerializeField] private DifficultyCurve difficultyCurve;

    private List<UpgradeData> runtimeUpgrades = new List<UpgradeData>();
    // this run's copy of each upgrade asset, so a character's starting weapon finds its copy
    private readonly Dictionary<UpgradeData, UpgradeData> runtimeFor = new Dictionary<UpgradeData, UpgradeData>();
    private int currentLevel = 1;
    private StatContext stats;

    // weapons added through WeaponData upgrades, by their upgrade
    private readonly Dictionary<UpgradeData, Weapon> weapons = new Dictionary<UpgradeData, Weapon>();

    public int totalKills = 0;

    [Header("Slots (a normal run; Endless holds everything)")]
    [SerializeField, Min(1)] private int weaponSlots = 6;
    [SerializeField, Min(1)] private int passiveSlots = 6;
    [Tooltip("how much likelier an upgrade for a weapon already held is to be offered: 0.1 = 10%")]
    [SerializeField, Min(0f)] private float heldWeaponBonus = 0.1f;

    // how many weapons and passives the run can hold; 0 = no limit (Endless)
    public int WeaponSlots => GameMode.IsEndless ? 0 : weaponSlots;
    public int PassiveSlots => GameMode.IsEndless ? 0 : passiveSlots;
    public int WeaponsHeld => CountTaken(UpgradeCategory.Weapon);
    public int PassivesHeld => CountTaken(UpgradeCategory.Passive);

    private int CountTaken(UpgradeCategory category)
    {
        int n = 0;
        foreach (var u in taken) if (u != null && u.Category == category) n++;
        return n;
    }

    private bool levelingLocked;

    public int CurrentLevel => currentLevel;

    // for the dev tools: every upgrade this run can offer, and taking one without the level up
    // menu. the pool's rules (owning the weapon first, evolution partners) don't apply here
    public IReadOnlyList<UpgradeData> RunUpgrades => runtimeUpgrades;

    // everything picked this run, in the order it was first taken (the starting weapon first)
    private readonly List<UpgradeData> taken = new List<UpgradeData>();
    public IReadOnlyList<UpgradeData> Taken => taken;

    public void TakeUpgrade(UpgradeData upgrade)
    {
        if (upgrade == null || !upgrade.CanOffer) return;
        float timeScale = Time.timeScale;   // ApplyUpgrade resumes time for the level up menu
        ApplyUpgrade(upgrade);
        Time.timeScale = timeScale;
    }

    private void OnEnable()
    {
        BuildRuntimeUpgrades();
        RefreshWenRequirement();
        UpdateProgress();
        GameEvents.OnEnemyKilled += HandleEnemyKilled;
        GameEvents.OnFinalBossDefeated += LockLeveling;
        PlayerHealth.OnPlayerDied += LockOnDeath;
    }

    // once the final boss is down the run is decided, so the purge's kills shouldn't pop
    // upgrade menus on the walk to the exit
    private void LockLeveling(Vector3 _) => levelingLocked = true;

    // nor for the kills of meteors still falling after the player is down
    private void LockOnDeath() => levelingLocked = true;

    private void OnDisable()
    {
        foreach (var u in allUpgrades) if (u != null) u.ResetLevel();
        runtimeUpgrades.Clear();
        GameEvents.OnEnemyKilled -= HandleEnemyKilled;
        GameEvents.OnFinalBossDefeated -= LockLeveling;
        PlayerHealth.OnPlayerDied -= LockOnDeath;
    }

    private void Awake()
    {
        stats = GetComponent<StatContext>();
    }

    private void Start() => RefreshWenRequirement();

    private void HandleEnemyKilled(int count)
    {
        if (count <= 0) return;

        totalKills += count;
        AddWen(count);
    }

    // Growth can pay fractions of a wen; the remainder carries over to the next pickup
    private float wenCarry;

    public void AddWen(int amount)
    {
        if (levelingLocked) return;

        float grown = amount * (stats ? stats.GrowthMul : 1f) + wenCarry;
        int whole = Mathf.FloorToInt(grown);
        wenCarry = grown - whole;

        wenCount += whole;
        progressDirty = true;   // the bar is redrawn once per frame, not per wen
        if (wenCount >= wenForUpgrade)
        {
            wenCount -= wenForUpgrade;
            LevelUp();
        }
    }

    private void LevelUp()
    {
        currentLevel++;
        RunStats.ReachedLevel(currentLevel);
        RefreshWenRequirement();
        if (progressBarGradient) progressBarGradient.Celebrate();
        OpenUpgradeMenu();
    }

    private void OpenUpgradeMenu()
    {
        Juice.Yield();
        Time.timeScale = 0f;

        List<UpgradeData> pool = new List<UpgradeData>(runtimeUpgrades);
        pool.RemoveAll(u => u == null || !u.CanOffer);

        // an evolution waits until its partner weapon is held too
        pool.RemoveAll(u => u is WeaponData w && w.NextPickEvolves && !CanEvolve(w));

        // a full row takes nothing new: what's held can still level up
        if (WeaponSlots > 0 && WeaponsHeld >= WeaponSlots) pool.RemoveAll(u => u.Category == UpgradeCategory.Weapon && u.Level == 0);
        if (PassiveSlots > 0 && PassivesHeld >= PassiveSlots) pool.RemoveAll(u => u.Category == UpgradeCategory.Passive && u.Level == 0);

        List<UpgradeData> randomUpgrades = new List<UpgradeData>();
        for (int i = 0; i < 3 && pool.Count > 0; i++)
        {
            int index = PickWeighted(pool);
            randomUpgrades.Add(pool[index]);
            pool.RemoveAt(index);
        }

        randomUpgrades.RemoveAll(u => u == null || !u.CanOffer);

        upgradeMenuUI.Open(randomUpgrades, ApplyUpgrade, CurrentLevel);
    }

    private void ApplyUpgrade(UpgradeData upgrade)
    {
        if (progressBarGradient) progressBarGradient.EndCelebrate();
        if (upgrade == null)
        {
            Time.timeScale = 1f;
            return;
        }

        // a maxed upgrade being taken again is an overcharge stack, not another level. it must
        // skip stats.Apply, which would compound the level multiplier past its cap
        if (upgrade.IsOvercharging)
        {
            upgrade.AddOverchargeStack();
            if (stats != null) stats.SetOvercharge(upgrade.type, upgrade.OverchargeBonus());
            Time.timeScale = 1f;
            return;
        }

        if (stats != null)
            stats.Apply(upgrade);

        // every weapon is a WeaponData: the first pick adds it, every pick after levels it up.
        // passives are stats, handled by stats.Apply above
        if (upgrade is WeaponData weapon)
            WeaponFor(weapon).SetLevel(upgrade.Level + 1);

        if (upgrade.Level == 0 && !taken.Contains(upgrade)) taken.Add(upgrade);
        upgrade.LevelUp();
        Time.timeScale = 1f;
    }

    private Weapon WeaponFor(WeaponData data)
    {
        if (!weapons.TryGetValue(data, out var weapon) || weapon == null)
            weapons[data] = weapon = data.AddTo(gameObject);
        return weapon;
    }

    private bool CanEvolve(WeaponData weapon)
    {
        if (weapon.evolutionPartner != null) return Holds(weapon.evolutionPartner);

        // no partner set: any second weapon counts
        int held = 0;
        foreach (var w in weapons.Values) if (w != null) held++;
        return held >= 2;
    }

    // whether the player has this weapon right now. takes the asset, as a partner field holds it
    public bool Holds(UpgradeData weaponAsset)
    {
        if (!(weaponAsset is WeaponData data)) return false;

        // a pick adds the weapon under the run's copy of the asset, a starting weapon granted
        // outside the pool under the asset itself
        if (weapons.TryGetValue(data, out var weapon) && weapon != null) return true;
        return runtimeFor.TryGetValue(data, out var runtime) && runtime is WeaponData copy &&
               weapons.TryGetValue(copy, out weapon) && weapon != null;
    }

    // the picked character's starting weapon, any weapon asset. it's taken the way a level up
    // would take it, so it counts as picked and its next levels come from the pool. null gives
    // the Bow, like before characters
    public void GrantStartingWeapon(UpgradeData starting)
    {
        if (starting == null) starting = (allUpgrades ?? new List<UpgradeData>()).Find(u => u is BowData);
        if (!(starting is WeaponData weapon)) return;

        if (runtimeFor.TryGetValue(starting, out var runtime) && runtime != null)
        {
            // ApplyUpgrade resumes time for the level up menu; spawn shouldn't unpause anything
            float timeScale = Time.timeScale;
            ApplyUpgrade(runtime);
            Time.timeScale = timeScale;
        }
        else
        {
            // switched off in the pool: the weapon still works, it just never levels
            WeaponFor(weapon).SetLevel(1);
        }
    }

    public void ResetRun()
    {
        wenCount = 0;
        currentLevel = 1;

        var curve = DifficultyCurve.For(difficultyCurve);
        if (curve != null)
            wenForUpgrade = curve.WenForLevel(currentLevel);
        else
            wenForUpgrade = 15;

        foreach (var u in runtimeUpgrades) if (u != null) u.ResetLevel();
        taken.Clear();
        UpdateProgress();
        if (stats != null) stats.ResetStats();
    }

    private bool progressDirty;

    private void LateUpdate()
    {
        if (!progressDirty) return;
        progressDirty = false;
        UpdateProgress();
    }

    private void UpdateProgress()
    {
        float t = wenForUpgrade > 0 ? Mathf.Clamp01((float)wenCount / wenForUpgrade) : 0f;
        if (progressBarGradient) progressBarGradient.SetProgress01(t);
    }

    private void RefreshWenRequirement()
    {
        var curve = DifficultyCurve.For(difficultyCurve);
        if (curve != null)
            wenForUpgrade = curve.WenForLevel(currentLevel);

        UpdateProgress();
    }

    // new upgrades beat overcharge while any are left, so overcharge fills in around a build
    // instead of crowding out the next unlock. once everything is maxed they're all equal
    private const float OverchargeWeight = 0.35f;

    private int PickWeighted(List<UpgradeData> pool)
    {
        float total = 0f;
        foreach (var u in pool) total += Weight(u);

        float r = Random.value * total;
        for (int i = 0; i < pool.Count; i++)
        {
            r -= Weight(pool[i]);
            if (r <= 0f) return i;
        }
        return pool.Count - 1;
    }

    // and the next level of a weapon already held comes up a little more often than anything else
    private float Weight(UpgradeData u)
    {
        float w = u.IsOvercharging ? OverchargeWeight : 1f;
        if (u is WeaponData && u.Level > 0) w *= 1f + heldWeaponBonus;
        return w;
    }

    private void BuildRuntimeUpgrades()
    {
        runtimeUpgrades.Clear();
        runtimeFor.Clear();
#if UNITY_EDITOR
        // play mode in the editor always sees every upgrade asset, even if the scene
        // hasn't been saved since a new one was added
        allUpgrades = FindAllUpgradeAssets();
#endif
        if (allUpgrades == null) return;
        foreach (var src in allUpgrades)
        {
            if (src == null || !src.includeInPool) continue;
            var inst = Instantiate(src);
            inst.ResetLevel();
            runtimeUpgrades.Add(inst);
            runtimeFor[src] = inst;
        }
    }

#if UNITY_EDITOR
    // every UpgradeData asset in the project is picked up automatically, so adding an
    // upgrade is just creating the asset. the list is still serialized so builds get it
    private void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall -= AutofillUpgrades;
        UnityEditor.EditorApplication.delayCall += AutofillUpgrades;
    }

    private void AutofillUpgrades()
    {
        // delayCall can land after this component was destroyed or play mode started
        if (this == null || Application.isPlaying) return;

        var found = FindAllUpgradeAssets();
        if (SameUpgrades(allUpgrades, found)) return;

        allUpgrades = found;
        UnityEditor.EditorUtility.SetDirty(this);
        if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(this))
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
    }

    private static List<UpgradeData> FindAllUpgradeAssets()
    {
        var paths = new List<string>();
        foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:UpgradeData"))
            paths.Add(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));

        // sorted so the serialized list is stable and doesn't dirty the scene on every validate
        paths.Sort(System.StringComparer.Ordinal);

        var list = new List<UpgradeData>(paths.Count);
        foreach (var path in paths)
        {
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<UpgradeData>(path);
            if (asset != null) list.Add(asset);
        }
        return list;
    }

    private static bool SameUpgrades(List<UpgradeData> a, List<UpgradeData> b)
    {
        if (a == null || a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
            if (a[i] != b[i]) return false;
        return true;
    }
#endif
}
