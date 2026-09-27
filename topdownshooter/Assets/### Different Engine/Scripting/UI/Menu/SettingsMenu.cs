using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

// the options page. put it on the settings panel and drag in the controls; each one is optional.
// the dropdowns fill themselves, and every change applies and saves at once through GameSettings
public class SettingsMenu : MonoBehaviour
{
    [Header("Display")]
    [SerializeField] private TMP_Dropdown resolution;
    [SerializeField] private TMP_Dropdown windowMode;
    [SerializeField] private Toggle vSync;

    [Header("Audio (sliders run 0 to 100)")]
    [Tooltip("everything at once")]
    [SerializeField] private Slider general;
    [SerializeField] private Slider music;
    [SerializeField] private Slider sfx;
    [Tooltip("optional. the MasterMixer; found on its own when empty")]
    [SerializeField] private AudioMixer mixer;

    [Header("Gameplay")]
    [SerializeField] private Toggle showBlood;

    // the window modes offered, in dropdown order
    private static readonly FullScreenMode[] Modes =
        { FullScreenMode.ExclusiveFullScreen, FullScreenMode.FullScreenWindow, FullScreenMode.Windowed };
    private static readonly string[] ModeNames = { "Fullscreen", "Borderless", "Windowed" };

    private readonly List<Vector2Int> sizes = new List<Vector2Int>();

    private void Awake()
    {
        GameSettings.Mixer = mixer;

        if (resolution != null) resolution.onValueChanged.AddListener(i => ApplyDisplay());
        if (windowMode != null) windowMode.onValueChanged.AddListener(i => ApplyDisplay());
        if (vSync != null) vSync.onValueChanged.AddListener(on => GameSettings.VSync = on);

        SetUp(general, v => GameSettings.MasterVolume = v);
        SetUp(music, v => GameSettings.MusicVolume = v);
        SetUp(sfx, v => GameSettings.SfxVolume = v);

        if (showBlood != null) showBlood.onValueChanged.AddListener(on => GameSettings.ShowBlood = on);
    }

    private static void SetUp(Slider slider, UnityEngine.Events.UnityAction<float> apply)
    {
        if (slider == null) return;
        slider.minValue = 0f;
        slider.maxValue = 100f;
        slider.wholeNumbers = false;
        slider.onValueChanged.AddListener(apply);
    }

    // every time the page opens the controls show what's saved, without re-applying it
    private void OnEnable()
    {
        FillResolutions();
        FillWindowModes();
        if (vSync != null) vSync.SetIsOnWithoutNotify(GameSettings.VSync);

        if (general != null) general.SetValueWithoutNotify(GameSettings.MasterVolume);
        if (music != null) music.SetValueWithoutNotify(GameSettings.MusicVolume);
        if (sfx != null) sfx.SetValueWithoutNotify(GameSettings.SfxVolume);

        if (showBlood != null) showBlood.SetIsOnWithoutNotify(GameSettings.ShowBlood);
    }

    private void OnDisable() => PlayerPrefs.Save();

    // every size the monitor offers, biggest first, once each (refresh rates are left to Unity)
    private void FillResolutions()
    {
        if (resolution == null) return;

        sizes.Clear();
        foreach (var r in Screen.resolutions)
        {
            var size = new Vector2Int(r.width, r.height);
            if (!sizes.Contains(size)) sizes.Add(size);
        }

        // a window dragged to an odd size still shows up as picked
        var current = GameSettings.Resolution;
        if (!sizes.Contains(current)) sizes.Add(current);
        sizes.Sort((a, b) => a.x != b.x ? b.x.CompareTo(a.x) : b.y.CompareTo(a.y));

        var labels = new List<string>(sizes.Count);
        foreach (var size in sizes) labels.Add($"{size.x} x {size.y}");

        resolution.ClearOptions();
        resolution.AddOptions(labels);
        resolution.SetValueWithoutNotify(sizes.IndexOf(current));
        resolution.RefreshShownValue();
    }

    private void FillWindowModes()
    {
        if (windowMode == null) return;

        windowMode.ClearOptions();
        windowMode.AddOptions(new List<string>(ModeNames));

        int index = System.Array.IndexOf(Modes, GameSettings.WindowMode);
        windowMode.SetValueWithoutNotify(index >= 0 ? index : 0);
        windowMode.RefreshShownValue();
    }

    private void ApplyDisplay()
    {
        var size = resolution != null && resolution.value < sizes.Count ? sizes[resolution.value] : GameSettings.Resolution;
        var mode = windowMode != null ? Modes[Mathf.Clamp(windowMode.value, 0, Modes.Length - 1)] : GameSettings.WindowMode;
        GameSettings.SetDisplay(size, mode);
    }
}
