using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum BurstTier { Phase, Card, Final }

// what a boss's burst looks like: its art set in Resources/Burst ("yama", "mengpo"), its colours,
// the sigil that turns up behind it, and its stinger in Resources/Sfx
public struct BurstTheme
{
    public string art;
    public Color main, glow;
    public Sprite sigil;
    public string stinger;
}

// an end boss's phase beginning, the way a Genshin Impact burst goes off. the wind-up: the world
// held nearly still, darkening round the boss, the camera pushing in on it, radial speed lines,
// motes of its power spiralling in, its sigil swelling and turning behind it, the power knotting
// at its heart, the air sucked in. then the release: a flash, a shockwave tearing out, shards
// flung everywhere, the camera kicked back, the screen shaken, a boom and the boss's stinger,
// and embers drifting down after. three sizes: a plain phase's, short and light; a spell card's,
// the whole of it; and the last phase's, the ultimate: longer, letterboxed, three heartbeats that
// jolt the world as it gathers, and two releases, the second the greater. it keeps to unscaled
// time, and only holds time still if nothing else already owns it (Juice.Freeze's rule); a pause
// menu opened mid-way holds it where it is
public class PhaseBurst : MonoBehaviour
{
    private const float Slow = 0.05f;
    // the sigil's size across at a tier scale of 1, world units
    private const float SigilSize = 4.2f;

    private struct Tier
    {
        public float windup, zoom, lean, dim, lines, sigil, core, wave, shake, flash;
        public int motes, sparks, embers, beats;
    }

    private static readonly Tier[] Tiers =
    {
        new Tier { windup = 0.45f, zoom = 0.9f, lean = 0.45f, dim = 0.3f, lines = 0.35f, sigil = 0.8f, core = 0.8f, wave = 1.5f, shake = 0.35f, flash = 0.45f, motes = 16, sparks = 20, embers = 0, beats = 0 },
        new Tier { windup = 0.85f, zoom = 0.8f, lean = 0.62f, dim = 0.55f, lines = 0.75f, sigil = 1.2f, core = 1.1f, wave = 2.2f, shake = 0.7f, flash = 0.7f, motes = 32, sparks = 40, embers = 26, beats = 0 },
        new Tier { windup = 1.8f, zoom = 0.68f, lean = 0.8f, dim = 0.75f, lines = 1f, sigil = 1.7f, core = 1.6f, wave = 3.1f, shake = 1.1f, flash = 0.95f, motes = 64, sparks = 80, embers = 60, beats = 3 },
    };

    private static PhaseBurst instance;
    private static Texture2D linesTex, holeTex;

    private RectTransform canvas;
    private RawImage lines, hole;
    private readonly List<SpriteRenderer> pool = new List<SpriteRenderer>();
    private Camera cam;
    private float baseSize;
    private bool ownsTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; }

    private static PhaseBurst Get()
    {
        if (instance == null && Application.isPlaying) instance = new GameObject("Phase Burst").AddComponent<PhaseBurst>();
        return instance;
    }

    // the burst, yielded by the boss's phase before its card is declared. `core`: where its power
    // gathers (the boss's middle), asked every frame as the boss drifts
    public static IEnumerator Play(Transform boss, System.Func<Vector2> core, BurstTheme theme, BurstTier tier)
    {
        var b = Get();
        if (b == null || boss == null) yield break;
        yield return b.Run(boss, core, theme, Tiers[(int)tier], tier == BurstTier.Final);
    }

    // ---- setting up

    private void Awake()
    {
        var go = new GameObject("Phase Burst UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(transform, false);
        var c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 30; // over the HUD, under the cut-in (YamaScreen's, 36)
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;
        canvas = (RectTransform)go.transform;
        hole = Raw("Dark", HoleTexture(), new Vector2(4200f, 4200f));
        lines = Raw("Speed Lines", LinesTexture(), new Vector2(3000f, 3000f));
        hole.color = lines.color = new Color(1f, 1f, 1f, 0f);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        GiveBack();
    }

    private RawImage Raw(string name, Texture tex, Vector2 size)
    {
        var img = new GameObject(name, typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        img.rectTransform.SetParent(canvas, false);
        img.rectTransform.sizeDelta = size;
        img.texture = tex;
        img.raycastTarget = false;
        return img;
    }

    // speed lines radiating from the middle: long thin wedges of uneven width, clear at the heart
    private static Texture2D LinesTexture()
    {
        if (linesTex != null) return linesTex;
        const int n = 384;
        linesTex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        var rnd = new System.Random(7);
        var widths = new float[160];
        for (int i = 0; i < widths.Length; i++) widths[i] = rnd.NextDouble() < 0.45 ? (float)(0.08 + rnd.NextDouble() * 0.35) : 0f;
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            float dx = (x + 0.5f) / n - 0.5f, dy = (y + 0.5f) / n - 0.5f, d = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
            float a = (Mathf.Atan2(dy, dx) / (2f * Mathf.PI) + 0.5f) * widths.Length;
            int i = (int)a % widths.Length;
            float frac = a - Mathf.Floor(a), w = widths[i] * Mathf.Clamp01((d - 0.22f) / 0.5f);
            bool on = w > 0f && Mathf.Abs(frac - 0.5f) < w * 0.5f && d > 0.24f;
            px[y * n + x] = on ? new Color32(255, 255, 255, (byte)(255 * Mathf.Clamp01((d - 0.24f) * 2.2f))) : new Color32(0, 0, 0, 0);
        }
        linesTex.SetPixels32(px);
        linesTex.Apply();
        return linesTex;
    }

    // the dark closing in: clear round the middle, black beyond
    private static Texture2D HoleTexture()
    {
        if (holeTex != null) return holeTex;
        const int n = 128;
        holeTex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            float dx = (x + 0.5f) / n - 0.5f, dy = (y + 0.5f) / n - 0.5f, d = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
            px[y * n + x] = new Color32(6, 2, 10, (byte)(255 * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 0.1f) / 0.3f))));
        }
        holeTex.SetPixels32(px);
        holeTex.Apply();
        return holeTex;
    }

    // ---- the burst

    private IEnumerator Run(Transform boss, System.Func<Vector2> coreAt, BurstTheme theme, Tier t, bool final)
    {
        cam = Camera.main;
        baseSize = cam != null && cam.orthographic ? cam.orthographicSize : 0f;
        var screen = YamaScreen.Get();
        var grade = BossGrade.Current;
        var moteArt = YamaArt.Strip("Burst/" + theme.art + "_mote");
        var coreArt = YamaArt.Strip("Burst/" + theme.art + "_core");

        // the world held still, if nobody else is holding it
        ownsTime = Time.timeScale >= 0.99f;
        if (ownsTime) Time.timeScale = Slow;
        if (final && screen != null) screen.Letterbox(true);
        YamaArt.Play(final ? "burst_ult_charge" : "burst_charge", boss.position, final ? 1f : 0.85f, final ? 1f : Mathf.Lerp(1.3f, 1f, t.windup));

        // the sigil behind it, the knot of power at its heart, the motes spiralling in
        var sigil = Take(theme.sigil, "Aura", 37);
        sigil.color = new Color(theme.glow.r, theme.glow.g, theme.glow.b, 0f);
        // the sigils come in any size: each is brought to SigilSize across, times its tier's scale
        float sigilUnit = theme.sigil != null ? SigilSize / Mathf.Max(0.01f, Mathf.Max(theme.sigil.bounds.size.x, theme.sigil.bounds.size.y)) : 1f;
        var core = Take(coreArt != null ? coreArt[0] : null, "Aura", 190);
        var motes = new List<Mote>();
        var rnd = new System.Random();
        for (int i = 0; i < t.motes; i++)
        {
            float a = (float)(rnd.NextDouble() * Mathf.PI * 2f), r = 4.5f + (float)rnd.NextDouble() * 4f;
            var m = new Mote { angle = a, radius = r, spin = (rnd.NextDouble() < 0.5 ? -1f : 1f) * (2.2f + (float)rnd.NextDouble() * 2f),
                start = (float)rnd.NextDouble() * t.windup * 0.6f, sr = Take(moteArt != null ? moteArt[0] : null, "Aura", 191) };
            m.sr.enabled = false;
            motes.Add(m);
        }

        int beat = 0;
        for (float time = 0f; time < t.windup;)
        {
            // a pause menu opened mid-way: hold here
            if (Time.timeScale == 0f && ownsTime) { yield return null; continue; }
            float dt = Time.unscaledDeltaTime;
            time += dt;
            float k = Mathf.Clamp01(time / t.windup), e = 1f - (1f - k) * (1f - k);
            Vector2 c = coreAt();

            // the camera leaning in and pushing close
            CameraFollow.FocusPoint = c;
            CameraFollow.FocusWeight = t.lean * e;
            if (baseSize > 0f) cam.orthographicSize = baseSize * Mathf.Lerp(1f, t.zoom, e);

            // the dark and the lines, centred on it on the screen
            Vector2 at = ScreenAt(c);
            hole.rectTransform.anchoredPosition = at;
            hole.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.6f, 0.8f, e);
            hole.color = new Color(1f, 1f, 1f, t.dim * Mathf.Clamp01(k * 3f));
            lines.rectTransform.anchoredPosition = at;
            lines.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            lines.rectTransform.localScale = Vector3.one * Random.Range(0.92f, 1.08f);
            lines.color = new Color(theme.glow.r, theme.glow.g, theme.glow.b, t.lines * Mathf.Clamp01((k - 0.15f) * 3f) * Random.Range(0.6f, 1f));

            // the sigil swelling and turning, the core knotting
            sigil.transform.position = c;
            sigil.transform.localScale = Vector3.one * t.sigil * sigilUnit * e;
            sigil.transform.rotation = Quaternion.Euler(0f, 0f, time * (final ? 260f : 180f));
            sigil.color = new Color(theme.glow.r, theme.glow.g, theme.glow.b, 0.85f * e);
            core.transform.position = c;
            core.transform.localScale = Vector3.one * t.core;
            if (coreArt != null) core.sprite = coreArt[Mathf.Min(coreArt.Length - 1, (int)(k * coreArt.Length))];

            // the motes: each sets off in its turn and spirals in, faster as it closes, arriving as
            // the wind-up ends
            foreach (var m in motes)
            {
                if (time < m.start) continue;
                float u = Mathf.Clamp01((time - m.start) / Mathf.Max(0.05f, t.windup - m.start));
                float r = m.radius * (1f - u * u), ang = m.angle + m.spin * u;
                Vector2 p = c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
                Vector2 v = c - p;
                m.sr.enabled = u < 0.98f;
                m.sr.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, 0f, Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg));
                if (moteArt != null) m.sr.sprite = moteArt[(int)(time * 16f + m.angle * 3f) % moteArt.Length];
                m.sr.color = Color.Lerp(theme.glow, Color.white, u);
            }

            // the ultimate's heartbeats: the world lurches with each
            if (beat < t.beats && k >= (beat + 1f) / (t.beats + 1f))
            {
                beat++;
                if (screen != null) screen.Flash(theme.main, 0.18f + 0.06f * beat, 0.25f);
                if (grade != null) grade.Punch(0.35f + 0.15f * beat);
                Juice.Shake(0.15f + 0.1f * beat);
                if (baseSize > 0f) cam.orthographicSize *= 0.96f;
            }
            yield return null;
        }

        // the release
        if (ownsTime && Time.timeScale == Slow) Time.timeScale = 1f;
        ownsTime = false;
        Vector2 at0 = coreAt();
        Release(at0, theme, t, final, screen, grade);
        foreach (var m in motes) Give(m.sr);
        Give(core);
        StartCoroutine(Aftermath(at0, theme, t, final, sigil, sigilUnit));
        if (final)
        {
            // the second, greater release
            for (float w = 0f; w < 0.32f; w += Time.unscaledDeltaTime) yield return null;
            Release(coreAt(), theme, t, true, screen, grade, second: true);
        }
        for (float w = 0f; w < 0.18f; w += Time.unscaledDeltaTime) yield return null;
    }

    private void Release(Vector2 at, BurstTheme theme, Tier t, bool final, YamaScreen screen, BossGrade grade, bool second = false)
    {
        var wave = YamaArt.Strip("Burst/" + theme.art + "_wave");
        var spark = YamaArt.Strip("Burst/" + theme.art + "_spark");
        if (wave != null) FxBatch.Play(wave, 20f, at, t.wave * (second ? 1.35f : 1f), "Aura", 192);
        if (screen != null) screen.Flash(second ? Color.white : Color.Lerp(Color.white, theme.glow, 0.3f), t.flash, second ? 0.8f : 0.5f);
        if (grade != null) grade.Punch(second ? 1f : 0.8f);
        Juice.Shake(t.shake * (second ? 1.3f : 1f));
        if (!second)
        {
            YamaArt.Play(final ? "burst_ult_release" : "burst_release", at, 1f);
            if (t.embers > 0 && !string.IsNullOrEmpty(theme.stinger)) YamaArt.Play(theme.stinger, at, 0.9f);
        }
        StartCoroutine(Sparks(at, spark, (int)(t.sparks * (second ? 1.4f : 1f))));
    }

    // the shards flung outward
    private IEnumerator Sparks(Vector2 at, Sprite[] art, int n)
    {
        var list = new List<(SpriteRenderer sr, Vector2 v)>();
        for (int i = 0; i < n; i++)
        {
            float a = Random.value * Mathf.PI * 2f, s = Random.Range(8f, 22f);
            var sr = Take(art != null ? art[0] : null, "Aura", 193);
            sr.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg));
            list.Add((sr, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * s));
        }
        const float life = 0.55f;
        for (float time = 0f; time < life; time += Time.unscaledDeltaTime)
        {
            float k = time / life;
            foreach (var (sr, v) in list)
            {
                sr.transform.position += (Vector3)(v * Time.unscaledDeltaTime * (1f - k * 0.7f));
                if (art != null) sr.sprite = art[Mathf.Min(art.Length - 1, (int)(k * art.Length))];
            }
            yield return null;
        }
        foreach (var (sr, _) in list) Give(sr);
    }

    // after: the camera kicked out past where it was and settling, the dark lifting, the sigil
    // flung wide and gone, embers drifting down
    private IEnumerator Aftermath(Vector2 at, BurstTheme theme, Tier t, bool final, SpriteRenderer sigil, float sigilUnit)
    {
        var screen = YamaScreen.Get();
        var emberArt = YamaArt.Strip("Burst/" + theme.art + "_ember");
        var embers = new List<(SpriteRenderer sr, Vector2 v, float phase)>();
        for (int i = 0; i < t.embers; i++)
        {
            var sr = Take(emberArt != null ? emberArt[0] : null, "Aura", 189);
            sr.transform.position = at + Random.insideUnitCircle * (final ? 9f : 6f) + Vector2.up * 3f;
            embers.Add((sr, new Vector2(Random.Range(-0.6f, 0.6f), Random.Range(-2.2f, -0.8f)), Random.value * 10f));
        }
        float kickFrom = baseSize > 0f ? cam.orthographicSize * 0.93f : 0f;
        float settle = final ? 1.2f : 0.7f;
        for (float time = 0f; time < settle; time += Time.unscaledDeltaTime)
        {
            float k = time / settle;
            // a damped spring back to where the camera was, overshooting a little
            float spring = 1f - Mathf.Exp(-6f * k) * Mathf.Cos(9f * k);
            if (baseSize > 0f) cam.orthographicSize = Mathf.LerpUnclamped(kickFrom, baseSize, spring);
            CameraFollow.FocusWeight = t.lean * (1f - Mathf.Clamp01(k * 2f));
            hole.color = new Color(1f, 1f, 1f, t.dim * (1f - Mathf.Clamp01(k * 3f)));
            lines.color = new Color(theme.glow.r, theme.glow.g, theme.glow.b, t.lines * (1f - Mathf.Clamp01(k * 5f)));
            sigil.transform.localScale = Vector3.one * t.sigil * sigilUnit * (1f + k * 1.4f);
            sigil.color = new Color(theme.glow.r, theme.glow.g, theme.glow.b, 0.85f * (1f - Mathf.Clamp01(k * 2.5f)));
            foreach (var (sr, v, ph) in embers)
            {
                sr.transform.position += (Vector3)((v + new Vector2(Mathf.Sin(time * 3f + ph) * 0.6f, 0f)) * Time.unscaledDeltaTime);
                sr.color = new Color(1f, 1f, 1f, 1f - k);
                if (emberArt != null) sr.sprite = emberArt[(int)(time * 12f + ph) % emberArt.Length];
            }
            yield return null;
        }
        if (baseSize > 0f) cam.orthographicSize = baseSize;
        CameraFollow.FocusWeight = 0f;
        hole.color = lines.color = new Color(1f, 1f, 1f, 0f);
        if (final && screen != null) screen.Letterbox(false);
        Give(sigil);
        foreach (var (sr, _, _) in embers) Give(sr);
    }

    // ---- pieces

    private sealed class Mote { public SpriteRenderer sr; public float angle, radius, spin, start; }

    private SpriteRenderer Take(Sprite s, string layer, int order)
    {
        SpriteRenderer sr = null;
        for (int i = pool.Count - 1; i >= 0; i--) if (!pool[i].gameObject.activeSelf) { sr = pool[i]; break; }
        if (sr == null)
        {
            sr = new GameObject("Burst").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(transform, false);
            pool.Add(sr);
        }
        sr.gameObject.SetActive(true);
        sr.enabled = true;
        sr.sprite = s;
        sr.color = Color.white;
        sr.sortingLayerName = layer;
        sr.sortingOrder = order;
        sr.transform.localScale = Vector3.one;
        sr.transform.rotation = Quaternion.identity;
        return sr;
    }

    private static void Give(SpriteRenderer sr) { if (sr != null) sr.gameObject.SetActive(false); }

    // the scene ending mid-burst: the camera and time put back as they were
    private void GiveBack()
    {
        if (ownsTime && Time.timeScale == Slow) Time.timeScale = 1f;
        ownsTime = false;
        if (cam != null && baseSize > 0f) cam.orthographicSize = baseSize;
        CameraFollow.FocusWeight = 0f;
    }

    private Vector2 ScreenAt(Vector2 world)
    {
        if (cam == null) return Vector2.zero;
        Vector2 sp = cam.WorldToScreenPoint(world);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, sp, null, out var local);
        return local;
    }
}
