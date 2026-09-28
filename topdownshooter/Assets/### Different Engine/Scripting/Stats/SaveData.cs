using UnityEngine;

// everything the game keeps between sessions is in PlayerPrefs. Reset wipes the player's progress
// (coins, the shop's upgrades, characters bought, maps cleared, runs finished, the endless best) and
// keeps their settings (volume, resolution, window mode, v-sync, blood), which belong to the
// computer rather than to the save
public static class SaveData
{
    private static readonly string[] FloatSettings = { GameSettings.MasterKey, GameSettings.MusicKey, GameSettings.SfxKey };
    private static readonly string[] IntSettings = { "vsync", "show_blood", "res_width", "res_height", "window_mode" };

    public static void ResetProgress()
    {
        var floats = new float?[FloatSettings.Length];
        var ints = new int?[IntSettings.Length];
        for (int i = 0; i < FloatSettings.Length; i++) if (PlayerPrefs.HasKey(FloatSettings[i])) floats[i] = PlayerPrefs.GetFloat(FloatSettings[i]);
        for (int i = 0; i < IntSettings.Length; i++) if (PlayerPrefs.HasKey(IntSettings[i])) ints[i] = PlayerPrefs.GetInt(IntSettings[i]);

        PlayerPrefs.DeleteAll();

        for (int i = 0; i < FloatSettings.Length; i++) if (floats[i].HasValue) PlayerPrefs.SetFloat(FloatSettings[i], floats[i].Value);
        for (int i = 0; i < IntSettings.Length; i++) if (ints[i].HasValue) PlayerPrefs.SetInt(IntSettings[i], ints[i].Value);

        // the coin balance is cached in memory: set it too, so nothing shows the old one
        Coins.Set(0);
        PlayerPrefs.Save();
    }
}
