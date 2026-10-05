using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    [Header("ui")]
    [SerializeField] GameObject pausePanel;
    [SerializeField] Slider musicSlider;
    [SerializeField] Slider sfxSlider;

    [Header("audio mixer")]
    [SerializeField] AudioMixer mixer;         // assign your MasterMixer asset
    [SerializeField] string musicParam = "MusicVol";
    [SerializeField] string sfxParam = "SFXVol";

    [Header("other ui")]
    [SerializeField] private UpgradeMenuUI upgradeMenu; // block pause while this is open

    [Header("buttons and options (empty: found by name under the pause panel)")]
    [Tooltip("empty: the toggle with Blood in its name. switches the Show Blood option")]
    [SerializeField] private Toggle showBloodToggle;
    [Tooltip("empty: the button called Resume. wired to Resume unless it already calls something")]
    [SerializeField] private Button resumeButton;
    [Tooltip("empty: the button called Quit. wired to Exit To Menu unless it already calls something")]
    [SerializeField] private Button quitButton;

    // shared with the main menu's options, so both sets of sliders move the same volumes
    const string MusicKey = GameSettings.MusicKey;
    const string SfxKey = GameSettings.SfxKey;

    bool paused;

    void Start()
    {
        // auto-find upgrade menu if not wired in inspector
        if (upgradeMenu == null)
            upgradeMenu = FindFirstObjectByType<UpgradeMenuUI>();

        // 1) migrate any old saved zeros so we don't load “perma-mute”
        float musicSaved = PlayerPrefs.GetFloat(MusicKey, 100f);
        float sfxSaved = PlayerPrefs.GetFloat(SfxKey, 100f);
        if (musicSaved <= 0f) { musicSaved = 1f; PlayerPrefs.SetFloat(MusicKey, musicSaved); }
        if (sfxSaved <= 0f) { sfxSaved = 1f; PlayerPrefs.SetFloat(SfxKey, sfxSaved); }

        musicSlider.minValue = 0f; musicSlider.maxValue = 100f;
        sfxSlider.minValue = 0f; sfxSlider.maxValue = 100f;

        musicSlider.SetValueWithoutNotify(musicSaved);
        sfxSlider.SetValueWithoutNotify(sfxSaved);

        ApplyMusic(musicSaved);
        ApplySfx(sfxSaved);

        musicSlider.onValueChanged.AddListener(ApplyMusic);
        sfxSlider.onValueChanged.AddListener(ApplySfx);

        if (showBloodToggle == null) showBloodToggle = FindUnderPanel<Toggle>("Blood");
        if (showBloodToggle != null)
        {
            showBloodToggle.SetIsOnWithoutNotify(GameSettings.ShowBlood);
            showBloodToggle.onValueChanged.AddListener(on => GameSettings.ShowBlood = on);
        }
        Wire(resumeButton != null ? resumeButton : FindUnderPanel<Button>("Resume"), OnResume);
        Wire(quitButton != null ? quitButton : FindUnderPanel<Button>("Quit"), OnExitToMenu);

        // Current Upgrades and Favored Items share a place; the NextPanel arrow turns between them
        PanelFlip.Wire(pausePanel);

        pausePanel.SetActive(false);

        // keep mouse usable
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // safety: ensure global listener isn’t paused. its volume is the options menu's General
        AudioListener.pause = false;
        AudioListener.volume = GameSettings.MasterVolume / 100f;
        GameSettings.Mixer = mixer;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && !SceneLoader.Busy)
        {
            // not over the level up, an envelope opening, the Wuchang's taking or the end screen:
            // each owns the game's clock while it's up, and unpausing under it would start the game
            if (upgradeMenu != null && upgradeMenu.IsOpen)
                return;
            if (EnvelopeOpening.Busy || SoulTaking.Running || RunEnded)
                return;

            TogglePause();
        }

        // DEV: press F9 to reset volumes to defaults if needed
        if (Input.GetKeyDown(KeyCode.F9))
            ResetVolumesToDefault();
    }

    // something opened under the pause (a level up landing the same frame): resuming leaves it stopped
    private bool ClockHeld => (upgradeMenu != null && upgradeMenu.IsOpen) || EnvelopeOpening.Busy || RunEnded;

    private GameOverScreen endScreen;
    private bool RunEnded
    {
        get
        {
            if (endScreen == null) endScreen = FindFirstObjectByType<GameOverScreen>(FindObjectsInactive.Include);
            return endScreen != null && endScreen.Shown;
        }
    }

    public void TogglePause()
    {
        paused = !paused;
        if (paused) Juice.Yield();
        Time.timeScale = paused || ClockHeld ? 0f : 1f;
        pausePanel.SetActive(paused);

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // 2) re-apply current values whenever state changes, in case mixer got suspended previously
        ReapplyMixer();
    }

    public void OnResume()
    {
        if (!paused) return;
        paused = false;
        Time.timeScale = ClockHeld ? 0f : 1f;
        pausePanel.SetActive(false);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        ReapplyMixer();
    }

    public void OnRestart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // back to the menu behind the loading screen (it holds the game still until the menu is up)
    public void OnExitToMenu() => SceneLoader.Load("MainMenu");

    public void OnQuitApp()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // a button set up in the inspector keeps its own calls; an empty one gets this
    private static void Wire(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null && button.onClick.GetPersistentEventCount() == 0)
            button.onClick.AddListener(action);
    }

    private T FindUnderPanel<T>(string namePart) where T : Component
    {
        if (pausePanel == null) return null;
        foreach (var c in pausePanel.GetComponentsInChildren<T>(true))
            if (c.name.IndexOf(namePart, System.StringComparison.OrdinalIgnoreCase) >= 0) return c;
        return null;
    }

    // === volume logic ===
    void ApplyMusic(float v)
    {
        // 0 → tiny floor to avoid -80 dB hard mute
        float lin = Mathf.Max(v / 100f, 0.001f);
        mixer.SetFloat(musicParam, Mathf.Log10(lin) * 20f);
        PlayerPrefs.SetFloat(MusicKey, v);
    }

    void ApplySfx(float v)
    {
        float lin = Mathf.Max(v / 100f, 0.001f);
        mixer.SetFloat(sfxParam, Mathf.Log10(lin) * 20f);
        PlayerPrefs.SetFloat(SfxKey, v);
    }

    void ReapplyMixer()
    {
        ApplyMusic(musicSlider.value);
        ApplySfx(sfxSlider.value);
        // extra nudge: clear then set, in case a stale value latched
        mixer.ClearFloat(musicParam);
        mixer.ClearFloat(sfxParam);
        ApplyMusic(musicSlider.value);
        ApplySfx(sfxSlider.value);
    }

    void ResetVolumesToDefault()
    {
        musicSlider.value = 100f;
        sfxSlider.value = 100f;
        ReapplyMixer();
    }
}
