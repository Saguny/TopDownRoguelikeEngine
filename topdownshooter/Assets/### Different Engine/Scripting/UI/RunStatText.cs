using TMPro;
using UnityEngine;

// one of the run's stats (RunStats) written into this text when it's shown, like the game over
// screen's "Damage dealt: 123k". it goes on the text that holds the number. added to a text named
// for its stat (DamageDealtData, WavesClearedData, ...) it picks that stat itself
[RequireComponent(typeof(TMP_Text))]
public class RunStatText : MonoBehaviour
{
    public enum Stat
    {
        DamageDealt, DamageReceived, HpHealed, HealedByPickups, Regenerated, DamageBlocked,
        DistanceWalked, LevelReached, WavesCleared, TimePlayed, RunClock,
        Kills, EliteKills, BossKills, Dps, PeakDps, BiggestHit, Crits,
        HitsTaken, LowestHealth, RevivalsUsed, WenPickedUp,
    }

    [SerializeField] private Stat stat;
    [Tooltip("written before the number, e.g. \"Lv \"")]
    [SerializeField] private string prefix;
    [Tooltip("written after the number, e.g. \" m\"")]
    [SerializeField] private string suffix;

    private void OnEnable() => Refresh();

    public void Refresh()
    {
        if (TryGetComponent(out TMP_Text text)) text.text = prefix + Value(stat) + suffix;
    }

    public static string Value(Stat stat)
    {
        switch (stat)
        {
            case Stat.DamageDealt: return RunStats.Short(RunStats.DamageDealt);
            case Stat.DamageReceived: return RunStats.Short(RunStats.HealthLost);          // health that came off, after armour
            case Stat.HpHealed: return RunStats.Short(RunStats.Healed + RunStats.Regenerated);
            case Stat.HealedByPickups: return RunStats.Short(RunStats.Healed);
            case Stat.Regenerated: return RunStats.Short(RunStats.Regenerated);
            case Stat.DamageBlocked: return RunStats.Short(RunStats.DamageBlocked);
            case Stat.DistanceWalked: return RunStats.Short(RunStats.DistanceWalked);
            case Stat.LevelReached: return RunStats.Level.ToString();
            case Stat.WavesCleared: return RunStats.WavesCleared.ToString();
            case Stat.TimePlayed: return RunStats.Clock(RunStats.Now);                   // real time played, menus left out
            case Stat.RunClock: return RunStats.Clock(RunStats.RunClock);                // the HUD's clock
            case Stat.Kills: return RunStats.Short(RunStats.Kills);
            case Stat.EliteKills: return RunStats.EliteKills.ToString();
            case Stat.BossKills: return RunStats.BossKills.ToString();
            case Stat.Dps: return RunStats.Short(RunStats.Dps);
            case Stat.PeakDps: return RunStats.Short(RunStats.PeakDps);
            case Stat.BiggestHit: return RunStats.Short(RunStats.BiggestHit);
            case Stat.Crits: return RunStats.Short(RunStats.Crits);
            case Stat.HitsTaken: return RunStats.HitsTaken.ToString();
            case Stat.LowestHealth: return $"{RunStats.LowestHealth * 100f:0}%";
            case Stat.RevivalsUsed: return RunStats.RevivalsUsed.ToString();
            case Stat.WenPickedUp: return RunStats.Short(RunStats.WenPickedUp);
        }
        return string.Empty;
    }

    // added in the editor: the stat from the object's name, so the texts can all take it at once
    private void Reset()
    {
        string n = name.ToLowerInvariant();
        if (n.Contains("dealt")) stat = Stat.DamageDealt;
        else if (n.Contains("receiv") || n.Contains("taken") || n.Contains("lost")) stat = Stat.DamageReceived;
        else if (n.Contains("regen")) stat = Stat.Regenerated;
        else if (n.Contains("heal")) stat = Stat.HpHealed;
        else if (n.Contains("block")) stat = Stat.DamageBlocked;
        else if (n.Contains("walk") || n.Contains("distance")) { stat = Stat.DistanceWalked; suffix = " m"; }
        else if (n.Contains("level")) stat = Stat.LevelReached;
        else if (n.Contains("wave")) stat = Stat.WavesCleared;
        else if (n.Contains("clock")) stat = Stat.RunClock;
        else if (n.Contains("time") || n.Contains("played")) stat = Stat.TimePlayed;
        else if (n.Contains("elite")) stat = Stat.EliteKills;
        else if (n.Contains("boss")) stat = Stat.BossKills;
        else if (n.Contains("kill")) stat = Stat.Kills;
        else if (n.Contains("peak")) stat = Stat.PeakDps;
        else if (n.Contains("dps")) stat = Stat.Dps;
        else if (n.Contains("biggest")) stat = Stat.BiggestHit;
        else if (n.Contains("crit")) stat = Stat.Crits;
        else if (n.Contains("lowest")) stat = Stat.LowestHealth;
        else if (n.Contains("reviv")) stat = Stat.RevivalsUsed;
        else if (n.Contains("wen")) stat = Stat.WenPickedUp;
    }
}
