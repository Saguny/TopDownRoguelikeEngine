using System;
using UnityEngine;

// global upgrade ranks bought in the shop. they survive between runs, like RunProgress
public static class MetaProgress
{
    private const string Prefix = "global_upgrade_";

    // fires after any rank changes, so an open stat panel can redraw
    public static event Action Changed;

    public static int RankOf(GlobalUpgradeDef upgrade)
    {
        if (upgrade == null || string.IsNullOrEmpty(upgrade.id)) return 0;
        return Mathf.Clamp(PlayerPrefs.GetInt(Prefix + upgrade.id, 0), 0, upgrade.maxRank);
    }

    public static void SetRank(GlobalUpgradeDef upgrade, int rank)
    {
        if (upgrade == null || string.IsNullOrEmpty(upgrade.id)) return;

        PlayerPrefs.SetInt(Prefix + upgrade.id, Mathf.Clamp(rank, 0, upgrade.maxRank));
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

#if UNITY_EDITOR
    // for testing the stat panel before the shop exists
    [UnityEditor.MenuItem("Tools/Progress/Max All Global Upgrades")]
    private static void MaxAll()
    {
        foreach (var u in StatCatalog.Load().globalUpgrades)
            if (u != null) SetRank(u, u.maxRank);
    }

    [UnityEditor.MenuItem("Tools/Progress/Reset Global Upgrades")]
    private static void ResetAll()
    {
        foreach (var u in StatCatalog.Load().globalUpgrades)
            if (u != null) SetRank(u, 0);
    }
#endif
}
