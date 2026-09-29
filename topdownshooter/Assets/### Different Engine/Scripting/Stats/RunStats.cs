using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

// this run's numbers. the HUD's counters (enemies killed, wen picked up); every weapon's damage
// and DPS, an evolution counted apart from the weapon it grew out of (see WeaponRecord); and the
// rest of a results screen: damage dealt and taken, health healed and regenerated, and so on.
// every scene load starts a new run, so they start from zero there. wen picked up is also what
// pays coins into the wallet.
// only damage from the player's weapons counts as dealt (EnemyHealth.TakeDamage with a source);
// the dev tools' kills and the final purge aren't the player's. the damage numbers change every
// frame, so a screen showing them reads them as it draws; Changed is for the HUD's counters
public static class RunStats
{
    // run time: seconds played since the scene loaded, standing still while paused or in a menu,
    // and stopped for good when the run ends
    public static float Now => endedAt >= 0f ? endedAt : Time.timeSinceLevelLoad;

    // the player died or won. the stats are final from here: the clock stops, and hits and kills
    // that land after (a meteor still falling) don't count
    public static bool Ended => endedAt >= 0f;
    public static void EndRun()
    {
        if (endedAt < 0f) endedAt = Time.timeSinceLevelLoad;
    }
    private static float endedAt = -1f;

    // fires after the kill or wen counter changes
    public static event Action Changed;

    // ---- the HUD's counters
    public static int Kills { get; private set; }
    public static int WenPickedUp { get; private set; }

    // ---- dealt
    public static double DamageDealt { get; private set; }
    public static int Hits { get; private set; }
    public static int Crits { get; private set; }
    public static float BiggestHit { get; private set; }
    public static int EliteKills { get; private set; }
    public static int BossKills { get; private set; }
    // over the whole run, over the last few seconds, and the best few seconds
    public static float Dps => (float)(DamageDealt / Mathf.Max(1f, Now));
    public static float LiveDps => total.PerSecond(Now, Now);
    public static float PeakDps => total.Peak;

    // every weapon held this run, in the order taken. an evolution comes straight after the
    // weapon it grew out of
    public static IReadOnlyList<WeaponRecord> Weapons => weapons;

    // ---- taken
    // health actually lost, after armour: a hit bigger than what was left counts what was left
    public static float HealthLost { get; private set; }
    public static int HitsTaken { get; private set; }
    public static float DamageBlocked { get; private set; }      // taken off by Armor
    public static float Healed { get; private set; }             // heals: pickups, not Recovery
    public static int HealsPickedUp { get; private set; }
    public static float Regenerated { get; private set; }        // Recovery
    public static int RevivalsUsed { get; private set; }
    // the least health the player had left after a hit, of their max. 0 for a revival or a death
    public static float LowestHealth { get; private set; } = 1f;

    // ---- the run
    public static int Level { get; private set; } = 1;
    public static int WavesCleared { get; private set; }
    public static float RunClock { get; private set; }           // the game's run clock, as the HUD shows it
    public static float DistanceWalked { get; private set; }

    // ---- unlocked by this run, a line each as the end screen shows it: the next map, Endless
    // Mode. UnlocksChanged fires on each, since some land after the end screen has opened
    public static IReadOnlyList<string> Unlocks => unlocks;
    public static event Action UnlocksChanged;

    private static readonly List<string> unlocks = new List<string>();
    private static readonly List<WeaponRecord> weapons = new List<WeaponRecord>();
    private static RollingDamage total = new RollingDamage();
    private static int run;

    // ---------------------------------------------------------------- reported by the game

    // qi that reached the player: a pickup touched, or swept in at the end of a wave. it fills the
    // level bar and nothing else (coins come out of envelopes). kills that pay qi straight into the
    // level bar don't count; they never hit the ground
    public static void PickedUpWen(int amount)
    {
        if (amount <= 0) return;
        WenPickedUp += amount;
        Changed?.Invoke();
    }

    // a weapon taken or levelled (Weapon.SetLevel). the pick that evolves it retires its record
    // and starts the evolution's
    public static WeaponRecord Track(Weapon weapon)
    {
        if (weapon == null) return null;

        var record = weapon.Record;
        bool current = record != null && record.Run == run;
        if (!current || (weapon.Evolved && !record.Evolved))
        {
            float now = Now;
            var next = new WeaponRecord(weapon.Asset, weapon.Evolved, weapon.Level, current ? record : null, run, now);
            if (current)
            {
                record.Retire(now);
                weapons.Insert(weapons.IndexOf(record) + 1, next);
            }
            else weapons.Add(next);
            weapon.Record = record = next;
        }

        record.Level = weapon.Level;
        return record;
    }

    // a hit from one of the player's weapons that landed, after armour (EnemyHealth.TakeDamage)
    public static void Dealt(Weapon source, float damage, bool crit, bool killed, EnemyHealth enemy)
    {
        if (source == null || damage <= 0f || Ended) return;

        float now = Now;
        DamageDealt += damage;
        Hits++;
        if (crit) Crits++;
        if (damage > BiggestHit) BiggestHit = damage;
        total.Add(damage, now);

        if (killed && enemy != null)
        {
            if (enemy.TryGetComponent(out BossMarker _) || enemy.TryGetComponent(out SecretBossBehavior _)) BossKills++;
            else if (enemy.TryGetComponent(out EliteOutline _)) EliteKills++;
        }

        var record = source.Record;
        if (record == null || record.Run != run) record = Track(source);
        record.Add(damage, crit, killed, now);
    }

    // a hit on the player that got through: lost is the health it took, blocked what armour took
    // off, left the health remaining of the max
    public static void Hurt(float lost, float blocked, float left)
    {
        HitsTaken++;
        HealthLost += Mathf.Max(0f, lost);
        DamageBlocked += Mathf.Max(0f, blocked);
        LowestHealth = Mathf.Min(LowestHealth, Mathf.Clamp01(left));
    }

    public static void WasHealed(float amount) => Healed += Mathf.Max(0f, amount);
    public static void PickedUpHeal() => HealsPickedUp++;
    public static void Regen(float amount) => Regenerated += Mathf.Max(0f, amount);
    public static void Revived() => RevivalsUsed++;
    public static void Walked(float distance) => DistanceWalked += Mathf.Max(0f, distance);
    public static void ReachedLevel(int level) => Level = Mathf.Max(Level, level);

    // something this run unlocked, worded the way the end screen shows it
    public static void Unlocked(string what)
    {
        if (string.IsNullOrEmpty(what) || unlocks.Contains(what)) return;
        unlocks.Add(what);
        UnlocksChanged?.Invoke();
    }

    // ---------------------------------------------------------------- for screens and logs

    // 950, 12.3k, 4.56M
    public static string Short(double v)
    {
        double a = Math.Abs(v);
        if (a >= 1e9) return (v / 1e9).ToString("0.##") + "B";
        if (a >= 1e6) return (v / 1e6).ToString("0.##") + "M";
        if (a >= 1e4) return (v / 1e3).ToString("0.#") + "k";
        return v.ToString(a >= 100 ? "0" : "0.#");
    }

    // everything above as text, a line a stat and a line a weapon
    public static string Report()
    {
        var s = new StringBuilder();
        s.AppendLine($"Played {Clock(Now)} (run clock {Clock(RunClock)}), level {Level}, {WavesCleared} waves cleared");
        s.AppendLine($"Dealt {Short(DamageDealt)} in {Hits} hits: {Dps:0} dps, {LiveDps:0} now, {PeakDps:0} at best");
        s.AppendLine($"Crits {Crits} ({(Hits > 0 ? 100f * Crits / Hits : 0f):0}%), biggest hit {Short(BiggestHit)}");
        s.AppendLine($"Kills {Kills}, elites {EliteKills}, bosses {BossKills}");
        s.AppendLine($"Health lost {HealthLost:0} in {HitsTaken} hits, {DamageBlocked:0} blocked by armour, lowest {LowestHealth * 100f:0}%");
        s.AppendLine($"Healed {Healed:0} ({HealsPickedUp} pickups), regenerated {Regenerated:0.#}, revivals used {RevivalsUsed}");
        s.AppendLine($"Qi gathered {WenPickedUp}, coins {Coins.EarnedThisRun} ({Coins.FromEnvelopesThisRun} from envelopes), walked {DistanceWalked:0}");
        if (unlocks.Count > 0) s.AppendLine("Unlocked: " + string.Join(", ", unlocks));
        foreach (var w in weapons)
            s.AppendLine($"  {(w.Evolved ? "> " : "")}{w.Title} Lv {w.Level}: {Short(w.Damage)} ({w.Share * 100f:0}%), {w.Dps:0} dps over {Clock(w.Seconds)}, {w.LiveDps:0} now, {w.PeakDps:0} at best, {w.Kills} kills");
        return s.ToString();
    }

    public static string Clock(float seconds)
    {
        int t = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return $"{t / 60}:{t % 60:00}";
    }

    // ---------------------------------------------------------------- the run's lifetime

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Reset();
        Changed = null;
        UnlocksChanged = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Hook()
    {
        GameEvents.OnEnemyKilled -= OnKilled;
        GameEvents.OnEnemyKilled += OnKilled;
        GameEvents.OnRunWon -= EndRun;
        GameEvents.OnRunWon += EndRun;
        GameEvents.OnWaveCleared -= OnWaveCleared;
        GameEvents.OnWaveCleared += OnWaveCleared;
        GameEvents.OnRunTimeChanged -= OnRunTimeChanged;
        GameEvents.OnRunTimeChanged += OnRunTimeChanged;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        Reset();
        Changed?.Invoke();
    }

    private static void Reset()
    {
        run++;      // records still held by weapons from before are stale from here
        endedAt = -1f;
        Kills = WenPickedUp = 0;
        DamageDealt = 0.0;
        Hits = Crits = EliteKills = BossKills = 0;
        BiggestHit = 0f;
        weapons.Clear();
        unlocks.Clear();
        total = new RollingDamage();
        HealthLost = DamageBlocked = Healed = Regenerated = 0f;
        HitsTaken = HealsPickedUp = RevivalsUsed = 0;
        LowestHealth = 1f;
        Level = 1;
        WavesCleared = 0;
        RunClock = DistanceWalked = 0f;
    }

    private static void OnKilled(int count)
    {
        if (count <= 0 || Ended) return;
        Kills += count;
        Changed?.Invoke();
    }

    private static void OnWaveCleared(int wave) => WavesCleared = Mathf.Max(WavesCleared, wave);
    private static void OnRunTimeChanged(float seconds) => RunClock = seconds;
}
