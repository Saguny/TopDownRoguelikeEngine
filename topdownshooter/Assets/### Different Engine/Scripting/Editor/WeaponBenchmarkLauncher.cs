using UnityEditor;

// Tools > Balance > Weapon DPS Benchmark: measures every weapon's damage per second in play mode
// (see WeaponBenchmark) and writes Benchmarks/weapon_dps.csv. it starts from whatever scene is open
// and loads the game scene itself, so the scenes open in the editor are left as they are. it takes
// a few minutes; the Console shows each measurement as it's made
internal static class WeaponBenchmarkLauncher
{
    [MenuItem("Tools/Balance/Weapon DPS Benchmark")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        SessionState.SetBool(WeaponBenchmark.PendingKey, true);
        EditorApplication.EnterPlaymode();
    }

    [MenuItem("Tools/Balance/Open Benchmark Results")]
    public static void Open()
    {
        if (System.IO.File.Exists(WeaponBenchmark.CsvPath)) EditorUtility.RevealInFinder(WeaponBenchmark.CsvPath);
        else EditorUtility.DisplayDialog("Weapon DPS Benchmark", "No results yet: run Tools > Balance > Weapon DPS Benchmark first.", "OK");
    }
}
