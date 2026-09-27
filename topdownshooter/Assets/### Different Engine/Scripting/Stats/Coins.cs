using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// the coin wallet the upgrade shop spends from, saved between runs. every wen the player picks
// up in a run pays a coin, scaled by the run's Greed; fractions carry over so small bonuses
// still add up (see RunStats.PickedUpWen)
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
    private static float carry;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Changed = null;
        carry = 0f;
        greedMultiplier = 1f;
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
        Save();
    }

    // wen picked up pays one coin each, times Greed
    public static void Earn(int wen)
    {
        if (wen <= 0) return;
        carry += wen * greedMultiplier;
        int whole = Mathf.FloorToInt(carry);
        if (whole <= 0) return;
        carry -= whole;
        Add(whole);
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/Progress/Add 1000 Coins")]
    private static void AddThousand() => Add(1000);

    [UnityEditor.MenuItem("Tools/Progress/Reset Coins")]
    private static void ResetCoins() => Set(0);
#endif
}
