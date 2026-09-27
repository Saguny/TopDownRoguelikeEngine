using UnityEngine;
using UnityEngine.Rendering;

// damage numbers in a pixel font: bold 6x8 digits with a dark outline, drawn at whole multiples of the
// world's pixel so they stay crisp, every number on screen in one mesh. a number pops in, drifts
// up and fades. Juice.Number is the way in
public class PixelNumbers : MonoBehaviour
{
    private const int GlyphW = 6, GlyphH = 8, CellW = GlyphW + 2, CellH = GlyphH + 2, Advance = GlyphW + 1;
    private const float WorldPixel = 1.3f / 37f;
    private const int Max = 320;

    // 0-9 then !, bold: strokes two pixels wide, so they read at a glance over a busy screen
    private static readonly string[][] Glyphs =
    {
        new[] { ".####.", "##..##", "##..##", "##.###", "###.##", "##..##", "##..##", ".####." },
        new[] { "..##..", ".###..", "####..", "..##..", "..##..", "..##..", "..##..", "######" },
        new[] { ".####.", "##..##", "....##", "...##.", "..##..", ".##...", "##....", "######" },
        new[] { ".####.", "##..##", "....##", "..###.", "....##", "....##", "##..##", ".####." },
        new[] { "...##.", "..###.", ".####.", "##.##.", "##.##.", "######", "...##.", "...##." },
        new[] { "######", "##....", "#####.", "....##", "....##", "....##", "##..##", ".####." },
        new[] { ".####.", "##....", "##....", "#####.", "##..##", "##..##", "##..##", ".####." },
        new[] { "######", "....##", "...##.", "...##.", "..##..", "..##..", ".##...", ".##..." },
        new[] { ".####.", "##..##", "##..##", ".####.", "##..##", "##..##", "##..##", ".####." },
        new[] { ".####.", "##..##", "##..##", "##..##", ".#####", "....##", "...##.", ".###.." },
        new[] { "..##..", "..##..", "..##..", "..##..", "..##..", "......", "..##..", "..##.." },
    };

    private struct Number
    {
        public Vector2 pos, drift;
        public float age, life;
        public int value, size;
        public bool crit;
        public Color32 color;
    }

    private static PixelNumbers instance;
    private readonly Number[] numbers = new Number[Max];
    private int count;
    private Mesh mesh;
    private Vector3[] verts = new Vector3[Max * 6 * 4];
    private Vector2[] uvs = new Vector2[Max * 6 * 4];
    private Color32[] colors = new Color32[Max * 6 * 4];
    private int[] tris = new int[Max * 6 * 6];
    private readonly int[] digits = new int[8];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    // a number rising from a point. size is how many world pixels each font pixel covers
    public static void Show(Vector3 at, int value, bool crit, Color color, int size)
    {
        if (!Application.isPlaying) return;
        if (instance == null)
        {
            var go = new GameObject("Pixel Numbers");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<PixelNumbers>();
        }
        instance.Add(at, value, crit, color, size);
    }

    private void Awake()
    {
        var shader = Shader.Find("Rogue/Sprite Batch");
        mesh = new Mesh { name = "Pixel Numbers", indexFormat = IndexFormat.UInt32 };
        mesh.MarkDynamic();
        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = gameObject.AddComponent<MeshRenderer>();
        if (shader != null) mr.sharedMaterial = new Material(shader) { mainTexture = Atlas(), name = "Pixel Numbers" };
        mr.sortingLayerName = "Default";
        mr.sortingOrder = 1000;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;

        for (int q = 0; q < Max * 6; q++)
        {
            int o = q * 4, t = q * 6;
            tris[t] = o; tris[t + 1] = o + 1; tris[t + 2] = o + 2;
            tris[t + 3] = o; tris[t + 4] = o + 2; tris[t + 5] = o + 3;
        }
    }

    // the font: white glyphs in a dark plum outline, side by side in one strip
    private static Texture2D Atlas()
    {
        int w = CellW * Glyphs.Length;
        var tex = new Texture2D(w, CellH, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[w * CellH];
        var fill = new Color32(255, 255, 255, 255);
        var line = new Color32(0x1a, 0x0c, 0x26, 255);
        bool On(int g, int x, int y) => x >= 0 && y >= 0 && x < GlyphW && y < GlyphH && Glyphs[g][y][x] == '#';
        for (int g = 0; g < Glyphs.Length; g++)
            for (int cy = 0; cy < CellH; cy++)
                for (int cx = 0; cx < CellW; cx++)
                {
                    int gx = cx - 1, gy = cy - 1;
                    var c = new Color32(0, 0, 0, 0);
                    if (On(g, gx, gy)) c = fill;
                    else if (On(g, gx - 1, gy) || On(g, gx + 1, gy) || On(g, gx, gy - 1) || On(g, gx, gy + 1)) c = line;
                    px[(CellH - 1 - cy) * w + g * CellW + cx] = c;     // the strip's rows run bottom up
                }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    private void Add(Vector3 at, int value, bool crit, Color color, int size)
    {
        int i = count < Max ? count++ : Oldest();
        numbers[i] = new Number
        {
            pos = at,
            drift = new Vector2(Random.Range(-0.5f, 0.5f), Random.Range(2.2f, 2.8f)),
            age = 0f,
            life = crit ? 0.7f : 0.5f,
            value = Mathf.Max(0, value),
            size = Mathf.Max(1, size),
            crit = crit,
            color = color,
        };
    }

    private int Oldest()
    {
        int best = 0;
        for (int i = 1; i < count; i++) if (numbers[i].age > numbers[best].age) best = i;
        return best;
    }

    private void LateUpdate()
    {
        // unscaled, so numbers keep rising through a hit stop
        float dt = Time.unscaledDeltaTime;
        int quads = 0;
        for (int i = count - 1; i >= 0; i--)
        {
            ref var n = ref numbers[i];
            n.age += dt;
            if (n.age >= n.life)
            {
                numbers[i] = numbers[--count];
                continue;
            }
            float k = n.age / n.life;
            n.pos += n.drift * dt * (1f - k);
            quads = Emit(ref n, k, quads);
        }

        const MeshUpdateFlags flags = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices;
        mesh.Clear(true);
        if (quads > 0)
        {
            mesh.SetVertices(verts, 0, quads * 4, flags);
            mesh.SetUVs(0, uvs, 0, quads * 4, flags);
            mesh.SetColors(colors, 0, quads * 4, flags);
            mesh.SetIndices(tris, 0, quads * 6, MeshTopology.Triangles, 0, false);
        }
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(100000f, 100000f, 1000f));
    }

    private int Emit(ref Number n, float k, int quads)
    {
        int len = 0, v = n.value;
        do { digits[len++] = v % 10; v /= 10; } while (v > 0 && len < 7);
        int glyphs = len + (n.crit ? 1 : 0);

        // a pop on arrival, then its own size; fading over the last part of its life
        float pop = 1f + 0.55f * Mathf.Clamp01(1f - n.age / 0.08f);
        float px = WorldPixel * n.size * pop;
        byte alpha = (byte)(255f * Mathf.Clamp01((1f - k) / 0.4f));
        var col = new Color32(n.color.r, n.color.g, n.color.b, alpha);

        float width = (glyphs * Advance + 1) * px;
        float x0 = n.pos.x - width * 0.5f, y0 = n.pos.y - CellH * px * 0.5f;
        float atlasW = CellW * Glyphs.Length;
        for (int g = 0; g < glyphs; g++)
        {
            if (quads >= Max * 6) return quads;
            int glyph = g < len ? digits[len - 1 - g] : 10;
            float x = x0 + g * Advance * px;
            int o = quads * 4;
            verts[o] = new Vector3(x, y0, 0f);
            verts[o + 1] = new Vector3(x, y0 + CellH * px, 0f);
            verts[o + 2] = new Vector3(x + CellW * px, y0 + CellH * px, 0f);
            verts[o + 3] = new Vector3(x + CellW * px, y0, 0f);
            float u0 = glyph * CellW / atlasW, u1 = (glyph + 1) * CellW / atlasW;
            uvs[o] = new Vector2(u0, 0f);
            uvs[o + 1] = new Vector2(u0, 1f);
            uvs[o + 2] = new Vector2(u1, 1f);
            uvs[o + 3] = new Vector2(u1, 0f);
            colors[o] = colors[o + 1] = colors[o + 2] = colors[o + 3] = col;
            quads++;
        }
        return quads;
    }
}
