using System.Collections.Generic;
using UnityEngine;

// the peaches of immortality: healing stays scarce and spread out. each stretch of the run (from
// its start to the first Final Rush's end, then from one rush's end to the next's) holds 0, 1 or 2
// peaches, rolled as it begins, each due at a random moment through it: the first kill after that
// moment drops it. an elite's kill drops one straight away if the stretch has any left (the last
// due goes with it). so a run can't be handed all its healing in its first minute
public static class PeachDrops
{
    public const int MaxPerStretch = 2;

    // chances of none, one and two in a stretch
    private static readonly float[] Odds = { 0.25f, 0.45f, 0.30f };
    // when in a stretch they come due, seconds after it starts: never in its first moments, and
    // before a wave's three minutes are up
    private const float Earliest = 20f, Latest = 160f;

    private static readonly List<float> due = new List<float>(MaxPerStretch);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => due.Clear();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Hook()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        GameEvents.OnFinalRushEnded -= OnRushEnded;
        GameEvents.OnFinalRushEnded += OnRushEnded;
    }

    // every run is a scene load, and starts its first stretch with it
    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (mode == UnityEngine.SceneManagement.LoadSceneMode.Single) NewStretch();
    }

    private static void OnRushEnded(int wave) => NewStretch();

    private static void NewStretch()
    {
        due.Clear();
        float r = Random.value;
        int count = r < Odds[0] ? 0 : r < Odds[0] + Odds[1] ? 1 : 2;
        for (int i = 0; i < Mathf.Min(count, MaxPerStretch); i++) due.Add(Time.time + Random.Range(Earliest, Latest));
        due.Sort();
    }

    // an enemy died: does it drop a peach? an elite's always does while the stretch has one left
    public static bool Take(bool elite)
    {
        if (due.Count == 0) return false;
        if (elite)
        {
            due.RemoveAt(due.Count - 1);
            return true;
        }
        if (Time.time < due[0]) return false;
        due.RemoveAt(0);
        return true;
    }
}
