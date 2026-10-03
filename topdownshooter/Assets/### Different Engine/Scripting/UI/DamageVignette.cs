using UnityEngine;
using UnityEngine.UI;

// being hurt, felt at the edge of the eye rather than read off a number: a hit floods the screen's
// edges red, harder the bigger it was against max health, and it ebbs over half a second. low on
// health, the edges hold a dim red that beats like a heart, faster and deeper the lower it gets.
// the vignette is pixel art like everything else: a low-resolution ring, dithered in steps,
// point filtered. it sits under every HUD panel. PlayerHealth puts one up (Ensure)
public class DamageVignette : MonoBehaviour
{
    private const int TexW = 160, TexH = 90;           // the ring, in art pixels across the screen
    private static readonly Color Blood = new Color(0.62f, 0.02f, 0.06f);

    [Tooltip("the strongest a hit flashes (share of the edges' full red)")]
    public float hitPeak = 0.7f;
    [Tooltip("the least a hit flashes, however small")]
    public float hitFloor = 0.25f;
    [Tooltip("a hit worth this share of max health flashes at the peak")]
    public float hitForPeak = 0.2f;
    [Tooltip("seconds a hit's flash takes to ebb away")]
    public float ebbSeconds = 0.5f;
    [Tooltip("below this share of health the edges hold red and beat")]
    public float lowHealth = 0.35f;
    [Tooltip("how red the edges hold at almost no health")]
    public float lowHold = 0.5f;

    private static DamageVignette instance;
    private RawImage image;
    private PlayerHealth player;
    private float flash;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    public static void Ensure(PlayerHealth health)
    {
        if (instance != null || health == null) return;
        var go = new GameObject("Damage Vignette", typeof(RectTransform), typeof(Canvas));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -6;              // under the HUD and every panel (the boss screen's dim is -5)
        instance = go.AddComponent<DamageVignette>();
        instance.player = health;

        var img = new GameObject("Edges", typeof(RectTransform), typeof(RawImage));
        var rt = (RectTransform)img.transform;
        rt.SetParent(go.transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        instance.image = img.GetComponent<RawImage>();
        instance.image.raycastTarget = false;
        instance.image.texture = Ring();
        instance.image.color = new Color(Blood.r, Blood.g, Blood.b, 0f);
        instance.image.enabled = false;
    }

    private void OnEnable() => PlayerHealth.OnPlayerDamaged += Hurt;
    private void OnDisable() => PlayerHealth.OnPlayerDamaged -= Hurt;

    private void OnDestroy()
    {
        if (image != null && image.texture != null) Destroy(image.texture);
        if (instance == this) instance = null;
    }

    private void Hurt(float amount, float healthNow)
    {
        if (player == null || amount <= 0f) return;
        float share = amount / Mathf.Max(1f, player.Max);
        float k = Mathf.Lerp(hitFloor, hitPeak, Mathf.Clamp01(share / hitForPeak));
        flash = Mathf.Max(flash, k);
    }

    private void Update()
    {
        if (image == null) return;
        float dt = Time.unscaledDeltaTime;
        flash = Mathf.MoveTowards(flash, 0f, dt / Mathf.Max(0.05f, ebbSeconds) * Mathf.Max(0.3f, flash));

        // low on health: a dim hold that beats, lub-dub, quicker and deeper the closer to nothing
        float hold = 0f;
        if (player != null && !player.IsDead && player.Max > 0f)
        {
            float share = player.Current / player.Max;
            if (share < lowHealth)
            {
                float urgency = 1f - share / lowHealth;
                float rate = Mathf.Lerp(0.9f, 1.6f, urgency);
                float phase = Time.unscaledTime * rate % 1f;
                float beat = Mathf.Max(Pulse(phase, 0f), 0.6f * Pulse(phase, 0.22f));
                hold = lowHold * urgency * (0.55f + 0.45f * beat);
            }
        }

        float a = Mathf.Clamp01(Mathf.Max(flash, hold));
        image.enabled = a > 0.005f;
        if (image.enabled) image.color = new Color(Blood.r, Blood.g, Blood.b, a);
    }

    // one beat: a quick swell and a slower fall, starting at `at` in the cycle
    private static float Pulse(float phase, float at)
    {
        float x = phase - at;
        if (x < 0f) x += 1f;
        return x < 0.06f ? x / 0.06f : Mathf.Exp(-(x - 0.06f) * 9f);
    }

    // the ring: clear in the middle, red toward the edges, in four dithered steps
    private static Texture2D Ring()
    {
        var tex = new Texture2D(TexW, TexH, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Damage Vignette",
        };
        var px = new Color32[TexW * TexH];
        int[] bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
        for (int y = 0; y < TexH; y++)
            for (int x = 0; x < TexW; x++)
            {
                // distance from the middle as an ellipse the shape of the screen, 1 at the corners' edges
                float dx = (x + 0.5f) / TexW * 2f - 1f, dy = (y + 0.5f) / TexH * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx * 0.82f + dy * dy * 0.9f);
                float v = Mathf.Clamp01((d - 0.7f) / 0.45f);
                v = v * v;
                // dithered into steps, so the edge reads as pixels, not a smear
                float threshold = (bayer[(y & 3) * 4 + (x & 3)] + 0.5f) / 16f;
                float stepped = Mathf.Floor(v * 4f + threshold) / 4f;
                px[y * TexW + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(stepped) * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return tex;
    }
}
