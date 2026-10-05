using UnityEngine;
using UnityEngine.UI;

// a rainbow running round the edge of a card: a favoured item's level up card (RunCredits). a
// band of chunky pixels just outside the card and over its rim, every hue in turn along the
// perimeter, the whole band turning. drawn into a small texture, point filtered, and recoloured
// a few times a second on real time (the game is stopped behind the level up). Set turns it on or
// off for a card; it makes itself the first time
public class RainbowFrame : MonoBehaviour
{
    private const float Pixel = 4f;        // canvas units per art pixel
    private const int Band = 2;            // art pixels thick
    private const int Out = 1;             // of which outside the card's edge
    private const float Turn = 0.6f;       // turns of the rainbow round the card a second
    private const float Step = 1f / 20f;   // seconds between recolours

    private RawImage image;
    private Texture2D tex;
    private Color32[] px;
    private int w, h;
    private float next;

    public static void Set(GameObject card, bool on)
    {
        if (card == null) return;
        var frame = card.GetComponent<RainbowFrame>();
        if (!on) { if (frame != null) frame.Show(false); return; }
        if (frame == null) frame = card.AddComponent<RainbowFrame>();
        frame.Show(true);
    }

    private void Show(bool on)
    {
        if (on && image == null) Make();
        if (image != null) image.gameObject.SetActive(on);
        enabled = on;
        next = 0f;
    }

    private void Make()
    {
        var go = new GameObject("Rainbow Frame", typeof(RectTransform), typeof(RawImage), typeof(LayoutElement));
        var rt = (RectTransform)go.transform;
        rt.SetParent(transform, false);
        rt.SetAsLastSibling();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-Out * Pixel, -Out * Pixel);
        rt.offsetMax = new Vector2(Out * Pixel, Out * Pixel);
        go.GetComponent<LayoutElement>().ignoreLayout = true;
        image = go.GetComponent<RawImage>();
        image.raycastTarget = false;
    }

    private void OnDestroy()
    {
        if (tex != null) Destroy(tex);
    }

    private void Update()
    {
        if (image == null || Time.unscaledTime < next) return;
        next = Time.unscaledTime + Step;

        // a texture the frame's size in art pixels, remade if the card changed size
        var size = ((RectTransform)image.transform).rect.size;
        int tw = Mathf.Max(4, Mathf.RoundToInt(size.x / Pixel)), th = Mathf.Max(4, Mathf.RoundToInt(size.y / Pixel));
        if (tex == null || tw != w || th != h)
        {
            if (tex != null) Destroy(tex);
            w = tw; h = th;
            tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Rainbow Frame" };
            px = new Color32[w * h];
            image.texture = tex;
        }

        // each band pixel's hue is how far round the card it is, plus the turning
        float spin = Time.unscaledTime * Turn;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int edge = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y));
                if (edge >= Band) { px[y * w + x] = default; continue; }
                float round = Mathf.Atan2((y + 0.5f - h * 0.5f) / h, (x + 0.5f - w * 0.5f) / w) / (2f * Mathf.PI);
                float hue = Mathf.Repeat(round - spin, 1f);
                // in steps, like a palette, and a shade darker on the inner pixel
                hue = Mathf.Floor(hue * 12f) / 12f;
                var c = Color.HSVToRGB(hue, 0.85f, edge == 0 ? 1f : 0.8f);
                px[y * w + x] = c;
            }
        tex.SetPixels32(px);
        tex.Apply(false);
    }
}
