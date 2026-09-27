using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// a giant calligraphy brush floats at the player's heels and paints burning cinnabar on the ground
// wherever they walk. the ink burns whatever stands in it, dries as it ages and fades away. the
// stroke is blots stamped along the way the brush went, every Spacing, turned along the stroke and
// pressed harder and lighter; all of them are one mesh, every blot's rim first and every blot over
// the rims, so only the outside of the stroke is edged. evolved (the Calligraphic Seal Grid),
// closing a loop with the ink stamps a seal in it: everything inside is wiped out in a ripple of
// ink blasts, bosses and elites take a heavy blow, and the ink of the loop is used up
public class CinnabarInkBrush : Weapon<CinnabarInkBrushData>
{
    private struct Blot
    {
        public Vector2 at;
        public float angle, born, press;
        public int shape;
    }

    private struct Pending
    {
        public Vector2 at;
        public float when, scale;
    }

    private const int MaxBlots = 600;

    private readonly List<Blot> trail = new List<Blot>(256);    // oldest first
    private readonly List<Pending> blasts = new List<Pending>();
    private readonly HashSet<EnemyHealth> burnt = new HashSet<EnemyHealth>();
    private readonly List<EnemyHealth> touching = new List<EnemyHealth>();
    private readonly List<GameObject> snapshot = new List<GameObject>(256);
    private readonly List<Vector2> loop = new List<Vector2>(128);

    private SpriteRenderer brush;
    private Vector2 tip, lastPaint, heading = Vector2.right, lastPos;
    private bool started;
    private float tickTimer, flameTimer, loopReady, lean;
    private int painted;

    private Mesh mesh;
    private Vector3[] verts = new Vector3[0];
    private Vector2[] uvs = new Vector2[0];
    private Color32[] colors = new Color32[0];
    private int[] tris = new int[0];

    private void Update()
    {
        if (Data == null || Level <= 0) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return; // paused

        var lv = Data.At(Level);
        float now = Time.time, life = Mathf.Max(0.1f, lv.seconds);
        Vector2 me = transform.position;

        if (!started)
        {
            started = true;
            lastPos = me;
            tip = me + new Vector2(0f, -Data.feetBelow);
            lastPaint = tip;
        }

        // which way the player is going, eased so the brush doesn't twitch
        Vector2 moved = me - lastPos;
        lastPos = me;
        if (moved.sqrMagnitude > 0.000001f) heading = Vector2.Lerp(heading, moved.normalized, 1f - Mathf.Exp(-10f * dt)).normalized;
        bool walking = moved.sqrMagnitude > 0.0004f * dt;

        // the brush's tip trails behind the player's feet, catching up with a little lag
        Vector2 target = me + new Vector2(0f, -Data.feetBelow) - heading * Data.trailBehind;
        if ((target - tip).sqrMagnitude > 25f) { tip = target; lastPaint = tip; }   // teleported
        tip = Vector2.Lerp(tip, target, 1f - Mathf.Exp(-Data.brushFollow * dt));

        Paint(now, lv);
        while (trail.Count > 0 && now - trail[0].born > life) trail.RemoveAt(0);

        Burn(dt, lv);
        Flames(dt, now, life);
        Ripple(now);
        PlaceBrush(dt, now, walking);
        Draw(now, life, lv.width * AreaMul);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (mesh != null) Destroy(mesh);
    }

    // ---------------------------------------------------------------- painting

    // blots every Spacing along the way the tip went, so a fast dash leaves no gaps
    private void Paint(float now, CinnabarInkBrushData.LevelStats lv)
    {
        float spacing = Data.spacing;
        Vector2 to = tip - lastPaint;
        float dist = to.magnitude;
        if (dist < spacing) return;

        Vector2 dir = to / dist;
        float angle = Mathf.Atan2(dir.y, dir.x);
        while (dist >= spacing)
        {
            lastPaint += dir * spacing;
            dist -= spacing;
            painted++;
            if (trail.Count >= MaxBlots) trail.RemoveAt(0);
            trail.Add(new Blot
            {
                at = lastPaint,
                angle = angle,
                born = now,
                // the brush pressing harder and lighter as it goes
                press = 1f + Data.pressure * (Mathf.PerlinNoise(painted * 0.13f, 7.3f) - 0.5f) * 2f,
                shape = Random.Range(0, 4),
            });
            if (Data.IsEvolved(Level)) CloseLoop(now);
        }
    }

    // ---------------------------------------------------------------- burning

    // every tick, everything standing on the ink burns once
    private void Burn(float dt, CinnabarInkBrushData.LevelStats lv)
    {
        tickTimer += dt;
        if (tickTimer < Data.tickSeconds) return;
        tickTimer -= Data.tickSeconds;
        if (trail.Count == 0) return;

        burnt.Clear();
        float damage = lv.damage * Might, radius = lv.width * AreaMul * 0.5f;
        for (int i = 0; i < trail.Count; i += 2)   // the blots overlap by more than half; every other one covers the stroke
        {
            EnemiesIn(trail[i].at, radius * trail[i].press, touching);
            foreach (var e in touching)
            {
                if (!burnt.Add(e)) continue;
                Hit(e, damage, true);   // a burn, so it goes through armor
            }
        }
    }

    // now and then a flame licks up off the wet part of the trail
    private void Flames(float dt, float now, float life)
    {
        if (Data.flameFrames == null || Data.flameFrames.Length == 0 || trail.Count == 0) return;
        // the wet part is the newest end of the trail
        int wet = 0;
        while (wet < trail.Count && (now - trail[trail.Count - 1 - wet].born) / life < Data.wetShare) wet++;
        flameTimer += dt * Data.flamesPerSecond * wet * Data.spacing;
        if (flameTimer < 1f || wet == 0) return;
        flameTimer -= 1f;
        var b = trail[trail.Count - 1 - Random.Range(0, wet)];
        // the flame's base is 6 pixels under its canvas centre
        FxBatch.Play(Data.flameFrames, Data.flameFps, b.at + Random.insideUnitCircle * 0.1f + new Vector2(0f, 6f / WeaponFxPixels), 1f, Data.brushLayer, Data.brushOrder - 1);
    }

    private const float WeaponFxPixels = 28.46f;

    // ---------------------------------------------------------------- the evolution

    // the newest blot has come back round onto the ink behind it: if what it closes is big enough,
    // it goes off
    private void CloseLoop(float now)
    {
        if (now < loopReady || trail.Count < 8) return;
        int newest = trail.Count - 1;
        Vector2 head = trail[newest].at;
        float reach = Mathf.Max(Data.At(Level).width * AreaMul * 0.5f, Data.spacing * 1.5f);
        // the last metre and a half is the stroke that's still being drawn, not a loop
        int skip = Mathf.CeilToInt(1.5f / Data.spacing);
        for (int i = newest - skip; i >= 0; i--)
        {
            if ((trail[i].at - head).sqrMagnitude > reach * reach) continue;
            loop.Clear();
            for (int k = i; k <= newest; k++) loop.Add(trail[k].at);
            if (Area(loop) < Data.minLoopArea) return;
            Detonate(now);
            // the loop's ink is spent
            trail.RemoveRange(i, trail.Count - i);
            lastPaint = tip;
            loopReady = now + Data.loopCooldown;
            return;
        }
    }

    private void Detonate(float now)
    {
        Vector2 centre = Centroid(loop);
        Vector2 min = loop[0], max = loop[0];
        foreach (var p in loop) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }

        // everything inside: wiped out, but for elites and bosses, which take a heavy blow
        snapshot.Clear();
        snapshot.AddRange(EnemyRegistry.All);
        foreach (var go in snapshot)
        {
            if (go == null || !go.TryGetComponent(out EnemyHealth e) || !IsAlive(e)) continue;
            Vector2 p = go.transform.position;
            if (p.x < min.x || p.y < min.y || p.x > max.x || p.y > max.y || !Inside(loop, p)) continue;
            bool boss = go.TryGetComponent(out BossMarker _) || go.TryGetComponent(out SecretBossBehavior _);
            float damage = boss ? Data.loopDamage * Might
                : go.TryGetComponent(out EliteOutline _) ? Mathf.Max(Data.loopDamage * Might, e.Max * Data.eliteShare)
                : Mathf.Max(Data.loopDamage * Might, e.Current + 1f);
            Hit(e, damage, true);
        }

        // the seal where the loop closed round, as big as the loop, and blasts rippling out from it
        float size = Mathf.Sqrt(Area(loop));
        if (Data.sealFrames != null && Data.sealFrames.Length > 0)
            FxBatch.Play(Data.sealFrames, Data.sealFps, centre, Mathf.Clamp(size / Data.sealArtSize, 1f, 2.4f), Data.blastLayer, Data.blastOrder + 1);
        float far = Mathf.Max(0.01f, Mathf.Max((max - centre).magnitude, (min - centre).magnitude));
        int made = 0;
        for (float y = min.y; y <= max.y && made < Data.maxBlasts; y += Data.blastSpacing)
            for (float x = min.x; x <= max.x && made < Data.maxBlasts; x += Data.blastSpacing)
            {
                var p = new Vector2(x, y) + Random.insideUnitCircle * Data.blastSpacing * 0.3f;
                if (!Inside(loop, p)) continue;
                blasts.Add(new Pending { at = p, when = now + Data.rippleSeconds * (p - centre).magnitude / far, scale = Random.Range(0.85f, 1.2f) });
                made++;
            }
        // the stroke of the loop flares up as it's spent
        for (int k = 0; k < loop.Count; k += 3)
            blasts.Add(new Pending { at = loop[k], when = now + 0.02f * (k / 3), scale = -1f });

        Juice.Shake(Data.loopShake);
        if (Data.loopSound != null) SfxPlayer.PlayAt(Data.loopSound, centre, Data.loopVolume);
    }

    private void Ripple(float now)
    {
        for (int i = blasts.Count - 1; i >= 0; i--)
        {
            var b = blasts[i];
            if (now < b.when) continue;
            blasts.RemoveAt(i);
            if (b.scale < 0f)
            {
                if (Data.flameFrames != null && Data.flameFrames.Length > 0)
                    FxBatch.Play(Data.flameFrames, Data.flameFps, b.at + new Vector2(0f, 6f / WeaponFxPixels), 1.4f, Data.blastLayer, Data.blastOrder);
            }
            else if (Data.blastFrames != null && Data.blastFrames.Length > 0)
                FxBatch.Play(Data.blastFrames, Data.blastFps, b.at, b.scale, Data.blastLayer, Data.blastOrder);
        }
    }

    // shoelace: the area a closed run of points holds, either way round
    private static float Area(List<Vector2> pts)
    {
        float a = 0f;
        for (int i = 0, j = pts.Count - 1; i < pts.Count; j = i++) a += pts[j].x * pts[i].y - pts[i].x * pts[j].y;
        return Mathf.Abs(a) * 0.5f;
    }

    private static Vector2 Centroid(List<Vector2> pts)
    {
        Vector2 sum = Vector2.zero;
        foreach (var p in pts) sum += p;
        return sum / Mathf.Max(1, pts.Count);
    }

    // even-odd: a ray from the point crosses the outline an odd number of times when it's inside
    private static bool Inside(List<Vector2> pts, Vector2 p)
    {
        bool inside = false;
        for (int i = 0, j = pts.Count - 1; i < pts.Count; j = i++)
        {
            Vector2 a = pts[i], b = pts[j];
            if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
        }
        return inside;
    }

    // ---------------------------------------------------------------- the brush

    private void PlaceBrush(float dt, float now, bool walking)
    {
        if (Data.brushFrames == null || Data.brushFrames.Length == 0) return;
        if (brush == null)
        {
            brush = new GameObject("Brush").AddComponent<SpriteRenderer>();
            brush.transform.SetParent(Fx, false);
            brush.sortingLayerName = Data.brushLayer;
            brush.sortingOrder = Data.brushOrder;
        }
        brush.sprite = Data.brushFrames[(int)(now * Data.brushFps) % Data.brushFrames.Length];

        // it leans into the way the player walks, the tip trailing, and sways a little standing still
        float wanted = (walking ? -heading.x * Data.brushLean : 0f) + Mathf.Sin(now * 2.1f) * 4f;
        lean = Mathf.Lerp(lean, wanted, 1f - Mathf.Exp(-8f * dt));
        var turn = Quaternion.Euler(0f, 0f, lean);
        float tipDrop = Data.brushTipPixels / WeaponFxPixels;
        Vector2 centre = tip + (Vector2)(turn * new Vector3(0f, tipDrop, 0f));
        brush.transform.SetPositionAndRotation(centre, turn);
    }

    // ---------------------------------------------------------------- the stroke

    private void Draw(float now, float life, float width)
    {
        if (!Data.Ready) return;
        if (mesh == null)
        {
            var go = new GameObject("Ink");
            go.transform.SetParent(Fx, false);
            mesh = new Mesh { name = "Cinnabar Ink", indexFormat = IndexFormat.UInt32 };
            mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var shader = Shader.Find("Rogue/Sprite Batch");
            mr.sharedMaterial = new Material(shader != null ? shader : Shader.Find("Sprites/Default")) { mainTexture = Data.dabFrames[0].texture, name = "Cinnabar Ink" };
            mr.sortingLayerName = Data.trailLayer;
            mr.sortingOrder = Data.trailOrder;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        int n = trail.Count, quads = n * 2;
        if (verts.Length < quads * 4)
        {
            int cap = Mathf.NextPowerOfTwo(Mathf.Max(64, quads));
            verts = new Vector3[cap * 4];
            uvs = new Vector2[cap * 4];
            colors = new Color32[cap * 4];
            tris = new int[cap * 6];
        }

        float stretch = width / Mathf.Max(0.01f, Data.dabArtWidth);
        int flicker = (int)(now * 9f);
        int q = 0;
        // the rims, then the blots over them
        for (int pass = 0; pass < 2; pass++)
            for (int i = 0; i < n; i++)
            {
                var b = trail[i];
                float age = (now - b.born) / life;
                int state = pass == 0 ? 4
                    : age < Data.wetShare ? (flicker + i) % 2
                    : age < Data.wetShare + Data.dryingShare ? 2 : 3;
                float fade = Mathf.Clamp01((1f - age) / Mathf.Max(0.01f, Data.fadeShare));
                var sprite = Data.dabFrames[Mathf.Min(Data.dabFrames.Length - 1, b.shape * CinnabarInkBrushData.DabStates + state)];
                Quad(q++, sprite, b.at, b.angle, stretch * b.press * Mathf.Lerp(0.55f, 1f, fade), (byte)(255f * fade));
            }

        for (int i = q * 4; i < verts.Length; i++) verts[i] = Vector3.zero;
        mesh.Clear();
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.colors32 = colors;
        mesh.triangles = tris;
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(100000f, 100000f, 1000f));
    }

    private void Quad(int q, Sprite s, Vector2 at, float angle, float scale, byte alpha)
    {
        var tex = s.texture;
        var r = s.rect;
        Vector2 size = new Vector2(tex.width, tex.height);
        Vector2 uv0 = r.min / size, uv1 = r.max / size;
        Vector2 v0 = -s.pivot / s.pixelsPerUnit * scale, v1 = (r.size - s.pivot) / s.pixelsPerUnit * scale;
        float c = Mathf.Cos(angle), sn = Mathf.Sin(angle);
        Vector3 P(float x, float y) => new Vector3(at.x + x * c - y * sn, at.y + x * sn + y * c, 0f);

        int o = q * 4, t = q * 6;
        verts[o] = P(v0.x, v0.y); uvs[o] = new Vector2(uv0.x, uv0.y);
        verts[o + 1] = P(v0.x, v1.y); uvs[o + 1] = new Vector2(uv0.x, uv1.y);
        verts[o + 2] = P(v1.x, v1.y); uvs[o + 2] = new Vector2(uv1.x, uv1.y);
        verts[o + 3] = P(v1.x, v0.y); uvs[o + 3] = new Vector2(uv1.x, uv0.y);
        var col = new Color32(255, 255, 255, alpha);
        colors[o] = colors[o + 1] = colors[o + 2] = colors[o + 3] = col;
        tris[t] = o; tris[t + 1] = o + 1; tris[t + 2] = o + 2;
        tris[t + 3] = o; tris[t + 4] = o + 2; tris[t + 5] = o + 3;
    }
}
