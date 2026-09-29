#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// a debug overlay for testing: F1 opens and closes it. it only exists in the editor and in
// development builds, creates itself, and needs nothing in any scene
public class DevTools : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (FindFirstObjectByType<DevTools>() != null) return;
        var go = new GameObject("DevTools (F1)");
        DontDestroyOnLoad(go);
        go.AddComponent<DevTools>();
    }

    private static readonly float[] Speeds = { 0f, 0.25f, 0.5f, 1f, 2f, 4f };

    private readonly List<GameObject> snapshot = new List<GameObject>(256);
    private Rect window = new Rect(16, 16, 420, 760);
    private Vector2 scroll;
    private string filter = string.Empty;
    private bool open;
    private bool showStats;
    private float speed = 1f;
    private float fps;
    private PlayerInventory inventory;
    private PlayerHealth health;
    private SpawnDirector director;
    private GameLoopController loop;
    private RunTimeLimit limit;
    // the upgrade list, sorted, remade a few times a second rather than for every GUI event
    // (IMGUI calls Draw several times a frame; a scene search and a LINQ sort in it cost ~25 ms
    // a frame over a big horde)
    private readonly List<UpgradeData> upgrades = new List<UpgradeData>();
    private float nextList;

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.f1Key.wasPressedThisFrame) open = !open;

        fps = Mathf.Lerp(fps, 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime), 0.05f);

        // the game sets time back to 1 after its menus; keep the speed picked here
        if (!Mathf.Approximately(speed, 1f) && Mathf.Approximately(Time.timeScale, 1f)) Time.timeScale = speed;

        if (inventory == null)
        {
            inventory = FindFirstObjectByType<PlayerInventory>();
            health = inventory != null ? inventory.GetComponent<PlayerHealth>() : null;
        }

        if (director == null && Time.frameCount % 30 == 0)
        {
            director = FindFirstObjectByType<SpawnDirector>();
            loop = FindFirstObjectByType<GameLoopController>();
            limit = FindFirstObjectByType<RunTimeLimit>();
        }

        if (open && inventory != null && Time.unscaledTime >= nextList)
        {
            nextList = Time.unscaledTime + 0.5f;
            upgrades.Clear();
            upgrades.AddRange(inventory.RunUpgrades.Where(u => u != null).OrderBy(u => IsWeapon(u) ? 0 : 1).ThenBy(u => u.GetBaseTitle()));
        }
    }

    private void OnGUI()
    {
        if (!open) return;
        float scale = Mathf.Max(1f, Screen.height / 900f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        window = GUILayout.Window(0x5EED, window, Draw, "Dev Tools  (F1)");
    }

    private void Draw(int id)
    {
        GUILayout.Label($"{fps:0} fps    enemies {EnemyRegistry.Count}    speed x{Time.timeScale:0.##}");

        GUILayout.BeginHorizontal();
        foreach (float s in Speeds)
            if (GUILayout.Button(s == 0f ? "pause" : "x" + s)) SetSpeed(s);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button($"+100k coins ({Coins.Balance})")) Coins.Add(100000);
        if (GUILayout.Button("Max shop")) SetShopRanks(true);
        if (GUILayout.Button("Reset shop")) SetShopRanks(false);
        GUILayout.EndHorizontal();

        if (inventory == null)
        {
            GUILayout.Label("no player in this scene");
            GUI.DragWindow();
            return;
        }

        string hp = health != null ? $"{health.Current:0}/{health.Max:0}" : "-";
        GUILayout.Label($"level {inventory.CurrentLevel}    kills {inventory.totalKills}    hp {hp}");

        GUILayout.BeginHorizontal();
        if (health != null) health.godMode = GUILayout.Toggle(health.godMode, " God mode");
        if (GUILayout.Button("Heal") && health != null) health.Heal(health.Max);
        if (GUILayout.Button("Level up")) inventory.AddWen(Mathf.Max(1, inventory.wenForUpgrade - inventory.wenCount));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Kill all enemies")) KillAll();
        if (GUILayout.Button("Max all weapons")) MaxWeapons();
        GUILayout.EndHorizontal();

        GUI.enabled = loop != null && !loop.InFinalRush && loop.RunSeconds < PlaytestAt;
        if (GUILayout.Button("Playtest: 8:50, level 25, a typical build")) PlaytestLoadout();
        GUI.enabled = loop != null && !GameMode.IsEndless && !loop.InFinalRush && !loop.FinalBossStarted;
        if (GUILayout.Button($"Duel test: maxed build, final boss in {DuelLead:0}s")) DuelTest();
        GUI.enabled = true;

        // maps: picking one restarts the scene on it
        if (director != null && MapSelection.Count > 1)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("map", GUILayout.Width(34));
            var maps = MapCatalog.Load().maps;
            for (int i = 0; i < maps.Count; i++)
            {
                GUI.enabled = i != MapSelection.Index;
                if (GUILayout.Button(maps[i].title))
                {
                    MapSelection.Index = i;
                    SetSpeed(1f);
                    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                }
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        if (director != null)
        {
            GUILayout.Space(6);
            GUILayout.Label($"spawning   {director.RunMinute:0.0} min   {director.BeatLabel}{(director.SurgeActive ? "   SURGE" : "")}");
            GUILayout.Label($"next event   {director.NextEventText}");
            GUILayout.BeginHorizontal();
            foreach (SpawnTimeline.EventKind kind in Enum.GetValues(typeof(SpawnTimeline.EventKind)))
                if (GUILayout.Button(kind.ToString())) director.RunDevEvent(kind);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Clock +1 min")) SkipAhead(60f);
            if (GUILayout.Button("Clock +3 min")) SkipAhead(180f);
            if (limit != null && GUILayout.Button("Time's up")) limit.TimeUpNow();
            GUILayout.EndHorizontal();
        }

        // the horde worst case, measured: see StressTest
        if (director != null)
        {
            GUI.enabled = !StressTest.Running;
            if (GUILayout.Button(StressTest.Running ? "stress test running..." : $"Stress test ({StressTest.Enemies} enemies, {StressTest.Wen} wen)"))
                StressTest.Begin();
            GUI.enabled = true;
            if (!string.IsNullOrEmpty(StressTest.Last)) GUILayout.Label(StressTest.Last);
        }

        GUILayout.Space(6);
        showStats = GUILayout.Toggle(showStats, " Run stats (damage, DPS, health)");
        if (showStats) DrawStats();

        GUILayout.Space(6);
        GUILayout.Label("Take an upgrade (skips the level up menu and its rules):");
        filter = GUILayout.TextField(filter);
        scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(showStats ? 150 : 360));
        foreach (var u in upgrades)
        {
            if (filter.Length > 0 && u.GetBaseTitle().IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;

            bool evolvesNext = u is WeaponData w && w.NextPickEvolves;
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{u.GetBaseTitle()}   {u.Level}/{u.MaxLevel}{(evolvesNext ? "   EVO next" : "")}", GUILayout.Width(300));
            GUI.enabled = u.CanOffer;
            if (GUILayout.Button("+", GUILayout.Width(44))) inventory.TakeUpgrade(u);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();

        GUI.DragWindow();
    }

    // what RunStats has counted so far: the totals, then a row a weapon, an evolution on its own
    private static void DrawStats()
    {
        GUILayout.Label($"dealt {RunStats.Short(RunStats.DamageDealt)}   {RunStats.Dps:0} dps   now {RunStats.LiveDps:0}   best {RunStats.PeakDps:0}");
        GUILayout.Label($"hits {RunStats.Hits}   crits {RunStats.Crits}   biggest {RunStats.Short(RunStats.BiggestHit)}   elites {RunStats.EliteKills}   bosses {RunStats.BossKills}");
        GUILayout.Label($"hp lost {RunStats.HealthLost:0} in {RunStats.HitsTaken} hits   blocked {RunStats.DamageBlocked:0}   lowest {RunStats.LowestHealth * 100f:0}%");
        GUILayout.Label($"healed {RunStats.Healed:0} ({RunStats.HealsPickedUp} pickups)   regen {RunStats.Regenerated:0.#}   revivals {RunStats.RevivalsUsed}");
        GUILayout.Label($"walked {RunStats.DistanceWalked:0}   waves {RunStats.WavesCleared}   played {RunStats.Clock(RunStats.Now)}");

        StatRow("weapon", "damage", "dps", "now", "kills");
        foreach (var w in RunStats.Weapons)
            StatRow($"{(w.Evolved ? "> " : "")}{w.Title} {w.Level}", RunStats.Short(w.Damage), w.Dps.ToString("0"),
                w.Held ? w.LiveDps.ToString("0") : "-", w.Kills.ToString());

        if (GUILayout.Button("Log run stats")) Debug.Log(RunStats.Report());
    }

    private static void StatRow(string name, string damage, string dps, string now, string kills)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(name, GUILayout.Width(170));
        GUILayout.Label(damage, GUILayout.Width(56));
        GUILayout.Label(dps, GUILayout.Width(44));
        GUILayout.Label(now, GUILayout.Width(44));
        GUILayout.Label(kills, GUILayout.Width(44));
        GUILayout.EndHorizontal();
    }

    // the run clock and enemy toughness move together, so a skip lands where waiting would have
    private void SkipAhead(float seconds)
    {
        if (loop != null) loop.SkipAhead(seconds);
        director.SkipTime(seconds);
    }

    // a playtester's build at the third Final Rush (9:00), ten seconds before it: level 25, six
    // weapons and four passives at these levels (what's held already is levelled up to them)
    private const float PlaytestAt = 8 * 60 + 50;
    private static readonly (string title, int level)[] PlaytestBuild =
    {
        ("Bow", 2), ("Treasure Gourd", 6), ("Seven Star Swords", 4), ("Dragon Line", 1),
        ("Electrical Aura", 2), ("Peach Talismans", 4),
        ("Might", 4), ("Area", 1), ("Executioner", 1), ("Cooldown", 1),
    };

    private void PlaytestLoadout()
    {
        if (loop == null || director == null || inventory == null) return;
        float was = loop.RunSeconds;
        loop.JumpTo(PlaytestAt);
        director.SkipTime(PlaytestAt - was);
        inventory.SetLevel(25);
        foreach (var (title, level) in PlaytestBuild)
        {
            var u = inventory.RunUpgrades.FirstOrDefault(x => x != null && x.GetBaseTitle() == title);
            if (u == null) { Debug.LogWarning("Dev Tools: no upgrade called " + title); continue; }
            for (int guard = 0; guard < 16 && u.Level < level && u.CanOffer; guard++) inventory.TakeUpgrade(u);
        }
        if (health != null) health.Heal(health.Max);
    }

    // the end boss's duel (BossDuel), to try: a full, maxed build first (six weapons and six
    // passives, every one at its cap, evolutions and all, and the Command Token for its bomb), so
    // putting them away shows as it would at the end of a strong run; then the clock set so the
    // map's final boss comes a few seconds later. the character's starting weapon is one of the
    // six, whatever was held already is kept
    private const float DuelLead = 5f;

    private void DuelTest()
    {
        if (loop == null || director == null || inventory == null) return;

        int picks = 0;
        FillAndMax(UpgradeCategory.Weapon, inventory.WeaponSlots, ref picks);
        FillAndMax(UpgradeCategory.Passive, inventory.PassiveSlots, ref picks);
        foreach (var u in inventory.RunUpgrades)
            if (u != null && !u.TakesSlot) Max(u, ref picks);
        inventory.SetLevel(Mathf.Max(inventory.CurrentLevel, 1 + picks));

        float was = loop.RunSeconds;
        if (loop.JumpToFinalBoss(DuelLead)) director.SkipTime(loop.RunSeconds - was);
        if (health != null) health.Heal(health.Max);
    }

    // what's held of a kind levelled to its cap, then new ones taken and maxed until its slots are full
    private void FillAndMax(UpgradeCategory kind, int slots, ref int picks)
    {
        var all = inventory.RunUpgrades.Where(u => u != null && u.TakesSlot && u.Category == kind).ToList();
        int held = 0;
        foreach (var u in all.Where(u => u.Level > 0)) { Max(u, ref picks); held++; }
        foreach (var u in all.Where(u => u.Level == 0))
        {
            if (slots > 0 && held >= slots) break;
            Max(u, ref picks);
            held++;
        }
    }

    private void Max(UpgradeData u, ref int picks)
    {
        for (int guard = 0; guard < 32 && u.CanOffer && !u.IsAtCap; guard++)
        {
            inventory.TakeUpgrade(u);
            picks++;
        }
    }

    private void SetSpeed(float s)
    {
        speed = s;
        Time.timeScale = s;
    }

    private static void SetShopRanks(bool max)
    {
        foreach (var u in StatCatalog.Load().globalUpgrades)
            if (u != null) MetaProgress.SetRank(u, max ? u.maxRank : 0);
    }

    private static bool IsWeapon(UpgradeData u) => u is WeaponData;

    private void KillAll()
    {
        // a kill unregisters the enemy, so go through a copy of the list
        snapshot.Clear();
        snapshot.AddRange(EnemyRegistry.All);
        foreach (var go in snapshot)
            if (go != null && go.TryGetComponent(out EnemyHealth e) && e.Current > 0f)
                e.TakeDamage(1e9f, DamageKind.Normal, false, true);
    }

    private void MaxWeapons()
    {
        foreach (var u in inventory.RunUpgrades.Where(u => u != null && IsWeapon(u)).ToList())
            for (int guard = 0; guard < 32 && u.CanOffer && !u.IsAtCap; guard++)
                inventory.TakeUpgrade(u);
    }
}
#endif
