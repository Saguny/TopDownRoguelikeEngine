using UnityEngine;

// which maps have been cleared, saved between runs. clearing is winning a normal run there (lasting
// until the Wuchang come for the player at the time limit). the first map is always open; every other one opens
// once the map before it has been cleared
public static class MapProgress
{
    private const string Prefix = "map_cleared_";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Hook()
    {
        GameEvents.OnRunWon -= Won;
        GameEvents.OnRunWon += Won;
        GameEvents.OnFinalBossStarted -= MetBoss;
        GameEvents.OnFinalBossStarted += MetBoss;
    }

    // ---- the final boss met: its map's Practice button opens

    private const string BossPrefix = "map_boss_seen_";
    public static bool BossSeen(int index) => PlayerPrefs.GetInt(BossPrefix + Key(index), 0) == 1;

    private static void MetBoss()
    {
        int index = MapSelection.Index;
        if (index < 0 || BossSeen(index)) return;
        PlayerPrefs.SetInt(BossPrefix + Key(index), 1);
        PlayerPrefs.Save();
    }

    private static void Won()
    {
        if (GameMode.IsEndless || GameMode.IsPractice) return;

        // the end screen's unlocks: the next map, when this run is what opened it
        int index = MapSelection.Index;
        bool nextWasOpen = IsUnlocked(index + 1);
        MarkCleared(index);
        if (!nextWasOpen && index + 1 < MapSelection.Count && IsUnlocked(index + 1))
            RunStats.Unlocked("Map unlocked: " + Title(index + 1));
    }

    private static string Title(int index)
    {
        var catalog = MapCatalog.Load();
        return catalog != null && index < catalog.maps.Count && !string.IsNullOrEmpty(catalog.maps[index].title)
            ? catalog.maps[index].title
            : $"Map {index + 1}";
    }

    public static bool IsCleared(int index) => PlayerPrefs.GetInt(Prefix + Key(index), 0) == 1;

    public static bool IsUnlocked(int index) => index <= 0 || IsCleared(index - 1);

    public static void MarkCleared(int index)
    {
        if (index < 0 || IsCleared(index)) return;
        PlayerPrefs.SetInt(Prefix + Key(index), 1);
        PlayerPrefs.Save();
    }

    // by the map's title rather than its place, so reordering the catalog keeps what was cleared
    private static string Key(int index)
    {
        var catalog = MapCatalog.Load();
        return catalog != null && index < catalog.maps.Count && !string.IsNullOrEmpty(catalog.maps[index].title)
            ? catalog.maps[index].title
            : index.ToString();
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/Progress/Clear All Maps")]
    private static void ClearAll()
    {
        for (int i = 0; i < MapSelection.Count; i++) MarkCleared(i);
    }

    [UnityEditor.MenuItem("Tools/Progress/Meet Every Final Boss")]
    private static void MeetAll()
    {
        for (int i = 0; i < MapSelection.Count; i++) PlayerPrefs.SetInt(BossPrefix + Key(i), 1);
        PlayerPrefs.Save();
    }

    [UnityEditor.MenuItem("Tools/Progress/Lock Maps Again")]
    private static void LockAll()
    {
        for (int i = 0; i < MapSelection.Count; i++) PlayerPrefs.DeleteKey(Prefix + Key(i));
        PlayerPrefs.Save();
    }
#endif
}
