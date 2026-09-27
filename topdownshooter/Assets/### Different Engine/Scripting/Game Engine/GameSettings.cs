using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

// the player's options, saved in PlayerPrefs. read them from anywhere, e.g. GameSettings.ShowBlood.
// setting one applies it straight away and saves it, and the saved values are applied again
// every time the game starts or a scene loads, so the options menu never has to be opened
public static class GameSettings
{
    // volumes are 0-100, like the pause menu's sliders, which share the music and sfx keys
    public const string MasterKey = "vol_master";
    public const string MusicKey = "vol_music";
    public const string SfxKey = "vol_sfx";
    public const string MusicParam = "MusicVol";
    public const string SfxParam = "SFXVol";

    private const string VSyncKey = "vsync";
    private const string BloodKey = "show_blood";
    private const string WidthKey = "res_width";
    private const string HeightKey = "res_height";
    private const string WindowModeKey = "window_mode";

    public static float MasterVolume
    {
        get => PlayerPrefs.GetFloat(MasterKey, 100f);
        set
        {
            PlayerPrefs.SetFloat(MasterKey, Mathf.Clamp(value, 0f, 100f));
            AudioListener.volume = MasterVolume / 100f;
        }
    }

    public static float MusicVolume
    {
        get => PlayerPrefs.GetFloat(MusicKey, 100f);
        set
        {
            PlayerPrefs.SetFloat(MusicKey, Mathf.Clamp(value, 0f, 100f));
            SetMixer(MusicParam, MusicVolume);
        }
    }

    public static float SfxVolume
    {
        get => PlayerPrefs.GetFloat(SfxKey, 100f);
        set
        {
            PlayerPrefs.SetFloat(SfxKey, Mathf.Clamp(value, 0f, 100f));
            SetMixer(SfxParam, SfxVolume);
        }
    }

    // until it's first changed, whatever the project's quality settings say
    public static bool VSync
    {
        get => PlayerPrefs.HasKey(VSyncKey) ? PlayerPrefs.GetInt(VSyncKey) == 1 : QualitySettings.vSyncCount > 0;
        set
        {
            PlayerPrefs.SetInt(VSyncKey, value ? 1 : 0);
            QualitySettings.vSyncCount = value ? 1 : 0;
        }
    }

    // gameplay reads this when it spawns blood, e.g. the enemy death burst
    public static bool ShowBlood
    {
        get => PlayerPrefs.GetInt(BloodKey, 1) == 1;
        set => PlayerPrefs.SetInt(BloodKey, value ? 1 : 0);
    }

    // the window size last picked in the options, or the current one if nothing was picked.
    // Unity restores the window itself on launch, so this is only read back for the menu
    public static Vector2Int Resolution =>
        PlayerPrefs.HasKey(WidthKey)
            ? new Vector2Int(PlayerPrefs.GetInt(WidthKey), PlayerPrefs.GetInt(HeightKey))
            : new Vector2Int(Screen.width, Screen.height);

    public static FullScreenMode WindowMode =>
        PlayerPrefs.HasKey(WindowModeKey) ? (FullScreenMode)PlayerPrefs.GetInt(WindowModeKey) : Screen.fullScreenMode;

    // resolution and window mode change together, since Unity sets them in one call. neither does
    // anything in the editor's game view, only in a build
    public static void SetDisplay(Vector2Int size, FullScreenMode mode)
    {
        PlayerPrefs.SetInt(WidthKey, size.x);
        PlayerPrefs.SetInt(HeightKey, size.y);
        PlayerPrefs.SetInt(WindowModeKey, (int)mode);
        Screen.SetResolution(size.x, size.y, mode);
    }

    // the mixer holding MusicVol and SFXVol. the options menu and pause menu hand theirs over;
    // without one, whichever loaded mixer has those parameters is used
    private static AudioMixer mixer;

    public static AudioMixer Mixer
    {
        get
        {
            if (mixer == null)
                foreach (var loaded in Resources.FindObjectsOfTypeAll<AudioMixer>())
                    if (loaded.GetFloat(MusicParam, out _)) { mixer = loaded; break; }
            return mixer;
        }
        set { if (value != null) mixer = value; }
    }

    // 0-100 to decibels. 0 stops at a tiny floor instead of the mixer's -80 dB hard mute
    public static float ToDecibels(float volume) => Mathf.Log10(Mathf.Max(volume / 100f, 0.001f)) * 20f;

    private static void SetMixer(string param, float volume)
    {
        var m = Mixer;
        if (m != null) m.SetFloat(param, ToDecibels(volume));
    }

    public static void ApplyAudio()
    {
        AudioListener.volume = MasterVolume / 100f;
        SetMixer(MusicParam, MusicVolume);
        SetMixer(SfxParam, SfxVolume);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplySaved()
    {
        mixer = null;
        QualitySettings.vSyncCount = VSync ? 1 : 0;
        AudioListener.volume = MasterVolume / 100f;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static async void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // a mixer ignores values set before its first frame, so wait one
        await Awaitable.NextFrameAsync();
        if (Application.isPlaying) ApplyAudio();
    }
}
