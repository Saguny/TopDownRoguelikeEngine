using UnityEngine;
using UnityEngine.UI;

// a scene opening out of black: the dark is wound up and away from the middle of the screen in a
// spiral, in chunky pixels like the rest of the game. Close runs it backwards, the dark winding in
// from the corners until the screen is black, and stays black until it's destroyed (SceneLoader
// covers it with the loading screen). a full-screen overlay on its own canvas above everything,
// drawn from a small texture that's recut each frame. unscaled time, so it runs whatever the
// game's time is doing
public class SpiralReveal : MonoBehaviour
{
    private const int Height = 90;            // the black's pixel grid, rows on screen

    [Min(0.1f)] public float duration = 1.25f;
    [Tooltip("how many arms the spiral has")]
    [Min(1)] public int arms = 3;
    [Tooltip("how far each arm curls between the middle and the corners, in turns")]
    public float twist = 1f;
    [Tooltip("the edge of the black, a shade of the outline colour")]
    public Color edge = new Color(0.10f, 0.05f, 0.15f, 1f);

    private float[] reveal;                   // when each pixel clears, 0 to 1
    private Color32[] pixels;
    private Texture2D texture;
    private RawImage image;
    private float clock;
    private bool closing;

    // closing: the screen is black now
    public bool Done { get; private set; }

    // black at once, cleared by the spiral from the next frame on
    public static SpiralReveal Play(float duration = 1.25f) => Make(duration, false);

    // clear at first, then black winding in to the middle; it stays black when Done
    public static SpiralReveal Close(float duration = 0.7f) => Make(duration, true);

    private static SpiralReveal Make(float duration, bool closing)
    {
        var go = new GameObject("Spiral Reveal", typeof(RectTransform));
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32001;          // over the scene loader's fade
        var reveal = go.AddComponent<SpiralReveal>();
        reveal.duration = duration;
        reveal.closing = closing;
        reveal.Paint(closing ? 1f : 0f);
        return reveal;
    }

    private void Awake()
    {
        int h = Height, w = Mathf.Max(1, Mathf.RoundToInt(Height * (float)Screen.width / Mathf.Max(1, Screen.height)));
        texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Spiral Reveal" };
        pixels = new Color32[w * h];
        reveal = new float[w * h];

        // curled arms grow out of the middle and widen until they meet: a pixel clears when an arm's
        // tip has reached it and the arm has grown wide enough to cover it. every edge is a curve
        // of the spiral, with no seam where it comes round
        float cx = (w - 1) * 0.5f, cy = (h - 1) * 0.5f, far = Mathf.Sqrt(cx * cx + cy * cy);
        float most = 0f;
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float dx = x - cx, dy = y - cy;
            float r = Mathf.Sqrt(dx * dx + dy * dy) / far;
            float a = Mathf.Atan2(dy, dx) / (Mathf.PI * 2f);
            float along = Mathf.Repeat(arms * a + twist * r, 1f);
            float off = Mathf.Abs(along - 0.5f) * 2f;               // 0 down an arm's middle, 1 between two
            float k = Mathf.Max(r / 1.25f, Mathf.Max(0f, off - 0.2f) * 1.15f);
            reveal[y * w + x] = k;
            most = Mathf.Max(most, k);
        }
        // an ordered dither along the fronts, so they fray rather than cutting clean lines
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
            reveal[y * w + x] = Mathf.Clamp01(reveal[y * w + x] / Mathf.Max(0.01f, most) + (Bayer(x, y) - 0.5f) * 0.035f);

        var rt = (RectTransform)transform;
        var child = new GameObject("Black", typeof(RectTransform));
        var crt = (RectTransform)child.transform;
        crt.SetParent(rt, false);
        crt.anchorMin = Vector2.zero;
        crt.anchorMax = Vector2.one;
        crt.offsetMin = crt.offsetMax = Vector2.zero;
        image = child.AddComponent<RawImage>();
        image.texture = texture;
        image.raycastTarget = true;           // the menu can't be clicked through the black
        Paint(0f);
    }

    private void Update()
    {
        if (Done) return;
        clock += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
        float t = Mathf.Clamp01(clock / duration);
        if (closing)
        {
            Paint(Mathf.Lerp(1f, -0.05f, t * t));    // in from the corners, quickening to the middle
            Done = t >= 1f;
            return;
        }
        Paint(1f - (1f - t) * (1f - t));      // quick out of the middle, easing into the corners
        if (t >= 1f) Destroy(gameObject);
    }

    private void Paint(float progress)
    {
        var black = new Color32(0, 0, 0, 255);
        var rim = (Color32)edge;
        var clear = new Color32(0, 0, 0, 0);
        for (int i = 0; i < reveal.Length; i++)
        {
            float k = reveal[i] - progress;
            pixels[i] = k <= 0f ? clear : k < 0.012f ? rim : black;
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        if (image != null) image.raycastTarget = progress < 0.5f;
    }

    private void OnDestroy()
    {
        if (texture != null) Destroy(texture);
    }

    private static readonly int[] Matrix = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
    private static float Bayer(int x, int y) => (Matrix[(y & 3) * 4 + (x & 3)] + 0.5f) / 16f;
}
