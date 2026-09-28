using UnityEngine;

// a thin health bar over a big enemy's head, in the world's pixels, the way the Magistrate has one:
// a dark frame, a dark red back and the fill. Huangquan Road's guardians wear one
[RequireComponent(typeof(EnemyHealth))]
public class WorldHealthBar : MonoBehaviour
{
    public float width = 2.2f;
    [Tooltip("how high over its position the bar sits, world units")]
    public float height = 2.4f;
    public Color fill = new Color32(0xd0, 0x28, 0x38, 0xff);

    private const float PixelSize = 1.3f / 37f;
    private static Sprite pixel;
    private EnemyHealth health;
    private SpriteRenderer bar;
    private float shown = -1f;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        if (pixel == null)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.DontSave };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            pixel = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            pixel.hideFlags = HideFlags.DontSave;
        }
        Make("Bar Frame", new Color32(0x17, 0x11, 0x1d, 0xff), width + 2f * PixelSize, 5f * PixelSize, 40);
        Make("Bar Back", new Color32(0x3e, 0x08, 0x12, 0xff), width, 3f * PixelSize, 41);
        bar = Make("Bar Fill", fill, width, 3f * PixelSize, 42);
    }

    private SpriteRenderer Make(string label, Color color, float w, float h, int order)
    {
        var go = new GameObject(label);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, height, 0f);
        go.transform.localScale = new Vector3(w, h, 1f);
        var s = go.AddComponent<SpriteRenderer>();
        s.sprite = pixel;
        s.color = color;
        s.sortingLayerName = "Default";
        s.sortingOrder = order;
        return s;
    }

    private void LateUpdate()
    {
        float k = health.Max > 0f ? Mathf.Clamp01(health.Current / health.Max) : 0f;
        if (Mathf.Approximately(k, shown)) return;
        shown = k;
        bar.transform.localScale = new Vector3(width * k, 3f * PixelSize, 1f);
        bar.transform.localPosition = new Vector3(-width * (1f - k) * 0.5f, height, 0f);
    }
}
