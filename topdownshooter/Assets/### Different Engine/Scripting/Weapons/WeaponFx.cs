using UnityEngine;

// sprite helpers for weapons, plus plain placeholder shapes so a weapon works before its art exists
public static class WeaponFx
{
    private static Sprite disc, ring, square;

    public static Sprite Disc => disc != null ? disc : (disc = Circle(false));
    public static Sprite Ring => ring != null ? ring : (ring = Circle(true));
    public static Sprite Square => square != null ? square : (square = Box());

    // a sprite object under parent. real art shows as drawn; a placeholder takes the colour
    public static SpriteRenderer Make(Transform parent, string name, Sprite art, Sprite placeholder,
        Color placeholderColor, string sortingLayer, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = art != null ? art : placeholder;
        sr.color = art != null ? Color.white : placeholderColor;
        sr.sortingLayerName = sortingLayer;
        sr.sortingOrder = sortingOrder;
        if (PlayerShots.UnderRoot(parent)) PlayerShots.Tag(sr);
        return sr;
    }

    // scales the object so the sprite's longest side is this many world units
    public static void Resize(SpriteRenderer sr, float worldSize)
    {
        if (sr == null || sr.sprite == null) return;

        var size = sr.sprite.bounds.size;
        float longest = Mathf.Max(size.x, size.y);
        if (longest > 0f) sr.transform.localScale = Vector3.one * (worldSize / longest);
    }

    public static void SetAlpha(SpriteRenderer sr, float alpha)
    {
        var c = sr.color;
        c.a = alpha;
        sr.color = c;
    }

    // a pixel laser: one column of colours across the beam, one pixel per world unit, pivot on the
    // left edge. stretch it along x to the beam's length and scale y by the width of a pixel row
    public static Sprite Beam(params Color32[] across)
    {
        var tex = new Texture2D(1, across.Length, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave,
        };
        tex.SetPixels32(across);
        tex.Apply();

        var sprite = Sprite.Create(tex, new Rect(0, 0, 1, across.Length), new Vector2(0f, 0.5f), 1f);
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }

    private static Sprite Circle(bool hollow)
    {
        const int size = 64;
        float centre = (size - 1) * 0.5f, outer = size * 0.5f - 0.5f, inner = outer - 6f;

        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(centre, centre));
                float a = Mathf.Clamp01(outer - d);
                if (hollow) a *= Mathf.Clamp01(d - inner);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        return ToSprite(pixels, size);
    }

    private static Sprite Box()
    {
        const int size = 16;
        var pixels = new Color32[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
        return ToSprite(pixels, size);
    }

    // one world unit across, and kept alive across scene loads
    private static Sprite ToSprite(Color32[] pixels, int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave,
        };
        tex.SetPixels32(pixels);
        tex.Apply();

        var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }
}

// a fading row of copies left behind a moving sprite: a trail that stays crisp pixel art. call
// Record after moving the sprite each frame; a copy is dropped every `spacing` world units
public sealed class Afterimage
{
    private readonly SpriteRenderer source;
    private readonly SpriteRenderer[] copies;
    private readonly Vector3[] points;
    private readonly Quaternion[] turns;
    private readonly float spacing, startAlpha, endScale;
    private int count;
    private Vector3 last;
    private Quaternion lastTurn;

    public Afterimage(SpriteRenderer source, int length, float spacing, float startAlpha = 0.6f, float endScale = 0.6f)
    {
        this.source = source;
        this.spacing = Mathf.Max(0.01f, spacing);
        this.startAlpha = startAlpha;
        this.endScale = endScale;
        copies = new SpriteRenderer[Mathf.Max(0, length)];
        points = new Vector3[copies.Length];
        turns = new Quaternion[copies.Length];

        for (int i = 0; i < copies.Length; i++)
        {
            var go = new GameObject("Trail");
            go.transform.SetParent(source.transform.parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerID = source.sortingLayerID;
            sr.sortingOrder = source.sortingOrder - 1;
            sr.sharedMaterial = source.sharedMaterial;      // a faded shot's trail fades with it
            sr.enabled = false;
            copies[i] = sr;
        }
        Restart();
    }

    // forgets the old trail, e.g. when a pooled sprite is launched again from somewhere else
    public void Restart()
    {
        count = 0;
        last = source.transform.position;
        lastTurn = source.transform.rotation;
        foreach (var c in copies) c.enabled = false;
    }

    public void Hide()
    {
        count = 0;
        foreach (var c in copies) c.enabled = false;
    }

    public void Record()
    {
        var t = source.transform;
        if ((t.position - last).sqrMagnitude >= spacing * spacing)
        {
            for (int i = points.Length - 1; i > 0; i--)
            {
                points[i] = points[i - 1];
                turns[i] = turns[i - 1];
            }
            if (points.Length > 0)
            {
                points[0] = last;
                turns[0] = lastTurn;
            }
            last = t.position;
            lastTurn = t.rotation;
            count = Mathf.Min(points.Length, count + 1);
        }

        for (int i = 0; i < copies.Length; i++)
        {
            var c = copies[i];
            bool on = i < count && source.enabled && source.gameObject.activeInHierarchy;
            c.enabled = on;
            if (!on) continue;

            float k = 1f - (i + 1f) / (copies.Length + 1f);     // 1 near the sprite, 0 at the tail
            c.sprite = source.sprite;
            c.transform.SetPositionAndRotation(points[i], turns[i]);
            c.transform.localScale = t.localScale * Mathf.Lerp(endScale, 1f, k);
            var col = source.color;
            col.a *= startAlpha * k;
            c.color = col;
        }
    }
}
