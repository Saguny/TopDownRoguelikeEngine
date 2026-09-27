using UnityEngine;
using UnityEngine.UI;

public class ProgressBarGradient : MonoBehaviour
{
    [SerializeField] private Slider slider;
    [SerializeField] private Image fillImage;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Color leftColor = new Color(0.6f, 0.9f, 1f, 0.6f);
    [SerializeField] private Color rightColor = new Color(0.8f, 0.6f, 1f, 0.6f);
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.3f);
    [SerializeField] private int gradientWidth = 256;

    [Header("Level up")]
    [Tooltip("how fast the level up's rainbow runs along the bar, in bars per second")]
    [SerializeField] private float rainbowSpeed = 1.3f;
    [Tooltip("seconds between the shine sweeping across it")]
    [SerializeField] private float shineEvery = 0.55f;
    [Tooltip("how much taller the bar pops when it fills")]
    [SerializeField] private float pop = 0.45f;

    private Sprite gradientSprite;

    // the level up's look: a few chunky texels a bar wide and a bevel of rows, redrawn every frame
    private const int FlashWidth = 48, FlashRows = 6;
    private Texture2D flashTexture;
    private Sprite flashSprite;
    private readonly Color32[] flashPixels = new Color32[FlashWidth * FlashRows];
    private bool celebrating;
    private float celebrateClock, progress;
    private Canvas lift;

    private void Awake()
    {
        if (backgroundImage) backgroundImage.color = backgroundColor;

        if (fillImage)
        {
            gradientSprite = CreateGradientSprite(leftColor, rightColor, gradientWidth);
            fillImage.sprite = gradientSprite;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.fillAmount = 0f;
            fillImage.color = Color.white;
        }

        if (slider)
        {
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;
            slider.wholeNumbers = false;
        }
    }

    public void SetProgress01(float t)
    {
        progress = Mathf.Clamp01(t);
        if (celebrating) return;   // stays full until the level up is over
        Show(progress);
    }

    private void Show(float t)
    {
        if (slider) slider.value = t;
        if (fillImage) fillImage.fillAmount = t;
    }

    // Vampire Survivors' level up: the bar fills, pops, flashes white and runs with colour until
    // EndCelebrate. it's lifted over the level up's dimmed backdrop meanwhile, so it shows
    public void Celebrate()
    {
        if (!fillImage) return;
        if (flashTexture == null)
        {
            flashTexture = new Texture2D(FlashWidth, FlashRows, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "Level Up Bar",
            };
            flashSprite = Sprite.Create(flashTexture, new Rect(0, 0, FlashWidth, FlashRows), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }
        celebrating = true;
        celebrateClock = 0f;
        fillImage.sprite = flashSprite;
        Show(1f);
        Paint(0f);

        if (lift == null)
        {
            lift = gameObject.GetComponent<Canvas>();
            if (lift == null) lift = gameObject.AddComponent<Canvas>();
        }
        var parent = transform.parent != null ? transform.parent.GetComponentInParent<Canvas>() : null;
        lift.overrideSorting = true;
        lift.sortingOrder = (parent != null ? parent.sortingOrder : 0) + 1;
    }

    public void EndCelebrate()
    {
        if (!celebrating) return;
        celebrating = false;
        if (fillImage) fillImage.sprite = gradientSprite;
        if (lift != null) lift.overrideSorting = false;
        Bar().localScale = Vector3.one;
        Show(progress);
    }

    private void Update()
    {
        if (!celebrating) return;
        celebrateClock += Time.unscaledDeltaTime;
        Paint(celebrateClock);
    }

    private RectTransform Bar() => (RectTransform)(slider ? slider.transform : fillImage.transform);

    // the bar on screen, for things that bounce round the screen's edges (the Flying Sword): its
    // bottom is their top wall, so they don't disappear behind it
    public static ProgressBarGradient Active { get; private set; }

    private void OnEnable() => Active = this;

    private void OnDisable()
    {
        if (Active == this) Active = null;
    }

    private static readonly Vector3[] corners = new Vector3[4];

    // the lowest point of the bar as drawn, its background and fill, on screen in pixels up from
    // the bottom. not this object's own rect: that's an invisible holder in the middle of the screen
    public bool TryScreenBottom(out float y)
    {
        y = float.MaxValue;
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return false;
        var root = canvas.rootCanvas;
        var cam = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
        if (backgroundImage) Lowest(backgroundImage.rectTransform, cam, ref y);
        if (fillImage) Lowest(fillImage.rectTransform, cam, ref y);
        else if (slider) Lowest(Bar(), cam, ref y);
        return y < float.MaxValue;
    }

    private static void Lowest(RectTransform rect, Camera cam, ref float y)
    {
        if (rect == null) return;
        rect.GetWorldCorners(corners);
        foreach (var c in corners) y = Mathf.Min(y, RectTransformUtility.WorldToScreenPoint(cam, c).y);
    }

    private void Paint(float t)
    {
        // a pop taller that settles back
        float settle = Mathf.Clamp01(t / 0.3f);
        float punch = pop * (1f - settle) * (1f - settle);
        Bar().localScale = new Vector3(1f + punch * 0.06f, 1f + punch, 1f);

        // white at first, then a rainbow running along it with a shine sweeping across
        float white = 1f - Mathf.Clamp01((t - 0.08f) / 0.12f);
        float shine = (t / Mathf.Max(0.1f, shineEvery) % 1f) * (FlashWidth + 16) - 8;
        for (int x = 0; x < FlashWidth; x++)
        {
            float hue = Mathf.Repeat(x / (float)FlashWidth - t * rainbowSpeed, 1f);
            var col = Color.HSVToRGB(hue, 0.72f, 1f);
            float sweep = Mathf.Abs(x - shine) < 1.5f ? 0.75f : Mathf.Abs(x - shine) < 3f ? 0.35f : 0f;
            for (int row = 0; row < FlashRows; row++)
            {
                // a bevel: a dark bottom row, a light top one
                var c = row == 0 ? col * 0.62f : row == FlashRows - 1 ? Color.Lerp(col, Color.white, 0.55f) : row == FlashRows - 2 ? Color.Lerp(col, Color.white, 0.2f) : col;
                c = Color.Lerp(c, Color.white, Mathf.Max(white, sweep));
                c.a = 1f;
                flashPixels[row * FlashWidth + x] = c;
            }
        }
        flashTexture.SetPixels32(flashPixels);
        flashTexture.Apply(false, false);
    }

    private Sprite CreateGradientSprite(Color left, Color right, int width)
    {
        if (width < 2) width = 2;
        Texture2D tex = new Texture2D(width, 1, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        for (int x = 0; x < width; x++)
        {
            float t = x / (float)(width - 1);
            Color c = Color.Lerp(left, right, t);
            tex.SetPixel(x, 0, c);
        }

        tex.Apply(false, false);
        return Sprite.Create(tex, new Rect(0, 0, width, 1), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
    }
}
