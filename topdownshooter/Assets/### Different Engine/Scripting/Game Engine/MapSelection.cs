using System;
using UnityEngine;

// the map picked in the menu, remembered between sessions. the game scene reads it as it loads
// (see MapLoader), the same way CharacterSelection and GameMode carry their picks across
public static class MapSelection
{
    private const string Key = "map_index";

    public static event Action Changed;

    public static int Count
    {
        get
        {
            var catalog = MapCatalog.Load();
            return catalog != null ? catalog.maps.Count : 0;
        }
    }

    // the picked map, or the nearest open one before it: a locked map can't be played
    public static int Index
    {
        get
        {
            if (Count == 0) return 0;
            int i = Mathf.Clamp(PlayerPrefs.GetInt(Key, 0), 0, Count - 1);
            while (i > 0 && !MapProgress.IsUnlocked(i)) i--;
            return i;
        }
        set
        {
            int n = Count;
            int wrapped = n == 0 ? 0 : ((value % n) + n) % n;
            if (!MapProgress.IsUnlocked(wrapped) || wrapped == PlayerPrefs.GetInt(Key, 0)) return;
            PlayerPrefs.SetInt(Key, wrapped);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }

    public static MapCatalog.Map Current
    {
        get
        {
            var catalog = MapCatalog.Load();
            return catalog != null && catalog.maps.Count > 0 ? catalog.maps[Index] : null;
        }
    }

    public static void Next() => Index++;
    public static void Previous() => Index--;
}
