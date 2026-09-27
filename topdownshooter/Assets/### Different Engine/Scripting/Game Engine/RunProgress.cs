using UnityEngine;

// progress that survives between runs. endless is earned: it unlocks after a few finished normal
// runs, where finished means you died or won. quitting from the pause menu doesn't count, so the
// unlock can't be farmed by starting and abandoning runs
public static class RunProgress
{
    public const int RunsToUnlockEndless = 5;

    private const string RunsKey = "normal_runs_finished";
    private const string BestKey = "endless_best_seconds";

    public static int NormalRunsFinished => PlayerPrefs.GetInt(RunsKey, 0);
    public static bool EndlessUnlocked => NormalRunsFinished >= RunsToUnlockEndless;
    public static int RunsUntilEndless => Mathf.Max(0, RunsToUnlockEndless - NormalRunsFinished);

    // returns true when this is the run that unlocked endless, so the end screen can say so
    public static bool RecordNormalRun()
    {
        bool before = EndlessUnlocked;

        PlayerPrefs.SetInt(RunsKey, NormalRunsFinished + 1);
        PlayerPrefs.Save();

        bool unlocked = !before && EndlessUnlocked;
        if (unlocked) RunStats.Unlocked("Endless Mode unlocked");
        return unlocked;
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/Progress/Unlock Endless")]
    private static void UnlockEndless()
    {
        PlayerPrefs.SetInt(RunsKey, RunsToUnlockEndless);
        PlayerPrefs.Save();
    }

    [UnityEditor.MenuItem("Tools/Progress/Reset Progress")]
    private static void ResetProgress()
    {
        PlayerPrefs.DeleteKey(RunsKey);
        PlayerPrefs.DeleteKey(BestKey);
        PlayerPrefs.Save();
    }
#endif
}
