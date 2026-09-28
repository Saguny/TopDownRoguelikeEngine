#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

// dev only: every weapon's damage per second, measured in the real game rather than guessed.
// Tools > Balance > Weapon DPS Benchmark runs it and stops play afterwards. for each weapon it
// loads the game scene fresh, switches off the starting weapon (unless that's the one being
// measured), then levels the weapon up stage by stage (level 1, halfway, its last level and its
// evolution) and at each stage measures it against
//   crowd   a standing crowd of Crowd Size enemies closing in on the player
//   single  one enemy on its own
// it all happens on the TestGround, open floor far past the map, so no wall or pillar gets in the
// way of the player's walk or the crowd. the enemies can't die (their health is huge, their armour and resistances are off), so the
// crowd is the same size the whole time and every weapon faces the same thing. the player walks
// a slow circle so weapons that follow them, and the ink brush, work as they do in play.
// written to <project>/Benchmarks/weapon_dps.csv (read by Tools/Balance/balance.py, which fits
// the enemy health curve to it) and printed as a table
public class WeaponBenchmark : MonoBehaviour
{
    public const string PendingKey = "WeaponBenchmark.Pending";

    // game seconds of warm up before measuring, and of measuring, per stage and scenario
    private const float WarmUp = 2.5f;
    private const float Measure = 12f;
    private const int CrowdSize = 80;
    // time runs this much faster while it measures; the numbers are per game second either way
    private const float Speed = 2f;
    private const float Immortal = 1e8f;

    public static string CsvPath => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Benchmarks", "weapon_dps.csv"));

    private sealed class Row
    {
        public string weapon, type, scenario, stage;
        public int level;
        public bool evolved, signature;
        public float dps, hitsPerSecond, normalised;
    }

    private readonly List<Row> rows = new List<Row>();
    private GameObject crowdPrefab;
    private PlayerInventory inventory;
    private float mightMul = 1f, physicalMul = 1f, magicalMul = 1f, critFactor = 1f, areaMul = 1f, cooldownMul = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RunIfPending()
    {
        if (!UnityEditor.SessionState.GetBool(PendingKey, false)) return;
        UnityEditor.SessionState.EraseBool(PendingKey);
        var go = new GameObject("WeaponBenchmark");
        DontDestroyOnLoad(go);
        go.AddComponent<WeaponBenchmark>();
    }

    private IEnumerator Start()
    {
        // the weapons to measure, from a first look at the game
        yield return Load();
        if (inventory == null) { Done("no game scene to measure in"); yield break; }
        var types = inventory.RunUpgrades.OfType<WeaponData>()
            .Where(w => !(w is CommandTokenData))      // fired by a key, every 50 seconds: not a DPS weapon
            .Select(w => w.GetType()).Distinct().ToList();
        Debug.Log("BENCHMARK weapons: " + string.Join(", ", types.Select(t => t.Name)));

        for (int i = 0; i < types.Count; i++)
        {
            if (i > 0) yield return Load();
            if (inventory == null) { Done("the game scene didn't come back"); yield break; }
            yield return Bench(types[i]);
        }

        Write();
        Done(null);
    }

    // the game scene, fresh, with nothing spawning, no level ups and the player unhurtable
    private IEnumerator Load()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("Game");
        yield return null;
        SpawnDirector director = null;
        for (float waited = 0f; (director == null || inventory == null) && waited < 15f; waited += Time.unscaledDeltaTime)
        {
            director = FindFirstObjectByType<SpawnDirector>();
            inventory = FindFirstObjectByType<PlayerInventory>();
            yield return null;
        }
        if (director == null || inventory == null) { inventory = null; yield break; }

        // let the run start (the starting weapon is granted), then stop the horde
        yield return new WaitForSecondsRealtime(1.5f);
        director.enabled = false;
        if (inventory.TryGetComponent(out PlayerHealth health)) health.godMode = true;
        inventory.wenForUpgrade = int.MaxValue / 2;
        if (inventory.upgradeMenuUI != null && inventory.upgradeMenuUI.IsOpen) inventory.upgradeMenuUI.Close();
        Clear();
        // out onto open ground: the map's walls, pillars and road would stop the walk and the crowd
        TestGround.MovePlayer(inventory.gameObject);

        if (crowdPrefab == null && director.Timeline != null)
            crowdPrefab = director.Timeline.beats.SelectMany(b => b.enemies)
                .Where(p => p?.archetype?.prefab != null).Select(p => p.archetype.prefab).FirstOrDefault();

        if (inventory.TryGetComponent(out StatContext st))
        {
            mightMul = st.MightMul;
            physicalMul = st.ClassMul(AttackClass.Physical);
            magicalMul = st.ClassMul(AttackClass.Magical);
            critFactor = 1f + st.CritChanceTotal * (st.CritMultiplierTotal - 1f);
            areaMul = st.AreaMul;
            cooldownMul = st.CooldownFor(UpgradeType.Weapon, 1f);
        }
    }

    private IEnumerator Bench(System.Type type)
    {
        var data = inventory.RunUpgrades.OfType<WeaponData>().FirstOrDefault(w => w.GetType() == type);
        if (data == null) yield break;

        int top = data.EvolutionLevel > 0 ? data.EvolutionLevel - 1 : data.MaxLevel;
        var stages = new List<(string name, int level, bool single)>
        {
            ("L1", 1, true),
            ("mid", Mathf.Max(2, Mathf.CeilToInt(top / 2f)), false),
            ("max", top, true),
        };
        if (data.EvolutionLevel > 0) stages.Add(("evolved", data.EvolutionLevel, true));

        var character = inventory.TryGetComponent(out StatContext stats) ? stats.Character : null;
        foreach (var (name, level, single) in stages)
        {
            for (int guard = 0; data.Level < level && data.CanOffer && guard < 20; guard++) inventory.TakeUpgrade(data);
            Time.timeScale = 1f;
            var weapon = Isolate(data);
            if (weapon == null) { Debug.LogWarning("BENCHMARK: no " + type.Name + " on the player"); yield break; }
            bool signature = character != null && character.startingWeapon != null && character.startingWeapon.GetType() == type;

            foreach (var scenario in single ? new[] { "crowd", "single" } : new[] { "crowd" })
            {
                Clear();
                Spawn(scenario == "crowd" ? CrowdSize : 1);
                Time.timeScale = Speed;
                yield return Walk(WarmUp, scenario == "crowd");
                var record = weapon.Record;
                double d0 = record != null ? record.Damage : 0.0;
                int h0 = record != null ? record.Hits : 0;
                float t0 = Time.time;
                yield return Walk(Measure, scenario == "crowd");
                float seconds = Mathf.Max(0.01f, Time.time - t0);
                record = weapon.Record;
                float dps = record != null ? (float)((record.Damage - d0) / seconds) : 0f;
                float hps = record != null ? (record.Hits - h0) / seconds : 0f;
                float classMul = data.AttackClass == AttackClass.Magical ? magicalMul : physicalMul;
                float sig = signature && character != null ? character.signatureDamage : 1f;
                rows.Add(new Row
                {
                    weapon = data.GetBaseTitle(), type = type.Name, scenario = scenario, stage = name,
                    level = data.Level, evolved = weapon.Evolved, signature = signature,
                    dps = dps, hitsPerSecond = hps,
                    normalised = dps / Mathf.Max(0.01f, mightMul * classMul * critFactor * sig),
                });
                Debug.Log($"BENCHMARK {data.GetBaseTitle()} {name} (Lv {data.Level}) {scenario}: {dps:0.0} dps, {hps:0.0} hits/s");
                Time.timeScale = 1f;
            }
        }
        Clear();
    }

    // only the weapon being measured works: the starting weapon (and anything else) is switched off
    private Weapon Isolate(WeaponData data)
    {
        Weapon target = null;
        foreach (var w in inventory.GetComponents<Weapon>())
        {
            bool mine = w.Asset == data;
            w.enabled = mine;
            if (mine) target = w;
        }
        if (!(data is BowData) && inventory.TryGetComponent(out AutoShooter shooter)) shooter.Armed = false;
        return target;
    }

    // walks the player round a slow circle for `seconds` of game time, keeping the crowd full
    private IEnumerator Walk(float seconds, bool crowd)
    {
        var body = inventory.GetComponent<Rigidbody2D>();
        Vector2 home = inventory.transform.position;
        float start = Time.time;
        while (Time.time - start < seconds)
        {
            // a lap every 3.3 seconds: quick enough that the ink brush closes its loops before
            // the ink dries, which its evolution needs
            float lap = (Time.time - start) * 1.9f;
            Vector2 at = home + new Vector2(Mathf.Cos(lap) - 1f, Mathf.Sin(lap)) * 2.4f;
            if (body != null) body.MovePosition(at);
            else inventory.transform.position = at;
            if (crowd && EnemyRegistry.Count < CrowdSize) Spawn(CrowdSize - EnemyRegistry.Count);
            yield return null;
        }
    }

    // enemies that can't die, with no armour or resistances, in a ring just off the player
    private void Spawn(int n)
    {
        if (crowdPrefab == null) return;
        Vector2 at = inventory.transform.position;
        for (int i = 0; i < n; i++)
        {
            float a = Random.value * Mathf.PI * 2f, d = n == 1 ? 3.5f : Random.Range(2.5f, 8f);
            var go = ObjectPool.For(crowdPrefab).Get(at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d, Quaternion.identity);
            if (!go.TryGetComponent(out EnemyHealth h)) continue;
            h.SetScaled(Immortal);
            h.armour = 0f;
            h.physicalTaken = h.magicalTaken = 1f;
        }
    }

    private static void Clear()
    {
        foreach (var go in EnemyRegistry.All.ToList())
            if (go != null) ObjectPool.Recycle(go);
    }

    private void Write()
    {
        var inv = CultureInfo.InvariantCulture;
        var csv = new StringBuilder("weapon,type,stage,level,evolved,scenario,dps,hits_per_second,normalised_dps,signature\n");
        foreach (var r in rows)
            csv.AppendLine(string.Join(",", r.weapon, r.type, r.stage, r.level.ToString(inv), r.evolved ? "1" : "0", r.scenario,
                r.dps.ToString("0.##", inv), r.hitsPerSecond.ToString("0.##", inv), r.normalised.ToString("0.##", inv), r.signature ? "1" : "0"));
        Directory.CreateDirectory(Path.GetDirectoryName(CsvPath));
        File.WriteAllText(CsvPath, csv.ToString());

        var table = new StringBuilder($"BENCHMARK done: {CsvPath}\n" +
            $"the player's stats while measuring: Might x{mightMul:0.##}, physical x{physicalMul:0.##}, magical x{magicalMul:0.##}, crit x{critFactor:0.##} on average, Area x{areaMul:0.##}, Cooldown x{cooldownMul:0.##} (normalised = without them)\n");
        table.AppendLine($"{"weapon",-22}{"stage",-9}{"Lv",4}{"crowd dps",12}{"single dps",12}{"normalised",12}");
        foreach (var g in rows.GroupBy(r => (r.weapon, r.stage)))
        {
            var crowd = g.FirstOrDefault(r => r.scenario == "crowd");
            var one = g.FirstOrDefault(r => r.scenario == "single");
            table.AppendLine($"{g.Key.weapon + (crowd != null && crowd.signature ? " *" : ""),-22}{g.Key.stage,-9}{(crowd ?? one).level,4}" +
                $"{(crowd != null ? crowd.dps.ToString("0") : "-"),12}{(one != null ? one.dps.ToString("0") : "-"),12}{(crowd != null ? crowd.normalised.ToString("0") : "-"),12}");
        }
        table.AppendLine("* the character's starting weapon: its signature damage is in the dps, not in the normalised");
        Debug.Log(table.ToString());
    }

    private void Done(string error)
    {
        if (error != null) Debug.LogError("BENCHMARK failed: " + error);
        Time.timeScale = 1f;
        UnityEditor.EditorApplication.isPlaying = false;
        Destroy(gameObject);
    }
}
#endif
