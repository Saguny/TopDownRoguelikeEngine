using TMPro;
using UnityEngine;

// the HUD's words and numbers (the run clock, the wave, the kills, the coins), alive. the outline
// is drawn the pixel-font way: copies of the text laid behind it, a step out in eight directions
// (the game's font is drawn with TMP's Bitmap shader, which has no outline of its own), plus a
// drop shadow under them. the outline is deep ink with a violet rim light slowly turning round
// the letters, and every few seconds a gold glint runs across the letters themselves, each text on
// its own beat. the run clock while it's stopped (Frozen: from a Final Rush won until the next
// wave, and through the final boss) wears an outline of ice blue and red chasing each other round
// it instead, a glow of the two behind that, and the colours rippling through its digits. added
// by UIWaveAndTimer and RunCounterText to their texts
[RequireComponent(typeof(TMP_Text))]
public class HudTextFx : MonoBehaviour
{
    public bool Frozen { get; set; }

    private static readonly Color Ink = new Color(0.09f, 0.04f, 0.13f, 1f);
    private static readonly Color Rim = new Color(0.45f, 0.25f, 0.7f, 1f);
    private static readonly Color Shadow = new Color(0.03f, 0.01f, 0.06f, 0.6f);
    private static readonly Color Glint = new Color(1f, 0.86f, 0.42f, 1f);
    private static readonly Color Ice = new Color(0.35f, 0.72f, 1f, 1f);
    private static readonly Color Ember = new Color(1f, 0.24f, 0.3f, 1f);

    private const float GlintEvery = 4.5f;      // seconds between glints
    private const float GlintSweep = 0.7f;      // seconds a glint takes to cross the text
    private const int Around = 8;               // outline copies, one a direction

    private TMP_Text text;
    private RectTransform layer;                 // behind the text: the shadow, the glow, the outline
    private TMP_Text shadow;
    private readonly TMP_Text[] outline = new TMP_Text[Around];
    private readonly TMP_Text[] glow = new TMP_Text[Around];
    private readonly Vector2[] dirs = new Vector2[Around];
    private string shown;
    private float phase;

    // adds it to a text if it hasn't got one, and hands it back
    public static HudTextFx On(TMP_Text t)
    {
        if (t == null) return null;
        return t.TryGetComponent(out HudTextFx fx) ? fx : t.gameObject.AddComponent<HudTextFx>();
    }

    private void Awake()
    {
        text = GetComponent<TMP_Text>();
        phase = Random.value * GlintEvery;
        for (int i = 0; i < Around; i++)
        {
            float a = i * Mathf.PI * 2f / Around;
            dirs[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            // the diagonals a full step out on both axes, so the outline is square like the pixels
            if (i % 2 == 1) dirs[i] = new Vector2(Mathf.Sign(dirs[i].x), Mathf.Sign(dirs[i].y));
        }
    }

    private void OnEnable()
    {
        if (layer != null) layer.gameObject.SetActive(true);
    }

    private void OnDisable()
    {
        if (layer != null) layer.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (layer != null) Destroy(layer.gameObject);
    }

    // the copies sit in a sibling just before the text, so they draw under it
    private void Build()
    {
        var parent = text.rectTransform.parent;
        if (parent == null) return;
        layer = new GameObject(name + " Outline", typeof(RectTransform)).GetComponent<RectTransform>();
        layer.SetParent(parent, false);
        layer.SetSiblingIndex(text.rectTransform.GetSiblingIndex());
        // a layout group round the HUD mustn't make room for it
        layer.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().ignoreLayout = true;
        shadow = Copy("Shadow");
        for (int i = 0; i < Around; i++) glow[i] = Copy("Glow " + i);
        for (int i = 0; i < Around; i++) outline[i] = Copy("Outline " + i);
    }

    private TMP_Text Copy(string label)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(layer, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        var t = go.GetComponent<TextMeshProUGUI>();
        t.raycastTarget = false;
        return t;
    }

    private void LateUpdate()
    {
        if (text == null || !text.isActiveAndEnabled) return;
        if (layer == null) Build();
        if (layer == null) return;

        Follow();
        Restyle();

        float t = Time.unscaledTime;
        float step = Mathf.Max(1f, text.fontSize * 0.06f);
        bool frozen = Frozen;
        float spin = t * (frozen ? 5f : 1.1f) + phase;
        Vector2 rimDir = new Vector2(Mathf.Cos(spin), Mathf.Sin(spin));

        shadow.rectTransform.anchoredPosition = new Vector2(step * 1.6f, -step * 1.6f);
        shadow.color = new Color(Shadow.r, Shadow.g, Shadow.b, Shadow.a * text.alpha);

        for (int i = 0; i < Around; i++)
        {
            var o = outline[i];
            o.rectTransform.anchoredPosition = dirs[i] * step;
            Color c;
            if (frozen)
            {
                // ice and ember chasing each other round the letters
                float k = 0.5f + 0.5f * Mathf.Sin(spin + i * Mathf.PI * 2f / Around);
                c = Color.Lerp(Ice, Ember, k);
            }
            else
            {
                // deep ink, lit violet on the side the rim light has turned to
                float lit = Mathf.Clamp01(Vector2.Dot(dirs[i].normalized, rimDir));
                c = Color.Lerp(Ink, Rim, lit * lit * 0.8f);
            }
            c.a = text.alpha;
            o.color = c;

            var g = glow[i];
            g.enabled = frozen;
            if (!frozen) continue;
            g.rectTransform.anchoredPosition = dirs[i] * step * 2.2f;
            float kg = 0.5f + 0.5f * Mathf.Sin(spin + Mathf.PI + i * Mathf.PI * 2f / Around);
            var gc = Color.Lerp(Ice, Ember, kg);
            gc.a = 0.35f * text.alpha;
            g.color = gc;
        }

        Letters(t);
    }

    // the layer keeps to the text: where it is, its size and any swell it's given
    private void Follow()
    {
        var src = text.rectTransform;
        layer.anchorMin = src.anchorMin;
        layer.anchorMax = src.anchorMax;
        layer.pivot = src.pivot;
        layer.anchoredPosition3D = src.anchoredPosition3D;
        layer.sizeDelta = src.sizeDelta;
        layer.localRotation = src.localRotation;
        layer.localScale = src.localScale;
        // just under the text in its parent, so it draws first
        int mine = layer.GetSiblingIndex(), its = src.GetSiblingIndex();
        if (mine != its - 1) layer.SetSiblingIndex(mine < its ? its - 1 : its);
    }

    // the copies wear the text's words, font and layout; rewritten only when they change
    private void Restyle()
    {
        string now = text.text;
        bool changed = now != shown;
        shown = now;
        Match(shadow, changed);
        for (int i = 0; i < Around; i++)
        {
            Match(outline[i], changed);
            Match(glow[i], changed);
        }
    }

    private void Match(TMP_Text c, bool changed)
    {
        if (c.font != text.font) c.font = text.font;
        if (c.fontSharedMaterial != text.fontSharedMaterial) c.fontSharedMaterial = text.fontSharedMaterial;
        if (c.enableAutoSizing) c.enableAutoSizing = false;
        if (c.fontSize != text.fontSize) c.fontSize = text.fontSize;
        if (c.fontStyle != text.fontStyle) c.fontStyle = text.fontStyle;
        if (c.alignment != text.alignment) c.alignment = text.alignment;
        if (c.characterSpacing != text.characterSpacing) c.characterSpacing = text.characterSpacing;
        if (c.lineSpacing != text.lineSpacing) c.lineSpacing = text.lineSpacing;
        if (c.textWrappingMode != text.textWrappingMode) c.textWrappingMode = text.textWrappingMode;
        if (c.overflowMode != text.overflowMode) c.overflowMode = text.overflowMode;
        if (c.margin != text.margin) c.margin = text.margin;
        if (c.richText != text.richText) c.richText = text.richText;
        if (changed) c.text = shown;
    }

    // each letter's colour: the gold glint sweeping across, or the blue and red rippling through
    private void Letters(float t)
    {
        text.ForceMeshUpdate();
        var info = text.textInfo;
        if (info == null || info.characterCount == 0) return;

        Color32 baseColor = text.color;
        int count = info.characterCount;
        float cycle = (t + phase) % GlintEvery;
        float sweep = cycle < GlintSweep ? cycle / GlintSweep * (count + 3f) - 1.5f : -99f;

        for (int i = 0; i < count; i++)
        {
            var ch = info.characterInfo[i];
            if (!ch.isVisible) continue;
            Color c = baseColor;
            if (Frozen)
            {
                float w = 0.5f + 0.5f * Mathf.Sin(t * 6f - i * 0.9f);
                c = Color.Lerp(c, Color.Lerp(Ice, Ember, w), 0.35f);
            }
            else
            {
                float g = Mathf.Clamp01(1f - Mathf.Abs(i - sweep) / 1.4f);
                if (g > 0f) c = Color.Lerp(c, Glint, g * 0.85f);
            }
            c.a = baseColor.a / 255f;
            Color32 c32 = c;
            var colors = info.meshInfo[ch.materialReferenceIndex].colors32;
            int v = ch.vertexIndex;
            colors[v] = colors[v + 1] = colors[v + 2] = colors[v + 3] = c32;
        }
        text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
    }
}
