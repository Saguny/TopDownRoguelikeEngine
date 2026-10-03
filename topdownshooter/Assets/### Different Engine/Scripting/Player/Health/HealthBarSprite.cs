using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif


[ExecuteAlways]
public class HealthBarSprite : MonoBehaviour
{
    
    [Header("Bind")]
    [Tooltip("Transform to follow (usually the character root).")]
    public Transform target;

    [Tooltip("Component that implements IHealth (left empty = auto-find on target or its parents).")]
    public MonoBehaviour healthSource;

    private IHealth _health;         // current health source
    private IHealth _subscribed;     // track current subscription to avoid duplicate handlers

    
    [Header("Visual")]
    [Tooltip("1x1 white sprite (optional, auto-generated if left empty).")]
    public Sprite barSprite;

    [Tooltip("Sorting layer name for the bar renderers.")]
    public string sortingLayer = "Default";

    [Tooltip("Background render order.")]
    public int bgOrder = 199;

    [Tooltip("Foreground render order.")]
    public int fgOrder = 200;

    [Tooltip("World size of the foreground at full health (width, height).")]
    public Vector2 size = new Vector2(2.0f, 0.18f);

    [Tooltip("World offset from target position.")]
    public Vector3 offset = new Vector3(0f, 0.8f, 0f);

    [Tooltip("Extra padding around background (adds to size).")]
    public float bgPadding = 0.06f;

    [Tooltip("Snap position to a pixel grid (0 = off). For pixel art, use 1/PPU.")]
    public float pixelSnap = 0f;

   
    [Header("Color")]
    public Color fillColor = Color.red;
    public Color backColor = Color.black;
    [Tooltip("hidden while at full health")]
    public bool hideWhenFull = true;
    [Tooltip("hiding when full: seconds it stays up after health is topped back up, then how long it takes to fade")]
    [Min(0f)] public float fullLinger = 1.2f;
    [Min(0.01f)] public float fullFade = 0.35f;
    [Tooltip("the fill shrinks in whole steps of this size, e.g. one pixel of the art. 0 = smoothly")]
    public float pixelWidth = 0f;

    
    private Transform _bgT, _fgT;
    private SpriteRenderer _bg, _fg;

    private bool _needsLayout;   // width/height/padding changes
    private bool _needsValue;    // hp percent/color changes

    // ---------- Unity ----------
    private void Awake()
    {
        if (!enabled) enabled = true; // ensure component is on
        BuildOnce();                  // create child renderers (no size writes)
        CacheHealth();                // find/assign IHealth
        Subscribe(_health);           // listen for changes
        MarkAllDirty();               // defer layout/value until LateUpdate
        RequestEditorRefresh();
    }

    private void OnEnable()
    {
        CacheHealth();
        Subscribe(_health);
        MarkAllDirty();
        RequestEditorRefresh();
    }

    private void OnDisable() => Unsubscribe();
    private void OnDestroy() => Unsubscribe();

    private void OnValidate()
    {
        BuildOnce();
        CacheHealth();
        Subscribe(_health);
        MarkAllDirty();
        RequestEditorRefresh();
    }

    private void LateUpdate()
    {
        // follow target safely in LateUpdate
        if (target != null)
        {
            var pos = target.position + offset;
            if (pixelSnap > 0f)
            {
                pos = new Vector3(
                    Mathf.Round(pos.x / pixelSnap) * pixelSnap,
                    Mathf.Round(pos.y / pixelSnap) * pixelSnap,
                    pos.z
                );
            }
            transform.position = pos;

            // the bar keeps its own shape: a flip or squash of whatever it hangs under doesn't reach it
            var parent = transform.parent;
            if (parent != null)
            {
                var ls = parent.lossyScale;
                transform.localScale = new Vector3(Inverse(ls.x), Inverse(ls.y), 1f);
            }
        }

        // apply deferred ops (safe time to touch SpriteRenderer.size)
        if (_needsLayout) { ApplyLayout(); _needsLayout = false; }
        if (_needsValue) { ApplyValue(); _needsValue = false; }
        else { ApplyValue(); } // ensure live updates even without events
    }

    
    public void Bind(Transform follow, IHealth src)
    {
        target = follow;
        healthSource = src as MonoBehaviour;
        _health = src;
        Subscribe(_health);
        MarkAllDirty();
        RequestEditorRefresh();
    }

    
    private void BuildOnce()
    {
        if (_bg == null)
        {
            var bgGO = GetOrCreate("BG");
            _bgT = bgGO.transform;
            _bg = GetOrAdd<SpriteRenderer>(bgGO);
            _bg.drawMode = SpriteDrawMode.Tiled;
            _bg.sortingLayerName = sortingLayer;
            _bg.sortingOrder = bgOrder;
            _bg.color = new Color(0f, 0f, 0f, 0.65f);
        }

        if (_fg == null)
        {
            var fgGO = GetOrCreate("FG");
            _fgT = fgGO.transform;
            _fg = GetOrAdd<SpriteRenderer>(fgGO);
            _fg.drawMode = SpriteDrawMode.Tiled;
            _fg.sortingLayerName = sortingLayer;
            _fg.sortingOrder = fgOrder;
        }

        if (barSprite == null) barSprite = CreateDefaultWhiteSprite();

        if (_bg.sprite != barSprite) _bg.sprite = barSprite;
        if (_fg.sprite != barSprite) _fg.sprite = barSprite;

        // reset transforms (do not touch size here)
        transform.localScale = Vector3.one;
        _bgT.localScale = Vector3.one; _bgT.localPosition = Vector3.zero; _bgT.localRotation = Quaternion.identity;
        _fgT.localScale = Vector3.one; _fgT.localPosition = Vector3.zero; _fgT.localRotation = Quaternion.identity;
    }

    private void CacheHealth()
    {
        _health = null;
        if (healthSource != null) _health = healthSource as IHealth;
        if (_health == null && target != null)
            _health = target.GetComponentInParent<IHealth>();
    }

    
    private void Subscribe(IHealth h)
    {
        if (_subscribed == h) return;
        Unsubscribe();
        _subscribed = h;
        if (_subscribed != null)
            _subscribed.OnHealthChanged += HandleHealthChanged;
    }

    private void Unsubscribe()
    {
        if (_subscribed != null)
            _subscribed.OnHealthChanged -= HandleHealthChanged;
        _subscribed = null;
    }

    private void HandleHealthChanged(float cur, float max)
    {
        _needsValue = true;     // update next LateUpdate
        RequestEditorRefresh();
    }

    
    private void MarkAllDirty()
    {
        _needsLayout = true;
        _needsValue = true;
    }

    private static float Inverse(float v) => Mathf.Abs(v) < 0.0001f ? 1f : 1f / v;

    // width/height/padding (touches SpriteRenderer.size), and where it's drawn: set here rather
    // than when the renderers are made, which is before whoever added the bar has set it up
    private void ApplyLayout()
    {
        if (_bg == null || _fg == null) return;

        _bg.sortingLayerName = _fg.sortingLayerName = sortingLayer;
        _bg.sortingOrder = bgOrder;
        _fg.sortingOrder = fgOrder;

        _bg.size = new Vector2(size.x + bgPadding, size.y + bgPadding);
        // keep initial FG size (full) so ApplyValue can narrow it
        if (_fg.size.y != size.y) _fg.size = new Vector2(_fg.size.x, size.y);

        // position FG so its left edge is fixed
        float w = Mathf.Max(0f, _fg.size.x);
        _fgT.localPosition = new Vector3(-size.x * 0.5f + w * 0.5f, 0f, 0f);
    }

    // hp percent + color (touches SpriteRenderer.size)
    private void ApplyValue()
    {
        if (_fg == null || _bg == null) return;

        float cur = 1f, max = 1f;
        if (_health != null)
        {
            cur = Mathf.Max(0f, _health.Current);
            max = Mathf.Max(1f, _health.Max);
        }
        float t = Mathf.Clamp01(cur / max);

        float w = Mathf.Max(0f, size.x * t);
        // in whole pixels, rounded up so a sliver of health still shows one
        if (pixelWidth > 0f) w = Mathf.Min(size.x, Mathf.Ceil(w / pixelWidth - 0.001f) * pixelWidth);
        if (_fg.size.x != w) _fg.size = new Vector2(w, size.y);

        // keep left edge fixed after width change
        _fgT.localPosition = new Vector3(-size.x * 0.5f + w * 0.5f, 0f, 0f);

        // hiding when full: up the moment health drops, and once it's full again it waits a
        // moment, then fades out (in edit mode it simply shows or hides)
        float alpha = 1f;
        if (hideWhenFull)
        {
            bool full = t >= 0.999f;
            if (!Application.isPlaying) alpha = full ? 0f : 1f;
            else
            {
                float now = Time.unscaledTime;
                if (!full) _fullSince = -1f;
                else if (_fullSince < 0f) _fullSince = now;
                alpha = _fullSince < 0f ? 1f : 1f - Mathf.Clamp01((now - _fullSince - fullLinger) / fullFade);
                if (!_everHurt && full) alpha = 0f;      // never shown before the first hit
                if (!full) _everHurt = true;
            }
        }

        _fg.color = new Color(fillColor.r, fillColor.g, fillColor.b, fillColor.a * alpha);
        _bg.color = new Color(backColor.r, backColor.g, backColor.b, backColor.a * alpha);
        _bg.enabled = alpha > 0f;
        _fg.enabled = alpha > 0f && w > 0f;
    }

    private float _fullSince = -1f;
    private bool _everHurt;

    
    private GameObject GetOrCreate(string child)
    {
        var t = transform.Find(child);
        if (t != null) return t.gameObject;
        var go = new GameObject(child);
        go.transform.SetParent(transform, false);
        return go;
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c ? c : go.AddComponent<T>();
    }

    private static Sprite CreateDefaultWhiteSprite()
    {
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }

#if UNITY_EDITOR
    private void RequestEditorRefresh()
    {
        if (!Application.isPlaying)
            EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                // simulate LateUpdate part safely in editor
                if (_needsLayout) { ApplyLayout(); _needsLayout = false; }
                if (_needsValue) { ApplyValue(); _needsValue = false; }
                SceneView.RepaintAll();
            };
    }
#else
    private void RequestEditorRefresh() { }
#endif
}
