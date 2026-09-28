using UnityEngine;
using UnityEngine.UI;

// every button in the game makes a sound when it's pressed: Resources/Sounds/ButtonClick, or
// ButtonBack for the buttons that go back (named Back, Close, Return, Cancel or Resume), so
// replacing those two files changes them everywhere. a button whose UIButtonSound has a click
// sound of its own keeps that one instead. buttons are picked up as they appear, copies made at
// runtime included (character cards, the level up cards), and the sounds go through a 2D voice
// on the mixer's SFX group, so the SFX volume applies. it makes itself; nothing in any scene
public class ButtonSounds : MonoBehaviour
{
    // the files are normalized to the game's reference loudness; these set how loud they're heard
    public const float ClickVolume = 0.32f;
    public const float BackVolume = 0.28f;

    private static ButtonSounds instance;
    private AudioSource voice;
    private AudioClip click, back;
    private float nextScan;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (instance != null) return;
        var go = new GameObject("Button Sounds");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<ButtonSounds>();
    }

    private void Awake()
    {
        click = Resources.Load<AudioClip>("Sounds/ButtonClick");
        back = Resources.Load<AudioClip>("Sounds/ButtonBack");
        voice = gameObject.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.spatialBlend = 0f;
        voice.ignoreListenerPause = true;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextScan) return;
        // a search of the whole scene: often while a menu's up (the game stopped, where the level up
        // makes its cards) or the scene is light, rarely over a running horde, where it cost ~2.5 ms
        bool menus = Time.timeScale <= 0f || EnemySwarm.Count < 100;
        nextScan = Time.unscaledTime + (menus ? 0.25f : 3f);

        // the mixer can be handed over after this starts, so keep looking until it's found
        if (voice.outputAudioMixerGroup == null && GameSettings.Mixer != null)
        {
            var groups = GameSettings.Mixer.FindMatchingGroups("SFX");
            if (groups != null && groups.Length > 0) voice.outputAudioMixerGroup = groups[0];
        }

        foreach (var button in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (!button.TryGetComponent(out ButtonSound _)) button.gameObject.AddComponent<ButtonSound>();
    }

    public static void Play(bool goingBack)
    {
        if (instance == null) return;
        var clip = goingBack && instance.back != null ? instance.back : instance.click;
        if (clip != null) instance.voice.PlayOneShot(clip, goingBack ? BackVolume : ClickVolume);
    }
}
