using UnityEditor;

// Tools > Performance > Run Stress Test: plays, runs StressTest and stops again. it starts from
// whatever scene is open and loads the game scene in play mode, so the scenes open in the editor,
// saved or not, are left as they are
internal static class StressTestLauncher
{
    [MenuItem("Tools/Performance/Run Stress Test")]
    public static void Run()
    {
        if (EditorApplication.isPlaying)
        {
            StressTest.Begin();
            return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        SessionState.SetBool(StressTest.PendingKey, true);
        EditorApplication.EnterPlaymode();
    }
}
