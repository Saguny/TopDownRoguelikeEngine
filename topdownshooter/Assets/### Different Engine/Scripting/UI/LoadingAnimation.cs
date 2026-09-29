using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// the loading screen, animated with the weapons' own art: seven star swords fly in and wheel round
// a spinning jade wen inside the electric aura's ring, trailing afterimages; the Northern Dipper
// lights up underneath star by star as the progress; peach talismans drift up, and a meteor and a
// volley of arrows cross far behind. it plays whenever the loading panel it's on is up (the
// opening, loading a run). the opening ends it with Finale: the swords spiral into the coin and it
// all fades out to black. unscaled time, capped per frame so a loading hitch doesn't skip it
public class LoadingAnimation : MonoBehaviour
{
    // laid out for a canvas 1080 units tall; u scales it to whatever canvas the panel is on
    private const float Px = 4f;                                   // art pixel, in those units
    private static readonly Vector2 Centre = new Vector2(0f, 130f);
    private static readonly Vector2 LabelAt = new Vector2(0f, -235f);
    private static readonly Vector2 DipperMiddle = new Vector2(0f, -385f);
    private const float LabelSize = 44f;
    private const float Orbit = 235f;
    private const int Swords = 7, Ghosts = 3;
    private const float Turn = Mathf.PI * 2f / 2.4f;               // the wheel: a turn every 2.4 s, clockwise
    private const float Every = 2.6f;                              // meteors and arrows come round this often

    [Tooltip("seconds for the Dipper to light, star by star")]
    [Min(0.1f)] public float fillSeconds = 2f;
    [Tooltip("the opening: solid black under it, not the panel's see-through dark")]
    public bool opaque;

    private VfxLibrary lib;
    private RectTransform root;
    private Image backdrop, coin, ring, meteor, flash;
    private readonly Image[] swords = new Image[Swords];
    private readonly Image[,] ghosts = new Image[Swords, Ghosts];
    private readonly Image[] stars = new Image[7];
    private readonly Image[] links = new Image[7];
    private readonly Image[] talismans = new Image[4];
    private readonly Image[] arrows = new Image[3];
    private TMP_Text label;
    private string labelText;
    private Vector2 labelHome;
    private float labelSize;
    private TextWrappingModes labelWrap;
    private float clock, finaleClock = -1f;
    private float u = 1f;

    private void Measure()
    {
        float h = transform is RectTransform rt ? rt.rect.height : 0f;
        u = h > 1f ? h / 1080f : 1f;
    }

    public float Clock => clock;
    public bool Filled => clock >= fillSeconds;

    // the animation on a loading panel, made the first time
    public static LoadingAnimation On(GameObject panel)
    {
        if (panel == null) return null;
        return panel.TryGetComponent(out LoadingAnimation a) ? a : panel.AddComponent<LoadingAnimation>();
    }

    private void Awake()
    {
        lib = VfxLibrary.Get;
        foreach (var t in GetComponentsInChildren<TMP_Text>(true)) { label = t; break; }
        if (label != null)
        {
            labelText = label.text.TrimEnd('.', ' ', '…');
            labelHome = label.rectTransform.anchoredPosition;
            labelSize = label.fontSize;
            labelWrap = label.textWrappingMode;
        }
        Build();
    }

    private void OnEnable()
    {
        clock = 0f;
        finaleClock = -1f;
        // over every other canvas while it's up, and taking the clicks
        if (!TryGetComponent(out Canvas canvas)) canvas = gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 31000;
        if (!TryGetComponent(out GraphicRaycaster _)) gameObject.AddComponent<GraphicRaycaster>();
        if (root != null) Pose();
    }

    private void OnDisable()
    {
        if (label != null)
        {
            label.rectTransform.anchoredPosition = labelHome;
            label.fontSize = labelSize;
            label.textWrappingMode = labelWrap;
            label.text = labelText + "...";
        }
        if (flash != null) flash.color = Color.clear;
    }

    // the opening's end: the swords close in on the coin as it all fades to black
    public IEnumerator Finale()
    {
        finaleClock = 0f;
        while (finaleClock < FinaleSeconds) yield return null;
    }

    private const float FinaleSeconds = 0.55f;

    private void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
        clock += dt;
        if (finaleClock >= 0f) finaleClock += dt;
        if (root != null) Pose();
    }

    // ---------------------------------------------------------------- the motion

    private void Pose()
    {
        Measure();
        float t = clock;
        if (backdrop != null) backdrop.color = new Color(0f, 0f, 0f, opaque ? 1f : 0f);

        // far behind: talismans drifting up, a meteor and a volley of arrows now and then
        for (int i = 0; i < talismans.Length; i++)
        {
            var im = talismans[i];
            if (im == null) continue;
            float speed = 75f + i * 14f, span = 1300f;
            float y = Mathf.Repeat(-650f + i * 330f + t * speed, span) - 650f;
            float x = new[] { -720f, -430f, 470f, 760f }[i] + Mathf.Sin(t * 1.7f + i * 2.1f) * 18f;
            Frame(im, lib.loadingTalisman, t * 8f + i * 1.5f, 3f);
            im.rectTransform.anchoredPosition = new Vector2(x, y) * u;
            im.rectTransform.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(t * 1.3f + i) * 10f);
            im.color = new Color(0.8f, 0.78f, 0.85f, 0.5f * Appear(t, 0.1f + i * 0.08f));
        }

        if (meteor != null)
        {
            float k = Mathf.Repeat(t - 0.35f, Every) / 0.9f;
            bool flying = t >= 0.35f && k <= 1f;
            meteor.enabled = flying;
            if (flying)
            {
                Vector2 from = new Vector2(1150f, 640f), to = new Vector2(-1150f, -160f);
                Frame(meteor, lib.loadingMeteor, t * 14f, 3f);
                meteor.rectTransform.anchoredPosition = Vector2.Lerp(from, to, k) * u;
                meteor.rectTransform.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg);
                meteor.color = new Color(1f, 1f, 1f, 0.85f);
            }
        }

        for (int i = 0; i < arrows.Length; i++)
        {
            var im = arrows[i];
            if (im == null) continue;
            float k = Mathf.Repeat(t - 1.05f - i * 0.07f, Every) / 0.5f;
            bool flying = t >= 1.05f + i * 0.07f && k <= 1f;
            im.enabled = flying;
            if (!flying) continue;
            Frame(im, lib.loadingArrow, t * 20f + i, 3f);
            im.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-1150f, 1150f, k), 360f + (i - 1) * 34f + (i == 1 ? -12f : 0f)) * u;
            im.color = new Color(1f, 1f, 1f, 0.45f);
        }

        // the ring and the coin at the heart of it
        float end = finaleClock >= 0f ? Mathf.Clamp01(finaleClock / 0.4f) : 0f;
        if (ring != null)
        {
            Frame(ring, lib.loadingRing, t * 16.7f, Px);
            ring.rectTransform.anchoredPosition = Centre * u;
            ring.color = new Color(1f, 1f, 1f, 0.55f * Appear(t, 0.2f) * (1f - end));
        }
        if (coin != null)
        {
            // the wen, the currency, turning over (Resources/UI/wen_coin_spin): the pickups' spin
            // frames are qi now
            var wen = YamaArt.Strip("UI/wen_coin_spin");
            Frame(coin, wen != null && wen.Length > 0 ? wen : lib.wenSpinJade, t * 12f, 8f + end * 4f);
            coin.rectTransform.anchoredPosition = (Centre + new Vector2(0f, Mathf.Sin(t * 2.4f) * 6f)) * u;
            float pop = Appear(t, 0f, 0.35f);
            coin.rectTransform.localScale = Vector3.one * (pop < 1f ? Back(pop) : 1f);
        }

        // the swords: in from off screen, then round and round; at the end, spiralling into the coin
        float inward = end * end;
        for (int i = 0; i < Swords; i++)
        {
            float delay = i * 0.04f;
            float arrive = Appear(t, delay, 0.45f);
            float radius = Orbit * (1f + 0.04f * Mathf.Sin(t * 3f)) * Mathf.LerpUnclamped(2.4f, 1f, Back(arrive)) * (1f - inward);
            float spin = Turn * (1f + end * 2.5f);
            for (int g = Ghosts; g >= 0; g--)
            {
                // the sword itself, then its afterimages where it was a moment ago
                float lag = g * 0.085f * (1f + end);
                float a = Mathf.PI / 2f + i * Mathf.PI * 2f / Swords - (t * spin) + lag;
                var im = g == 0 ? swords[i] : ghosts[i, g - 1];
                if (im == null) continue;
                Vector2 pos = (Centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius) * u;
                Vector2 dir = new Vector2(Mathf.Sin(a), -Mathf.Cos(a));                 // clockwise
                Frame(im, lib.loadingSword, t * 12f + i, Px);
                im.rectTransform.anchoredPosition = pos;
                im.rectTransform.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
                float alpha = arrive > 0f ? 1f : 0f;
                im.color = g == 0 ? new Color(1f, 1f, 1f, alpha) : new Color(0.55f, 0.85f, 1f, alpha * new[] { 0.45f, 0.25f, 0.12f }[g - 1]);
            }
        }

        // the Dipper: each star lights in turn as the progress, the lines between them drawn in
        for (int i = 0; i < 7; i++)
        {
            var im = stars[i];
            if (im == null) continue;
            float at = fillSeconds * (i + 0.5f) / 7f, since = t - at;
            bool lit = since >= 0f;
            float pop = lit ? Mathf.Clamp01(since / 0.25f) : 0f;
            float size = lit ? 3f * (1f + Mathf.Sin(pop * Mathf.PI) * 0.7f) : 2f;
            Frame(im, lib.loadingStar, lit ? t * 10f + i * 1.7f : 0f, size);
            im.rectTransform.anchoredPosition = DipperAt(i) * u;
            im.color = lit ? Color.white : new Color(0.45f, 0.55f, 0.8f, 0.3f);
        }
        for (int i = 0; i < 7; i++)
        {
            var link = links[i];
            if (link == null) continue;
            var (a, b) = Link[i];
            float at = fillSeconds * (Mathf.Max(a, b) + 0.5f) / 7f;
            float drawn = Mathf.Clamp01((t - at) / 0.2f);
            Vector2 pa = DipperAt(a) * u, pb = DipperAt(b) * u;
            var rt = link.rectTransform;
            rt.anchoredPosition = pa;
            rt.sizeDelta = new Vector2(Vector2.Distance(pa, pb) * Mathf.Max(0.02f, drawn), 3f * u);
            rt.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(pb.y - pa.y, pb.x - pa.x) * Mathf.Rad2Deg);
            link.color = new Color(0.55f, 0.8f, 1f, drawn > 0f ? 0.55f : 0.12f);
        }

        // the word under the ring, the dots after it counting. placed every frame: a panel that has
        // only just been made isn't laid out yet
        if (label != null && labelText != null)
        {
            label.rectTransform.anchoredPosition = LabelAt * u;
            label.fontSize = LabelSize * u;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            // the dots still to come are there but clear, so the word doesn't shift as they appear
            int dots = (int)(t / 0.3f) % 4;
            label.text = labelText + new string('.', dots) + "<alpha=#00>" + new string('.', 3 - dots);
        }

        // the fade: down to black as the swords meet, for the spiral to open from
        if (flash != null)
            flash.color = finaleClock < 0f ? Color.clear
                : new Color(0f, 0f, 0f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((finaleClock - 0.15f) / 0.37f)));
    }

    // 0 before it starts, rising to 1 over its time
    private static float Appear(float t, float from, float seconds = 0.3f) => Mathf.Clamp01((t - from) / seconds);

    // eases out past the end and back, like something thrown into place
    private static float Back(float k)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float m = k - 1f;
        return 1f + c3 * m * m * m + c1 * m * m;
    }

    // shows frame n (wrapped) of a set at a whole-pixel scale, turning about the sprite's own pivot
    private void Frame(Image im, Sprite[] frames, float n, float scale)
    {
        if (frames == null || frames.Length == 0) { im.enabled = false; return; }
        var s = frames[((int)Mathf.Floor(n) % frames.Length + frames.Length) % frames.Length];
        if (s == null) return;
        if (im.sprite != s) im.sprite = s;
        var rt = im.rectTransform;
        rt.sizeDelta = s.rect.size * scale * u;
        rt.pivot = new Vector2(s.pivot.x / s.rect.width, s.pivot.y / s.rect.height);
    }

    // ---------------------------------------------------------------- the Northern Dipper

    // from the tip of the handle to the far end of the bowl, the order they light in
    private static readonly Vector2[] Dipper =
    {
        new Vector2(-3.2f, 0.35f), new Vector2(-2.2f, 0.75f), new Vector2(-1.2f, 0.8f), new Vector2(-0.25f, 0.55f),
        new Vector2(-0.1f, -0.45f), new Vector2(1.3f, -0.6f), new Vector2(1.45f, 0.45f),
    };
    private static readonly (int, int)[] Link = { (0, 1), (1, 2), (2, 3), (3, 4), (4, 5), (5, 6), (6, 3) };

    private static Vector2 DipperAt(int i) => DipperMiddle + (Dipper[i] + new Vector2(0.875f, 0f)) * 62f;

    // ---------------------------------------------------------------- building

    private void Build()
    {
        if (lib == null || lib.loadingSword == null || lib.loadingSword.Length == 0) return;

        // a copy of a panel that already had one: that one goes, this one is built fresh
        foreach (Transform child in transform)
            if (child.name == "Loading Animation" || child.name == "Flash") Destroy(child.gameObject);

        var go = new GameObject("Loading Animation", typeof(RectTransform));
        root = (RectTransform)go.transform;
        root.SetParent(transform, false);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;
        // over the panel's own dark, under its text
        int under = label != null && label.transform.parent == transform ? label.transform.GetSiblingIndex() : transform.childCount;
        root.SetSiblingIndex(under);

        backdrop = Plain("Backdrop", Color.black);
        var full = backdrop.rectTransform;
        full.anchorMin = Vector2.zero; full.anchorMax = Vector2.one; full.offsetMin = full.offsetMax = Vector2.zero;

        for (int i = 0; i < talismans.Length; i++) talismans[i] = Sprite("Talisman " + (i + 1));
        meteor = Sprite("Meteor");
        for (int i = 0; i < arrows.Length; i++) arrows[i] = Sprite("Arrow " + (i + 1));
        ring = Sprite("Aura Ring");
        for (int i = 0; i < 7; i++)
        {
            links[i] = Plain("Dipper Line " + (i + 1), Color.clear);
            links[i].rectTransform.pivot = new Vector2(0f, 0.5f);
        }
        for (int i = 0; i < 7; i++)
        {
            stars[i] = Sprite("Dipper Star " + (i + 1));
        }
        for (int i = 0; i < Swords; i++)
            for (int g = Ghosts - 1; g >= 0; g--) ghosts[i, g] = Sprite($"Sword {i + 1} Afterimage {g + 1}");
        for (int i = 0; i < Swords; i++) swords[i] = Sprite("Sword " + (i + 1));
        coin = Sprite("Wen");

        // the flash covers the text too, so it goes last on the panel itself
        var f = new GameObject("Flash", typeof(RectTransform));
        var frt = (RectTransform)f.transform;
        frt.SetParent(transform, false);
        frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = frt.offsetMax = Vector2.zero;
        flash = f.AddComponent<Image>();
        flash.raycastTarget = false;
        flash.color = Color.clear;
    }

    private Image Sprite(string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(root, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        var im = go.AddComponent<Image>();
        im.raycastTarget = false;
        return im;
    }

    private Image Plain(string name, Color color)
    {
        var im = Sprite(name);
        im.color = color;
        return im;
    }
}
