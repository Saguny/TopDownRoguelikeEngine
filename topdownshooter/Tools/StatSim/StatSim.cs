// StatSim: Monte Carlo stat and run simulator for the "### Different Engine" game.
//
// It reads the live values straight out of the Unity files, so re-run it after tweaking anything:
// the upgrade assets, FixedCurve, the enemy archetypes and prefabs, and the SpawnDirector,
// GameLoopController and player components in Scenes/MainTestGame.unity. Then it replays the
// game's own rules: StatContext stacking, PlayerInventory level ups and offers, AutoShooter, Aura,
// AOEAttack targeting, SpawnDirector budgets and GameLoopController waves.
//
// run from the project root (--file because the root holds unity's .csproj files):
//   dotnet run --file Tools/StatSim/StatSim.cs
//   dotnet run --file Tools/StatSim/StatSim.cs -- --runs 400 --endless-runs 100 --json Tools/StatSim/results.json
//
// what the game code doesn't decide, so the sim has to assume it:
//   - the player stands still in the middle of the screen and never takes damage, so this measures
//     offense and progression, not survival
//   - enemies walk straight at the player and pack into rings around them
//   - arrows land instantly along a straight line; pierce hits whatever is next on that line
//   - 60 fps, which matters for the spawner's per frame limits
//   - "greedy" picks whichever offered upgrade raises kill throughput the most; "random" picks blind

using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;

var opt = Options.Parse(args);
var game = GameData.Load(Options.FindProjectRoot(opt.Root));
var clock = Stopwatch.StartNew();

Report.BaseStats(game);
Report.Upgrades(game);
Report.Defense(game);

var normal = new List<ScenarioResult>();
var baselineRandom = Sim.RunScenario(game, Scenarios.Baseline, Policy.Random, endless: false, opt.Runs, opt.Seed);
foreach (var meta in Scenarios.All().Where(opt.Wants))
    normal.Add(Sim.RunScenario(game, meta, Policy.Greedy, endless: false, opt.Runs, opt.Seed));

Report.Timeline(game, normal[0], baselineRandom);
Report.Scenarios(normal);

var endless = new List<ScenarioResult>();
if (opt.EndlessRuns > 0)
{
    foreach (var meta in Scenarios.Endless().Where(opt.Wants))
        endless.Add(Sim.RunScenario(game, meta, Policy.Greedy, endless: true, opt.EndlessRuns, opt.Seed));
    Report.EndlessTable(endless);
}

Report.Notes(game);
if (opt.Json != null) Json.Write(opt.Json, game, normal, baselineRandom, endless);
Console.WriteLine($"\ndone in {clock.Elapsed.TotalSeconds:0.0}s ({opt.Runs} runs per scenario, {opt.EndlessRuns} endless)");

// ---------------------------------------------------------------------------------------------
// options
// ---------------------------------------------------------------------------------------------

sealed class Options
{
    public int Runs = 200;
    public int EndlessRuns = 60;
    public int Seed = 1234;
    public string? Json;
    public string? Root;
    public HashSet<string>? Only;       // scenario names to run; the baseline always runs

    public bool Wants(Meta m) => Only == null || m == Scenarios.Baseline || Only.Contains(m.Name);

    public static Options Parse(string[] a)
    {
        var o = new Options();
        for (int i = 0; i < a.Length; i++)
        {
            switch (a[i])
            {
                case "--only": o.Only = a[++i].Split(',').Select(s => s.Trim()).ToHashSet(); break;
                case "--runs": o.Runs = int.Parse(a[++i]); break;
                case "--endless-runs": o.EndlessRuns = int.Parse(a[++i]); break;
                case "--seed": o.Seed = int.Parse(a[++i]); break;
                case "--json": o.Json = a[++i]; break;
                case "--root": o.Root = a[++i]; break;
            }
        }
        return o;
    }

    public static string FindProjectRoot(string? hint)
    {
        foreach (var start in new[] { hint, Environment.CurrentDirectory })
        {
            if (start == null) continue;
            for (var d = new DirectoryInfo(start); d != null; d = d.Parent)
                if (Directory.Exists(Path.Combine(d.FullName, "Assets", "### Different Engine"))) return d.FullName;
        }
        throw new Exception("run this from inside the unity project (couldn't find Assets/### Different Engine)");
    }
}

// ---------------------------------------------------------------------------------------------
// unity yaml, just enough of it
// ---------------------------------------------------------------------------------------------

sealed class Doc
{
    public string ClassId = "", FileId = "";
    public readonly List<string> Lines = new();
}

static class Yaml
{
    public static List<Doc> Docs(string path)
    {
        var docs = new List<Doc>();
        Doc? cur = null;
        foreach (var line in File.ReadLines(path))
        {
            if (line.StartsWith("--- !u!"))
            {
                var m = Regex.Match(line, @"^--- !u!(\d+) &(-?\d+)");
                cur = new Doc { ClassId = m.Groups[1].Value, FileId = m.Groups[2].Value };
                docs.Add(cur);
                continue;
            }
            cur?.Lines.Add(line);
        }
        return docs;
    }

    // top level fields sit at two spaces of indent. "new|old" tries each name in turn, for fields
    // renamed with [FormerlySerializedAs] that keep their old name until unity re-saves the asset
    public static string? Get(Doc d, string key)
    {
        foreach (var name in key.Split('|'))
        {
            var p = "  " + name + ":";
            foreach (var l in d.Lines)
                if (l.StartsWith(p) && (l.Length == p.Length || l[p.Length] == ' ')) return Unquote(l[p.Length..].Trim());
        }
        return null;
    }

    // 'single quoted' yaml text, with '' standing for one quote
    private static string Unquote(string s) =>
        s.Length >= 2 && s[0] == '\'' && s[^1] == '\'' ? s[1..^1].Replace("''", "'") : s;

    public static float ParseF(string s) => s switch
    {
        "Infinity" => float.PositiveInfinity,
        "-Infinity" => float.NegativeInfinity,
        _ => float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture)
    };

    public static float F(Doc d, string key, float def) => Get(d, key) is { Length: > 0 } s ? ParseF(s) : def;
    public static int I(Doc d, string key, int def) => Get(d, key) is { Length: > 0 } s ? (int)ParseF(s) : def;
    public static bool B(Doc d, string key, bool def) => Get(d, key) is { Length: > 0 } s ? s != "0" : def;

    public static string? Guid(string? reference)
    {
        if (reference == null) return null;
        var m = Regex.Match(reference, @"guid: ([0-9a-f]{32})");
        return m.Success ? m.Groups[1].Value : null;
    }

    public static string? FileIdOf(string? reference)
    {
        if (reference == null) return null;
        var m = Regex.Match(reference, @"fileID: (-?\d+)");
        return m.Success ? m.Groups[1].Value : null;
    }

    public static string? ScriptGuid(Doc d) => Guid(Get(d, "m_Script"));

    public static List<string> RefList(Doc d, string key)
    {
        var res = new List<string>();
        int i = d.Lines.FindIndex(l => l == "  " + key + ":");
        if (i < 0) return res;
        for (int j = i + 1; j < d.Lines.Count && d.Lines[j].StartsWith("  - "); j++)
            if (Guid(d.Lines[j]) is { } g) res.Add(g);
        return res;
    }

    public static Curve Curve(Doc d, string key)
    {
        var keys = new List<Key>();
        int i = -1;
        foreach (var name in key.Split('|'))
            if ((i = d.Lines.FindIndex(l => l == "  " + name + ":")) >= 0) break;
        if (i < 0) return new Curve(keys);

        Key? k = null;
        for (int j = i + 1; j < d.Lines.Count && d.Lines[j].StartsWith("    "); j++)
        {
            var l = d.Lines[j].Trim();
            if (l.StartsWith("- serializedVersion")) { k = new Key(); keys.Add(k); continue; }
            if (k == null) continue;

            var parts = l.Split(':', 2);
            if (parts.Length < 2) continue;
            var v = parts[1].Trim();
            switch (parts[0])
            {
                case "time": k.Time = ParseF(v); break;
                case "value": k.Value = ParseF(v); break;
                case "inSlope": k.In = ParseF(v); break;
                case "outSlope": k.Out = ParseF(v); break;
                case "weightedMode": k.Weighted = v != "0"; break;
            }
        }
        return new Curve(keys);
    }

    // m_Modifications of a prefab instance: (target fileID, propertyPath) -> value
    public static Dictionary<(string, string), string> Overrides(Doc instance)
    {
        var mods = new Dictionary<(string, string), string>();
        string? target = null, path = null;
        foreach (var raw in instance.Lines)
        {
            var l = raw.Trim();
            if (l.StartsWith("- target:")) { target = FileIdOf(l); path = null; }
            else if (l.StartsWith("propertyPath:")) path = l["propertyPath:".Length..].Trim();
            else if (l.StartsWith("value:") && target != null && path != null) mods[(target, path)] = l["value:".Length..].Trim();
        }
        return mods;
    }
}

sealed class Key { public float Time, Value, In, Out; public bool Weighted; }

// unity AnimationCurve.Evaluate for unweighted keys, clamped outside the key range like the
// serialized curves here (m_PreInfinity / m_PostInfinity 2)
sealed class Curve
{
    public readonly List<Key> K;
    public Curve(List<Key> k) { K = k; }
    public int Length => K.Count;
    public Key Last => K[^1];

    public float Eval(float t)
    {
        if (K.Count == 0) return 0f;
        if (t <= K[0].Time) return K[0].Value;
        if (t >= K[^1].Time) return K[^1].Value;

        int i = 0;
        while (t >= K[i + 1].Time) i++;
        var a = K[i];
        var b = K[i + 1];
        if (float.IsInfinity(a.Out) || float.IsInfinity(b.In)) return a.Value;

        float dt = b.Time - a.Time;
        float s = (t - a.Time) / dt, s2 = s * s, s3 = s2 * s;
        return (2 * s3 - 3 * s2 + 1) * a.Value + (s3 - 2 * s2 + s) * dt * a.Out
             + (-2 * s3 + 3 * s2) * b.Value + (s3 - s2) * dt * b.In;
    }
}

// ---------------------------------------------------------------------------------------------
// game data
// ---------------------------------------------------------------------------------------------

enum UType
{
    ArrowCooldown, ArrowSpeed, ArrowCount, PickupRadius, AuraUnlock, AuraDamage, AuraRadius, ArrowDamage,
    MaxHealth, HealthRegen, AOEAttack, AOEAttackRadius, AOEAttackProjectileCount, AOEAttackDamage, CritChance,
    CritDamage, Pierce, MoveSpeed, AuraCooldown, AOEAttackCooldown, Cooldown, Area, WeaponSpeed,
    BowUnlock, Weapon, AOEAttackSmart
}

sealed class UpgradeDef
{
    public string File = "", Title = "";
    public UType Type;
    public float Value;
    public bool Additive, InPool, Overcharge;
    public int MaxLevel;
    public float OcCeiling, OcHalf;
}

sealed class Arch
{
    public string Name = "";
    public int Cost;
    public float Weight, BaseHealth, BaseSpeed, BaseDamage, TickInterval;
    public float Armor, DropChance, PrefabSpeed = 3f, Radius = 0.45f;
    public int WenOnKill = 1, MinWen = 1, MaxWen = 1;
    public bool HasDrop;
}

sealed class PlayerBase
{
    // defaults are the script defaults; the loader overwrites them with what's serialized
    public float MaxHealth = 100, InvulnOnHit = 0.15f, MoveSpeed = 6;
    public float ArrowCooldown = 1, ArrowDamage = 10, ArrowSpeed = 12, ArrowLifetime = 5, ArrowRadius = 0.2f, TargetRange = 12;
    public float PickupRadius = 1.5f, BodyRadius = 0.5f, Scale = 1;
    public float CritChance, CritMultiplier = 2;
    public float AuraDamage = 10, AuraInterval = 0.5f, AuraRadius = 2.5f;
    public float MeteorDamage = 20, MeteorInterval = 5, MeteorRadius = 8, MeteorFallSpeed = 10;
    public int MeteorCount = 1;
    public int PickupWen = 1;

    public float BodyRadiusWorld => BodyRadius * Scale;
    public float PickupRadiusWorld => PickupRadius * Scale;
}

sealed class DiffCurve
{
    public Curve Health = null!, Speed = null!, Damage = null!, Wen = null!;
    public float DensityMax = 6, PlateauTime = 900, Sharpness = 3, HealthDoublingMin = 4, DamageDoublingMin = 6;

    public float DensityAt(float t)
    {
        if (t <= 0f) return 1f;
        if (t >= PlateauTime) return DensityMax;
        float x = t / PlateauTime;
        float f = (1f - MathF.Exp(-Sharpness * x)) / (1f - MathF.Exp(-Sharpness));
        return MathF.Max(0.1f, 1f + (DensityMax - 1f) * f);
    }

    public float HealthAt(float t, bool endless) => MathF.Max(0.5f, Endless(Health, t, HealthDoublingMin, endless));
    public float SpeedAt(float t) => MathF.Max(0.5f, Speed.Eval(t));
    public float DamageAt(float t, bool endless) => MathF.Max(0.5f, Endless(Damage, t, DamageDoublingMin, endless));

    static float Endless(Curve c, float t, float doublingMinutes, bool endless)
    {
        if (c.Length == 0) return 1f;
        var last = c.Last;
        if (t <= last.Time || !endless) return c.Eval(t);
        float doublings = MathF.Min((t - last.Time) / (doublingMinutes * 60f), 100f);
        return last.Value * MathF.Pow(2f, doublings);
    }

    public int WenForLevel(int level)
    {
        if (Wen.Length == 0) return 5;
        float y = Wen.Eval(Math.Max(1, level));
        return Math.Max(1, (int)MathF.Round(y, MidpointRounding.ToEven));
    }
}

sealed class SpawnSettings
{
    public float DesiredPerScreen, BudgetPerSecond, FinalRushHealthMul, SpawnrateIncreasePerWave, PauseAfterClear, StartingBudget;
    public float FastDuration, FastCheckInterval, FastChance, FastSpawnrateMul, FastDensityMul, FastMinRunTime, FastPostClearDelay;
    public float FinalBossHealthMul, FinalBossScale;
    public int BaseMaxSpawnsPerFrame, ExtraSpawnsPerFrameAtMax;
    public float SpawnCapRamp, MinCdEarly, MinCdLate, MaxCdEarly, MaxCdLate;
    public List<Arch> Pool = new(), Chunky = new();
    public Arch? Fast, Boss, FinalBoss;
    public float CameraHalfHeight = 10, Aspect = 16f / 9f, OffscreenBand = 1;
}

sealed class LoopSettings
{
    public float WaveDuration = 180, BreakAfterWave = 2;
    public int BaseKillsToClear = 25, FinalWave = 10;
    public int Quota(int wave) => BaseKillsToClear * (int)MathF.Pow(1.4f, wave - 1);   // same cast as GameLoopController
}

sealed class GameData
{
    public PlayerBase P = new();
    public List<UpgradeDef> Upgrades = new();
    public DiffCurve Curve = new();
    public SpawnSettings S = new();
    public LoopSettings L = new();
    public List<string> Notes = new();

    const string Engine = "Assets/### Different Engine";

    public static GameData Load(string root)
    {
        var g = new GameData();
        var paths = new Dictionary<string, string>();     // guid -> asset path
        foreach (var dir in new[] { Engine, "Assets/Scenes" })
            foreach (var meta in Directory.EnumerateFiles(Path.Combine(root, dir), "*.meta", SearchOption.AllDirectories))
                foreach (var line in File.ReadLines(meta).Take(3))
                    if (line.StartsWith("guid: ")) { paths[line[6..].Trim()] = meta[..^5]; break; }

        string Script(string name) => paths.First(kv => Path.GetFileName(kv.Value) == name + ".cs").Key;
        Doc ByScript(List<Doc> docs, string script) => docs.First(d => Yaml.ScriptGuid(d) == Script(script));
        Doc? ByScriptOrNull(List<Doc> docs, string script) => docs.FirstOrDefault(d => Yaml.ScriptGuid(d) == Script(script));

        // upgrades: every UpgradeData asset, like PlayerInventory.FindAllUpgradeAssets
        var upgradeScript = Script("UpgradeData");
        foreach (var f in Directory.EnumerateFiles(Path.Combine(root, Engine, "Data"), "*.asset", SearchOption.AllDirectories))
        {
            var d = Yaml.Docs(f).FirstOrDefault(x => Yaml.ScriptGuid(x) == upgradeScript);
            if (d == null) continue;
            g.Upgrades.Add(new UpgradeDef
            {
                File = Path.GetFileNameWithoutExtension(f),
                Title = Yaml.Get(d, "title") ?? "",
                Type = (UType)Yaml.I(d, "type", 0),
                Value = Yaml.F(d, "value", 1.10f),
                Additive = Yaml.B(d, "additive", false),
                InPool = Yaml.B(d, "includeInPool", true),
                MaxLevel = Math.Max(1, Yaml.I(d, "maxLevel", 5)),
                Overcharge = Yaml.B(d, "overcharge", false),
                OcCeiling = MathF.Max(0f, Yaml.F(d, "overchargeCeiling", 1f)),
                OcHalf = MathF.Max(1f, Yaml.F(d, "overchargeHalfStacks", 8f)),
            });
        }
        g.Upgrades.Sort((a, b) => string.CompareOrdinal(a.File, b.File));

        var scene = Yaml.Docs(Path.Combine(root, "Assets/Scenes/MainTestGame.unity"));

        // difficulty + spawner
        var sd = ByScript(scene, "SpawnDirector");
        var curveDoc = Yaml.Docs(paths[Yaml.Guid(Yaml.Get(sd, "curve"))!]).First(d => d.ClassId == "114");
        g.Curve = new DiffCurve
        {
            Health = Yaml.Curve(curveDoc, "enemyHealth"),
            Speed = Yaml.Curve(curveDoc, "enemySpeed"),
            Damage = Yaml.Curve(curveDoc, "enemyDamage"),
            Wen = Yaml.Curve(curveDoc, "wenNeeded|gearsNeeded"),
            DensityMax = Yaml.F(curveDoc, "spawnDensityMax", 6),
            PlateauTime = Yaml.F(curveDoc, "spawnDensityPlateauTime", 900),
            Sharpness = Yaml.F(curveDoc, "spawnDensityCurveSharpness", 3),
            HealthDoublingMin = Yaml.F(curveDoc, "endlessHealthDoublingMinutes", 4),
            DamageDoublingMin = Yaml.F(curveDoc, "endlessDamageDoublingMinutes", 6),
        };

        var archCache = new Dictionary<string, Arch>();
        Arch? LoadArch(string? guid)
        {
            if (guid == null || !paths.ContainsKey(guid)) return null;
            if (archCache.TryGetValue(guid, out var cached)) return cached;

            var ad = Yaml.Docs(paths[guid]).First(d => d.ClassId == "114");
            var a = new Arch
            {
                Name = Path.GetFileNameWithoutExtension(paths[guid]),
                Cost = Yaml.I(ad, "cost", 1),
                Weight = Yaml.F(ad, "weight", 0.5f),
                BaseHealth = Yaml.F(ad, "baseHealth", 10),
                BaseSpeed = Yaml.F(ad, "baseSpeed", 1.5f),
                BaseDamage = Yaml.F(ad, "baseDamage", 5),
                TickInterval = Yaml.F(ad, "contactTickInterval", 0.5f),
            };

            if (Yaml.Guid(Yaml.Get(ad, "prefab")) is { } pg && paths.TryGetValue(pg, out var prefabPath))
            {
                var pd = Yaml.Docs(prefabPath);
                if (ByScriptOrNull(pd, "EnemyHealth") is { } eh)
                {
                    a.Armor = Yaml.F(eh, "armor", 0);
                    a.WenOnKill = Yaml.I(eh, "baseWenOnKill|baseGearsOnKill", 1);
                    a.DropChance = Yaml.F(eh, "wenDropChance|gearDropChance", 0.35f);
                    a.MinWen = Yaml.I(eh, "minWen|minGear", 1);
                    a.MaxWen = Yaml.I(eh, "maxWen|maxGear", 1);
                    a.HasDrop = Yaml.FileIdOf(Yaml.Get(eh, "wenDropPrefab|gearDropPrefab")) is { } fid && fid != "0";
                    if (a.HasDrop && Yaml.Guid(Yaml.Get(eh, "wenDropPrefab|gearDropPrefab")) is { } wenGuid && paths.TryGetValue(wenGuid, out var wenPath)
                        && ByScriptOrNull(Yaml.Docs(wenPath), "Pickup") is { } pick)
                        g.P.PickupWen = Yaml.I(pick, "wen|gears", 1);
                }
                if (ByScriptOrNull(pd, "EnemyMovement") is { } em) a.PrefabSpeed = Yaml.F(em, "_speed", 3);
                if (pd.FirstOrDefault(d => d.ClassId == "58") is { } cc) a.Radius = Yaml.F(cc, "m_Radius", 0.5f);
            }
            return archCache[guid] = a;
        }

        var s = g.S;
        s.DesiredPerScreen = Yaml.F(sd, "desiredPerScreen", 12);
        s.BudgetPerSecond = Yaml.F(sd, "budgetPerSecond", 10);
        s.FinalRushHealthMul = Yaml.F(sd, "finalRushHealthMul", 2);
        s.SpawnrateIncreasePerWave = Yaml.F(sd, "spawnrateIncreasePerWave", 2);
        s.PauseAfterClear = Yaml.F(sd, "pauseAfterClear", 2);
        s.StartingBudget = Yaml.F(sd, "startingBudget", 3);
        s.FastDuration = Yaml.F(sd, "fastPhaseDuration", 10);
        s.FastCheckInterval = Yaml.F(sd, "fastPhaseCheckInterval", 10);
        s.FastChance = Yaml.F(sd, "fastPhaseChance", 0.01f);
        s.FastSpawnrateMul = Yaml.F(sd, "fastPhaseSpawnrateMul", 3);
        s.FastDensityMul = Yaml.F(sd, "fastPhaseDensityMul", 3);
        s.FastMinRunTime = Yaml.F(sd, "fastPhaseMinRunTime", 120);
        s.FastPostClearDelay = Yaml.F(sd, "fastPhasePostClearDelay", 1);
        s.FinalBossHealthMul = Yaml.F(sd, "finalBossHealthMul", 12);
        s.FinalBossScale = Yaml.F(sd, "finalBossScale", 1.8f);
        s.BaseMaxSpawnsPerFrame = Yaml.I(sd, "baseMaxSpawnsPerFrame", 3);
        s.ExtraSpawnsPerFrameAtMax = Yaml.I(sd, "extraSpawnsPerFrameAtMax", 5);
        s.SpawnCapRamp = Yaml.F(sd, "spawnCapRampDuration", 600);
        s.MinCdEarly = Yaml.F(sd, "minSpawnCooldownEarly", 0.05f);
        s.MinCdLate = Yaml.F(sd, "minSpawnCooldownLate", 0.01f);
        s.MaxCdEarly = Yaml.F(sd, "maxSpawnCooldownEarly", 0.15f);
        s.MaxCdLate = Yaml.F(sd, "maxSpawnCooldownLate", 0.05f);
        s.OffscreenBand = Yaml.F(sd, "offscreenBand", 1);
        foreach (var guid in Yaml.RefList(sd, "pool")) if (LoadArch(guid) is { } a) s.Pool.Add(a);
        foreach (var guid in Yaml.RefList(sd, "chunkyPool")) if (LoadArch(guid) is { } a) s.Chunky.Add(a);
        s.Fast = LoadArch(Yaml.Guid(Yaml.Get(sd, "fastPhaseArchetype")));
        s.Boss = LoadArch(Yaml.Guid(Yaml.Get(sd, "bossArchetype")));
        s.FinalBoss = LoadArch(Yaml.Guid(Yaml.Get(sd, "finalBossArchetype"))) ?? s.Boss;

        var cam = scene.FirstOrDefault(d => d.ClassId == "20");
        if (cam != null) s.CameraHalfHeight = Yaml.F(cam, "orthographic size", 10);

        var gl = ByScript(scene, "GameLoopController");
        g.L = new LoopSettings
        {
            WaveDuration = Yaml.F(gl, "waveDuration", 180),
            BaseKillsToClear = Yaml.I(gl, "baseKillsToClear", 25),
            BreakAfterWave = Yaml.F(gl, "breakAfterWave", 2),
            FinalWave = Yaml.I(gl, "finalWave", 10),
        };

        // player: StatContext, PlayerHealth, PlayerMovement and AOEAttack were added to the prefab
        // instance in the scene; AutoShooter, AutoAimService and MagnetArea live on Player.prefab
        // with scene overrides on top; the Aura is its own object under the player
        var p = g.P;
        var ph = ByScript(scene, "PlayerHealth");
        p.MaxHealth = Yaml.F(ph, "maxHealth", p.MaxHealth);
        p.InvulnOnHit = Yaml.F(ph, "invulnTimeOnHit", p.InvulnOnHit);
        p.MoveSpeed = Yaml.F(ByScript(scene, "PlayerMovement"), "_speed", p.MoveSpeed);
        var sc = ByScript(scene, "StatContext");
        p.CritChance = Yaml.F(sc, "critChance", 0);
        p.CritMultiplier = Yaml.F(sc, "critMultiplier", 2);
        var aura = ByScript(scene, "Aura");
        p.AuraDamage = Yaml.F(aura, "damage", p.AuraDamage);
        p.AuraInterval = Yaml.F(aura, "damageInterval", p.AuraInterval);
        p.AuraRadius = Yaml.F(aura, "radius", p.AuraRadius);
        var aoe = ByScript(scene, "AOEAttack");
        p.MeteorDamage = Yaml.F(aoe, "damage", p.MeteorDamage);
        p.MeteorInterval = Yaml.F(aoe, "attackInterval", p.MeteorInterval);
        p.MeteorRadius = Yaml.F(aoe, "attackRange", p.MeteorRadius);
        p.MeteorCount = Yaml.I(aoe, "projectileCount", p.MeteorCount);
        if (Yaml.Guid(Yaml.Get(aoe, "projectilePrefab")) is { } meteorGuid && paths.TryGetValue(meteorGuid, out var meteorPath)
            && ByScriptOrNull(Yaml.Docs(meteorPath), "AOEProjectile") is { } meteor)
            p.MeteorFallSpeed = Yaml.F(meteor, "speed", p.MeteorFallSpeed);

        var playerPrefab = Path.Combine(root, Engine, "Prefabs/Player.prefab");
        var pp = Yaml.Docs(playerPrefab);
        var playerGuid = paths.First(kv => kv.Value == playerPrefab || kv.Value.Replace('\\', '/') == playerPrefab.Replace('\\', '/')).Key;
        var instance = scene.FirstOrDefault(d => d.ClassId == "1001" && Yaml.Guid(Yaml.Get(d, "m_SourcePrefab")) == playerGuid);
        var mods = instance != null ? Yaml.Overrides(instance) : new();
        float PF(Doc d, string key, float def)
        {
            foreach (var name in key.Split('|'))
                if (mods.TryGetValue((d.FileId, name), out var v) && v.Length > 0) return Yaml.ParseF(v);
            return Yaml.F(d, key, def);
        }

        var shooter = ByScript(pp, "AutoShooter");
        p.ArrowCooldown = PF(shooter, "baseCooldown", 1f);
        p.ArrowSpeed = PF(shooter, "baseArrowSpeed|baseBulletSpeed", p.ArrowSpeed);
        p.ArrowDamage = PF(shooter, "baseArrowDamage|baseBulletDamage", p.ArrowDamage);
        if (Yaml.Get(shooter, "baseCooldown") == null && Yaml.Get(shooter, "baseFireRate") != null)
            g.Notes.Add("AutoShooter: Player.prefab and the scene still store the old 'baseFireRate' field, which nothing reads. baseCooldown isn't serialized, so every run uses the code default of " + p.ArrowCooldown + "s.");
        p.TargetRange = PF(ByScript(pp, "AutoAimService"), "searchRadius", p.TargetRange);

        // AutoShooter sits on the player's root; the magnet is its own child object in the scene
        var rootGo = Yaml.FileIdOf(Yaml.Get(shooter, "m_GameObject"));
        if (ByScriptOrNull(scene, "MagnetArea") is { } magnet) p.PickupRadius = Yaml.F(magnet, "baseRadius", p.PickupRadius);
        var body = pp.FirstOrDefault(d => d.ClassId == "58" && Yaml.FileIdOf(Yaml.Get(d, "m_GameObject")) == rootGo);
        if (body != null) p.BodyRadius = PF(body, "m_Radius", p.BodyRadius);
        if (ByScriptOrNull(pp, "MagnetArea") is { } rootMagnet && Yaml.FileIdOf(Yaml.Get(rootMagnet, "m_GameObject")) == rootGo)
            g.Notes.Add("MagnetArea is back on the Player root, next to the body collider. It now refuses to take that collider over and logs an error; move it to a child object.");
        var rootTf = pp.FirstOrDefault(d => d.ClassId == "4" && Yaml.FileIdOf(Yaml.Get(d, "m_GameObject")) == rootGo);
        if (rootTf != null)
        {
            var m = Regex.Match(Yaml.Get(rootTf, "m_LocalScale") ?? "", @"x: (-?[\d.]+)");
            p.Scale = mods.TryGetValue((rootTf.FileId, "m_LocalScale.x"), out var sx) ? Yaml.ParseF(sx)
                    : m.Success ? Yaml.ParseF(m.Groups[1].Value) : 1f;
        }

        var arrowPath = Path.Combine(root, Engine, "Prefabs/Arrow.prefab");
        if (!File.Exists(arrowPath)) arrowPath = Path.Combine(root, Engine, "Prefabs/Bullet.prefab");
        var arrowDocs = Yaml.Docs(arrowPath);
        if (ByScriptOrNull(arrowDocs, "Projectile") is { } proj) p.ArrowLifetime = Yaml.F(proj, "maxLifetime", 5f);
        if (arrowDocs.FirstOrDefault(d => d.ClassId == "58") is { } bc) p.ArrowRadius = Yaml.F(bc, "m_Radius", 0.2f);

        // things worth knowing that the loader can see
        foreach (UType t in Enum.GetValues<UType>())
            if (!g.Upgrades.Any(u => u.Type == t))
                g.Notes.Add($"UpgradeType.{t} has no upgrade asset, so it never shows up in a run.");
        foreach (var u in g.Upgrades.Where(u => !u.InPool))
            g.Notes.Add($"'{u.Title}' ({u.File}) is switched off in the pool (includeInPool = 0).");
        var quotas = Enumerable.Range(1, g.L.FinalWave).Select(g.L.Quota).ToList();
        if (quotas.Count > 2 && quotas[0] == quotas[1])
            g.Notes.Add($"final rush quotas are {string.Join(", ", quotas)}: GameLoopController casts Mathf.Pow(1.4f, wave) to int before multiplying, so waves 1-3 all need {quotas[0]}. (int)(base * Mathf.Pow(...)) gives {string.Join(", ", Enumerable.Range(0, g.L.FinalWave).Select(w => (int)(g.L.BaseKillsToClear * MathF.Pow(1.4f, w))))}.");
        if (g.S.Pool.Any(a => a.WenOnKill > 0))
            g.Notes.Add("every kill pays wen twice: PlayerInventory adds 1 per kill through GameEvents.OnEnemyKilled and EnemyHealth.Die adds baseWenOnKill on top. The sim keeps both because the game does, but the wenNeeded curve was probably tuned against one of them.");
        g.Notes.Add("GameEvents.OnCollectAllWen is invoked after every final rush and the final boss, but nothing subscribes to it. Only WenSweepOnWaveClear collects dropped wen (1 each, while a magnet pickup pays " + g.P.PickupWen + ").");
        return g;
    }
}

// ---------------------------------------------------------------------------------------------
// global (meta) upgrades: a separate layer on top of the in-run stats
// ---------------------------------------------------------------------------------------------

#pragma warning disable CS0649 // survival stats the offense-only sim can't score yet
sealed class Meta
{
    public string Name = "Baseline", Note = "";
    public float Might;                 // all weapon damage, new multiplier
    public float ArrowDamage;          // arrow damage only, rides arrowDamageMul
    public float Cooldown;              // its own multiplicative layer, so it doesn't eat the 60% in-run cap
    public float Area, WeaponSpeed;     // added into the in-run Area / Weapon Speed buckets
    public int Amount, Pierce;
    public float CritChance, CritDamage;
    public float MaxHp, MoveSpeed, Magnet, Growth, Armor;
    public bool StartAura, StartMeteor;
    public int Rerolls;                 // per run; the game has no reroll yet, this models adding one

    public Meta With(string name, string note, Action<Meta> set)
    {
        var m = (Meta)MemberwiseClone();
        m.Name = name;
        m.Note = note;
        set(m);
        return m;
    }
}
#pragma warning restore CS0649

static class Scenarios
{
    public static readonly Meta Baseline = new();

    public static List<Meta> All() => new()
    {
        Baseline,
        Baseline.With("Might +25%", "all weapon damage x1.25 (new stat)", m => m.Might = 0.25f),
        Baseline.With("Arrow Dmg +25%", "arrows only, same bucket as the in-run pick", m => m.ArrowDamage = 0.25f),
        Baseline.With("Cooldown -10%", "own layer, x0.9 on every weapon", m => m.Cooldown = 0.10f),
        Baseline.With("Amount +1", "one more arrow per volley", m => m.Amount = 1),
        Baseline.With("Area +20%", "aura + meteor radius", m => m.Area = 0.20f),
        Baseline.With("Crit +10%", "crit chance", m => m.CritChance = 0.10f),
        Baseline.With("Crit Dmg +50%", "crit multiplier 2.0 -> 2.5", m => m.CritDamage = 0.5f),
        Baseline.With("Pierce +1", "arrows pass through one more enemy", m => m.Pierce = 1),
        Baseline.With("Growth +25%", "wen/XP gain (new stat)", m => m.Growth = 0.25f),
        Baseline.With("Magnet +50%", "pickup radius", m => m.Magnet = 0.5f),
        Baseline.With("Start: Aura", "aura unlocked at 0:00", m => m.StartAura = true),
        Baseline.With("Start: Meteor", "meteor unlocked at 0:00", m => m.StartMeteor = true),
        Baseline.With("Reroll x3", "3 rerolls a run, spent when no offer adds 2%", m => m.Rerolls = 3),
        Baseline.With("Reroll x10", "10 rerolls a run", m => m.Rerolls = 10),
        Baseline.With("Recommended", "Might 25%, Cooldown 10%, Amount 1, Area 20%, Growth 25%, 3 rerolls", m =>
        {
            m.Might = 0.25f; m.Cooldown = 0.10f; m.Amount = 1; m.Area = 0.20f; m.Growth = 0.25f; m.Rerolls = 3;
        }),
        Baseline.With("Everything", "all stat upgrades above except the start weapons", m =>
        {
            m.Might = 0.25f; m.Cooldown = 0.10f; m.Amount = 1; m.Area = 0.20f; m.CritChance = 0.10f;
            m.CritDamage = 0.5f; m.Pierce = 1; m.Growth = 0.25f; m.Magnet = 0.5f;
        }),
    };

    public static List<Meta> Endless()
    {
        var names = new[] { "Baseline", "Might +25%", "Cooldown -10%", "Amount +1", "Growth +25%", "Recommended", "Everything" };
        return All().Where(m => names.Contains(m.Name)).ToList();
    }
}

// ---------------------------------------------------------------------------------------------
// the player's stats, mirroring StatContext + PlayerInventory + the weapon scripts
// ---------------------------------------------------------------------------------------------

sealed class Build
{
    public const float MaxCooldownReduction = 0.6f;     // StatContext.MaxCooldownReduction
    const int NT = 23;

    public readonly GameData D;
    public readonly Meta M;
    PlayerBase P => D.P;

    public float ArrowSpeedMul = 1, PickupRadiusMul = 1, ArrowDamageMul = 1, HealthMul = 1, MoveSpeedMul = 1;
    public float CritChance, CritMultiplier;
    public int ArrowCountAdd, PierceAdd;
    public float[] Oc = new float[NT], CdRed = new float[NT], Global = new float[NT];

    public bool HasAura, HasMeteor;
    public float AuraDamageLv, AuraRadiusLv, MeteorIntervalLv, MeteorRangeLv, MeteorDamageLv;
    public int MeteorCountLv;

    public int[] Level, OcStacks;

    public Build(GameData d, Meta m)
    {
        D = d;
        M = m;
        Level = new int[d.Upgrades.Count];
        OcStacks = new int[d.Upgrades.Count];
        CritChance = P.CritChance;
        CritMultiplier = P.CritMultiplier;
        AuraDamageLv = P.AuraDamage;
        AuraRadiusLv = P.AuraRadius;
        MeteorIntervalLv = P.MeteorInterval;
        MeteorRangeLv = P.MeteorRadius;
        MeteorDamageLv = P.MeteorDamage;
        MeteorCountLv = P.MeteorCount;

        ArrowDamageMul *= 1f + m.ArrowDamage;
        Global[(int)UType.Area] += m.Area;
        Global[(int)UType.WeaponSpeed] += m.WeaponSpeed;
        ArrowCountAdd += m.Amount;
        PierceAdd += m.Pierce;
        CritChance = Math.Clamp(CritChance + m.CritChance, 0f, 1f);
        CritMultiplier += m.CritDamage;
        HealthMul *= 1f + m.MaxHp;
        MoveSpeedMul *= 1f + m.MoveSpeed;
        PickupRadiusMul *= 1f + m.Magnet;
        if (m.StartAura) Unlock(UType.AuraUnlock);
        if (m.StartMeteor) Unlock(UType.AOEAttack);

        // the simulated player is the default character, who starts with the bow. the WeaponData
        // weapons use their own scripts, so the loader never picks them up and they aren't simulated
        int bow = D.Upgrades.FindIndex(u => u.Type == UType.BowUnlock);
        if (bow >= 0) Level[bow] = D.Upgrades[bow].MaxLevel;
    }

    void Unlock(UType t)
    {
        int i = D.Upgrades.FindIndex(u => u.Type == t);
        if (i >= 0) Level[i] = Math.Max(Level[i], 1);
        if (t == UType.AuraUnlock) HasAura = true; else HasMeteor = true;
    }

    public Build Clone()
    {
        var b = (Build)MemberwiseClone();
        b.Oc = (float[])Oc.Clone();
        b.CdRed = (float[])CdRed.Clone();
        b.Global = (float[])Global.Clone();
        b.Level = (int[])Level.Clone();
        b.OcStacks = (int[])OcStacks.Clone();
        return b;
    }

    // --- StatContext ---
    float Reduction(UType t) => MathF.Min(CdRed[(int)t], MaxCooldownReduction);
    public float OC(UType t) => 1f + Oc[(int)t];
    public float OCBonus(UType t) => Oc[(int)t];
    public float AreaMul => 1f + Global[(int)UType.Area];
    public float WeaponSpeedMul => 1f + Global[(int)UType.WeaponSpeed];
    public float CooldownFor(UType weapon, float baseCd) =>
        baseCd * (1f - Reduction(UType.Cooldown)) * (1f - Reduction(weapon)) / OC(UType.Cooldown) * (1f - M.Cooldown);

    // --- AutoShooter / Projectile ---
    public float VolleyCd => MathF.Max(0.03f, CooldownFor(UType.ArrowCooldown, P.ArrowCooldown));
    public int Shots => Math.Max(1, 1 + ArrowCountAdd);
    public float ArrowDamage => P.ArrowDamage * ArrowDamageMul * OC(UType.ArrowDamage) * (1f + M.Might);
    public float ArrowSpeed => P.ArrowSpeed * ArrowSpeedMul * WeaponSpeedMul;
    public float CritMult => CritMultiplier + OCBonus(UType.CritDamage);
    public int Pierce => PierceAdd;

    // --- Aura ---
    public float AuraDmg => AuraDamageLv * OC(UType.AuraDamage) * (1f + M.Might);
    public float AuraR => AuraRadiusLv * AreaMul;
    public float AuraTick => MathF.Max(0.1f, CooldownFor(UType.AuraCooldown, P.AuraInterval));

    // --- AOEAttack (meteor) ---
    public float MeteorDmg => MeteorDamageLv * OC(UType.AOEAttackDamage) * (1f + M.Might);
    public float MeteorR => MeteorRangeLv * AreaMul;
    public float MeteorCd => MathF.Max(0.75f, CooldownFor(UType.AOEAttackCooldown, MeteorIntervalLv));

    // --- body ---
    public float MaxHp => P.MaxHealth * HealthMul * OC(UType.MaxHealth);
    public float MoveSpeed => P.MoveSpeed * MoveSpeedMul;
    public float PickupRadius => P.PickupRadius * PickupRadiusMul;

    public bool IsAtCap(int i) => Level[i] >= D.Upgrades[i].MaxLevel;
    public bool CanOffer(int i, bool endless) => !IsAtCap(i) || (D.Upgrades[i].Overcharge && endless);
    public bool IsOvercharging(int i, bool endless) => IsAtCap(i) && D.Upgrades[i].Overcharge && endless;

    // PlayerInventory.ApplyUpgrade -> StatContext.Apply -> weapon upgrades -> LevelUp
    public void Apply(int i, bool endless)
    {
        var u = D.Upgrades[i];
        if (IsOvercharging(i, endless))
        {
            int s = ++OcStacks[i];
            Oc[(int)u.Type] = u.OcCeiling * s / (s + u.OcHalf);
            return;
        }

        float Mul() => u.Additive ? 1f + u.Value : u.Value;
        switch (u.Type)
        {
            case UType.Area:
            case UType.WeaponSpeed: Global[(int)u.Type] += u.Value; break;
            case UType.Cooldown:
            case UType.ArrowCooldown:
            case UType.AuraCooldown:
            case UType.AOEAttackCooldown: CdRed[(int)u.Type] += u.Value; break;
            case UType.ArrowSpeed: ArrowSpeedMul *= Mul(); break;
            case UType.ArrowCount: ArrowCountAdd += (int)MathF.Round(u.Value); break;
            case UType.PickupRadius: PickupRadiusMul *= Mul(); break;
            case UType.ArrowDamage: ArrowDamageMul *= Mul(); break;
            case UType.MaxHealth: HealthMul *= Mul(); break;
            case UType.CritChance: CritChance = Math.Clamp(CritChance + u.Value, 0f, 1f); break;
            case UType.CritDamage: CritMultiplier += u.Value; break;
            case UType.Pierce: PierceAdd += (int)MathF.Round(u.Value); break;
            case UType.MoveSpeed: MoveSpeedMul *= Mul(); break;
        }

        switch (u.Type)
        {
            case UType.AuraUnlock: HasAura = true; break;
            case UType.AuraDamage: if (HasAura) AuraDamageLv *= u.Value; break;
            case UType.AuraRadius: if (HasAura) AuraRadiusLv *= u.Value; break;
            case UType.AOEAttack:
                if (Level[i] == 0) HasMeteor = true;
                else MeteorUpgrade(0.9f, 1.15f, 1f, 0);
                break;
            case UType.AOEAttackRadius: MeteorUpgrade(1f, 1f, u.Value, 0); break;
            case UType.AOEAttackProjectileCount: MeteorUpgrade(1f, 1f, 1f, 1); break;
            case UType.AOEAttackDamage: MeteorUpgrade(1f, u.Value, 1f, 0); break;
        }

        if (Level[i] < u.MaxLevel) Level[i]++;
    }

    void MeteorUpgrade(float interval, float damage, float range, int extra)
    {
        MeteorIntervalLv = MathF.Max(0.5f, MeteorIntervalLv * interval);
        MeteorDamageLv *= damage;
        MeteorRangeLv *= range;
        MeteorCountLv = Math.Max(1, MeteorCountLv + extra);
    }
}

// ---------------------------------------------------------------------------------------------
// one simulated run
// ---------------------------------------------------------------------------------------------

enum Policy { Random, Greedy }

sealed class Enemy
{
    public Arch A = null!;
    public float Hp, Armor, Radius, Speed, Dist, Cos, Sin, X, Y;
    public bool Dead;
}

struct Snap
{
    public float Minute, KillRate, ArrowDmg, VolleyCd, CritChance, CritMult, AuraDmg, AuraR, MeteorDmg, MeteorR, MeteorCd, MaxHp, Area, GruntHp, Alive, CapBlocked;
    public int Level, Shots, Pierce, MeteorCount, Wave;
    public bool Aura, Meteor, Rush;
}

sealed class RunResult
{
    public readonly List<Snap> Snaps = new();
    public readonly List<float> RushSeconds = new();
    public float FinalBossMinute = float.NaN, FinalBossHp, FinalBossTtkArrows, FinalBossTtkAll;
    public int FinalBossLevel, WavesCleared, RerollsUsed;
    public bool Stalled;
    public Build? FinalBuild;
}

sealed class RunSim
{
    const float Dt = 0.05f, FrameDt = 1f / 60f, Packing = 0.8f, RushStallSeconds = 15 * 60f, EndlessCapSeconds = 75 * 60f;

    readonly GameData D;
    readonly SpawnSettings S;
    readonly Meta M;
    readonly Policy Pol;
    readonly bool Endless;
    readonly Random Rng;
    public readonly Build B;
    readonly RunResult R = new();

    readonly List<Enemy> alive = new(512);
    readonly List<Enemy> targets = new(16);
    readonly List<Enemy> inView = new(512);
    readonly List<int> offers = new(4);
    readonly List<int> pool = new(32);
    bool[] covered = new bool[512];

    float t, runTime;
    float budget, budgetPerSecond, spawnCooldown, spawnPausedUntil, fastPhaseUntil = -1f, nextFastPhaseCheck, fastStartAt = -1f;
    bool finalRush, preparingFast;
    int currentWave, bossesSpawnedThisRush;
    readonly List<Enemy> activeBosses = new();

    bool loopRush;
    int rushKills;

    int level = 1, groundPickups, kills, rerollsLeft;
    float wenCount;
    int wenForUpgrade;

    float shootCd, auraNext, meteorTimer;
    bool auraOn;

    float nextSnap = 60f;
    int killsAtLastSnap, capFrames, frames;

    public RunSim(GameData d, Meta m, Policy p, bool endless, int seed)
    {
        D = d;
        S = d.S;
        M = m;
        Pol = p;
        Endless = endless;
        Rng = new Random(seed);
        B = new Build(d, m);
        rerollsLeft = m.Rerolls;
    }

    public RunResult Run()
    {
        budget = S.StartingBudget;
        budgetPerSecond = S.BudgetPerSecond;
        nextFastPhaseCheck = S.FastCheckInterval;
        wenForUpgrade = D.Curve.WenForLevel(level);

        for (int wave = 1; ; wave++)
        {
            loopRush = false;
            for (float el = 0f; el < D.L.WaveDuration; el += Dt)
            {
                Step();
                runTime += Dt;
            }

            // final rush: GameLoopController + SpawnDirector.HandleFinalRushStart
            loopRush = true;
            rushKills = 0;
            runTime = wave * D.L.WaveDuration;
            int quota = D.L.Quota(wave);
            finalRush = true;
            currentWave = wave;
            bossesSpawnedThisRush = 0;
            activeBosses.Clear();
            TrySpawnInitialBoss();

            float rushStart = t;
            while (rushKills < quota)
            {
                Step();
                if (t - rushStart > RushStallSeconds) { R.Stalled = true; break; }
            }
            R.RushSeconds.Add(t - rushStart);
            if (R.Stalled) break;

            // purge, then OnFinalRushEnded / OnWaveCleared / wen sweep
            foreach (var e in alive) if (!e.Dead) Die(e);
            Compact();
            finalRush = false;
            spawnPausedUntil = t + S.PauseAfterClear;
            activeBosses.Clear();
            budgetPerSecond += S.SpawnrateIncreasePerWave;
            R.WavesCleared = wave;
            int swept = groundPickups;
            groundPickups = 0;
            for (int i = 0; i < swept; i++) AddWen(1);

            for (float br = 0f; br < D.L.BreakAfterWave; br += Dt) Step();

            if (!Endless && wave >= D.L.FinalWave) { FinalBoss(); break; }
            if (Endless && t >= EndlessCapSeconds) break;
        }

        R.FinalBuild = B;
        return R;
    }

    void FinalBoss()
    {
        var arch = S.FinalBoss ?? S.Boss;
        R.FinalBossMinute = t / 60f;
        R.FinalBossLevel = level;
        if (arch == null) return;

        float hp = arch.BaseHealth * D.Curve.HealthAt(t, Endless) * S.FinalBossHealthMul;
        R.FinalBossHp = hp;

        float perHit = B.ArrowDamage * (1f + B.CritChance * (B.CritMult - 1f)) - arch.Armor;
        float arrowDps = perHit > 0 ? perHit * B.Shots / B.VolleyCd : 0f;
        float auraDps = B.HasAura ? MathF.Max(0f, B.AuraDmg - arch.Armor) / B.AuraTick : 0f;
        float meteorDps = B.HasMeteor ? MathF.Max(0f, B.MeteorDmg - arch.Armor) * B.MeteorCountLv / B.MeteorCd : 0f;
        R.FinalBossTtkArrows = arrowDps > 0 ? hp / arrowDps : float.PositiveInfinity;
        R.FinalBossTtkAll = hp / MathF.Max(1e-3f, arrowDps + auraDps + meteorDps);
    }

    // ---------------------------------------------------------------- per step

    void Step()
    {
        for (float f = 0f; f < Dt - 1e-4f; f += FrameDt)
        {
            t += FrameDt;
            SpawnerFrame();
        }

        Move();
        Shoot();
        AuraPulse();
        Meteors();
        Compact();

        if (t >= nextSnap) TakeSnap();
    }

    // SpawnDirector.Update, one frame
    void SpawnerFrame()
    {
        if (t < spawnPausedUntil) return;

        bool inFast = t < fastPhaseUntil;
        if (!preparingFast && !inFast && runTime >= S.FastMinRunTime && t >= nextFastPhaseCheck)
        {
            nextFastPhaseCheck = t + S.FastCheckInterval;
            if (Rng.NextSingle() < S.FastChance) { preparingFast = true; spawnCooldown = 0f; }
        }

        float density = D.Curve.DensityAt(t);
        float rushScale = finalRush ? density * 1.2f : density;
        int aliveCount = alive.Count;

        if (preparingFast)
        {
            if (aliveCount <= 0 && fastStartAt < 0f) fastStartAt = t + S.FastPostClearDelay;
            if (fastStartAt >= 0f && t >= fastStartAt)
            {
                preparingFast = false;
                fastStartAt = -1f;
                fastPhaseUntil = t + S.FastDuration;
                spawnCooldown = 0f;
            }
            return;
        }

        float spawnrateMul = inFast ? S.FastSpawnrateMul : 1f;
        float target = S.DesiredPerScreen * (inFast ? S.FastDensityMul : 1f) * rushScale;
        budget += budgetPerSecond * spawnrateMul * FrameDt * rushScale;

        float ramp = S.SpawnCapRamp > 0f ? Math.Clamp(t / S.SpawnCapRamp, 0f, 1f) : 1f;
        int maxPerFrame = S.BaseMaxSpawnsPerFrame + (int)MathF.Round(S.ExtraSpawnsPerFrameAtMax * ramp);
        if (inFast) maxPerFrame *= 2;

        spawnCooldown -= FrameDt;
        float minCd = Lerp(S.MinCdEarly, S.MinCdLate, ramp);
        float maxCd = Lerp(S.MaxCdEarly, S.MaxCdLate, ramp);

        frames++;
        if (spawnCooldown <= 0f && aliveCount >= target) capFrames++;

        int spawned = 0;
        while (spawned < maxPerFrame && aliveCount < target && spawnCooldown <= 0f)
        {
            var arch = ChooseArch(inFast);
            if (arch == null || budget < arch.Cost) break;

            var e = Spawn(arch, finalRush);
            budget -= arch.Cost;
            aliveCount++;
            spawned++;
            if (arch == S.Boss) { activeBosses.Add(e); bossesSpawnedThisRush++; }

            spawnCooldown = Lerp(minCd, maxCd, Rng.NextSingle()) / spawnrateMul;
        }
    }

    Arch? ChooseArch(bool inFast)
    {
        if (finalRush && S.Boss != null)
        {
            int maxBosses = currentWave >= 5 ? 4 : currentWave >= 3 ? 2 : 1;
            int aliveBosses = 0;
            foreach (var b in activeBosses) if (!b.Dead) aliveBosses++;
            if (aliveBosses < maxBosses && bossesSpawnedThisRush < maxBosses) return S.Boss;
            if (inFast && S.Fast != null) return S.Fast;
            return Pick(true);
        }
        if (inFast && S.Fast != null) return S.Fast;
        return Pick(finalRush);
    }

    Arch? Pick(bool rush)
    {
        var src = !rush || S.Chunky.Count == 0 ? S.Pool : S.Chunky;
        if (src.Count == 0) return null;
        float sum = 0f;
        foreach (var a in src) sum += MathF.Max(0.0001f, a.Weight);
        float r = Rng.NextSingle() * sum, acc = 0f;
        foreach (var a in src)
        {
            acc += MathF.Max(0.0001f, a.Weight);
            if (r <= acc) return a;
        }
        return src[^1];
    }

    void TrySpawnInitialBoss()
    {
        if (S.Boss == null) return;
        int maxBosses = currentWave >= 5 ? 4 : currentWave >= 3 ? 2 : 1;
        int aliveBosses = 0;
        foreach (var b in activeBosses) if (!b.Dead) aliveBosses++;
        if (aliveBosses >= maxBosses) return;

        activeBosses.Add(Spawn(S.Boss, true));
        bossesSpawnedThisRush++;
        budget = MathF.Max(0f, budget - S.Boss.Cost);
    }

    Enemy Spawn(Arch a, bool rush)
    {
        float hpMul = D.Curve.HealthAt(t, Endless) * (rush ? S.FinalRushHealthMul : 1f);
        var e = new Enemy
        {
            A = a,
            Hp = MathF.Max(1f, a.BaseHealth * hpMul),
            Armor = a.Armor,
            Radius = a.Radius,
            Speed = a.PrefabSpeed * D.Curve.SpeedAt(t) * a.BaseSpeed,
        };

        // a point in the band just off screen, picked by area like GetSpawnPositionNearOffscreenInsideBounds
        float h = S.CameraHalfHeight, w = h * S.Aspect, band = S.OffscreenBand, pad = 0.01f;
        float side = (band - pad) * 2f * h, topBottom = 2f * w * (band - pad);
        float pick = Rng.NextSingle() * (2 * side + 2 * topBottom);
        float x, y;
        if (pick < side) { x = -w - pad - Rng.NextSingle() * (band - pad); y = (Rng.NextSingle() * 2 - 1) * h; }
        else if (pick < 2 * side) { x = w + pad + Rng.NextSingle() * (band - pad); y = (Rng.NextSingle() * 2 - 1) * h; }
        else if (pick < 2 * side + topBottom) { y = -h - pad - Rng.NextSingle() * (band - pad); x = (Rng.NextSingle() * 2 - 1) * w; }
        else { y = h + pad + Rng.NextSingle() * (band - pad); x = (Rng.NextSingle() * 2 - 1) * w; }

        e.Dist = MathF.Sqrt(x * x + y * y);
        e.Cos = x / e.Dist;
        e.Sin = y / e.Dist;
        e.X = x;
        e.Y = y;

        // keep the list sorted by distance
        int i = alive.Count;
        alive.Add(e);
        while (i > 0 && alive[i - 1].Dist > e.Dist) { alive[i] = alive[i - 1]; i--; }
        alive[i] = e;
        return e;
    }

    // everyone walks straight at the player and queues up behind whoever is already packed around them
    void Move()
    {
        for (int i = 1; i < alive.Count; i++)
        {
            var e = alive[i];
            int j = i;
            while (j > 0 && alive[j - 1].Dist > e.Dist) { alive[j] = alive[j - 1]; j--; }
            alive[j] = e;
        }

        float cum = 0f, rp = D.P.BodyRadiusWorld;
        foreach (var e in alive)
        {
            float contact = rp + e.Radius;
            float minD = MathF.Sqrt(contact * contact + cum / MathF.PI);
            float nd = e.Dist - e.Speed * Dt;
            if (nd < minD) nd = MathF.Min(e.Dist, minD);
            e.Dist = nd;
            e.X = nd * e.Cos;
            e.Y = nd * e.Sin;
            cum += MathF.PI * e.Radius * e.Radius / Packing;
        }
    }

    // AutoShooter.Update + AutoAimService.FindTargets
    void Shoot()
    {
        shootCd -= Dt;
        while (shootCd <= 0f)
        {
            int shots = B.Shots;
            targets.Clear();
            foreach (var e in alive)
            {
                if (e.Dead) continue;
                if (e.Dist > D.P.TargetRange + e.Radius) break;
                targets.Add(e);
                if (targets.Count >= shots) break;
            }
            if (targets.Count == 0) { shootCd = 0f; return; }

            for (int i = 0; i < shots; i++)
            {
                var tg = i < targets.Count ? targets[i] : targets[0];
                FireArrow(tg.Cos, tg.Sin);
            }
            shootCd += B.VolleyCd + FrameDt * 0.5f;
        }
    }

    void FireArrow(float dx, float dy)
    {
        float dmg = B.ArrowDamage, cc = B.CritChance, cm = B.CritMult;
        int pierceLeft = B.Pierce;
        foreach (var e in alive)
        {
            if (e.Dead) continue;
            float along = e.X * dx + e.Y * dy;
            if (along <= 0f) continue;
            float across = MathF.Abs(e.X * dy - e.Y * dx);
            if (across > D.P.ArrowRadius + e.Radius) continue;

            bool crit = cc > 0f && Rng.NextSingle() < cc;
            Hit(e, crit ? dmg * cm : dmg);
            if (pierceLeft-- <= 0) return;
        }
    }

    void AuraPulse()
    {
        if (!B.HasAura) return;
        if (!auraOn) { auraOn = true; auraNext = t; }     // OnEnable pulses right away
        if (t < auraNext) return;

        float reach = B.AuraR, dmg = B.AuraDmg;
        foreach (var e in alive)
        {
            if (e.Dist > reach + 1.2f) break;
            if (!e.Dead && e.Dist <= reach + e.Radius) Hit(e, dmg);
        }
        auraNext += B.AuraTick + FrameDt * 0.5f;
        if (auraNext < t) auraNext = t;
    }

    // AOEAttack.FireProjectiles with smart targeting, landing instantly
    void Meteors()
    {
        if (!B.HasMeteor) return;
        meteorTimer += Dt;
        float cd = B.MeteorCd;
        if (meteorTimer < cd) return;
        meteorTimer -= cd + FrameDt * 0.5f;
        if (meteorTimer < 0f) meteorTimer = 0f;

        float h = S.CameraHalfHeight, w = h * S.Aspect;
        float xMin = -w + 0.1f * 2 * w, xMax = -w + 0.9f * 2 * w, yMin = -h + 0.2f * 2 * h, yMax = -h + 0.9f * 2 * h;

        inView.Clear();
        foreach (var e in alive)
            if (!e.Dead && e.X >= xMin && e.X <= xMax && e.Y >= yMin && e.Y <= yMax) inView.Add(e);
        if (covered.Length < inView.Count) covered = new bool[inView.Count * 2];
        Array.Clear(covered, 0, inView.Count);

        float r = B.MeteorR, r2 = r * r, dmg = B.MeteorDmg;
        for (int m = 0; m < B.MeteorCountLv; m++)
        {
            float ix, iy;
            if (inView.Count == 0)
            {
                ix = xMin + Rng.NextSingle() * (xMax - xMin);
                iy = yMin + Rng.NextSingle() * (yMax - yMin);
            }
            else
            {
                bool anyFree = false;
                for (int i = 0; i < inView.Count; i++) if (!covered[i]) { anyFree = true; break; }

                int best = -1, bestCount = 0;
                float bestDist = float.MaxValue;
                for (int i = 0; i < inView.Count; i++)
                {
                    if (anyFree && covered[i]) continue;
                    int count = 0;
                    var a = inView[i];
                    for (int j = 0; j < inView.Count; j++)
                    {
                        if (anyFree && covered[j]) continue;
                        float ddx = inView[j].X - a.X, ddy = inView[j].Y - a.Y;
                        if (ddx * ddx + ddy * ddy <= r2) count++;
                    }
                    float dist = a.X * a.X + a.Y * a.Y;
                    if (count > bestCount || (count == bestCount && dist < bestDist)) { best = i; bestCount = count; bestDist = dist; }
                }

                float sx = 0f, sy = 0f;
                int members = 0;
                var seed = inView[best];
                for (int j = 0; j < inView.Count; j++)
                {
                    float ddx = inView[j].X - seed.X, ddy = inView[j].Y - seed.Y;
                    if (ddx * ddx + ddy * ddy > r2) continue;
                    sx += inView[j].X;
                    sy += inView[j].Y;
                    members++;
                    covered[j] = true;
                }
                ix = Math.Clamp(sx / members, xMin, xMax);
                iy = Math.Clamp(sy / members, yMin, yMax);
            }

            foreach (var e in alive)
            {
                if (e.Dead) continue;
                float ddx = e.X - ix, ddy = e.Y - iy, reach = r + e.Radius;
                if (ddx * ddx + ddy * ddy <= reach * reach) Hit(e, dmg);
            }
        }
    }

    void Hit(Enemy e, float raw)
    {
        if (e.Dead) return;
        float dmg = raw - e.Armor;
        if (dmg <= 0f) return;
        e.Hp -= dmg;
        if (e.Hp <= 0f) Die(e);
    }

    // EnemyHealth.Die: the kill event pays a wen, baseWenOnKill pays again, drops wait on the floor
    void Die(Enemy e)
    {
        if (e.Dead) return;
        e.Dead = true;
        kills++;
        if (loopRush) rushKills++;

        AddWen(1);
        if (e.A.WenOnKill > 0) AddWen(e.A.WenOnKill);

        if (e.A.HasDrop && (e.A.MinWen > 0 || e.A.MaxWen > 0) && Rng.NextSingle() <= e.A.DropChance)
        {
            int n = Rng.Next(e.A.MinWen, e.A.MaxWen + 1);
            bool inMagnet = e.Dist <= B.PickupRadius * D.P.Scale + 0.2f;
            for (int i = 0; i < n; i++)
            {
                if (inMagnet) AddWen(D.P.PickupWen);
                else groundPickups++;
            }
        }
    }

    void Compact()
    {
        int w = 0;
        for (int i = 0; i < alive.Count; i++)
            if (!alive[i].Dead) alive[w++] = alive[i];
        alive.RemoveRange(w, alive.Count - w);
    }

    // ---------------------------------------------------------------- leveling

    void AddWen(int amount)
    {
        wenCount += amount * (1f + M.Growth);
        if (wenCount >= wenForUpgrade)
        {
            wenCount -= wenForUpgrade;
            level++;
            wenForUpgrade = D.Curve.WenForLevel(level);
            LevelUp();
        }
    }

    // PlayerInventory.OpenUpgradeMenu + the choice. a reroll redraws all three offers like
    // Vampire Survivors does, and only gets spent when nothing on the table is worth RerollBelow
    const float RerollBelow = 0.02f;

    void LevelUp()
    {
        DrawOffers();
        if (offers.Count == 0) return;

        if (Pol == Policy.Random)
        {
            B.Apply(offers[Rng.Next(offers.Count)], Endless);
            return;
        }

        int choice = Greedy(out float gain);
        while (gain < RerollBelow && rerollsLeft > 0)
        {
            rerollsLeft--;
            R.RerollsUsed++;
            DrawOffers();
            choice = Greedy(out gain);
        }
        B.Apply(choice, Endless);
    }

    void DrawOffers()
    {
        pool.Clear();
        for (int i = 0; i < D.Upgrades.Count; i++)
        {
            var u = D.Upgrades[i];
            if (!u.InPool || !B.CanOffer(i, Endless)) continue;
            if (B.HasAura ? u.Type == UType.AuraUnlock
                          : u.Type is UType.AuraDamage or UType.AuraRadius or UType.AuraCooldown) continue;
            if (B.HasMeteor ? u.Type == UType.AOEAttack && B.IsAtCap(i)
                            : u.Type is UType.AOEAttackRadius or UType.AOEAttackProjectileCount or UType.AOEAttackDamage or UType.AOEAttackCooldown) continue;
            pool.Add(i);
        }

        offers.Clear();
        for (int k = 0; k < 3 && pool.Count > 0; k++)
        {
            float total = 0f;
            foreach (var i in pool) total += B.IsOvercharging(i, Endless) ? 0.35f : 1f;
            float r = Rng.NextSingle() * total;
            int pickAt = pool.Count - 1;
            for (int j = 0; j < pool.Count; j++)
            {
                r -= B.IsOvercharging(pool[j], Endless) ? 0.35f : 1f;
                if (r <= 0f) { pickAt = j; break; }
            }
            offers.Add(pool[pickAt]);
            pool.RemoveAt(pickAt);
        }
    }

    int Greedy(out float bestGain)
    {
        float now = Score(B);
        int best = offers[0];
        bestGain = float.MinValue;
        foreach (var i in offers)
        {
            var copy = B.Clone();
            copy.Apply(i, Endless);
            float gain = (Score(copy) - now) / MathF.Max(1e-4f, now);
            if (gain <= 1e-5f) gain = D.Upgrades[i].Type switch
            {
                UType.MaxHealth or UType.MoveSpeed => 0.015f,
                UType.PickupRadius => 0.01f,
                _ => 0.005f,
            };
            if (gain > bestGain) { bestGain = gain; best = i; }
        }
        return best;
    }

    // kills per second against a packed crowd: 80% grunt, 20% armored fat
    float Score(Build b)
    {
        float hpMul = D.Curve.HealthAt(t, Endless);
        float crowd = MathF.Min(S.DesiredPerScreen * D.Curve.DensityAt(t), 150f);
        return 0.8f * KillRate(b, 10f * hpMul, 0f, crowd) + 0.2f * KillRate(b, 50f * hpMul * 1.3f, 5f, crowd);
    }

    float KillRate(Build b, float hp, float armor, float crowd)
    {
        const float re = 0.45f;
        float rc = D.P.BodyRadiusWorld + re;
        float Packed(float reach) => Math.Clamp((reach * reach - rc * rc) * Packing / (re * re), 0f, crowd);

        // hits to kill stays continuous (plus half a hit of overkill) so every damage or crit
        // gain scores above zero instead of only the ones that cross a breakpoint
        static float Hits(float hp, float perHit) => MathF.Max(1f, hp / perHit + 0.5f);

        float k = 0f;
        float perHit = b.ArrowDamage * (1f + b.CritChance * (b.CritMult - 1f)) - armor;
        if (perHit > 0f)
            k += b.Shots / b.VolleyCd * (1f + MathF.Min(b.Pierce, crowd) * 0.5f) / Hits(hp, perHit);
        if (b.HasAura && b.AuraDmg > armor)
            k += Packed(b.AuraR + re) / b.AuraTick / Hits(hp, b.AuraDmg - armor);
        if (b.HasMeteor && b.MeteorDmg > armor)
            k += b.MeteorCountLv * MathF.Min(crowd * 0.5f, (b.MeteorR + re) * (b.MeteorR + re) * Packing / (re * re)) / b.MeteorCd
                 / Hits(hp, b.MeteorDmg - armor);
        return k;
    }

    void TakeSnap()
    {
        R.Snaps.Add(new Snap
        {
            Minute = MathF.Round(t / 60f),
            Level = level,
            KillRate = (kills - killsAtLastSnap) / 60f,
            ArrowDmg = B.ArrowDamage,
            VolleyCd = B.VolleyCd,
            Shots = B.Shots,
            Pierce = B.Pierce,
            CritChance = B.CritChance,
            CritMult = B.CritMult,
            Aura = B.HasAura,
            AuraDmg = B.AuraDmg,
            AuraR = B.AuraR,
            Meteor = B.HasMeteor,
            MeteorDmg = B.MeteorDmg,
            MeteorR = B.MeteorR,
            MeteorCd = B.MeteorCd,
            MeteorCount = B.MeteorCountLv,
            MaxHp = B.MaxHp,
            Area = B.AreaMul,
            GruntHp = 10f * D.Curve.HealthAt(t, Endless),
            Alive = alive.Count,
            CapBlocked = frames > 0 ? (float)capFrames / frames : 0f,
            Wave = R.WavesCleared + 1,
            Rush = loopRush,
        });
        killsAtLastSnap = kills;
        capFrames = frames = 0;
        nextSnap += 60f;
    }

    static float Lerp(float a, float b, float k) => a + (b - a) * k;
}

// ---------------------------------------------------------------------------------------------
// many runs -> percentiles
// ---------------------------------------------------------------------------------------------

sealed class ScenarioResult
{
    public Meta Meta = null!;
    public Policy Policy;
    public bool Endless;
    public List<RunResult> Runs = new();

    public int MaxMinute => Runs.Max(r => r.Snaps.Count);

    public float Pct(Func<RunResult, float> f, float p)
    {
        var v = Runs.Select(f).Where(x => !float.IsNaN(x)).OrderBy(x => x).ToList();
        if (v.Count == 0) return float.NaN;
        float idx = p * (v.Count - 1);
        int lo = (int)MathF.Floor(idx), hi = Math.Min(v.Count - 1, lo + 1);
        return v[lo] + (v[hi] - v[lo]) * (idx - lo);
    }

    // percentile of a per-minute value, over the runs that are still going at that minute
    public float AtMinute(int minute, Func<Snap, float> f, float p = 0.5f)
    {
        var v = Runs.Where(r => r.Snaps.Count >= minute).Select(r => f(r.Snaps[minute - 1])).OrderBy(x => x).ToList();
        if (v.Count == 0) return float.NaN;
        float idx = p * (v.Count - 1);
        int lo = (int)MathF.Floor(idx), hi = Math.Min(v.Count - 1, lo + 1);
        return v[lo] + (v[hi] - v[lo]) * (idx - lo);
    }

    public float ShareAtMinute(int minute, Func<Snap, bool> f)
    {
        var v = Runs.Where(r => r.Snaps.Count >= minute).ToList();
        return v.Count == 0 ? float.NaN : v.Count(r => f(r.Snaps[minute - 1])) / (float)v.Count;
    }

    public int RunsAtMinute(int minute) => Runs.Count(r => r.Snaps.Count >= minute);

    // first minute (from 5 on) where the spawner is almost never waiting on the player to clear
    // space: from here the build out-kills the spawn rate and only the spawner sets the pace
    public float TakeoverMinute()
    {
        for (int m = 5; m <= MaxMinute; m++)
            if (RunsAtMinute(m) >= Runs.Count / 2 && AtMinute(m, x => x.CapBlocked) < 0.10f) return m;
        return float.NaN;
    }

    // endless: first minute past 30 where a 3 minute average of kills/s falls under half the
    // spawner's late game ceiling, i.e. enemy health has outgrown the build
    public const float WallKillRate = 13f;
    public static float WallMinute(RunResult r)
    {
        var s = r.Snaps;
        for (int i = 29; i + 2 < s.Count; i++)
            if ((s[i].KillRate + s[i + 1].KillRate + s[i + 2].KillRate) / 3f < WallKillRate) return s[i].Minute;
        return s.Count > 0 ? s[^1].Minute : float.NaN;
    }
}

static class Sim
{
    public static ScenarioResult RunScenario(GameData g, Meta m, Policy p, bool endless, int runs, int seed)
    {
        var results = new RunResult[runs];
        Parallel.For(0, runs, i => results[i] = new RunSim(g, m, p, endless, seed + i * 7919).Run());
        return new ScenarioResult { Meta = m, Policy = p, Endless = endless, Runs = results.ToList() };
    }
}

// ---------------------------------------------------------------------------------------------
// console report
// ---------------------------------------------------------------------------------------------

static class Report
{
    public static void Header(string title)
    {
        Console.WriteLine();
        Console.WriteLine(title);
        Console.WriteLine(new string('=', title.Length));
    }

    static string Pct(float v) => $"{v * 100:0.#}%";

    public static void BaseStats(GameData g)
    {
        var p = g.P;
        Header("BASE STATS (what every run starts with)");
        var rows = new (string, string, string)[]
        {
            ("Max Health", $"{p.MaxHealth:0.##}", "PlayerHealth.maxHealth"),
            ("Invulnerability after hit", $"{p.InvulnOnHit:0.##}s", "PlayerHealth.invulnTimeOnHit"),
            ("Move Speed", $"{p.MoveSpeed:0.##}", "PlayerMovement._speed"),
            ("Pickup Radius", $"{p.PickupRadius:0.##} ({p.PickupRadiusWorld:0.##} world at scale {p.Scale:0.####})", "MagnetArea.baseRadius"),
            ("Arrow Damage", $"{p.ArrowDamage:0.##}", "AutoShooter.baseArrowDamage"),
            ("Arrow Cooldown", $"{p.ArrowCooldown:0.##}s ({1f / p.ArrowCooldown:0.##} volleys/s)", "AutoShooter.baseCooldown"),
            ("Arrows per volley", "1", "AutoShooter (1 + arrowCountAdd)"),
            ("Arrow Speed", $"{p.ArrowSpeed:0.##}", "AutoShooter.baseArrowSpeed"),
            ("Arrow Range (auto-aim)", $"{p.TargetRange:0.##}", "AutoAimService.searchRadius"),
            ("Arrow Lifetime", $"{p.ArrowLifetime:0.##}s", "Projectile.maxLifetime"),
            ("Pierce", "0", "StatContext.pierceAdd"),
            ("Crit Chance", Pct(p.CritChance), "StatContext.critChance"),
            ("Crit Damage", $"x{p.CritMultiplier:0.##}", "StatContext.critMultiplier"),
            ("Cooldown reduction", "0% (cap 60% per layer)", "StatContext"),
            ("Area", "+0%", "StatContext.AreaMul"),
            ("Weapon (projectile) Speed", "+0%", "StatContext.WeaponSpeedMul"),
            ("Aura (locked)", $"{p.AuraDamage:0.##} dmg every {p.AuraInterval:0.##}s, radius {p.AuraRadius:0.##}", "Aura"),
            ("Meteor (locked)", $"{p.MeteorDamage:0.##} dmg x{p.MeteorCount} every {p.MeteorInterval:0.##}s, radius {p.MeteorRadius:0.##}, falls at {p.MeteorFallSpeed:0.##}", "AOEAttack"),
            ("Wen to first level up", $"{g.Curve.WenForLevel(1)}", "FixedCurve.wenNeeded(1)"),
            ("Wen per kill", $"{1 + (g.S.Pool.FirstOrDefault()?.WenOnKill ?? 0)} (+ drops worth {p.PickupWen} each)", "PlayerInventory + EnemyHealth"),
        };
        foreach (var (name, value, src) in rows) Console.WriteLine($"  {name,-28} {value,-50} {src}");
    }

    public static void Upgrades(GameData g)
    {
        Header("IN-RUN UPGRADES (what the stats can grow to)");
        Console.WriteLine($"  {"upgrade",-26} {"stat",-26} {"per pick",-12} {"max",4} {"normal-mode cap",-22} {"endless overcharge",-20}");
        foreach (var u in g.Upgrades)
        {
            string per = u.Type switch
            {
                UType.Area or UType.WeaponSpeed or UType.Cooldown or UType.ArrowCooldown or UType.AuraCooldown or UType.AOEAttackCooldown => $"+{u.Value * 100:0.#}% (sum)",
                UType.ArrowCount or UType.Pierce or UType.AOEAttackProjectileCount => "+1",
                UType.CritChance => $"+{u.Value * 100:0.#}%",
                UType.CritDamage => $"+{u.Value:0.##}x",
                UType.AuraUnlock or UType.AOEAttack or UType.BowUnlock => "unlock",
                _ => $"x{(u.Additive ? 1 + u.Value : u.Value):0.###}",
            };
            string cap = u.Type switch
            {
                UType.Area or UType.WeaponSpeed => $"+{u.Value * u.MaxLevel * 100:0.#}%",
                UType.Cooldown or UType.ArrowCooldown or UType.AuraCooldown or UType.AOEAttackCooldown => $"-{MathF.Min(u.Value * u.MaxLevel, Build.MaxCooldownReduction) * 100:0.#}% cooldown",
                UType.ArrowCount => $"{1 + u.MaxLevel} arrows",
                UType.Pierce => $"pierce {u.MaxLevel}",
                UType.AOEAttackProjectileCount => $"{g.P.MeteorCount + u.MaxLevel} meteors",
                UType.CritChance => $"{MathF.Min(1, u.Value * u.MaxLevel) * 100:0.#}% crit",
                UType.CritDamage => $"x{g.P.CritMultiplier + u.Value * u.MaxLevel:0.##} crit dmg",
                UType.AuraUnlock or UType.AOEAttack or UType.BowUnlock => "-",
                _ => $"x{MathF.Pow(u.Additive ? 1 + u.Value : u.Value, u.MaxLevel):0.###}",
            };
            string oc = u.Overcharge ? $"+{u.OcCeiling * 100:0}% max (half at {u.OcHalf:0})" : "-";
            string title = (u.InPool ? "" : "[off] ") + u.Title;
            Console.WriteLine($"  {title,-26} {u.Type,-26} {per,-12} {u.MaxLevel,4} {cap,-22} {oc,-20}");
        }
        int picks = g.Upgrades.Where(u => u.InPool).Sum(u => u.MaxLevel);
        Console.WriteLine($"\n  {picks} level ups max out every upgrade in the pool (normal mode has nothing to offer after that).");
    }

    public static void Defense(GameData g)
    {
        Header("DEFENSE (analytic): grunt contact hits it takes to kill a full-health player");
        var grunt = g.S.Pool.FirstOrDefault();
        if (grunt == null) return;
        Console.WriteLine($"  {"minute",-8} {"grunt hit",-10} {"base HP",-8} {"HP x1.2^3",-10} {"base+2 armor",-13} {"max ~ hits/s taken while swarmed",-10}");
        foreach (int min in new[] { 0, 5, 10, 15, 20, 25, 30 })
        {
            float tick = grunt.BaseDamage * g.Curve.DamageAt(min * 60f, false) * grunt.TickInterval;
            float hpMax = g.P.MaxHealth * 1.728f;
            Console.WriteLine($"  {min,-8} {tick,-10:0.#} {MathF.Ceiling(g.P.MaxHealth / tick),-8} {MathF.Ceiling(hpMax / tick),-10} {MathF.Ceiling(g.P.MaxHealth / MathF.Max(0.1f, tick - 2)),-13} {1f / g.P.InvulnOnHit:0.#} (invuln {g.P.InvulnOnHit}s)");
        }
    }

    public static void Timeline(GameData g, ScenarioResult greedy, ScenarioResult random)
    {
        Header("BASELINE RUN, NO GLOBAL UPGRADES (median of runs; p10-p90 in brackets)");
        Console.WriteLine($"  {"min",-4} {"level (greedy)",-17} {"level (random)",-17} {"kills/s",-8} {"grunt HP",-9} {"arrow dmg",-11} {"volley cd",-10} {"arrows",-8} {"pierce",-7} {"crit",-6} {"aura%",-6} {"meteor%",-7} {"screen full",-11}");
        int max = Math.Min(greedy.MaxMinute, 45);
        for (int m = 1; m <= max; m++)
        {
            if (m > 5 && m % 5 != 0) continue;
            if (greedy.RunsAtMinute(m) < greedy.Runs.Count / 2) break;
            string L(ScenarioResult s) => $"{s.AtMinute(m, x => x.Level):0} [{s.AtMinute(m, x => x.Level, 0.1f):0}-{s.AtMinute(m, x => x.Level, 0.9f):0}]";
            Console.WriteLine($"  {m,-4} {L(greedy),-17} {L(random),-17} {greedy.AtMinute(m, x => x.KillRate),-8:0.0} {greedy.AtMinute(m, x => x.GruntHp),-9:0.#} {greedy.AtMinute(m, x => x.ArrowDmg),-11:0.#} {greedy.AtMinute(m, x => x.VolleyCd),-10:0.###} {greedy.AtMinute(m, x => x.Shots),-8:0} {greedy.AtMinute(m, x => x.Pierce),-7:0} {Pct(greedy.AtMinute(m, x => x.CritChance)),-6} {Pct(greedy.ShareAtMinute(m, x => x.Aura)),-6} {Pct(greedy.ShareAtMinute(m, x => x.Meteor)),-7} {Pct(greedy.AtMinute(m, x => x.CapBlocked)),-11}");
        }

        Console.WriteLine();
        foreach (var s in new[] { greedy, random })
        {
            Console.WriteLine($"  {s.Policy}: final boss reached at {s.Pct(r => r.FinalBossMinute, 0.5f):0.0} min [{s.Pct(r => r.FinalBossMinute, 0.1f):0.0}-{s.Pct(r => r.FinalBossMinute, 0.9f):0.0}], level {s.Pct(r => r.FinalBossLevel, 0.5f):0}, " +
                              $"final boss {s.Pct(r => r.FinalBossHp, 0.5f):0} HP dies in {s.Pct(r => r.FinalBossTtkAll, 0.5f):0.0}s ({s.Pct(r => r.FinalBossTtkArrows, 0.5f):0.0}s arrows only), stalled runs {s.Runs.Count(r => r.Stalled)}/{s.Runs.Count}");
            var rush = Enumerable.Range(0, g.L.FinalWave).Select(w => s.Pct(r => r.RushSeconds.Count > w ? r.RushSeconds[w] : float.NaN, 0.5f));
            Console.WriteLine($"    final rush length per wave (s): {string.Join("  ", rush.Select((v, i) => $"w{i + 1}:{v:0}"))}");
        }

        var fb = greedy.Runs.Where(r => r.FinalBuild != null).Select(r => r.FinalBuild!).ToList();
        if (fb.Count > 0)
        {
            float Med(Func<Build, float> f) { var v = fb.Select(f).OrderBy(x => x).ToList(); return v[v.Count / 2]; }
            Console.WriteLine($"\n  typical greedy build at the final boss: arrow {Med(b => b.ArrowDamage):0.#} dmg x{Med(b => b.Shots):0} every {Med(b => b.VolleyCd):0.###}s, pierce {Med(b => b.Pierce):0}, crit {Pct(Med(b => b.CritChance))} x{Med(b => b.CritMult):0.##}, " +
                              $"aura {Med(b => b.AuraDmg):0.#}/{Med(b => b.AuraTick):0.###}s r{Med(b => b.AuraR):0.##}, meteor {Med(b => b.MeteorDmg):0.#} x{Med(b => b.MeteorCountLv):0} /{Med(b => b.MeteorCd):0.##}s r{Med(b => b.MeteorR):0.##}, HP {Med(b => b.MaxHp):0}, move {Med(b => b.MoveSpeed):0.##}");
        }
    }

    public static void Scenarios(List<ScenarioResult> all)
    {
        Header("GLOBAL UPGRADE SCENARIOS (greedy picks, normal mode, medians)");
        Console.WriteLine($"  {"scenario",-18} {"what it does",-44} {"lvl@5",-6} {"lvl@10",-7} {"lvl@20",-7} {"kills/s@10",-11} {"kills/s@20",-11} {"full@10",-8} {"takeover",-9} {"rush1",-6} {"boss at",-8} {"boss TTK",-8}");
        foreach (var s in all)
        {
            Console.WriteLine($"  {s.Meta.Name,-18} {s.Meta.Note,-44} {s.AtMinute(5, x => x.Level),-6:0} {s.AtMinute(10, x => x.Level),-7:0} {s.AtMinute(20, x => x.Level),-7:0} " +
                              $"{s.AtMinute(10, x => x.KillRate),-11:0.0} {s.AtMinute(20, x => x.KillRate),-11:0.0} {Pct(s.AtMinute(10, x => x.CapBlocked)),-8} " +
                              $"{"m" + s.TakeoverMinute(),-9} {s.Pct(r => r.RushSeconds.Count > 0 ? r.RushSeconds[0] : float.NaN, 0.5f),-6:0} " +
                              $"{s.Pct(r => r.FinalBossMinute, 0.5f),-8:0.0} {s.Pct(r => r.FinalBossTtkAll, 0.5f),-8:0.0}");
        }
        Console.WriteLine("  full@10 = share of frames the spawner sat at its alive cap in minute 10 (lower = you keep up);");
        Console.WriteLine("  takeover = first minute that share drops under 10%; rush1 = seconds to clear the first final rush;");
        Console.WriteLine("  boss at = real minutes to the final boss (the HUD clock skips final rush time); boss TTK = seconds to kill it.");
    }

    public static void EndlessTable(List<ScenarioResult> all)
    {
        Header("ENDLESS (greedy picks, overcharge on): kills per second by minute, median");
        var minutes = new[] { 20, 30, 35, 40, 45, 50, 55, 60, 65, 70 };
        Console.WriteLine($"  {"scenario",-18} " + string.Join(" ", minutes.Select(m => $"{"m" + m,6}")) + "   wall [p10-p90]     d wall   level@50");
        float baseWall = all[0].Pct(ScenarioResult.WallMinute, 0.5f);
        foreach (var s in all)
        {
            var cells = minutes.Select(m => s.RunsAtMinute(m) >= s.Runs.Count / 2 ? $"{s.AtMinute(m, x => x.KillRate),6:0.0}" : $"{"-",6}");
            float wall = s.Pct(ScenarioResult.WallMinute, 0.5f);
            string d = s == all[0] ? "" : $"{wall - baseWall:+0.0;-0.0} min";
            Console.WriteLine($"  {s.Meta.Name,-18} " + string.Join(" ", cells) +
                              $"   m{wall:0} [{s.Pct(ScenarioResult.WallMinute, 0.1f):0}-{s.Pct(ScenarioResult.WallMinute, 0.9f):0}]    {d,-9} {s.AtMinute(50, x => x.Level),5:0}");
        }
        Console.WriteLine("  (grunt HP by minute: " + string.Join(", ", minutes.Select(m => $"m{m} {all[0].AtMinute(m, x => x.GruntHp):0}")) + ")");
        Console.WriteLine($"  wall = first minute past 30 where kills/s (3 min average) drops under {ScenarioResult.WallKillRate}, half the spawner's late game ceiling");
    }

    public static void Notes(GameData g)
    {
        Header("THINGS NOTICED IN THE PROJECT FILES");
        foreach (var n in g.Notes) Console.WriteLine("  - " + n);
    }
}

// ---------------------------------------------------------------------------------------------
// json for charts
// ---------------------------------------------------------------------------------------------

static class Json
{
    public static void Write(string path, GameData g, List<ScenarioResult> normal, ScenarioResult random, List<ScenarioResult> endless)
    {
        using var fs = File.Create(path);
        using var w = new Utf8JsonWriter(fs, new JsonWriterOptions { Indented = false });
        w.WriteStartObject();

        w.WriteStartArray("normal");
        foreach (var s in normal.Prepend(random)) Scenario(w, s);
        w.WriteEndArray();

        w.WriteStartArray("endless");
        foreach (var s in endless) Scenario(w, s);
        w.WriteEndArray();

        w.WriteEndObject();
    }

    static void Scenario(Utf8JsonWriter w, ScenarioResult s)
    {
        w.WriteStartObject();
        w.WriteString("name", s.Meta.Name);
        w.WriteString("note", s.Meta.Note);
        w.WriteString("policy", s.Policy.ToString());
        w.WriteNumber("runs", s.Runs.Count);
        Num(w, "bossMinute", s.Pct(r => r.FinalBossMinute, 0.5f));
        Num(w, "bossMinuteP10", s.Pct(r => r.FinalBossMinute, 0.1f));
        Num(w, "bossMinuteP90", s.Pct(r => r.FinalBossMinute, 0.9f));
        Num(w, "bossLevel", s.Pct(r => r.FinalBossLevel, 0.5f));
        Num(w, "bossTtk", s.Pct(r => r.FinalBossTtkAll, 0.5f));
        Num(w, "wavesCleared", s.Pct(r => r.WavesCleared, 0.5f));
        Num(w, "takeover", s.TakeoverMinute());
        Num(w, "rush1", s.Pct(r => r.RushSeconds.Count > 0 ? r.RushSeconds[0] : float.NaN, 0.5f));
        Num(w, "rerollsUsed", s.Pct(r => r.RerollsUsed, 0.5f));
        if (s.Endless)
        {
            Num(w, "wall", s.Pct(ScenarioResult.WallMinute, 0.5f));
            Num(w, "wallP10", s.Pct(ScenarioResult.WallMinute, 0.1f));
            Num(w, "wallP90", s.Pct(ScenarioResult.WallMinute, 0.9f));
        }

        w.WriteStartArray("minutes");
        for (int m = 1; m <= s.MaxMinute; m++)
        {
            if (s.RunsAtMinute(m) < s.Runs.Count / 2) break;
            w.WriteStartObject();
            w.WriteNumber("m", m);
            Num(w, "level", s.AtMinute(m, x => x.Level));
            Num(w, "levelP10", s.AtMinute(m, x => x.Level, 0.1f));
            Num(w, "levelP90", s.AtMinute(m, x => x.Level, 0.9f));
            Num(w, "kills", s.AtMinute(m, x => x.KillRate));
            Num(w, "gruntHp", s.AtMinute(m, x => x.GruntHp));
            Num(w, "arrowDmg", s.AtMinute(m, x => x.ArrowDmg));
            Num(w, "volleyCd", s.AtMinute(m, x => x.VolleyCd));
            Num(w, "shots", s.AtMinute(m, x => x.Shots));
            Num(w, "aura", s.ShareAtMinute(m, x => x.Aura));
            Num(w, "meteor", s.ShareAtMinute(m, x => x.Meteor));
            Num(w, "full", s.AtMinute(m, x => x.CapBlocked));
            w.WriteEndObject();
        }
        w.WriteEndArray();
        w.WriteEndObject();
    }

    static void Num(Utf8JsonWriter w, string name, float v)
    {
        if (float.IsFinite(v)) w.WriteNumber(name, MathF.Round(v, 3));
        else w.WriteNull(name);
    }
}
