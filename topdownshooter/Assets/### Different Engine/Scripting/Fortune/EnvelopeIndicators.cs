using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// for every fortune envelope off screen, its little envelope at the screen's edge on the way to it,
// with the gold chevron beside it pointing at it, bobbing. made the first time an envelope lands
public class EnvelopeIndicators : MonoBehaviour
{
    private const float Scale = 4f;          // screen pixels per art pixel, at 1080p
    private const float Padding = 56f;

    private static EnvelopeIndicators instance;
    private RectTransform root;
    private Camera cam;
    private readonly List<(RectTransform box, Image icon, RectTransform arrow, Image arrowImage)> shown = new List<(RectTransform, Image, RectTransform, Image)>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    public static void Ensure()
    {
        if (instance != null) return;
        var go = new GameObject("Envelope Indicators", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;                  // with the HUD, under every menu
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;
        instance = go.AddComponent<EnvelopeIndicators>();
        instance.root = (RectTransform)go.transform;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        var list = FortuneEnvelope.Lying;
        int used = 0;
        if (cam != null)
        {
            Vector2 size = root.rect.size;
            foreach (var env in list)
            {
                if (env == null) continue;
                Vector3 v = cam.WorldToViewportPoint(env.transform.position);
                if (v.x >= 0f && v.x <= 1f && v.y >= 0f && v.y <= 1f) continue;

                // from the middle of the screen toward it, stopped at the edge
                Vector2 dir = new Vector2(v.x - 0.5f, v.y - 0.5f);
                Vector2 half = size * 0.5f - Vector2.one * Padding;
                float k = Mathf.Min(half.x / Mathf.Max(0.0001f, Mathf.Abs(dir.x * size.x)), half.y / Mathf.Max(0.0001f, Mathf.Abs(dir.y * size.y)));
                Vector2 at = new Vector2(dir.x * size.x, dir.y * size.y) * k;

                var slot = Get(used++);
                float bob = Mathf.Round(Mathf.Sin(Time.unscaledTime * 5f + used) * 1.5f) * Scale;
                Vector2 d = dir.normalized;
                slot.box.anchoredPosition = at - d * (Scale * 3f);
                slot.icon.rectTransform.anchoredPosition = new Vector2(0f, bob * 0.5f);
                // the chevron sits beyond the envelope, pointing out toward it
                slot.arrow.anchoredPosition = d * (Scale * 10f + bob);
                slot.arrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg + 90f);
                Animate(slot.icon, VfxLibrary.Get?.envelopeCarry, 8f);
                Animate(slot.arrowImage, VfxLibrary.Get?.envelopeMarker, 11f);
            }
        }
        for (int i = used; i < shown.Count; i++) shown[i].box.gameObject.SetActive(false);
    }

    private static void Animate(Image image, Sprite[] frames, float fps)
    {
        if (frames == null || frames.Length == 0 || frames[0] == null) return;
        image.sprite = frames[(int)(Time.unscaledTime * fps) % frames.Length];
    }

    private (RectTransform box, Image icon, RectTransform arrow, Image arrowImage) Get(int i)
    {
        while (shown.Count <= i)
        {
            var box = new GameObject("Envelope Indicator", typeof(RectTransform)).GetComponent<RectTransform>();
            box.SetParent(root, false);
            var lib = VfxLibrary.Get;
            var icon = Pixel(box, "Envelope", lib != null && lib.envelopeCarry.Length > 0 ? lib.envelopeCarry[0] : null, new Vector2(9f, 11f), new Color(0.85f, 0.15f, 0.2f));
            var arrow = Pixel(box, "Chevron", lib != null && lib.envelopeMarker.Length > 0 ? lib.envelopeMarker[0] : null, new Vector2(13f, 11f), new Color(1f, 0.85f, 0.3f));
            shown.Add((box, icon, arrow.rectTransform, arrow));
        }
        var s = shown[i];
        s.box.gameObject.SetActive(true);
        return s;
    }

    private static Image Pixel(RectTransform parent, string name, Sprite sprite, Vector2 artSize, Color placeholder)
    {
        var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        img.rectTransform.SetParent(parent, false);
        img.sprite = sprite;
        img.color = sprite != null ? Color.white : placeholder;
        img.raycastTarget = false;
        img.rectTransform.sizeDelta = (sprite != null ? sprite.rect.size : artSize) * Scale;
        return img;
    }
}
