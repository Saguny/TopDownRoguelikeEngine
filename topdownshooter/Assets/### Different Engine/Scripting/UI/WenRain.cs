using UnityEngine;
using UnityEngine.UI;

// Vampire Survivors' level up: wen pouring down behind the upgrade cards, turning over as they
// fall, each starting at the size wen has on the ground and shrinking as it drops, slowing and
// drifting toward the middle and into the dark as if falling away from the screen. it lives on
// the level up panel, so it starts and stops with the menu, and draws through a WenRainLayer it
// puts just over the panel's dark backdrop: every coin in one UI mesh. unscaled time, since the
// game is paused under it. its settings apply live, the count too
[DisallowMultipleComponent]
public class WenRain : MonoBehaviour
{
    private const int MostCoins = 16000;   // a UI mesh holds 65k vertices

    [SerializeField, Min(1)] private int count = 260;
    [Tooltip("a coin's size at the top, 1 being the size of wen on the ground")]
    [SerializeField, Min(0.1f)] private float startScale = 1f;
    [Tooltip("its size by the time it reaches the bottom")]
    [SerializeField, Range(0.05f, 1f)] private float endScale = 0.3f;
    [Tooltip("how fast a coin falls at the top, in screen heights per second; it slows as it shrinks")]
    [SerializeField] private Vector2 fallSpeed = new Vector2(0.75f, 1.15f);
    [Tooltip("how far toward the middle a coin drifts by the bottom")]
    [SerializeField, Range(0f, 1f)] private float converge = 0.22f;
    [SerializeField, Range(0f, 1f)] private float jadeShare = 0.15f;
    [Tooltip("turns a second, a coin's frames per second")]
    [SerializeField] private Vector2 spinFps = new Vector2(9f, 16f);
    [Tooltip("the colour a coin falls into")]
    [SerializeField] private Color depthTint = new Color(0.42f, 0.36f, 0.55f, 1f);
    [SerializeField, Min(0f)] private float fadeIn = 0.25f;

    private struct Coin
    {
        public float x, fallen, speed, phase, fps, sway, swayPhase;
        public bool jade;
    }

    private Coin[] coins = new Coin[0];
    private float[] depth = new float[0];
    private int[] order = new int[0];
    private Sprite[] bronze, jade;
    private float clock;
    private WenRainLayer layer;

    public Texture Texture => bronze != null && bronze.Length > 0 && bronze[0] != null ? bronze[0].texture : null;

    // the level up panel's rain, unless it has one already
    public static WenRain AddTo(GameObject panel)
    {
        if (panel == null) return null;
        var rain = panel.GetComponentInChildren<WenRain>(true);
        if (rain != null) return rain;
        var lib = VfxLibrary.Get;
        if (lib == null || lib.wenSpinBronze == null || lib.wenSpinBronze.Length == 0) return null;
        return panel.AddComponent<WenRain>();
    }

    private void Awake()
    {
        var lib = VfxLibrary.Get;
        if (lib == null) return;
        bronze = lib.wenSpinBronze;
        // jade only if it's cut from the same texture, so the rain stays one mesh
        jade = lib.wenSpinJade != null && lib.wenSpinJade.Length > 0 && lib.wenSpinJade[0] != null &&
               bronze != null && bronze.Length > 0 && lib.wenSpinJade[0].texture == bronze[0].texture ? lib.wenSpinJade : null;
    }

    private void OnEnable()
    {
        clock = 0f;
        coins = new Coin[0];
        Resize(count);
        EnsureLayer();
        if (layer != null) layer.enabled = true;
    }

    private void OnDisable()
    {
        if (layer != null) layer.enabled = false;
    }

    // the layer goes on this object just after its leading full-screen images (the level up
    // panel's dark backdrop), so the rain shows over the dimmed game and under everything else
    private void EnsureLayer()
    {
        if (layer != null) return;
        int after = 0;
        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i) as RectTransform;
            if (child == null || !child.TryGetComponent(out Image _) || child.GetComponentInChildren<Selectable>(true) != null) break;
            if (child.anchorMin != Vector2.zero || child.anchorMax != Vector2.one) break;
            after = i + 1;
        }

        var go = new GameObject("Wen Rain", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(transform, false);
        rt.SetSiblingIndex(after);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        layer = go.AddComponent<WenRainLayer>();
        layer.rain = this;
        layer.raycastTarget = false;
        layer.SetMaterialDirty();   // its texture is the coins', now it knows them
    }

    // to this many coins, keeping the ones falling; new ones queue up above the top like the first
    private void Resize(int n)
    {
        n = Mathf.Clamp(n, 1, MostCoins);
        int had = coins.Length;
        if (n == had) return;
        System.Array.Resize(ref coins, n);
        depth = new float[n];
        order = new int[n];
        // queued above the top as deep as one whole fall, so they pour in from the first frame on
        // and the first ones back round arrive as the last of the queue does: no gap in the stream
        for (int i = had; i < n; i++) coins[i] = NewCoin(-Random.value * Cycle);
    }

    private const float RespawnJitter = 0.1f;

    // one coin's fall from the top to the bottom and back round, as the distance it covers queued
    // at full speed in that time. it slows as it shrinks, so this is more than a screen
    private float Cycle =>
        (endScale >= 0.999f ? 1f : Mathf.Log(1f / endScale) / (1f - endScale)) + 0.05f / endScale + RespawnJitter * 0.5f;

    private Coin NewCoin(float fallen) => new Coin
    {
        x = Random.value,
        fallen = fallen,
        speed = Random.Range(fallSpeed.x, fallSpeed.y),
        phase = Random.value * 8f,
        fps = Random.Range(spinFps.x, spinFps.y) * (Random.value < 0.5f ? -1f : 1f),
        sway = Random.Range(0f, 0.012f),
        swayPhase = Random.value * 6.3f,
        jade = jade != null && Random.value < jadeShare,
    };

    private void Update()
    {
        Resize(count);
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        clock += dt;
        for (int i = 0; i < coins.Length; i++)
        {
            ref var c = ref coins[i];
            // slower as it shrinks: the farther away, the less it seems to move
            c.fallen += dt * c.speed * (c.fallen <= 0f ? 1f : ScaleAt(c.fallen));
            c.phase += dt * c.fps;
            if (c.fallen > 1.05f) c = NewCoin(c.fallen - 1.05f - Random.value * RespawnJitter);
        }
        if (layer != null) layer.SetVerticesDirty();
    }

    private float ScaleAt(float fallen) => Mathf.Lerp(1f, endScale, Mathf.Clamp01(fallen));

    // every coin as a quad in the layer's rect, the farthest first
    public void Fill(VertexHelper vh, Rect rect, Canvas canvas, Color color)
    {
        vh.Clear();
        var tex = Texture;
        if (tex == null) return;

        var canvasScale = canvas != null ? canvas.scaleFactor : 1f;
        // one art pixel on screen: as big as on the playfield, rounded to whole screen pixels
        var cam = Camera.main;
        float onGround = cam != null && cam.orthographic && cam.orthographicSize > 0f
            ? Screen.height / (2f * cam.orthographicSize * bronze[0].pixelsPerUnit) : 2f;
        float artPixel = Mathf.Max(1f, Mathf.Round(onGround)) * startScale / Mathf.Max(0.01f, canvasScale);
        float snap = Mathf.Max(0.01f, canvasScale);

        var texSize = new Vector2(tex.width, tex.height);
        float alpha = fadeIn > 0f ? Mathf.Clamp01(clock / fadeIn) : 1f;
        float cx = rect.center.x;
        var vert = UIVertex.simpleVert;

        // the farthest first, so nearer coins fall in front of them
        for (int i = 0; i < coins.Length; i++)
        {
            order[i] = i;
            depth[i] = -coins[i].fallen;
        }
        System.Array.Sort(depth, order);

        for (int k = 0; k < coins.Length; k++)
        {
            var c = coins[order[k]];
            if (c.fallen <= -0.1f) continue;   // still queued above the top
            var frames = c.jade ? jade : bronze;
            int f = ((int)Mathf.Floor(c.phase) % frames.Length + frames.Length) % frames.Length;
            var s = frames[f];
            if (s == null) continue;

            float t = Mathf.Clamp01(c.fallen), scale = ScaleAt(c.fallen) * artPixel;
            float x = rect.xMin + (c.x + Mathf.Sin(clock * 3f + c.swayPhase) * c.sway) * rect.width;
            x = cx + (x - cx) * (1f - converge * t);
            // from just above the top to just below the bottom
            float margin = s.rect.height * artPixel;
            float y = Mathf.LerpUnclamped(rect.yMax + margin, rect.yMin - margin * endScale, c.fallen);
            x = Mathf.Round(x * snap) / snap;
            y = Mathf.Round(y * snap) / snap;

            Vector2 a = -s.pivot * scale, b = (s.rect.size - s.pivot) * scale;
            Vector2 u0 = s.rect.min / texSize, u1 = s.rect.max / texSize;
            var tint = Color.Lerp(Color.white, depthTint, t * 0.85f);
            tint.a = alpha * Mathf.Clamp01((1.05f - c.fallen) * 6f);
            vert.color = tint * color;

            int o = vh.currentVertCount;
            vert.position = new Vector3(x + a.x, y + a.y); vert.uv0 = new Vector2(u0.x, u0.y); vh.AddVert(vert);
            vert.position = new Vector3(x + a.x, y + b.y); vert.uv0 = new Vector2(u0.x, u1.y); vh.AddVert(vert);
            vert.position = new Vector3(x + b.x, y + b.y); vert.uv0 = new Vector2(u1.x, u1.y); vh.AddVert(vert);
            vert.position = new Vector3(x + b.x, y + a.y); vert.uv0 = new Vector2(u1.x, u0.y); vh.AddVert(vert);
            vh.AddTriangle(o, o + 1, o + 2);
            vh.AddTriangle(o, o + 2, o + 3);
        }
    }
}
