using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// the menus' flair, on every screen with nothing to set up: the game's own pointer (gold, a jade
// inlay, pressed in while the button is held), two stars chasing each other round the edge of
// whatever button is hovered (or picked with the keyboard or a pad), trailing light, and a burst
// where a click lands on the menus. the art is drawn by Tools/VFX/ui/ui.js into Resources/UI
public class UiFlair : MonoBehaviour
{
    private const float StarScale = 4f;       // screen pixels per art pixel, at 1080p
    private const float BurstScale = 3f;
    private const float Pad = 7f;             // how far outside the button's edge the stars run
    private const float Speed = 620f;         // along the edge, screen pixels a second at 1080p
    private const float Corner = 10f;
    private const int Ghosts = 4;
    private static readonly Vector2 Hotspot = new Vector2(2f, 2f);

    private static UiFlair instance;

    private Texture2D cursorUp, cursorDown;
    private Sprite[] star, burst;
    private RectTransform root;
    private PointerEventData pointer;
    private EventSystem pointerSystem;
    private readonly List<RaycastResult> hits = new List<RaycastResult>();
    private readonly Vector3[] corners = new Vector3[4];

    private readonly Image[][] stars = new Image[2][];
    private Selectable target;
    private Rect edge;                         // the target's rect on our canvas
    private float along, appear, kick, clock;
    private bool showing;
    private Vector3 lastMouse;
    private float lastMouseMove = -99f, lastKey = -99f;

    private readonly List<(Image image, float age)> bursts = new List<(Image, float)>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (instance != null) return;
        var go = new GameObject("UI Flair", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        DontDestroyOnLoad(go);
        instance = go.AddComponent<UiFlair>();
    }

    private void Awake()
    {
        var canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;              // over every menu, the tooltip and the envelope
        var scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;
        root = (RectTransform)transform;

        cursorUp = Resources.Load<Texture2D>("UI/ui_cursor");
        cursorDown = Resources.Load<Texture2D>("UI/ui_cursor_down");
        if (cursorUp != null) Cursor.SetCursor(cursorUp, Hotspot, CursorMode.Auto);

        star = Slice(Resources.Load<Texture2D>("UI/ui_star"));
        burst = Slice(Resources.Load<Texture2D>("UI/ui_click"));

        // each star with a few fading copies of itself behind it
        for (int s = 0; s < 2; s++)
        {
            stars[s] = new Image[Ghosts + 1];
            for (int g = Ghosts; g >= 0; g--)
            {
                var img = Pixel(g == 0 ? "Star" : "Star Trail", star.Length > 0 ? star[0] : null, StarScale, 9f);
                img.color = new Color(1f, 1f, 1f, 0f);
                stars[s][g] = img;
            }
        }
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // a strip of square frames side by side, cut into sprites
    private static Sprite[] Slice(Texture2D strip)
    {
        if (strip == null) return new Sprite[0];
        strip.filterMode = FilterMode.Point;
        int size = strip.height, n = Mathf.Max(1, strip.width / size);
        var frames = new Sprite[n];
        for (int i = 0; i < n; i++)
            frames[i] = Sprite.Create(strip, new Rect(i * size, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        return frames;
    }

    private Image Pixel(string name, Sprite sprite, float scale, float artSize)
    {
        var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        img.rectTransform.SetParent(root, false);
        img.sprite = sprite;
        img.raycastTarget = false;
        img.rectTransform.sizeDelta = Vector2.one * (sprite != null ? sprite.rect.width : artSize) * scale;
        return img;
    }

    private void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 20f);
        clock += dt;

        // the pointer, pressed in while the button's held
        if (cursorUp != null)
        {
            if (Input.GetMouseButtonDown(0) && cursorDown != null) Cursor.SetCursor(cursorDown, Hotspot, CursorMode.Auto);
            if (Input.GetMouseButtonUp(0)) Cursor.SetCursor(cursorUp, Hotspot, CursorMode.Auto);
        }

        // which input was used last: the pointer, or keys and a pad
        if ((Input.mousePosition - lastMouse).sqrMagnitude > 1f) { lastMouse = Input.mousePosition; lastMouseMove = clock; }
        if (Input.anyKeyDown && !Input.GetMouseButtonDown(0) && !Input.GetMouseButtonDown(1)) lastKey = clock;

        var found = Hovered(out bool overUi);
        if (found != target)
        {
            target = found;
            if (target != null)
            {
                appear = 0f;
                showing = true;
                Measure();
                along = Perimeter(edge) * 0.25f;
            }
        }
        if (target != null) Measure();
        Stars(dt);

        // a click on the menus bursts where it lands; Enter on a picked button bursts on it
        if (Input.GetMouseButtonDown(0) && (overUi || Time.timeScale <= 0f))
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, Input.mousePosition, null, out var at);
            Burst(at);
        }
        else if (target != null && lastKey > lastMouseMove && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            Burst(edge.center);
        Bursts(dt);
    }

    // the button under the pointer (the top thing there must be it or inside it), or, when keys
    // or a pad were used last, the one they have picked
    private Selectable Hovered(out bool overUi)
    {
        overUi = false;
        var es = EventSystem.current;
        if (es == null) return null;

        if (Input.mousePresent)
        {
            // a new scene brings a new event system
            if (pointer == null || pointerSystem != es) { pointer = new PointerEventData(es); pointerSystem = es; }
            pointer.Reset();
            pointer.position = Input.mousePosition;
            hits.Clear();
            es.RaycastAll(pointer, hits);
            if (hits.Count > 0)
            {
                overUi = true;
                var s = hits[0].gameObject != null ? hits[0].gameObject.GetComponentInParent<Selectable>() : null;
                if (Worth(s) && lastMouseMove >= lastKey) return s;
            }
        }
        if (lastKey > lastMouseMove && es.currentSelectedGameObject != null)
        {
            var picked = es.currentSelectedGameObject.GetComponent<Selectable>();
            if (Worth(picked)) return picked;
        }
        return null;
    }

    private static bool Worth(Selectable s) =>
        s != null && (s is Button || s is Toggle) && s.IsInteractable() && s.isActiveAndEnabled;

    // the target's rect, from wherever its canvas is, onto ours, a little outside its edge
    private void Measure()
    {
        if (target == null) return;
        var rt = (RectTransform)target.transform;
        rt.GetWorldCorners(corners);
        var canvas = target.GetComponentInParent<Canvas>();
        var cam = canvas == null || canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.rootCanvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, RectTransformUtility.WorldToScreenPoint(cam, corners[0]), null, out var min);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, RectTransformUtility.WorldToScreenPoint(cam, corners[2]), null, out var max);
        edge = Rect.MinMaxRect(min.x - Pad, min.y - Pad, max.x + Pad, max.y + Pad);
    }

    // ---------------------------------------------------------------- the stars

    private void Stars(float dt)
    {
        appear = Mathf.MoveTowards(appear, showing && target != null ? 1f : 0f, dt / (target != null ? 0.18f : 0.12f));
        if (target == null && appear <= 0f) showing = false;
        kick = Mathf.MoveTowards(kick, 0f, dt * 4f);
        float length = Perimeter(edge);
        if (length <= 1f) return;
        along = (along + Speed * dt) % length;

        for (int s = 0; s < 2; s++)
        {
            // the two chase each other, half the way round apart, twinkling out of step
            int frame = star.Length > 0 ? ((int)(clock * 12f) + s * 3) % star.Length : 0;
            for (int g = 0; g <= Ghosts; g++)
            {
                var img = stars[s][g];
                if (appear <= 0f) { img.color = new Color(1f, 1f, 1f, 0f); continue; }
                float at = along + s * length * 0.5f - g * 16f;
                Vector2 onEdge = PointOn(edge, Mathf.Min(Corner, edge.width * 0.5f, edge.height * 0.5f), Mod(at, length));
                // flying out from the middle as it appears
                float e = BackOut(appear);
                img.rectTransform.anchoredPosition = Vector2.LerpUnclamped(edge.center, onEdge, e);
                float size = Mathf.Pow(0.8f, g) * (1f + kick * 0.6f) * Mathf.Clamp01(e);
                img.rectTransform.localScale = Vector3.one * size;
                img.color = new Color(1f, 1f, 1f, Mathf.Pow(0.55f, g) * Mathf.Clamp01(appear * 1.5f));
                if (star.Length > 0) img.sprite = star[g == 0 ? frame : (frame + g) % star.Length];
            }
        }
    }

    private static float Mod(float a, float b) => ((a % b) + b) % b;

    private static float Perimeter(Rect r)
    {
        float c = Mathf.Min(Corner, r.width * 0.5f, r.height * 0.5f);
        return 2f * (r.width + r.height) - 8f * c + 2f * Mathf.PI * c;
    }

    // the point `s` along a rect's edge with rounded corners, clockwise from the top left
    private static Vector2 PointOn(Rect r, float c, float s)
    {
        float w = r.width - 2f * c, h = r.height - 2f * c, arc = Mathf.PI * 0.5f * c;
        if (s < w) return new Vector2(r.xMin + c + s, r.yMax);
        s -= w;
        if (s < arc) return Arc(new Vector2(r.xMax - c, r.yMax - c), c, 90f - s / arc * 90f);
        s -= arc;
        if (s < h) return new Vector2(r.xMax, r.yMax - c - s);
        s -= h;
        if (s < arc) return Arc(new Vector2(r.xMax - c, r.yMin + c), c, -s / arc * 90f);
        s -= arc;
        if (s < w) return new Vector2(r.xMax - c - s, r.yMin);
        s -= w;
        if (s < arc) return Arc(new Vector2(r.xMin + c, r.yMin + c), c, -90f - s / arc * 90f);
        s -= arc;
        if (s < h) return new Vector2(r.xMin, r.yMin + c + s);
        s -= h;
        return Arc(new Vector2(r.xMin + c, r.yMax - c), c, 180f - Mathf.Min(s, arc) / arc * 90f);
    }

    private static Vector2 Arc(Vector2 centre, float radius, float degrees)
    {
        float a = degrees * Mathf.Deg2Rad;
        return centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
    }

    // ---------------------------------------------------------------- the click

    private void Burst(Vector2 at)
    {
        if (burst.Length == 0) return;
        Image img = null;
        for (int i = 0; i < bursts.Count; i++)
            if (!bursts[i].image.enabled) { img = bursts[i].image; bursts.RemoveAt(i); break; }
        if (img == null) img = Pixel("Click", burst[0], BurstScale, 40f);
        img.enabled = true;
        img.sprite = burst[0];
        img.rectTransform.anchoredPosition = at;
        img.transform.SetAsLastSibling();
        bursts.Add((img, 0f));
        if (target != null) kick = 1f;
    }

    private void Bursts(float dt)
    {
        for (int i = 0; i < bursts.Count; i++)
        {
            var (img, age) = bursts[i];
            if (!img.enabled) continue;
            age += dt;
            int f = (int)(age * 30f);
            if (f >= burst.Length) { img.enabled = false; bursts[i] = (img, age); continue; }
            img.sprite = burst[f];
            bursts[i] = (img, age);
        }
    }

    private static float BackOut(float t)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        t = Mathf.Clamp01(t) - 1f;
        return 1f + c3 * t * t * t + c1 * t * t;
    }
}
