using UnityEngine;

// the cut-in's frames (PersonaCutIn), painted pixel by pixel into one 480x270 buffer: the tear, the
// band opening, the eyes in it with its torn edges and wedge of colour, the character brushed on,
// the flash, the band sliced apart and its shards flying off. no Unity objects: only colours and
// sums, so it can be run and looked at outside the game
public class CutInPainter
{
    public const int W = 480, H = 270;          // the frame, in art pixels (4x at 1080p)
    public const int FW = 480, FH = 150;        // the eyes, in band space
    private const float Tilt = 7f;              // degrees the band leans, rising to the right
    private const float Thick = 50f;            // the band's half height at its widest
    public const float Tear = 0.16f, Open = 0.08f, Hold = 0.62f, Shatter = 0.84f, End = 1f;

    public readonly Color32[] Pixels = new Color32[W * H];
    private readonly bool[] ink = new bool[W * H];
    private Color32[] px => Pixels;

    // ---------------------------------------------------------------- one frame

    private static float Hash(int x, int y) { unchecked { uint h = (uint)(x * 374761393 + y * 668265263); h = (h ^ (h >> 13)) * 1274126177u; return (h & 0xffff) / 65535f; } }
    private static float Ease(float k) => 1f - (1f - k) * (1f - k) * (1f - k);

    // the band's half height along its length: widest just right of centre, thinning to threads at the edges
    private static float Half(float u) { float s = (u - 30f) / (W * 0.62f); return Thick * Mathf.Max(0.06f, 1f - Mathf.Pow(Mathf.Abs(s), 2.2f)); }
    // the torn edge: a ragged few pixels, changing as the edge boils
    private static float Rag(float u, int side, int boil) => 2.5f + 4f * Hash(Mathf.FloorToInt(u / 3f) + boil * 131, side * 977 + boil);

    public void Draw(PersonaCutIn.Glyph glyph, Color wedgeColor, Color wedgeDarkColor, Color32[] face, float time)
    {
        System.Array.Clear(px, 0, px.Length);
        float cx = W * 0.5f, cy = H * 0.5f;
        float c = Mathf.Cos(Tilt * Mathf.Deg2Rad), s = Mathf.Sin(Tilt * Mathf.Deg2Rad);
        int boil = Mathf.FloorToInt(time / 0.066f);
        var white = new Color32(255, 255, 255, 255);
        var black = new Color32(14, 8, 14, 255);
        Color32 wedge = wedgeColor, wedgeDark = wedgeDarkColor;

        bool tearing = time < Tear, opening = time >= Tear && time < Tear + Open, shattering = time >= Hold;
        float kTear = Ease(Mathf.Clamp01(time / Tear));
        float kOpen = Ease(Mathf.Clamp01((time - Tear) / Open));
        float kHold = Mathf.Clamp01((time - Tear - Open) / (Hold - Tear - Open));
        float kShat = Mathf.Clamp01((time - Hold) / (Shatter - Hold));
        float kEnd = Mathf.Clamp01((time - Shatter) / (End - Shatter));
        // the push-in: the face grows a little and drifts left while it's held
        float zoom = 1f + 0.06f * kHold, drift = -10f * kHold;

        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                // band space: u along it, v across it (up positive)
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                float u = dx * c + dy * s, v = -dx * s + dy * c;
                Color32 col = default;

                if (tearing)
                {
                    // the tear: a ragged white thread racing in from the left, flecks off its head
                    float head = Mathf.Lerp(-W * 0.62f, W * 0.18f, kTear);
                    if (u < head)
                    {
                        float th = 0.8f + 1.6f * Hash(Mathf.FloorToInt(u / 2f), boil) * Mathf.Clamp01((head - u) / 40f + 0.3f);
                        if (Mathf.Abs(v + Mathf.Sin(u * 0.05f) * 1.5f) < th) col = white;
                        else if (Mathf.Abs(v) < th + 1.2f) col = black;
                    }
                    else if (u < head + 18f && Hash(x, y + boil * 7) > 0.985f && Mathf.Abs(v) < 8f) col = white;
                }
                else if (opening)
                {
                    // the tear splitting open: the band's shape in flat white, swelling
                    float h = Half(u) * kOpen + 1.5f;
                    float top = h - Rag(u, 1, boil) * kOpen, bot = h - Rag(u, -1, boil) * kOpen;
                    if (v < top && v > -bot) col = white;
                    else if (v < top + 1.5f && v > -bot - 1.5f) col = black;
                }
                else if (!shattering || kShat < 1f)
                {
                    col = Band(face, u, v, boil, zoom, drift, shattering ? kShat : 0f, x, y, white, black, wedge, wedgeDark);
                }
                px[y * W + x] = col;
            }

        // the debris: shards of the band flying off to the top right, thinning out
        if (shattering)
        {
            var rng = new System.Random(4242);
            float k = Mathf.Clamp01((time - Hold) / (End - Hold));
            for (int i = 0; i < 46; i++)
            {
                float sx = (float)rng.NextDouble() * W * 0.8f, sy = cy + ((float)rng.NextDouble() - 0.5f) * 90f;
                float vx = 140f + (float)rng.NextDouble() * 260f, vy = 60f + (float)rng.NextDouble() * 200f;
                float x0 = sx + vx * k * 1.3f, y0 = sy + vy * k * 1.3f;
                int size = 2 + (int)(rng.NextDouble() * 5f * (1f - k));
                var sc = i % 3 == 0 ? wedge : i % 3 == 1 ? white : black;
                if (k > 0.25f + (float)rng.NextDouble() * 0.9f || size <= 0) continue;
                for (int j = 0; j < size; j++)
                    for (int q = 0; q <= size - j; q++)
                        Put((int)x0 + q, (int)y0 + j, sc);
            }
        }

        // the character, written stroke by stroke beside the eyes, breaking up as the band goes
        if (time >= Tear + Open)
        {
            float kInk = Mathf.Clamp01((time - Tear - Open) / 0.16f);
            float fade = shattering ? kShat : 0f;
            DrawGlyph(glyph, kInk, fade, W * 0.8f, H * 0.56f, 118f, white, black, boil);
        }

        // the flash as the eyes land: the whole screen washed white, fading
        float flash = time < Tear + Open ? 0f : Mathf.Clamp01(1f - (time - Tear - Open - 0.03f) / 0.16f) * 0.6f;
        if (flash > 0f)
        {
            byte fa = (byte)(flash * 255f);
            for (int i = 0; i < px.Length; i++)
            {
                var p = px[i];
                if (p.a == 0) { px[i] = new Color32(255, 255, 255, fa); continue; }
                px[i] = Color32.Lerp(p, new Color32(255, 255, 255, 255), flash);
            }
        }
        // and at the very end, what's left fades
        if (kEnd > 0f)
        {
            float keep = 1f - kEnd;
            for (int i = 0; i < px.Length; i++) if (px[i].a > 0) px[i].a = (byte)(px[i].a * keep);
        }
    }

    private void Put(int x, int y, Color32 c)
    {
        if (x < 0 || y < 0 || x >= W || y >= H) return;
        px[y * W + x] = c;
    }

    // the band at one point in band space: the eyes inside, a black line inside the torn edge and
    // a white one outside it with flecks breaking off, the wedge of colour to the left and a sliver
    // of it at the right. shattering: the band sliced into strips that slide apart and shred
    private Color32 Band(Color32[] face, float u, float v, int boil, float zoom, float drift, float k,
        int x, int y, Color32 white, Color32 black, Color32 wedge, Color32 wedgeDark)
    {
        float su = u, sv = v;
        if (k > 0f)
        {
            // which strip this pixel comes from: five strips across the band, each sliding its own way
            int strips = 5;
            for (int i = 0; i < strips; i++)
            {
                float dir = i % 2 == 0 ? 1f : -1f;
                float du = dir * Mathf.Pow(k, 1.5f) * W * 0.45f + k * W * 0.18f;
                float dv = k * (20f + i * 9f);
                float ou = u - du, ov = v - dv;
                float edge0 = -Thick + (2f * Thick / strips) * i, edge1 = edge0 + 2f * Thick / strips;
                float jag = (Hash(Mathf.FloorToInt(ou / 4f), i * 31) - 0.5f) * 8f;
                if (ov >= edge0 + jag && ov < edge1 + jag)
                {
                    // the strip shreds as it goes: bits of it drop away
                    if (Hash(x / 2, y / 2 + i * 17) < k * 1.15f - 0.25f) return default;
                    su = ou; sv = ov;
                    goto found;
                }
            }
            return default;
        }
        found:
        float h = Half(su);
        float top = h - Rag(su, 1, boil), bot = h - Rag(su, -1, boil);
        // outside the band: the white torn edge, and flecks of it breaking off
        if (sv >= top || sv <= -bot)
        {
            float outTop = sv - top, outBot = -bot - sv;
            float over = sv >= top ? outTop : outBot;
            float rag = 2f + 3f * Hash(Mathf.FloorToInt(su / 2f) + boil * 57, sv > 0 ? 3 : 5);
            if (over < rag) return white;
            if (over < rag + 1.2f) return black;
            if (over < 14f && Hash(Mathf.FloorToInt(su), Mathf.FloorToInt(sv) + boil * 13) > 0.975f) return white;
            return default;
        }
        // a black line just inside the tear
        if (sv > top - 1.6f || sv < -bot + 1.6f) return black;
        // the wedge of colour: most of the left, cut on a slant; a sliver at the right end
        float cutL = -W * 0.2f + sv * 0.9f, cutR = W * 0.43f - sv * 0.6f;
        if (su < cutL - 1.5f || su > cutR + 1.5f)
            return ((int)(su * 0.35f + sv) & 7) == 0 ? wedgeDark : wedge;
        if (su < cutL || su > cutR) return black;
        // the eyes, pushed in and drifting as they're held
        if (face == null) return new Color32(30, 20, 30, 255);
        int fx = Mathf.FloorToInt((su - drift) / zoom + FW * 0.5f);
        int fy = Mathf.FloorToInt(sv / zoom + FH * 0.5f);
        if (fx < 0 || fy < 0 || fx >= FW || fy >= FH) return black;
        var p = face[fy * FW + fx];
        p.a = 255;
        return p;
    }

    // ---------------------------------------------------------------- the character

    // strokes in a unit box, top left (0,0), each a polyline with its brush width at start and end
    private static readonly float[][] Judge =
    {
        new[] { 0.10f, 0.10f, 0.17f, 0.24f, 0.20f, 0.12f },                    // 判: the two dots of 丷
        new[] { 0.44f, 0.08f, 0.36f, 0.24f, 0.18f, 0.10f },
        new[] { 0.04f, 0.36f, 0.50f, 0.33f, 0.14f, 0.12f },                    // the two strokes across
        new[] { 0.00f, 0.56f, 0.54f, 0.53f, 0.16f, 0.12f },
        new[] { 0.27f, 0.04f, 0.27f, 0.70f, 0.12f, 0.98f, 0.18f, 0.06f },      // down, sweeping away left
        new[] { 0.68f, 0.16f, 0.68f, 0.64f, 0.14f, 0.08f },                    // 刂
        new[] { 0.93f, 0.02f, 0.93f, 0.94f, 0.84f, 0.86f, 0.18f, 0.08f },
    };
    private static readonly float[][] Forget =
    {
        new[] { 0.46f, 0.00f, 0.54f, 0.10f, 0.18f, 0.12f },                    // 忘: 亡's dot
        new[] { 0.12f, 0.20f, 0.88f, 0.18f, 0.14f, 0.12f },                    // across
        new[] { 0.26f, 0.22f, 0.26f, 0.50f, 0.92f, 0.49f, 0.14f, 0.12f },      // down and along
        new[] { 0.08f, 0.70f, 0.16f, 0.86f, 0.18f, 0.12f },                    // 心: the dot to the left
        new[] { 0.28f, 0.60f, 0.30f, 0.94f, 0.78f, 0.92f, 0.84f, 0.80f, 0.14f, 0.08f },
        new[] { 0.50f, 0.62f, 0.58f, 0.76f, 0.16f, 0.10f },
        new[] { 0.78f, 0.62f, 0.92f, 0.78f, 0.16f, 0.10f },
    };

    private void DrawGlyph(PersonaCutIn.Glyph glyph, float kInk, float fade, float gx, float gy, float size, Color32 white, Color32 black, int boil)
    {
        var strokes = glyph == PersonaCutIn.Glyph.Judge ? Judge : Forget;
        System.Array.Clear(ink, 0, ink.Length);
        int n = strokes.Length;
        float written = kInk * n;                      // strokes done, and how far into the next
        float left = gx - size * 0.5f, topY = gy + size * 0.5f;
        for (int si = 0; si < n && si < written; si++)
        {
            var st = strokes[si];
            int pts = (st.Length - 2) / 2;
            float w0 = st[st.Length - 2] * size * 0.55f, w1 = st[st.Length - 1] * size * 0.55f;
            float part = Mathf.Clamp01(written - si);
            // walk the polyline, stamping the brush, thinning toward the stroke's end
            int steps = 60;
            for (int k = 0; k <= steps * part; k++)
            {
                float t = k / (float)steps, seg = t * (pts - 1);
                int i = Mathf.Min(pts - 2, Mathf.FloorToInt(seg));
                float f = seg - i;
                float ux = Mathf.Lerp(st[i * 2], st[i * 2 + 2], f), uy = Mathf.Lerp(st[i * 2 + 1], st[i * 2 + 3], f);
                float r = Mathf.Lerp(w0, w1, t) * 0.5f * (0.85f + 0.3f * Hash(k, si));
                float px0 = left + ux * size, py0 = topY - uy * size;
                for (int yy = Mathf.FloorToInt(py0 - r); yy <= py0 + r; yy++)
                    for (int xx = Mathf.FloorToInt(px0 - r); xx <= px0 + r; xx++)
                    {
                        if (xx < 0 || yy < 0 || xx >= W || yy >= H) continue;
                        if ((xx + 0.5f - px0) * (xx + 0.5f - px0) + (yy + 0.5f - py0) * (yy + 0.5f - py0) > r * r) continue;
                        // dry brush: a few hairs of the stroke's tail left bare
                        if (t > 0.7f && Hash(xx, yy) < (t - 0.7f) * 1.2f) continue;
                        ink[yy * W + xx] = true;
                    }
            }
        }
        // white brushwork in a black outline, shredding away with the band
        for (int y = 1; y < H - 1; y++)
            for (int x = 1; x < W - 1; x++)
            {
                int i = y * W + x;
                if (fade > 0f && Hash(x / 2 + 5, y / 2) < fade * 1.2f - 0.2f) continue;
                if (ink[i]) { px[i] = white; continue; }
                if (ink[i - 1] || ink[i + 1] || ink[i - W] || ink[i + W] || ink[i - W - 1] || ink[i + W + 1]) px[i] = black;
            }
    }
}
