using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

// the music: one playlist for the menus, one for a run. after a loading screen it waits for the
// screen to go, then fades in somewhere random in the track (in a run, a random track too). going
// from the menus into a run or back, it fades out as the spiral closes and the other fades in on
// the far side; from one menu scene to another, or a run restarting, it just carries on. it lives
// on from the scene it started in, and counts real time, since the loading screen holds the
// game's time still
public class BGMManager : MonoBehaviour
{
    public static BGMManager Instance;

    public AudioSource bgmSource;

    [Header("Menus")]
    public AudioClip[] playlist;
    public string[] playlistNames;

    [SerializeField]
    private string[] allowedScenes =
    {
        "MainMenu",
        "CreditsScene"
    };

    [Header("In a run")]
    [Tooltip("the scenes a run is played in: they play the Run Playlist")]
    [SerializeField] private string[] runScenes = { "Game" };
    [Tooltip("the music of a run, a random track each time. empty = the menu playlist")]
    public AudioClip[] runPlaylist;
    public string[] runPlaylistNames;

    [Header("Song Title UI (TMP)")]
    public TextMeshProUGUI songNameText; // will be filled at runtime

    [Header("Fades")]
    [Tooltip("seconds the music takes to fade in, once the loading screen has gone")]
    [SerializeField, Min(0f)] private float fadeInSeconds = 2.5f;
    [Tooltip("seconds it takes to fade out when the game heads into a run or back to the menu")]
    [SerializeField, Min(0f)] private float fadeOutSeconds = 1.2f;
    [Tooltip("each time the music starts it's somewhere random in the track, up to this far through it. 0 = always from the beginning")]
    [SerializeField, Range(0f, 1f)] private float randomStartUpTo = 0.7f;

    private enum Mood { None, Menu, Run }

    private Mood mood = Mood.None;
    private int currentIndex = 0;
    private int runIndex = 0;
    private Coroutine switchRoutine;
    private Coroutine fadeRoutine;
    private float fullVolume = 1f;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        if (bgmSource != null) fullVolume = bgmSource.volume;
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneLoader.Started += OnLoadStarted;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneLoader.Started -= OnLoadStarted;
    }

    private Mood MoodOf(string scene)
    {
        foreach (string s in allowedScenes) if (scene == s) return Mood.Menu;
        foreach (string s in runScenes) if (scene == s) return Mood.Run;
        return Mood.None;
    }

    private bool RunHasItsOwn => runPlaylist != null && runPlaylist.Length > 0;
    private AudioClip[] Clips => mood == Mood.Run && RunHasItsOwn ? runPlaylist : playlist;
    private string[] Names => mood == Mood.Run && RunHasItsOwn ? runPlaylistNames : playlistNames;

    private int Index
    {
        get => mood == Mood.Run ? runIndex : currentIndex;
        set { if (mood == Mood.Run) runIndex = value; else currentIndex = value; }
    }

    // a load into somewhere with other music, or none (Start, into a run; Quit, back to the menu):
    // the music fades out as the spiral closes
    private void OnLoadStarted(string scene)
    {
        if (MoodOf(scene) == mood || bgmSource == null || !bgmSource.isPlaying) return;
        Fade(FadeOutAndStop());
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // every time a new scene loads, try to find the TMP again
        RebindSongTextInScene();
        if (bgmSource == null) return;

        Mood next = MoodOf(scene.name);
        if (next == Mood.None)
        {
            mood = next;
            if (bgmSource.isPlaying && fadeRoutine == null) bgmSource.Stop();
            return;
        }

        // the same music as before, already playing: it carries on
        if (next == mood && bgmSource.isPlaying)
        {
            UpdateSongNameUI();
            return;
        }

        mood = next;
        var clips = Clips;
        if (clips == null || clips.Length == 0) return;
        Index = mood == Mood.Run ? Random.Range(0, clips.Length) : Mathf.Clamp(Index, 0, clips.Length - 1);
        bgmSource.Stop();
        bgmSource.clip = clips[Index];
        Fade(FadeInAfterLoading());
    }

    private void Fade(IEnumerator routine)
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(routine);
    }

    // the music waits for the loading screen (the game's opening, or on the far side of a load) to
    // go, then starts somewhere random in the track and fades in, its low pass sweeping open
    private IEnumerator FadeInAfterLoading()
    {
        bgmSource.volume = 0f;
        yield return null;
        yield return null;    // the menu puts its loading screen up in its first frame
        while (SceneLoader.Busy || FindAnyObjectByType<LoadingAnimation>() != null) yield return null;
        if (bgmSource.clip == null) { fadeRoutine = null; yield break; }

        bgmSource.time = Random.Range(0f, bgmSource.clip.length * randomStartUpTo);
        bgmSource.Play();
        UpdateSongNameUI();
        if (TryGetComponent(out IntroLowpassSweep sweep)) sweep.Restart();
        yield return Volume(0f, fullVolume, fadeInSeconds);
        fadeRoutine = null;
    }

    private IEnumerator FadeOutAndStop()
    {
        yield return Volume(bgmSource.volume, 0f, fadeOutSeconds);
        bgmSource.Stop();
        fadeRoutine = null;
    }

    private IEnumerator Volume(float from, float to, float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            float k = t / seconds;
            // an ease that sounds even: slow at the quiet end, where the ear is most sensitive
            bgmSource.volume = Mathf.Lerp(from, to, to > from ? k * k : 1f - (1f - k) * (1f - k));
            yield return null;
        }
        bgmSource.volume = to;
    }

    private void RebindSongTextInScene()
    {
        // look for a TMP text with tag "SongNameText" in the current scene
        GameObject go = GameObject.FindWithTag("SongNameText");
        if (go != null)
        {
            songNameText = go.GetComponent<TextMeshProUGUI>();
            UpdateSongNameUI();
        }
        else
        {
            songNameText = null;
        }
    }

    public void NextTrack()
    {
        var clips = Clips;
        if (clips == null || clips.Length == 0 || bgmSource == null)
            return;

        if (switchRoutine != null)
            StopCoroutine(switchRoutine);

        switchRoutine = StartCoroutine(SwitchToNextTrackCoroutine());
    }

    private IEnumerator SwitchToNextTrackCoroutine()
    {
        if (bgmSource.isPlaying)
            bgmSource.Stop();

        if (songNameText != null)
            songNameText.text = string.Empty;

        yield return new WaitForSecondsRealtime(0.5f);

        var clips = Clips;
        Index = (Index + 1) % clips.Length;
        bgmSource.clip = clips[Index];
        bgmSource.Play();

        UpdateSongNameUI();
        switchRoutine = null;
    }

    private void UpdateSongNameUI()
    {
        if (songNameText == null)
            return;

        var names = Names;
        int i = Index;
        if (names != null && i >= 0 && names.Length > i && names[i] != null)
        {
            songNameText.text = names[i];
        }
        else if (bgmSource.clip != null)
        {
            songNameText.text = bgmSource.clip.name;
        }
        else
        {
            songNameText.text = string.Empty;
        }
    }
}
