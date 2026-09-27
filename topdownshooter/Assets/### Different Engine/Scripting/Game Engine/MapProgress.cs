using UnityEngine;

// which maps have been cleared, saved between runs. clearing is winning a normal run there (the
// final boss down and out through the exit). the first map is always open; every other one opens
// once the map before it has been cleared
public static class MapProgress
{
    private const string Prefix = "map_cleared_";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Hook()
    {
        GameEvents.OnRunWon -= Won;
        GameEvents.OnRunWon += Won;
    }

    private static void Won()
    {
        if (GameMode.IsEndless) return;

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

    [UnityEditor.MenuItem("Tools/Progress/Lock Maps Again")]
    private static void LockAll()
    {
        for (int i = 0; i < MapSelection.Count; i++) PlayerPrefs.DeleteKey(Prefix + Key(i));
        PlayerPrefs.Save();
    }
#endif
}
