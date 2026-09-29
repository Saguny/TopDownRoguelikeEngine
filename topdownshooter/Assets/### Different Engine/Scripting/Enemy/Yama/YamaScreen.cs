using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// what Yama's fight puts on the screen: the world dimming and a vignette closing in, cinematic
// bars for his entrance and his end, flashes, his title card, the spell card cut-in (his portrait
// sweeping across on a band of red) with the card's name sliding in under his bar, his health bar
// (one bar a phase, a trail behind the damage, a diamond for each spell card left) and the spell
// card bonus. the dim sits under the HUD; the rest with it, under every menu. made on first use,
// gone with the scene
public class YamaScreen : MonoBehaviour
{
    private static YamaScreen instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    public static YamaScreen Get()
    {
        if (instance == null && Application.isPlaying) instance = new GameObject("Yama Screen").AddComponent<YamaScreen>();
        return instance;
    }

    private const float Px = 3f;                 // one art pixel, in reference pixels
    private static readonly Color Gold = new Color(1f, 0.84f, 0.4f);
    private static readonly Color Blood = new Color(0.82f, 0.13f, 0.18f);
    private static readonly Color Ink = new Color(0.07f, 0.03f, 0.08f);

    private TMP_FontAsset font;
    private RectTransform under, over;
    private Image dim, vignette, flash, barTop, barBottom;
    private float dimTarget, dimNow, vignetteTarget, vignetteNow, flashAlpha, flashFade = 1f, letterTarget, letterNow;
    private Color vignetteColor = Blood, flashColor = Color.white;
    private bool vignettePulse;

    private RectTransform bar;
    private Image barFill, barTrail;
    private TMP_Text barName;
    private readonly List<Image> stars = new List<Image>();
    private float barShown, barShownTarget, fillNow, trailNow, fillTarget = 1f;
    private bool barLocked;
    private const float BarInnerWidth = (256f - 20f) * Px;

    private RectTransform cutIn, band;
    private Image portrait;
    private TMP_Text cardText, cardShadow;
    private RectTransform card;
    private TMP_Text bonusText, titleText, subtitleText, duelTag, duelName, duelLine;

    private void Awake()
    {
        font = Resources.Load<TMP_FontAsset>("fonts/Pixelta");
        under = CanvasAt("Yama Under", -5);
        over = CanvasAt("Yama Over", 36);

        dim = Fill("Dim", under, new Color(0.03f, 0.01f, 0.05f, 0f));
        vignette = Fill("Vignette", under, new Color(1f, 1f, 1f, 0f));
        vignette.sprite = VignetteSprite();

        // the letterbox
        barTop = Fill("Letterbox Top", over, Color.black);
        barBottom = Fill("Letterbox Bottom", over, Color.black);
        foreach (var b in new[] { barTop, barBottom })
        {
            var rt = b.rectTransform;
            bool top = b == barTop;
            rt.anchorMin = new Vector2(0f, top ? 1f : 0f);
            rt.anchorMax = new Vector2(1f, top ? 1f : 0f);
            rt.pivot = new Vector2(0.5f, top ? 1f : 0f);
            rt.sizeDelta = new Vector2(0f, 0f);
        }

        BuildBar();
        BuildCard();

        bonusText = Text("Bonus", over, 46f, Gold, true);
        bonusText.rectTransform.anchoredPosition = new Vector2(0f, 250f);
        bonusText.alpha = 0f;
        titleText = Text("Title", over, 170f, Gold, true);
        titleText.rectTransform.anchoredPosition = new Vector2(0f, 60f);
        titleText.characterSpacing = 18f;
        subtitleText = Text("Subtitle", over, 40f, new Color(1f, 0.9f, 0.8f), true);
        subtitleText.rectTransform.anchoredPosition = new Vector2(0f, -60f);
        titleText.alpha = subtitleText.alpha = 0f;

        // the duel's weapon, along the bottom letterbox bar
        duelTag = Text("Duel Tag", over, 24f, new Color(0.75f, 0.7f, 0.72f), true);
        duelTag.rectTransform.anchoredPosition = new Vector2(0f, -422f);
        duelTag.characterSpacing = 30f;
        duelName = Text("Duel Name", over, 50f, Gold, true);
        duelName.rectTransform.anchoredPosition = new Vector2(0f, -462f);
        duelLine = Text("Duel Line", over, 24f, new Color(1f, 0.92f, 0.85f), true);
        duelLine.rectTransform.anchoredPosition = new Vector2(0f, -506f);
        duelTag.alpha = duelName.alpha = duelLine.alpha = 0f;

        flash = Fill("Flash", over, new Color(1f, 1f, 1f, 0f));
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        if (under != null) Destroy(under.gameObject);
        if (over != null) Destroy(over.gameObject);
    }

    // ---- what the fight calls

    public void Dim(float alpha) => dimTarget = Mathf.Clamp01(alpha);
    public void Vignette(Color c, float strength, bool pulse = false) { vignetteColor = c; vignetteTarget = Mathf.Clamp01(strength); vignettePulse = pulse; }
    public void Letterbox(bool on) => letterTarget = on ? 1f : 0f;

    public void Flash(Color c, float alpha, float seconds)
    {
        flashColor = c;
        flashAlpha = Mathf.Max(flashAlpha, alpha);
        flashFade = Mathf.Max(0.05f, seconds);
    }

    public void ShowBar(string name)
    {
        barName.text = name;
        barShownTarget = 1f;
        fillNow = trailNow = 0f;
    }

    public void HideBar() => barShownTarget = 0f;

    // this phase's share of its bar still standing, and how many spell cards are left
    public void SetBar(float fill, int spellsLeft, bool locked)
    {
        fillTarget = Mathf.Clamp01(fill);
        barLocked = locked;
        for (int i = 0; i < stars.Count; i++) stars[i].enabled = i < spellsLeft;
    }

    // the bar refills for a new phase, sweeping up
    public void RefillBar() { fillNow = trailNow = 0f; fillTarget = 1f; }

    public void Declare(string cardName, Sprite face)
    {
        StopCoroutine(nameof(CutIn));
        cardText.text = cardShadow.text = cardName;
        portrait.sprite = face;
        portrait.enabled = face != null;
        StartCoroutine(nameof(CutIn));
    }

    public void ClearCard() => StartCoroutine(SlideCardOut());

    public void Bonus(string text, Color c) { StopCoroutine(nameof(ShowBonus)); bonusText.text = text; bonusText.color = c; StartCoroutine(nameof(ShowBonus)); }

    public void Title(string title, string subtitle, float seconds) => StartCoroutine(ShowTitle(title, subtitle, seconds));

    // the end boss's duel: the weapon it's fought with, its name in its colour
    public void Duel(string weapon, string line, Color c) => StartCoroutine(ShowDuel(weapon, line, c));

    // the fight's over: everything it put up fades away
    public void EndFight()
    {
        Dim(0f);
        Vignette(vignetteColor, 0f);
        Letterbox(false);
        HideBar();
    }

    // ---- per frame

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        dimNow = Mathf.MoveTowards(dimNow, dimTarget, dt * 0.6f);
        dim.color = new Color(dim.color.r, dim.color.g, dim.color.b, dimNow);

        vignetteNow = Mathf.MoveTowards(vignetteNow, vignetteTarget, dt * 0.8f);
        float pulse = vignettePulse ? 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 3.2f) : 1f;
        vignette.color = new Color(vignetteColor.r, vignetteColor.g, vignetteColor.b, vignetteNow * pulse);

        flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, dt / flashFade);
        flash.color = new Color(flashColor.r, flashColor.g, flashColor.b, flashAlpha);

        letterNow = Mathf.MoveTowards(letterNow, letterTarget, dt * 2.2f);
        float h = 130f * (1f - (1f - letterNow) * (1f - letterNow));
        barTop.rectTransform.sizeDelta = new Vector2(0f, h);
        barBottom.rectTransform.sizeDelta = new Vector2(0f, h);

        // the bar drops in from above, its fill sweeping up on a new phase and the trail chasing damage
        barShown = Mathf.MoveTowards(barShown, barShownTarget, dt * 2.5f);
        bar.anchoredPosition = new Vector2(0f, Mathf.Lerp(120f, -48f, 1f - (1f - barShown) * (1f - barShown)));
        fillNow = fillTarget > fillNow ? Mathf.MoveTowards(fillNow, fillTarget, dt * 0.9f) : fillTarget;
        trailNow = trailNow < fillNow ? fillNow : Mathf.MoveTowards(trailNow, fillNow, dt * 0.45f);
        barFill.rectTransform.sizeDelta = new Vector2(BarInnerWidth * fillNow, barFill.rectTransform.sizeDelta.y);
        barTrail.rectTransform.sizeDelta = new Vector2(BarInnerWidth * trailNow, barTrail.rectTransform.sizeDelta.y);
        barFill.color = barLocked ? new Color(0.45f, 0.4f, 0.55f) : Color.Lerp(Blood, Gold, 0.15f + 0.1f * Mathf.Sin(Time.unscaledTime * 6f));
    }

    // ---- the spell card: a band of red sweeping across with his face on it, the name sliding in

    private IEnumerator CutIn()
    {
        const float seconds = 1.4f;
        cutIn.gameObject.SetActive(true);
        var group = cutIn.GetComponent<CanvasGroup>();
        card.anchoredPosition = new Vector2(900f, -120f);
        cardText.alpha = cardShadow.alpha = 0f;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            float k = t / seconds;
            // fast in, a long slow drift across the middle, fast out
            float s = k < 0.2f ? Ease(k / 0.2f) * 0.4f : k < 0.8f ? 0.4f + (k - 0.2f) / 0.6f * 0.2f : 0.6f + Ease((k - 0.8f) / 0.2f) * 0.4f;
            portrait.rectTransform.anchoredPosition = Vector2.Lerp(new Vector2(1100f, -520f), new Vector2(-1100f, 520f), s);
            band.anchoredPosition = Vector2.Lerp(new Vector2(700f, -330f), new Vector2(-700f, 330f), s * 0.6f + 0.2f);
            group.alpha = k < 0.1f ? k / 0.1f : k > 0.85f ? (1f - k) / 0.15f : 1f;
            // the card's name slides in once the face has crossed
            float c = Mathf.Clamp01((k - 0.3f) / 0.35f);
            card.anchoredPosition = new Vector2(Mathf.Lerp(900f, -40f, Ease(c)), -120f);
            cardText.alpha = cardShadow.alpha = c;
            yield return null;
        }
        cutIn.gameObject.SetActive(false);
        card.anchoredPosition = new Vector2(-40f, -120f);
        cardText.alpha = cardShadow.alpha = 1f;
    }

    private IEnumerator SlideCardOut()
    {
        for (float t = 0f; t < 0.5f; t += Time.unscaledDeltaTime)
        {
            float k = Ease(t / 0.5f);
            card.anchoredPosition = new Vector2(Mathf.Lerp(-40f, 900f, k), -120f);
            cardText.alpha = cardShadow.alpha = 1f - k;
            yield return null;
        }
        cardText.alpha = cardShadow.alpha = 0f;
    }

    private IEnumerator ShowBonus()
    {
        var rt = bonusText.rectTransform;
        for (float t = 0f; t < 2.6f; t += Time.unscaledDeltaTime)
        {
            float pop = t < 0.18f ? Mathf.Lerp(1.8f, 1f, Ease(t / 0.18f)) : 1f;
            rt.localScale = Vector3.one * pop;
            bonusText.alpha = t < 0.1f ? t / 0.1f : t > 2.1f ? (2.6f - t) / 0.5f : 1f;
            rt.anchoredPosition = new Vector2(0f, 250f + t * 8f);
            yield return null;
        }
        bonusText.alpha = 0f;
    }

    private IEnumerator ShowTitle(string title, string subtitle, float seconds)
    {
        titleText.text = title;
        subtitleText.text = subtitle;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.Clamp01(t / 0.35f);
            titleText.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.7f, 1f, Ease(k));
            titleText.characterSpacing = Mathf.Lerp(60f, 18f, Ease(k)) + t * 4f;
            float fade = t > seconds - 0.6f ? (seconds - t) / 0.6f : 1f;
            titleText.alpha = Mathf.Min(k * 1.5f, 1f) * fade;
            subtitleText.alpha = Mathf.Clamp01((t - 0.4f) / 0.4f) * fade;
            yield return null;
        }
        titleText.alpha = subtitleText.alpha = 0f;
    }

    private IEnumerator ShowDuel(string weapon, string line, Color c)
    {
        const float seconds = 3.2f;
        duelTag.text = "- DUEL -";
        duelName.text = weapon;
        duelName.color = Color.Lerp(c, Color.white, 0.2f);
        duelLine.text = line;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            float fade = t > seconds - 0.5f ? (seconds - t) / 0.5f : 1f;
            float k = Ease(Mathf.Clamp01(t / 0.4f));
            duelName.characterSpacing = Mathf.Lerp(40f, 6f, k) + t * 2f;
            duelName.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.3f, 1f, k);
            duelName.alpha = Mathf.Min(1f, t / 0.2f) * fade;
            duelTag.alpha = Mathf.Clamp01((t - 0.15f) / 0.3f) * fade;
            duelLine.alpha = Mathf.Clamp01((t - 0.45f) / 0.4f) * fade;
            yield return null;
        }
        duelTag.alpha = duelName.alpha = duelLine.alpha = 0f;
    }

    private static float Ease(float k) { k = Mathf.Clamp01(k); return 1f - (1f - k) * (1f - k) * (1f - k); }

    // ---- building

    private void BuildBar()
    {
        bar = Box("Boss Bar", over);
        bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 1f);
        bar.pivot = new Vector2(0.5f, 1f);
        bar.anchoredPosition = new Vector2(0f, 120f);
        bar.sizeDelta = new Vector2(256f * Px, 16f * Px);

        var back = Fill("Back", bar, new Color(0.08f, 0.03f, 0.07f, 0.92f));
        Inset(back.rectTransform, 10f, 3f);
        barTrail = Fill("Trail", bar, new Color(1f, 0.95f, 0.8f, 0.85f));
        LeftBar(barTrail.rectTransform);
        barFill = Fill("Fill", bar, Blood);
        LeftBar(barFill.rectTransform);
        var frame = new GameObject("Frame", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        frame.rectTransform.SetParent(bar, false);
        frame.rectTransform.anchorMin = Vector2.zero;
        frame.rectTransform.anchorMax = Vector2.one;
        frame.rectTransform.sizeDelta = Vector2.zero;
        frame.sprite = YamaArt.Whole("bar", 100f);
        frame.raycastTarget = false;
        if (frame.sprite == null) frame.enabled = false;

        barName = Text("Name", bar, 34f, new Color(1f, 0.9f, 0.75f), false);
        barName.alignment = TextAlignmentOptions.BottomLeft;
        barName.rectTransform.anchorMin = barName.rectTransform.anchorMax = new Vector2(0f, 1f);
        barName.rectTransform.pivot = new Vector2(0f, 0f);
        barName.rectTransform.sizeDelta = new Vector2(700f, 50f);
        barName.rectTransform.anchoredPosition = new Vector2(8f, 2f);

        // the spell cards left: gold diamonds to the right of the name
        for (int i = 0; i < 6; i++)
        {
            var d = new GameObject("Card " + i, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            d.rectTransform.SetParent(bar, false);
            d.rectTransform.anchorMin = d.rectTransform.anchorMax = new Vector2(1f, 1f);
            d.rectTransform.sizeDelta = new Vector2(16f, 16f);
            d.rectTransform.anchoredPosition = new Vector2(-18f - i * 26f, 20f);
            d.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            d.color = Gold;
            d.raycastTarget = false;
            d.enabled = false;
            stars.Add(d);
        }
    }

    private static void Inset(RectTransform rt, float x, float y)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(x * Px, y * Px);
        rt.offsetMax = new Vector2(-x * Px, -y * Px);
    }

    private static void LeftBar(RectTransform rt)
    {
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(10f * Px, 0f);
        rt.sizeDelta = new Vector2(0f, -6f * Px);
    }

    private void BuildCard()
    {
        cutIn = Box("Cut In", over);
        cutIn.anchorMin = Vector2.zero;
        cutIn.anchorMax = Vector2.one;
        cutIn.sizeDelta = Vector2.zero;
        cutIn.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
        var b = Fill("Band", cutIn, new Color(0.45f, 0.04f, 0.1f, 0.8f));
        band = b.rectTransform;
        band.anchorMin = band.anchorMax = new Vector2(0.5f, 0.5f);
        band.sizeDelta = new Vector2(3200f, 260f);
        band.localRotation = Quaternion.Euler(0f, 0f, 25f);
        var edge = Fill("Edge", band, new Color(1f, 0.8f, 0.4f, 0.9f));
        edge.rectTransform.anchorMin = new Vector2(0f, 1f);
        edge.rectTransform.anchorMax = new Vector2(1f, 1f);
        edge.rectTransform.sizeDelta = new Vector2(0f, 6f);
        portrait = new GameObject("Portrait", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        portrait.rectTransform.SetParent(cutIn, false);
        portrait.rectTransform.sizeDelta = new Vector2(112f * 5f, 112f * 5f);
        portrait.raycastTarget = false;
        cutIn.gameObject.SetActive(false);

        card = Box("Card", over);
        card.anchorMin = card.anchorMax = new Vector2(1f, 1f);
        card.pivot = new Vector2(1f, 1f);
        card.sizeDelta = new Vector2(1100f, 60f);
        card.anchoredPosition = new Vector2(900f, -120f);
        cardShadow = Text("Shadow", card, 40f, Ink, false);
        cardShadow.alignment = TextAlignmentOptions.Right;
        cardShadow.rectTransform.anchoredPosition = new Vector2(3f, -3f);
        cardText = Text("Name", card, 40f, new Color(1f, 0.93f, 0.85f), false);
        cardText.alignment = TextAlignmentOptions.Right;
        cardText.alpha = cardShadow.alpha = 0f;
        var underline = Fill("Underline", card, new Color(0.85f, 0.2f, 0.25f, 0.9f));
        underline.rectTransform.anchorMin = new Vector2(0.35f, 0f);
        underline.rectTransform.anchorMax = new Vector2(1f, 0f);
        underline.rectTransform.sizeDelta = new Vector2(0f, 4f);
        underline.transform.SetSiblingIndex(0);
    }

    private static RectTransform CanvasAt(string name, int order)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = order;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;
        return (RectTransform)go.transform;
    }

    private static RectTransform Box(string name, Transform parent)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        return rt;
    }

    private static Image Fill(string name, Transform parent, Color c)
    {
        var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        img.rectTransform.SetParent(parent, false);
        img.rectTransform.anchorMin = Vector2.zero;
        img.rectTransform.anchorMax = Vector2.one;
        img.rectTransform.sizeDelta = Vector2.zero;
        img.color = c;
        img.raycastTarget = false;
        return img;
    }

    // text with a hard drop shadow under it, the way the rest of the game's pixel text is set
    private TMP_Text Text(string name, Transform parent, float size, Color c, bool shadow)
    {
        var holder = Box(name, parent);
        holder.sizeDelta = new Vector2(1800f, size * 1.6f);
        TMP_Text Make(string n, Color col, Vector2 offset)
        {
            var t = new GameObject(n, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            t.rectTransform.SetParent(holder, false);
            t.rectTransform.anchorMin = Vector2.zero;
            t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.sizeDelta = Vector2.zero;
            t.rectTransform.anchoredPosition = offset;
            if (font != null) t.font = font;
            t.fontSize = size;
            t.color = col;
            t.alignment = TextAlignmentOptions.Center;
            t.enableWordWrapping = false;
            t.raycastTarget = false;
            return t;
        }
        if (!shadow) return Make("Text", c, Vector2.zero);
        var drop = new Vector2(size * 0.06f, -size * 0.06f);
        var s = Make("Shadow", Ink, drop);
        var main = Make("Text", c, Vector2.zero);
        // the shadow follows the main text's words, place and fade
        holder.gameObject.AddComponent<ShadowFollow>().Set(main, s, drop);
        return main;
    }

    // the vignette: clear in the middle, closing in at the edges
    private static Sprite VignetteSprite()
    {
        const int S = 128;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[S * S];
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
        {
            float dx = (x + 0.5f) / S * 2f - 1f, dy = (y + 0.5f) / S * 2f - 1f;
            float d = Mathf.Sqrt(dx * dx * 0.8f + dy * dy);
            float a = Mathf.Clamp01((d - 0.55f) / 0.6f);
            px[y * S + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
    }

    // keeps a drop shadow's text and fade the same as the text above it
    private class ShadowFollow : MonoBehaviour
    {
        private TMP_Text main, shadow;
        private Vector2 drop;
        public void Set(TMP_Text m, TMP_Text s, Vector2 d) { main = m; shadow = s; drop = d; }
        private void LateUpdate()
        {
            if (main == null || shadow == null) return;
            if (shadow.text != main.text) shadow.text = main.text;
            shadow.alpha = main.alpha;
            shadow.characterSpacing = main.characterSpacing;
            shadow.fontSize = main.fontSize;
            shadow.rectTransform.localScale = main.rectTransform.localScale;
            shadow.rectTransform.anchoredPosition = main.rectTransform.anchoredPosition + drop;
        }
    }
}
