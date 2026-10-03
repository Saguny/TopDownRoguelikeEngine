using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// credits (shown as tickets): a few banked between runs and spent before one to favour a weapon,
// passive or ability for it. a favoured item comes up a little more often in that run's level
// ups and envelopes (a marginal nudge, not a guarantee), and a pity timer makes sure it's offered
// at least once: after enough level ups without it, the next one shows it.
//
// earning: a run that ends with a win, or after EarnAfterSeconds on the run clock, earns one; a
// player can bank up to Max. spending: TryFavour before the run (Unfavour gives it back); the
// favours are committed as the run starts (BeginRun, from PlayerInventory) and last that run only.
// a credit can go on a different item each, or stack on one (stronger nudge, sooner pity).
//
// the menu builds its own UI on this: Count, Max, Changed, Pending, StacksFor, CanFavour,
// TryFavour, Unfavour, ClearPending. in a run: IsFavoured (a badge on the level up card) and
// EarnedThisRun (the end screen)
public static class RunCredits
{
    public const int Max = 3;
    public const int MaxStacks = 3;
    public const float EarnAfterSeconds = 600f;         // 10:00 on the run clock
    // how much likelier a favoured item is to be drawn, per credit on it: 1 credit is +15%
    public const float WeightPerStack = 0.15f;

    private const string CountKey = "run_credits";
    private const string PendingKey = "run_favours";

    // fires when the count or the pending favours change
    public static event Action Changed;

    private static Dictionary<string, int> pending;                      // the next run's, saved
    private static readonly Dictionary<string, int> active = new Dictionary<string, int>();   // this run's

    // whether the run that just ended earned a credit (false at the cap, or too short)
    public static bool EarnedThisRun { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Changed = null;
        pending = null;
        active.Clear();
        EarnedThisRun = false;
    }

    // ---------------------------------------------------------------- the bank

    // credits banked and not yet put on anything
    public static int Count => Mathf.Clamp(PlayerPrefs.GetInt(CountKey, 0), 0, Max);

    private static void SetCount(int n)
    {
        PlayerPrefs.SetInt(CountKey, Mathf.Clamp(n, 0, Max));
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    // the end of a run (GameOverScreen): a win, or a run that lasted, earns one if there's room.
    // returns whether it did
    public static bool EarnFromRun(bool won, float runClockSeconds)
    {
        EarnedThisRun = false;
        if (!won && runClockSeconds < EarnAfterSeconds) return false;
        // credits waiting on favours count against the cap too: they're still the player's
        if (Count + PendingTotal >= Max) return false;
        SetCount(Count + 1);
        EarnedThisRun = true;
        RunStats.Unlocked($"+1 Credit ({Count + PendingTotal}/{Max})");
        return true;
    }

    // ---------------------------------------------------------------- favours before a run

    // an item's key: its asset's name (a run's copy carries "(Clone)", which is dropped)
    public static string Key(UpgradeData u)
    {
        if (u == null) return null;
        string n = u.name;
        int clone = n.IndexOf("(Clone)", StringComparison.Ordinal);
        return (clone >= 0 ? n.Substring(0, clone) : n).Trim();
    }

    // what a credit can go on: anything the level up can offer except the gifts
    public static bool CanFavour(UpgradeData u) => u != null && u.includeInPool && !(u is GiftUpgrade);

    private static Dictionary<string, int> Pending
    {
        get
        {
            if (pending == null) pending = Parse(PlayerPrefs.GetString(PendingKey, ""));
            return pending;
        }
    }

    // the favours set for the next run, by item key
    public static IReadOnlyDictionary<string, int> PendingFavours => Pending;
    public static int PendingTotal { get { int n = 0; foreach (var v in Pending.Values) n += v; return n; } }
    public static int StacksFor(UpgradeData u) => u != null && Pending.TryGetValue(Key(u), out int s) ? s : 0;

    // a credit onto an item for the next run. false with no credit left, or the item at MaxStacks
    public static bool TryFavour(UpgradeData u)
    {
        if (!CanFavour(u) || Count <= 0) return false;
        string k = Key(u);
        Pending.TryGetValue(k, out int s);
        if (s >= MaxStacks) return false;
        Pending[k] = s + 1;
        SavePending();
        SetCount(Count - 1);
        return true;
    }

    // one credit back off an item. false if it had none
    public static bool Unfavour(UpgradeData u)
    {
        if (u == null) return false;
        string k = Key(u);
        if (!Pending.TryGetValue(k, out int s) || s <= 0) return false;
        if (s == 1) Pending.Remove(k); else Pending[k] = s - 1;
        SavePending();
        SetCount(Count + 1);
        return true;
    }

    // every pending credit back in the bank
    public static void ClearPending()
    {
        int back = PendingTotal;
        Pending.Clear();
        SavePending();
        SetCount(Count + back);
    }

    private static void SavePending()
    {
        var sb = new StringBuilder();
        foreach (var kv in Pending)
        {
            if (sb.Length > 0) sb.Append('|');
            sb.Append(kv.Key.Replace("|", "").Replace(":", "")).Append(':').Append(kv.Value);
        }
        PlayerPrefs.SetString(PendingKey, sb.ToString());
        PlayerPrefs.Save();
    }

    private static Dictionary<string, int> Parse(string s)
    {
        var d = new Dictionary<string, int>();
        if (string.IsNullOrEmpty(s)) return d;
        foreach (var part in s.Split('|'))
        {
            int colon = part.LastIndexOf(':');
            if (colon <= 0 || !int.TryParse(part.Substring(colon + 1), out int n) || n <= 0) continue;
            d[part.Substring(0, colon)] = Mathf.Min(n, MaxStacks);
        }
        return d;
    }

    // ---------------------------------------------------------------- the run

    // the run starts (PlayerInventory): the pending favours are spent on it
    public static void BeginRun()
    {
        active.Clear();
        EarnedThisRun = false;
        foreach (var kv in Pending) active[kv.Key] = kv.Value;
        if (Pending.Count == 0) return;
        Pending.Clear();
        SavePending();
        Changed?.Invoke();
    }

    // this run's credits on an item (a run's copy or the asset)
    public static int ActiveStacks(UpgradeData u) => u != null && active.TryGetValue(Key(u), out int s) ? s : 0;
    public static bool IsFavoured(UpgradeData u) => ActiveStacks(u) > 0;

    // how much likelier it's drawn this run
    public static float WeightFor(UpgradeData u) => 1f + WeightPerStack * ActiveStacks(u);

    // level ups in a row it can come up in without being shown before it's guaranteed: 6, 5, 4
    public static int PityAfter(int stacks) => Mathf.Max(1, 7 - Mathf.Clamp(stacks, 1, MaxStacks));

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/Progress/Give Credits")]
    private static void GiveCredits() => SetCount(Max - PendingTotal);
#endif
}
