using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// the coin wallet (wen, the currency) the upgrade shop spends from, saved between runs. coins come
// only out of fortune envelopes, Vampire Survivors' way: each rarity pays a roll between its own
// min and max (FortuneEnvelope.RollCoins), times the run's Greed. the qi the horde drops fills the
// level bar and nothing else. the one other source is the level up's String of Wen, the gift it
// offers once there's nothing left to level
public static class Coins
{
    private const string Key = "coins";

    // fires after the balance changes, so coin counters and buy buttons can update
    public static event Action Changed;

    // the balance lives in memory and is written out when a scene changes, when the game quits
    // and on every purchase, rather than on every single wen picked up
    private static int balance = -1;
    private static bool unsaved;

    public static int Balance
    {
        get
        {
            if (balance < 0) balance = PlayerPrefs.GetInt(Key, 0);
            return balance;
        }
    }

    public static void Add(int amount)
    {
        if (amount <= 0) return;
        balance = Balance + amount;
        unsaved = true;
        Changed?.Invoke();
    }

    public static bool TrySpend(int amount)
    {
        if (amount < 0 || Balance < amount) return false;
        balance = Balance - amount;
        unsaved = true;
        Save();
        Changed?.Invoke();
        return true;
    }

    // dev tools and tests only: puts the balance to an exact number
    public static void Set(int amount)
    {
        balance = Mathf.Max(0, amount);
        unsaved = true;
        Save();
        Changed?.Invoke();
    }

    public static void Save()
    {
        if (!unsaved) return;
        PlayerPrefs.SetInt(Key, Balance);
        PlayerPrefs.Save();
        unsaved = false;
    }

    // ---- earning ----
    private static float greedMultiplier = 1f;

    // coins this run has paid so far, envelopes and gifts, for the end screen (RunStatText's Coins
    // Earned); and those out of envelopes alone, for the HUD's counter
    public static int EarnedThisRun { get; private set; }
    public static int FromEnvelopesThisRun { get; private set; }


    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Changed = null;
        greedMultiplier = 1f;
        EarnedThisRun = 0;
        FromEnvelopesThisRun = 0;
        balance = -1;
        unsaved = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Application.quitting -= Save;
        Application.quitting += Save;
    }

    // Greed is read as each scene loads, so a run earns at the rate it started with
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        greedMultiplier = 1f + Mathf.Max(0f, StatSheet.ForRun()[StatId.Greed]);
        EarnedThisRun = 0;
        FromEnvelopesThisRun = 0;
        Save();
    }

    // a set amount, times Greed
    public static int WithGreed(int amount) => Mathf.Max(0, Mathf.RoundToInt(amount * greedMultiplier));

    // what a fortune envelope pays, after Greed: the HUD's counter shows these alone
    public static int FromEnvelope(int amount)
    {
        int paid = Gift(amount);
        FromEnvelopesThisRun += paid;
        if (paid > 0) Changed?.Invoke();
        return paid;
    }

    // the level up's String of Wen: what it paid, after Greed; in the run's coins earned (the end
    // screen) but not the HUD's envelope counter
    public static int Gift(int amount)
    {
        int paid = WithGreed(amount);
        if (paid <= 0) return 0;
        Add(paid);
        EarnedThisRun += paid;
        return paid;
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/Progress/Add 1000 Coins")]
    private static void AddThousand() => Add(1000);

    [UnityEditor.MenuItem("Tools/Progress/Reset Coins")]
    private static void ResetCoins() => Set(0);
#endif
}
