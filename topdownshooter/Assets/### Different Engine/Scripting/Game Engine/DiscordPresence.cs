using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// Discord rich presence: while the game runs, the player's Discord profile shows what they're doing,
// e.g. "Normal run · Courtyard", "Zhuo Lan · Level 12" and the time the run has gone on. set
// ApplicationId to the game's application in the Discord Developer Portal, and upload the art
// there (Rich Presence > Art Assets) under the keys below. without Discord open it does nothing
public class DiscordPresence : MonoBehaviour
{
    // TODO: your application's ID from https://discord.com/developers/applications (0 = off)
    private const long ApplicationId = 1553838168003051541;
    private const string LargeImage = "logo";   // the art asset's key in the Developer Portal

    private DiscordGameSdk.Discord discord;
    private long runStarted;
    private float nextUpdate;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (ApplicationId == 0 || FindFirstObjectByType<DiscordPresence>() != null) return;
        var go = new GameObject("Discord Presence");
        DontDestroyOnLoad(go);
        go.AddComponent<DiscordPresence>();
    }

    private void Awake()
    {
        try { discord = new DiscordGameSdk.Discord(ApplicationId, (ulong)DiscordGameSdk.CreateFlags.NoRequireDiscord); }
        catch (Exception) { discord = null; }   // Discord isn't running
        if (discord == null) { Destroy(gameObject); return; }
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        discord?.Dispose();
        discord = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        runStarted = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        nextUpdate = 0f;
    }

    private void Update()
    {
        if (discord == null) return;
        if (Time.unscaledTime >= nextUpdate)
        {
            nextUpdate = Time.unscaledTime + 3f;   // Discord takes about one change every few seconds
            Push();
        }
        try { discord.RunCallbacks(); }
        catch (Exception) { discord.Dispose(); discord = null; }   // Discord closed
    }

    private void Push()
    {
        bool inRun = FindFirstObjectByType<SpawnDirector>() != null;
        var activity = new DiscordGameSdk.Activity
        {
            Details = inRun ? $"{(GameMode.IsEndless ? "Endless" : "Normal run")} · {MapName()}" : "In the menus",
            State = inRun ? $"{CharacterName()} · Level {RunStats.Level}" : "Choosing a path",
            Assets = { LargeImage = LargeImage, LargeText = Application.productName },
        };
        if (inRun) activity.Timestamps.Start = runStarted;
        discord.GetActivityManager().UpdateActivity(activity, _ => { });
    }

    private static string MapName()
    {
        var maps = MapCatalog.Load();
        int i = MapSelection.Index;
        return maps != null && i >= 0 && i < maps.maps.Count ? maps.maps[i].title : "the Wilds";
    }

    private static string CharacterName()
    {
        var c = CharacterSelection.Current;
        return c != null && !string.IsNullOrEmpty(c.displayName) ? c.displayName : "A wanderer";
    }
}
