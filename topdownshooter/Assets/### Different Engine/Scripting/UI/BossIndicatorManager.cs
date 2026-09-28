using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// a boss off the screen gets a medallion at the screen's edge on the line towards it: a bronze-gold
// rim (Resources/UI/boss_ring, made by Tools/VFX/alerts) round a window showing the boss itself,
// its own sprite as it moves, and a gold arrowhead orbiting the rim to point at it. it pops in as
// the boss leaves the screen and shrinks away as it comes back, nudges towards it in a slow beat,
// and sits a little smaller the further off the boss is. BossMarker registers every boss. it
// builds its own canvas, drawn pixel for pixel at a whole multiple of the art
public class BossIndicatorManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera cam;

    [Header("Placement (art pixels)")]
    [Tooltip("how far in from the screen's sides and bottom the medallion sits")]
    [SerializeField] private float edgePadding = 22f;
    [Tooltip("how far in from the top: clear of the level bar and the clock")]
    [SerializeField] private float topPadding = 40f;
    [Tooltip("how far out from the medallion's centre the arrowhead orbits")]
    [SerializeField] private float arrowRadius = 21f;

    private const float ArtFps = 8f;
    private const float PortraitBox = 20f;     // the boss's sprite fits in this square, art pixels

    private sealed class Indicator
    {
        public Transform boss;
        public SpriteRenderer look;
        public RectTransform root, arrow, portrait;
        public Image ring, arrowImage, portraitImage;
        public float shown;          // 0 hidden, 1 fully up: pops in and shrinks away
        public bool off;             // the boss is off the screen
    }

    private readonly Dictionary<Transform, Indicator> indicators = new Dictionary<Transform, Indicator>();
    private readonly List<Transform> gone = new List<Transform>();
    private Canvas canvas;
    private RectTransform layer;
    private Sprite[] ringFrames, arrowFrames;
    private Sprite window;

    private void Awake()
    {
        if (cam == null) cam = Camera.main;
        ringFrames = YamaArt.Strip("UI/boss_ring", 1f);
        arrowFrames = YamaArt.Strip("UI/boss_arrow", 1f);
        var w = YamaArt.Strip("UI/boss_window", 1f);
        window = w != null && w.Length > 0 ? w[0] : null;
        MakeCanvas();
    }

    private void OnDestroy()
    {
        if (canvas != null) Destroy(canvas.gameObject);
    }

    private void MakeCanvas()
    {
        var go = new GameObject("Boss Indicators", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        canvas.pixelPerfect = true;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        layer = go.GetComponent<RectTransform>();
    }

    // a whole number of screen pixels to an art pixel: 3 at 1080p
    private static float ArtScale => Mathf.Max(1f, Mathf.Round(Screen.height / 360f));

    private void Update()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null || canvas == null) return;
        var scaler = canvas.GetComponent<CanvasScaler>();
        float scale = ArtScale;
        if (!Mathf.Approximately(scaler.scaleFactor, scale)) scaler.scaleFactor = scale;

        // the screen in art pixels, and the frame the medallions ride round inside it
        Vector2 screen = new Vector2(Screen.width, Screen.height) / scale;
        Rect inner = Rect.MinMaxRect(edgePadding, edgePadding, screen.x - edgePadding, screen.y - topPadding);
        Vector2 middle = inner.center;
        float t = Time.unscaledTime;
        int ringFrame = ringFrames != null && ringFrames.Length > 0 ? (int)(t * ArtFps) % ringFrames.Length : 0;
        int arrowFrame = arrowFrames != null && arrowFrames.Length > 0 ? (int)(t * ArtFps * 1.5f) % arrowFrames.Length : 0;

        gone.Clear();
        foreach (var kv in indicators)
        {
            var ind = kv.Value;
            if (ind.boss == null || !ind.boss.gameObject.activeInHierarchy) { gone.Add(kv.Key); continue; }

            Vector3 vp = cam.WorldToViewportPoint(ind.boss.position);
            bool behind = vp.z < 0f;
            if (behind) vp = new Vector3(1f - vp.x, 1f - vp.y, 0f);
            ind.off = behind || vp.x < -0.02f || vp.x > 1.02f || vp.y < -0.02f || vp.y > 1.02f;
            ind.shown = Mathf.MoveTowards(ind.shown, ind.off ? 1f : 0f, Time.unscaledDeltaTime / (ind.off ? 0.25f : 0.15f));
            bool visible = ind.shown > 0f;
            if (ind.root.gameObject.activeSelf != visible) ind.root.gameObject.SetActive(visible);
            if (!visible) continue;

            // along the line from the middle of the screen towards the boss, to the frame's edge
            Vector2 target = new Vector2(vp.x * screen.x, vp.y * screen.y);
            Vector2 dir = target - middle;
            if (dir.sqrMagnitude < 0.01f) dir = Vector2.up;
            dir.Normalize();
            float reach = Mathf.Min(
                Mathf.Abs(dir.x) > 0.0001f ? (dir.x > 0f ? inner.xMax - middle.x : middle.x - inner.xMin) / Mathf.Abs(dir.x) : float.MaxValue,
                Mathf.Abs(dir.y) > 0.0001f ? (dir.y > 0f ? inner.yMax - middle.y : middle.y - inner.yMin) / Mathf.Abs(dir.y) : float.MaxValue);
            // a slow beat nudging it towards the boss
            float beat = Mathf.Max(0f, Mathf.Sin(t * 5f)) * 1.5f;
            Vector2 at = middle + dir * reach + dir * beat;
            ind.root.anchoredPosition = new Vector2(Mathf.Round(at.x), Mathf.Round(at.y));

            // pops in with an overshoot; the further off the boss, the smaller it sits
            float k = ind.shown;
            float pop = ind.off ? 1f + 0.18f * Mathf.Sin(k * Mathf.PI) : k;
            float far = Mathf.Clamp01((Vector2.Distance(target, middle) - screen.magnitude * 0.5f) / screen.magnitude);
            ind.root.localScale = Vector3.one * (pop * Mathf.Lerp(1f, 0.8f, far));

            if (ringFrames != null && ringFrames.Length > 0) ind.ring.sprite = ringFrames[ringFrame];
            if (arrowFrames != null && arrowFrames.Length > 0) ind.arrowImage.sprite = arrowFrames[arrowFrame];
            ind.arrow.anchoredPosition = dir * (arrowRadius + beat * 0.5f);
            ind.arrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

            ShowPortrait(ind);
        }
        // a boss destroyed outright (its transform now null to Unity) is dropped by its key
        foreach (var b in gone) Drop(b);
    }

    // the boss's own sprite as it is right now, fitted into the window, facing the way it faces
    private static void ShowPortrait(Indicator ind)
    {
        var s = ind.look != null ? ind.look.sprite : null;
        ind.portraitImage.enabled = s != null;
        if (s == null) return;
        if (ind.portraitImage.sprite != s)
        {
            ind.portraitImage.sprite = s;
            Vector2 size = s.rect.size;
            float fit = PortraitBox / Mathf.Max(1f, Mathf.Max(size.x, size.y));
            ind.portrait.sizeDelta = size * fit;
        }
        ind.portrait.localScale = new Vector3(ind.look.flipX ? -1f : 1f, ind.look.flipY ? -1f : 1f, 1f);
    }

    // called by BossMarker
    public void RegisterBoss(Transform boss)
    {
        if (boss == null || indicators.ContainsKey(boss) || layer == null) return;

        var root = Node("Boss Indicator", layer, Vector2.zero);
        root.anchorMin = root.anchorMax = Vector2.zero;

        // the window, which clips the portrait to its round shape
        var win = Node("Window", root, new Vector2(36f, 36f));
        var winImage = win.gameObject.AddComponent<Image>();
        winImage.sprite = window;
        winImage.color = window != null ? Color.white : new Color(0.25f, 0.03f, 0.07f);
        winImage.raycastTarget = false;
        win.gameObject.AddComponent<Mask>().showMaskGraphic = true;

        var portrait = Node("Boss", win, new Vector2(PortraitBox, PortraitBox));
        var portraitImage = portrait.gameObject.AddComponent<Image>();
        portraitImage.raycastTarget = false;
        portraitImage.preserveAspect = true;

        var ring = Node("Rim", root, new Vector2(36f, 36f)).gameObject.AddComponent<Image>();
        ring.raycastTarget = false;
        if (ringFrames != null && ringFrames.Length > 0) ring.sprite = ringFrames[0];
        else ring.color = new Color(0.9f, 0.7f, 0.3f, 0.6f);

        var arrow = Node("Arrow", root, new Vector2(16f, 16f));
        var arrowImage = arrow.gameObject.AddComponent<Image>();
        arrowImage.raycastTarget = false;
        if (arrowFrames != null && arrowFrames.Length > 0) arrowImage.sprite = arrowFrames[0];
        else arrowImage.color = new Color(1f, 0.8f, 0.3f);

        root.gameObject.SetActive(false);

        var look = boss.GetComponent<SpriteRenderer>();
        if (look == null) look = boss.GetComponentInChildren<SpriteRenderer>();
        indicators.Add(boss, new Indicator
        {
            boss = boss, look = look, root = root, ring = ring, arrow = arrow, arrowImage = arrowImage,
            portrait = portrait, portraitImage = portraitImage,
        });
    }

    // called by BossMarker
    public void UnregisterBoss(Transform boss)
    {
        if (boss != null) Drop(boss);
    }

    private void Drop(Transform key)
    {
        if (!indicators.TryGetValue(key, out var ind)) return;
        if (ind.root != null) Destroy(ind.root.gameObject);
        indicators.Remove(key);
    }

    private static RectTransform Node(string name, Transform parent, Vector2 size)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.sizeDelta = size;
        return rt;
    }
}
