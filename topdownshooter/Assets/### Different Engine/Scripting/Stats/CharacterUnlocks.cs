using System;
using UnityEngine;

// which characters have been bought, saved between runs like the shop's ranks. the catalog's
// first character and any marked Starts Unlocked are owned from the start; the rest are bought
// with coins on the select screen, each one dearer than the last: 6,000, 7,800, 9,600 and so on,
// whichever order they're bought in
public static class CharacterUnlocks
{
    public const int FirstPrice = 6000;
    public const int PriceStep = 1800;

    private const string Prefix = "character_owned_";
    private const string BoughtKey = "characters_bought";

    // fires after a purchase, so cards and buttons can redraw
    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Changed = null;

    public static bool IsUnlocked(CharacterData character)
    {
        if (character == null) return false;
        if (character.startsUnlocked || character == StatCatalog.Load().DefaultCharacter) return true;
        return PlayerPrefs.GetInt(Prefix + character.name, 0) == 1;
    }

    public static int Bought => PlayerPrefs.GetInt(BoughtKey, 0);

    // what the next character costs, whoever it is
    public static int NextPrice => FirstPrice + PriceStep * Bought;

    public static bool CanAfford => Coins.Balance >= NextPrice;

    public static bool TryBuy(CharacterData character)
    {
        if (character == null || IsUnlocked(character)) return false;
        if (!Coins.TrySpend(NextPrice)) return false;

        PlayerPrefs.SetInt(Prefix + character.name, 1);
        PlayerPrefs.SetInt(BoughtKey, Bought + 1);
        PlayerPrefs.Save();
        Changed?.Invoke();
        return true;
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/Progress/Lock All Characters")]
    private static void LockAll()
    {
        foreach (var c in StatCatalog.Load().characters)
            if (c != null) PlayerPrefs.DeleteKey(Prefix + c.name);
        PlayerPrefs.DeleteKey(BoughtKey);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    [UnityEditor.MenuItem("Tools/Progress/Unlock All Characters")]
    private static void UnlockAll()
    {
        foreach (var c in StatCatalog.Load().characters)
            if (c != null) PlayerPrefs.SetInt(Prefix + c.name, 1);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
#endif
}
