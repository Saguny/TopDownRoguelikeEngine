using TMPro;
using UnityEngine;
using UnityEngine.UI;

// the Command Token's prompt on screen: its icon under a circle that winds round like a spiral
// slider as it recharges, and its key once it's ready. put this on your own prompt and fill the
// fields in; with none in the scene a plain one is built in the bottom corner so it always shows
[DisallowMultipleComponent]
public class CommandTokenHUD : MonoBehaviour
{
    [Header("Parts (all optional)")]
    [Tooltip("the token's icon. empty sprite = the Command Token's own")]
    [SerializeField] private Image icon;
    [Tooltip("the circle over the icon: a Filled, Radial 360 image. it fills as the token recharges")]
    [SerializeField] private Image spiral;
    [Tooltip("drawn over the icon while it recharges and shrinks away with the spiral: a Filled, Radial 360 image")]
    [SerializeField] private Image shade;
    [Tooltip("says the key, e.g. E, once it's ready")]
    [SerializeField] private TMP_Text keyLabel;
    [Tooltip("the seconds left while it recharges")]
    [SerializeField] private TMP_Text secondsLabel;
    [Tooltip("scaled for the ready pulse. empty = this object")]
    [SerializeField] private RectTransform pulseTarget;

    [Header("Look")]
    [SerializeField] private Color chargingIcon = new Color(0.45f, 0.45f, 0.5f, 1f);
    [SerializeField] private Color readyIcon = Color.white;
    [SerializeField] private Color spiralCharging = new Color(0.85f, 0.2f, 0.15f, 1f);
    [SerializeField] private Color spiralReady = new Color(1f, 0.85f, 0.3f, 1f);
    [Tooltip("how much the prompt swells and settles when it becomes ready")]
    [SerializeField, Min(0f)] private float readyPop = 0.25f;
    [SerializeField, Min(0f)] private float idlePulse = 0.05f;

    private bool wasReady;
    private float popTime = -1f;
    private CanvasGroup group;

    private static CommandTokenHUD instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    // the scene's own prompt, or a plain one built on the spot
    public static void Ensure()
    {
        if (instance != null) return;
        instance = FindAnyObjectByType<CommandTokenHUD>(FindObjectsInactive.Include);
        if (instance == null) instance = BuildDefault();
    }

    private void Awake()
    {
        if (instance == null) instance = this;
        if (pulseTarget == null) pulseTarget = transform as RectTransform;
        if (!TryGetComponent(out group)) group = gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Update()
    {
        var token = CommandToken.Current;
        group.alpha = token != null ? 1f : 0f;
        if (token == null) return;

        float charge = token.Charge;
        bool ready = token.Ready;

        if (icon != null)
        {
            if (icon.sprite == null && token.Icon != null) icon.sprite = token.Icon;
            icon.color = ready ? readyIcon : chargingIcon;
        }
        if (spiral != null)
        {
            spiral.fillAmount = charge;
            spiral.color = ready ? spiralReady : spiralCharging;
        }
        if (shade != null) shade.fillAmount = 1f - charge;

        if (keyLabel != null)
        {
            keyLabel.text = KeyName(token.Key);
            keyLabel.enabled = ready;
        }
        if (secondsLabel != null)
        {
            secondsLabel.enabled = !ready && !token.Casting;
            secondsLabel.text = Mathf.CeilToInt(token.SecondsLeft).ToString();
        }

        if (ready && !wasReady) popTime = 0f;
        wasReady = ready;

        // a pop as it becomes ready, then a slow breathe while it waits for the key
        float scale = 1f;
        if (popTime >= 0f)
        {
            popTime += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(popTime / 0.3f);
            scale += readyPop * Mathf.Sin(k * Mathf.PI);
            if (k >= 1f) popTime = -1f;
        }
        else if (ready) scale += idlePulse * Mathf.Sin(Time.unscaledTime * 4f);
        if (pulseTarget != null) pulseTarget.localScale = Vector3.one * scale;
    }

    private static string KeyName(KeyCode key)
    {
        string name = key.ToString();
        return name.StartsWith("Alpha") ? name.Substring(5) : name;
    }

    // ---------------------------------------------------------------- the plain prompt

    private static CommandTokenHUD BuildDefault()
    {
        var canvasGo = new GameObject("Command Token Prompt", typeof(Canvas), typeof(CanvasScaler));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;

        var root = new GameObject("Command Token", typeof(RectTransform));
        var rt = (RectTransform)root.transform;
        rt.SetParent(canvasGo.transform, false);
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(-110f, 110f);
        rt.sizeDelta = new Vector2(112f, 112f);

        var disc = Disc();
        var back = Part<Image>(rt, "Back", 112f);
        back.sprite = disc;
        back.color = new Color(0.08f, 0.04f, 0.1f, 0.75f);

        var spiral = Part<Image>(rt, "Spiral", 112f);
        spiral.sprite = Ring();
        Radial(spiral);

        var icon = Part<Image>(rt, "Icon", 76f);
        icon.preserveAspect = true;

        var shade = Part<Image>(rt, "Shade", 76f);
        shade.sprite = disc;
        shade.color = new Color(0f, 0f, 0f, 0.55f);
        Radial(shade);
        shade.fillClockwise = false;

        var key = Label(rt, "Key", 40f, new Vector2(0f, -70f));
        var seconds = Label(rt, "Seconds", 34f, Vector2.zero);

        var hud = root.AddComponent<CommandTokenHUD>();
        hud.icon = icon;
        hud.spiral = spiral;
        hud.shade = shade;
        hud.keyLabel = key;
        hud.secondsLabel = seconds;
        hud.pulseTarget = rt;
        return hud;
    }

    private static T Part<T>(RectTransform parent, string name, float size) where T : Graphic
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(T));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.sizeDelta = new Vector2(size, size);
        var g = go.GetComponent<T>();
        g.raycastTarget = false;
        return g;
    }

    private static void Radial(Image image)
    {
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Radial360;
        image.fillOrigin = (int)Image.Origin360.Top;
        image.fillClockwise = true;
    }

    private static TMP_FontAsset font;

    private static TMP_Text Label(RectTransform parent, string name, float size, Vector2 at)
    {
        var text = Part<TextMeshProUGUI>(parent, name, 112f);
        text.rectTransform.anchoredPosition = at;
        text.alignment = TextAlignmentOptions.Center;
        // the game's own font, as the rest of the HUD
        if (font == null) font = Resources.Load<TMP_FontAsset>("fonts/Pixelta");
        if (font != null) text.font = font;
        text.fontSize = size;
        text.fontStyle = FontStyles.Bold;
        text.color = Color.white;
        text.outlineWidth = 0.25f;
        text.outlineColor = new Color32(0x1a, 0x0c, 0x26, 0xff);
        return text;
    }

    // a filled circle and a ring, drawn in pixels so the plain prompt matches the pixel art
    private static Sprite Disc() => Circle(32, 0f);
    private static Sprite Ring() => Circle(32, 0.72f);

    private static Sprite Circle(int size, float hole)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[size * size];
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = new Vector2(x + 0.5f - r, y + 0.5f - r).magnitude / r;
                px[y * size + x] = d <= 1f && d >= hole ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
