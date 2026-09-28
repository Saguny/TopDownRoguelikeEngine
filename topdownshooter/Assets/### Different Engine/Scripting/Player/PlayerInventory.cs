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
    [Tooltip("how much likelier an upgrade the player already took this run (a weapon or a passive) is to be offered: 0.1 = 10%")]
    [SerializeField, Min(0f)] private float heldWeaponBonus = 0.1f;

    [Header("Evolutions and gifts")]
    [Tooltip("a weapon at its top level evolves only out of a fortune envelope (while another weapon is held), like Vampire Survivors' chests. off: the evolution is a level up card")]
    [SerializeField] private bool evolveFromEnvelopesOnly = true;
    [Tooltip("offered when nothing is left to level: coins. empty makes one")]
    [SerializeField] private GiftUpgrade coinsGift;
    [Tooltip("offered when nothing is left to level: a heal. empty makes one")]
    [SerializeField] private GiftUpgrade healGift;

    // the run's rule, for the weapon cards' text
    public static bool EvolvesFromEnvelopes { get; private set; } = true;

    // how many weapons and passives the run can hold; 0 = no limit (Endless)
    public int WeaponSlots => GameMode.IsEndless ? 0 : weaponSlots;
    public int PassiveSlots => GameMode.IsEndless ? 0 : passiveSlots;
    public int WeaponsHeld => CountTaken(UpgradeCategory.Weapon);
    public int PassivesHeld => CountTaken(UpgradeCategory.Passive);

    private int CountTaken(UpgradeCategory category)
    {
        int n = 0;
        foreach (var u in taken) if (u != null && u.TakesSlot && u.Category == category) n++;
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

    // dev tools: straight to a level, no menus for the levels skipped
    public void SetLevel(int level)
    {
        currentLevel = Mathf.Max(1, level);
        RunStats.ReachedLevel(currentLevel);
        wenCount = 0;
        pendingLevelUps = 0;
        RefreshWenRequirement();
    }

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
        PlayerHealth.OnPlayerDied += LockLeveling;
    }

    // no level ups once the run is decided: meteors still falling after the player is down, or
    // the Wuchang come (RunTimeLimit), when the purge's kills mustn't open menus
    public void LockLeveling()
    {
        levelingLocked = true;
        pendingLevelUps = 0;
    }

    private void OnDisable()
    {
        foreach (var u in allUpgrades) if (u != null) u.ResetLevel();
        runtimeUpgrades.Clear();
        GameEvents.OnEnemyKilled -= HandleEnemyKilled;
        PlayerHealth.OnPlayerDied -= LockLeveling;
    }

    private void Awake()
    {
        stats = GetComponent<StatContext>();
        EvolvesFromEnvelopes = evolveFromEnvelopesOnly;
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
        // a big pickup or a mass kill can be worth several levels: each one waits its turn
        while (wenCount >= wenForUpgrade && wenForUpgrade > 0)
        {
            wenCount -= wenForUpgrade;
            LevelUp();
        }
    }

    // level ups not yet chosen. a kill that levels up while a menu (or an envelope) is open, or
    // several levels at once, open one menu each, one after another
    private int pendingLevelUps;
    public int PendingLevelUps => pendingLevelUps;

    private void LevelUp()
    {
        currentLevel++;
        RunStats.ReachedLevel(currentLevel);
        RefreshWenRequirement();
        pendingLevelUps++;
    }

    // a level up menu opens when nothing else has the game stopped: not paused, no other menu or
    // envelope up, the run not over
    private bool CanOpenMenu =>
        pendingLevelUps > 0 && !levelingLocked && Time.timeScale > 0f && upgradeMenuUI != null &&
        !upgradeMenuUI.IsOpen && !EnvelopeOpening.Busy && !SceneLoader.Busy &&
        !(TryGetComponent(out PlayerHealth h) && h.IsDead);

    private void OpenUpgradeMenu()
    {
        pendingLevelUps--;
        Juice.Yield();
        Time.timeScale = 0f;
        if (progressBarGradient) progressBarGradient.Celebrate();

        var offers = RollOffers();
        // nothing left to level: the two gifts
        if (offers.Count == 0) offers.AddRange(GiftsOffered());

        upgradeMenuUI.Open(offers, ChooseFromMenu, CurrentLevel - pendingLevelUps, Tools());
    }

    // the cards the pool can offer right now, weighted, without repeats
    private List<UpgradeData> RollOffers(int count = 3)
    {
        var pool = OfferPool();
        var picked = new List<UpgradeData>();
        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            int index = PickWeighted(pool);
            picked.Add(pool[index]);
            pool.RemoveAt(index);
        }
        return picked;
    }

    private List<UpgradeData> OfferPool()
    {
        var pool = new List<UpgradeData>(runtimeUpgrades);
        pool.RemoveAll(u => u == null || !u.CanOffer || banished.Contains(u));

        // an evolution comes out of a fortune envelope; or, with that off, waits until its partner
        // weapon is held too
        pool.RemoveAll(u => u is WeaponData w && w.NextPickEvolves && (evolveFromEnvelopesOnly || !CanEvolve(w)));

        // a full row takes nothing new: what's held can still level up
        if (WeaponSlots > 0 && WeaponsHeld >= WeaponSlots) pool.RemoveAll(u => u.Category == UpgradeCategory.Weapon && u.Level == 0);
        if (PassiveSlots > 0 && PassivesHeld >= PassiveSlots) pool.RemoveAll(u => u.Category == UpgradeCategory.Passive && u.Level == 0);
        return pool;
    }

    private List<UpgradeData> GiftsOffered()
    {
        if (coinsGift == null) coinsGift = FindGift(GiftKind.Coins);
        if (healGift == null) healGift = FindGift(GiftKind.Heal);
        return new List<UpgradeData> { coinsGift, healGift };
    }

    private GiftUpgrade FindGift(GiftKind kind)
    {
        if (allUpgrades != null)
            foreach (var u in allUpgrades)
                if (u is GiftUpgrade g && g.kind == kind) return g;
        return GiftUpgrade.Make(kind);
    }

    // ---------------------------------------------------------------- reroll, skip, banish

    // bought in the shop (the Reroll, Skip and Banish global upgrades): a few a run
    private int rerollsLeft = -1, skipsLeft, banishesLeft;
    private bool ownsReroll, ownsSkip, ownsBanish;
    private readonly HashSet<UpgradeData> banished = new HashSet<UpgradeData>();

    private void ReadCharges()
    {
        if (rerollsLeft >= 0) return;
        var sheet = StatSheet.ForRun();
        rerollsLeft = Mathf.Max(0, Mathf.RoundToInt(sheet[StatId.Reroll]));
        skipsLeft = Mathf.Max(0, Mathf.RoundToInt(sheet[StatId.Skip]));
        banishesLeft = Mathf.Max(0, Mathf.RoundToInt(sheet[StatId.Banish]));
        ownsReroll = rerollsLeft > 0;
        ownsSkip = skipsLeft > 0;
        ownsBanish = banishesLeft > 0;
    }

    private UpgradeMenuUI.Tools Tools()
    {
        ReadCharges();
        return new UpgradeMenuUI.Tools
        {
            ownsReroll = ownsReroll, ownsSkip = ownsSkip, ownsBanish = ownsBanish,
            rerolls = rerollsLeft, skips = skipsLeft, banishes = banishesLeft,
            reroll = Reroll, skip = Skip, banish = Banish,
        };
    }

    // three new cards; the ones showing may come back
    private List<UpgradeData> Reroll()
    {
        if (rerollsLeft <= 0) return null;
        rerollsLeft--;
        var offers = RollOffers();
        if (offers.Count == 0) offers.AddRange(GiftsOffered());
        upgradeMenuUI.SetCharges(rerollsLeft, skipsLeft, banishesLeft);
        return offers;
    }

    // no pick this level
    private void Skip()
    {
        if (skipsLeft <= 0) return;
        skipsLeft--;
        ChooseFromMenu(null);
    }

    // that card never comes up again this run; a new one takes its place (null: none left)
    private UpgradeData Banish(UpgradeData card, List<UpgradeData> showing)
    {
        if (banishesLeft <= 0 || card == null || card is GiftUpgrade) return card;
        banishesLeft--;
        banished.Add(card);
        var pool = OfferPool();
        pool.RemoveAll(showing.Contains);
        upgradeMenuUI.SetCharges(rerollsLeft, skipsLeft, banishesLeft);
        return pool.Count > 0 ? pool[PickWeighted(pool)] : null;
    }

    [Tooltip("seconds the player can't be hurt after the level up menu closes, to get their bearings")]
    [SerializeField, Min(0f)] private float menuGraceSeconds = 2f;

    // the level up menu's pick: taken, then a moment of grace so the crowd that closed in while
    // the game was paused can't land a free hit
    private void ChooseFromMenu(UpgradeData upgrade)
    {
        ApplyUpgrade(upgrade);
        if (TryGetComponent(out PlayerHealth health)) health.GrantInvulnerability(menuGraceSeconds);
    }

    private void ApplyUpgrade(UpgradeData upgrade)
    {
        if (progressBarGradient) progressBarGradient.EndCelebrate();
        if (upgrade == null)
        {
            Time.timeScale = 1f;
            return;
        }

        // a gift pays out and is gone: nothing is held or levelled
        if (upgrade is GiftUpgrade gift)
        {
            gift.Give(gameObject);
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

    // ---------------------------------------------------------------- fortune envelopes

    // what an envelope gives: an evolution first, if it can give one (FortuneEnvelope.Evolves) and a
    // weapon is ready (at its top level, its partner held); one at most, like Vampire Survivors'
    // chests. then levels of whatever is held, drawn from the pool the level up uses (the same one
    // more than once when it's worth it), each taken as it's drawn. with nothing left to level, the
    // rest is coins. returns what was given, in order, for the scroll
    public List<EnvelopeReward> OpenEnvelope(int upgrades, bool evolves = true)
    {
        var given = new List<EnvelopeReward>();
        bool evolved = false;
        for (int i = 0; i < upgrades; i++)
        {
            UpgradeData pick = null;
            bool evolution = false;
            if (evolves && !evolved)
                foreach (var u in runtimeUpgrades)
                    if (u is WeaponData w && w.Level > 0 && w.NextPickEvolves && CanEvolve(w) && !banished.Contains(u)) { pick = u; evolution = evolved = true; break; }
            if (pick == null)
            {
                // levels of what's held only: an envelope doesn't fill a slot
                var pool = OfferPool();
                pool.RemoveAll(u => u.Level == 0);
                if (pool.Count > 0) pick = pool[PickWeighted(pool)];
            }
            if (pick == null)
            {
                Coins.Gift(GiftCoins);
                given.Add(EnvelopeReward.Coins(Coins.WithGreed(GiftCoins)));
                continue;
            }

            float timeScale = Time.timeScale;
            ApplyUpgrade(pick);
            Time.timeScale = timeScale;
            given.Add(new EnvelopeReward { item = pick, evolution = evolution, level = pick.Level });
        }
        return given;
    }

    // coins in place of an upgrade an envelope couldn't give, before Greed: the level up's String
    // of Wen, the gift it offers once there's nothing left to level
    public int GiftCoins
    {
        get
        {
            if (coinsGift == null) coinsGift = FindGift(GiftKind.Coins);
            return coinsGift != null ? coinsGift.coins : 25;
        }
    }

    // an envelope would have nothing to give but coins: no evolution ready, nothing held left to
    // level. it just pays out then (EnvelopeOpening), like the level up's gift
    public bool EnvelopeWouldBeEmpty(bool evolves)
    {
        if (evolves)
            foreach (var u in runtimeUpgrades)
                if (u is WeaponData w && w.Level > 0 && w.NextPickEvolves && CanEvolve(w) && !banished.Contains(u)) return false;
        var pool = OfferPool();
        pool.RemoveAll(u => u.Level == 0);
        return pool.Count == 0;
    }

    private bool CanEvolve(WeaponData weapon)
    {
        if (weapon.evolutionPartner != null) return Holds(weapon.evolutionPartner);

        // no partner set: any second weapon counts
        int held = 0;
        foreach (var w in weapons.Values) if (w != null && (w.Asset == null || w.Asset.TakesSlot)) held++;
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

        // nobody holds their weapon in their hands: it's worn on their back
        BackWeapon.Wear(gameObject, weapon);

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
        pendingLevelUps = 0;
        banished.Clear();
        rerollsLeft = -1;
        UpdateProgress();
        if (stats != null) stats.ResetStats();
    }

    private bool progressDirty;

    private void LateUpdate()
    {
        // the next level up waiting, once nothing else has the game stopped
        if (CanOpenMenu) OpenUpgradeMenu();
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

    // and the next level of anything already picked this run comes up a little more often
    private float Weight(UpgradeData u)
    {
        float w = u.IsOvercharging ? OverchargeWeight : 1f;
        if (u.Level > 0) w *= 1f + heldWeaponBonus;
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
