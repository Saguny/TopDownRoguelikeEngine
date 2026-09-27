using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// every trip between the menu and a run: the spiral winds shut to black, the loading screen plays
// while the next scene loads in the background, held back until the screen has run at least
// MinSeconds, then the scene starts, the loading screen flashes out and the spiral opens on it.
// the game is held still underneath the whole time. it all lives on an overlay that survives the
// scene change. the loading screen is a copy of the main menu's LoadingPanel with its
// LoadingAnimation (remembered the first time the menu opens), or a plain one built to match when
// the game was started from another scene
public class SceneLoader : MonoBehaviour
{
    public const float MinSeconds = 2.5f;

    public static bool Busy { get; private set; }

    // a load has started, with the scene it's going to (the menu music fades out on it)
    public static event System.Action<string> Started;

    private static GameObject template;
    // the canvas scaling the template was made for, so its text comes out the same size
    private static Vector2 referenceResolution = new Vector2(1920f, 1080f);
    private static float matchWidthOrHeight;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Busy = false;
        template = null;
        referenceResolution = new Vector2(1920f, 1080f);
        matchWidthOrHeight = 0f;
        Started = null;
    }

    // keeps a hidden copy of the menu's loading panel for every loading screen this session
    public static void Remember(GameObject loadingPanel)
    {
        if (template != null || loadingPanel == null) return;
        bool was = loadingPanel.activeSelf;
        loadingPanel.SetActive(false);        // copied switched off, so the copy wakes only when shown
        template = Instantiate(loadingPanel);
        loadingPanel.SetActive(was);
        template.name = "Loading Screen Template";
        DontDestroyOnLoad(template);
        var scaler = loadingPanel.GetComponentInParent<CanvasScaler>(true);
        if (scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
        {
            referenceResolution = scaler.referenceResolution;
            matchWidthOrHeight = scaler.matchWidthOrHeight;
        }
    }

    // loadingPanel: remembered as the look for loading screens if none is yet
    public static void Load(string scene, GameObject loadingPanel = null, float unused = 0f)
    {
        if (Busy) return;
        Busy = true;
        Remember(loadingPanel);
        Started?.Invoke(scene);

        var host = new GameObject("Loading Screen");
        DontDestroyOnLoad(host);
        var loader = host.AddComponent<SceneLoader>();
        loader.StartCoroutine(loader.Run(scene));
    }

    private IEnumerator Run(string scene)
    {
        Time.timeScale = 0f;                  // nothing moves while the screen is covered

        // shut
        var close = SpiralReveal.Close();
        while (close != null && !close.Done) yield return null;

        // the loading screen over the black, then the load behind it, not started yet
        var screen = MakeScreen();
        var animation = LoadingAnimation.On(screen);
        animation.opaque = true;
        animation.fillSeconds = MinSeconds - 0.2f;
        screen.SetActive(true);
        if (close != null) Destroy(close.gameObject);
        yield return null;

        var op = SceneManager.LoadSceneAsync(scene);
        if (op == null)
        {
            Debug.LogError($"SceneLoader: can't load \"{scene}\". Is it in the build settings?");
            Time.timeScale = 1f;
            Busy = false;
            Destroy(gameObject);
            yield break;
        }
        op.allowSceneActivation = false;
        while (op.progress < 0.9f || !animation.Filled) yield return null;

        // start it, and let it draw its first frames before it's shown
        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;
        Time.timeScale = 1f;
        yield return null;
        yield return new WaitForEndOfFrame();
        yield return null;

        // open
        yield return animation.Finale();
        SpiralReveal.Play();
        Busy = false;
        Destroy(gameObject);
    }

    // the loading screen on this object's own overlay canvas, scaled like the game's UI
    private GameObject MakeScreen()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 31000;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.matchWidthOrHeight = matchWidthOrHeight;
        gameObject.AddComponent<GraphicRaycaster>();

        GameObject screen;
        if (template != null)
        {
            screen = Instantiate(template, transform, false);
            screen.name = "Loading Panel";
        }
        else
        {
            // no menu seen this session: an overlay and the word, like the menu's panel
            screen = new GameObject("Loading Panel", typeof(RectTransform));
            screen.transform.SetParent(transform, false);
            screen.SetActive(false);
            var overlay = new GameObject("Overlay", typeof(RectTransform)).AddComponent<Image>();
            Stretch(overlay.rectTransform, screen.transform);
            overlay.color = new Color(0f, 0f, 0f, 0.9f);
            var label = new GameObject("LoadingText", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            label.rectTransform.SetParent(screen.transform, false);
            label.rectTransform.sizeDelta = new Vector2(300f, 50f);
            var font = FindFirstObjectByType<TMP_Text>();
            if (font != null) label.font = font.font;
            label.fontSize = 36f;
            label.alignment = TextAlignmentOptions.Center;
            label.text = "Loading...";
        }
        Stretch((RectTransform)screen.transform, transform);
        return screen;
    }

    private static void Stretch(RectTransform rt, Transform parent)
    {
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
