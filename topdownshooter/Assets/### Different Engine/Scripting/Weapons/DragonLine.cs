using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Qing Long Xian: every cooldown a line is cast across the screen at random and an azure dragon,
// ten segments long, flies along it, weaving about the line. whatever a segment passes through is
// hit once; whatever is in the way of its head is hit three times as hard. evolved, it flies no
// more lines: every so often a much longer dragon coils out of the middle of the screen in a tight
// spiral to its edge, shoving the horde back with its coils and spitting fire ahead of its head
public class DragonLine : Weapon<DragonLineData>
{
    private const float WorldPpu = 37f / 1.3f;

    private sealed class Dragon
    {
        public SpriteRenderer head, tail, line;
        public readonly List<SpriteRenderer> body = new List<SpriteRenderer>();
        public readonly HashSet<EnemyHealth> headHit = new HashSet<EnemyHealth>();
        public readonly HashSet<EnemyHealth> bodyHit = new HashSet<EnemyHealth>();
        public readonly Dictionary<EnemyHealth, float> nextHead = new Dictionary<EnemyHealth, float>();
        public readonly Dictionary<EnemyHealth, float> nextCoil = new Dictionary<EnemyHealth, float>();
        public readonly Dictionary<EnemyHealth, float> nextFire = new Dictionary<EnemyHealth, float>();
        public readonly List<Vector2> path = new List<Vector2>(512);
        public readonly List<float> along = new List<float>(512);
        public float phase;

        public void Show(bool on)
        {
            head.enabled = on;
            tail.enabled = on;
            foreach (var b in body) b.enabled = on;
        }
    }

    private readonly Stack<Dragon> spare = new Stack<Dragon>();
    private readonly List<EnemyHealth> touching = new List<EnemyHealth>();
    private Camera cam;
    private float timer, spiralTimer;
    private bool primed, spiralPrimed;

    protected override void OnLevelChanged()
    {
        if (Data == null) return;
        // the first dragon comes soon after the pick, not a whole cooldown later; the same for
        // the first coil after the evolution
        if (!primed)
        {
            primed = true;
            timer = Mathf.Max(0f, Cooldown(Data.At(Level).cooldown) - 0.75f);
        }
        if (Data.IsEvolved(Level) && !spiralPrimed)
        {
            spiralPrimed = true;
            spiralTimer = Mathf.Max(0f, Cooldown(Data.evolvedCooldown) - 1f);
        }
    }

    private void Update()
    {
        if (Data == null || Level <= 0) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return; // paused
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        var lv = Data.At(Level);
        if (!Data.IsEvolved(Level))
        {
            timer += dt;
            if (timer >= Cooldown(lv.cooldown))
            {
                timer = 0f;
                for (int i = 0; i < lv.dragons; i++) StartCoroutine(Cross(lv, i * 0.25f));
            }
        }
        else
        {
            spiralTimer += dt;
            if (spiralTimer >= Cooldown(Data.evolvedCooldown))
            {
                spiralTimer = 0f;
                StartCoroutine(Coil());
            }
        }
    }

    // ---------------------------------------------------------------- the line

    private IEnumerator Cross(DragonLineData.LevelStats lv, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (cam == null) yield break;

        int count = Mathf.Max(1, Data.segments - 1);
        var d = Take(count);
        float spacing = Spacing(1f);
        float bodyLength = spacing * Data.segments;

        // a line from edge to edge, in a random direction through an enemy on screen picked at
        // random (a line through a random spot often missed everyone); with nobody on screen,
        // through a random spot in the middle part of it
        Vector2 centre = cam.transform.position;
        float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;
        Vector2 through = centre + new Vector2(Random.Range(-0.4f, 0.4f) * halfW, Random.Range(-0.4f, 0.4f) * halfH);
        var target = RandomEnemy(centre, Mathf.Max(halfW, halfH));
        if (target != null && OnScreen(target.transform.position, cam)) through = target.transform.position;
        float a = Random.Range(0f, Mathf.PI * 2f);
        Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        // the line runs far past the screen's edges, so walking toward either end never shows it cut off
        float over = Mathf.Sqrt(halfW * halfW + halfH * halfH) * Data.lineOverhang;
        ClipToRect(through, dir, centre, halfW + over, halfH + over, out Vector2 from, out Vector2 to);
        float length = Vector2.Distance(from, to);
        // the pace is set by the part on screen, however long the line runs past it
        ClipToRect(through, dir, centre, halfW, halfH, out Vector2 seenFrom, out Vector2 seenTo);
        float seen = Mathf.Max(1f, Vector2.Distance(seenFrom, seenTo));

        // the path: the line, with the dragon's weave about it
        Vector2 side = new Vector2(-dir.y, dir.x);
        d.path.Clear();
        d.along.Clear();
        int samples = Mathf.Max(16, Mathf.CeilToInt(length / 0.1f));
        for (int i = 0; i <= samples; i++)
        {
            float s = length * i / samples;
            float sway = Mathf.Sin((s * Data.weaveFrequency + d.phase) * Mathf.PI * 2f) * Data.weave;
            d.path.Add(from + dir * s + side * sway);
        }
        Measure(d);

        // the line is cast first, growing across the screen
        ShowLine(d, from, dir, 0f);
        float tele = Mathf.Max(0.01f, Data.telegraphSeconds);
        for (float t = 0f; t < tele; t += Time.deltaTime)
        {
            ShowLine(d, from, dir, length * Mathf.Clamp01(t / (tele * 0.6f)));
            yield return null;
        }
        ShowLine(d, from, dir, length);

        // then the dragon flies it, head first, until its tail has left
        d.headHit.Clear();
        d.bodyHit.Clear();
        float speed = seen / Mathf.Max(0.1f, Data.crossSeconds) * SpeedMul;
        float damage = lv.damage * Might;
        for (float head = 0f; head < length + bodyLength + spacing; head += speed * Time.deltaTime)
        {
            Place(d, head, spacing, count);
            // the line is used up behind the tail
            float behind = Mathf.Clamp(head - bodyLength, 0f, length);
            if (d.line != null) ShowLine(d, from + dir * behind, dir, length - behind);

            HitOnce(d.head.transform.position, Data.headRadius * AreaMul, d.headHit, damage * Data.headMultiplier, true);
            foreach (var seg in d.body)
                if (seg.enabled) HitOnce(seg.transform.position, Data.bodyRadius * AreaMul, d.bodyHit, damage, false);
            if (d.tail.enabled) HitOnce(d.tail.transform.position, Data.bodyRadius * AreaMul, d.bodyHit, damage, false);
            yield return null;
        }

        Give(d);
    }

    private void HitOnce(Vector2 at, float radius, HashSet<EnemyHealth> already, float damage, bool bite)
    {
        EnemiesIn(at, radius, touching);
        foreach (var e in touching)
        {
            if (!already.Add(e)) continue;
            if (bite && Data.biteFx != null) FxOneShot.Play(Data.biteFx, e.transform.position);
            Hit(e, damage);
        }
    }

    // ---------------------------------------------------------------- evolution: the coil

    private IEnumerator Coil()
    {
        if (cam == null) yield break;
        // a much longer dragon, and bigger: more segments, each scaled up, spaced to overlap
        int count = Mathf.Max(1, Data.evolvedSegments - 1);
        var d = Take(count);
        float spacing = Spacing(Data.evolvedScale);
        float bodyLength = spacing * Data.evolvedSegments;

        // an Archimedean spiral from the middle of the screen out past its corners
        Vector2 centre = cam.transform.position;
        float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;
        float reach = Mathf.Sqrt(halfW * halfW + halfH * halfH) + 0.5f;
        float turns = Data.spiralTurns * Mathf.PI * 2f, start = Random.Range(0f, Mathf.PI * 2f);
        d.path.Clear();
        d.along.Clear();
        const int samples = 600;
        for (int i = 0; i <= samples; i++)
        {
            float k = (float)i / samples, th = start + k * turns, r = Mathf.Lerp(0.3f, reach, k);
            d.path.Add(centre + new Vector2(Mathf.Cos(th), Mathf.Sin(th)) * r);
        }
        Measure(d);
        float length = d.along[d.along.Count - 1];

        d.nextHead.Clear();
        d.nextCoil.Clear();
        d.nextFire.Clear();
        float speed = Data.spiralSpeed * SpeedMul;
        float fire = Data.fireDamage * Might, coil = Data.coilDamage * Might, fireTimer = 0f;
        Juice.Shake(0.15f);

        for (float head = 0f; head < length + bodyLength + spacing; head += speed * Time.deltaTime)
        {
            Place(d, head, spacing, count);
            float now = Time.time;
            Vector2 h = d.head.transform.position;
            Vector2 forward = d.head.transform.right;

            // its head, and the fire it spits ahead of it
            HitEvery(h, Data.headRadius * AreaMul * 1.3f, d.nextHead, fire * Data.evolvedHeadMultiplier, now, centre, false);
            fireTimer += Time.deltaTime;
            if (fireTimer >= Data.fireInterval && head < length)
            {
                fireTimer = 0f;
                Vector2 at = h + forward * Data.fireReach * AreaMul;
                if (Data.fireFx != null) FxOneShot.Play(Data.fireFx, at, FxOneShot.Angle(forward), AreaMul);
                HitEvery(at, Data.fireRadius * AreaMul, d.nextFire, fire, now, centre, false);
            }

            // its coils shove the horde outward
            foreach (var seg in d.body)
                if (seg.enabled) HitEvery(seg.transform.position, Data.bodyRadius * AreaMul * 1.4f, d.nextCoil, coil, now, centre, true);
            yield return null;
        }

        Give(d);
    }

    private void HitEvery(Vector2 at, float radius, Dictionary<EnemyHealth, float> next, float damage, float now, Vector2 centre, bool shove)
    {
        EnemiesIn(at, radius, touching);
        foreach (var e in touching)
        {
            if (shove && e.TryGetComponent(out EnemyMovement move))
            {
                Vector2 away = (Vector2)e.transform.position - centre;
                if (away.sqrMagnitude < 0.0001f) away = Random.insideUnitCircle;
                move.Shove(away.normalized * Data.pushSpeed, Data.pushSeconds);
            }
            if (next.TryGetValue(e, out float ready) && now < ready) continue;
            next[e] = now + Data.evolvedRehit;
            Hit(e, damage);
        }
    }

    // ---------------------------------------------------------------- the body

    // world distance between two segments: a line dragon is Length Of Screen of the screen's
    // height; bigger segments are spaced wider so they still overlap the same
    private float Spacing(float bigger)
    {
        float screen = cam != null ? cam.orthographicSize * 2f : 10f;
        return screen * Data.lengthOfScreen * bigger / Mathf.Max(2, Data.segments);
    }

    // the head at `head` along the path and every segment Spacing behind the one before; a part
    // not on the path yet (still coming out) is hidden
    private void Place(Dragon d, float head, float spacing, int count)
    {
        float scale = spacing / Mathf.Max(0.01f, Data.segmentArtPixels / WorldPpu);
        int frame = (int)((Time.time + d.phase) * Data.bodyFps);

        Put(d.head, d, head, Frame(Data.headFrames, frame), scale, spacing * 1.8f);
        float behind = 0f;
        for (int i = 0; i < d.body.Count; i++)
        {
            // a pooled dragon can have more segments than this one uses
            if (i >= count)
            {
                d.body[i].enabled = false;
                continue;
            }
            // a little thinner toward the tail, and each a frame behind the one before, so the ripple runs down it
            // the gaps close up with it, so the thinner segments still overlap into one body
            float taper = Mathf.Lerp(1f, 0.7f, (float)i / Mathf.Max(1, count));
            behind += spacing * taper;
            var art = HasLeg(i, count) ? Frame(Data.legFrames, frame - i - 1) : null;
            Put(d.body[i], d, head - behind, art != null ? art : Frame(Data.bodyFrames, frame - i - 1), scale * taper, spacing * 1.3f * taper);
        }
        behind += spacing * 0.7f;
        Put(d.tail, d, head - behind, Frame(Data.tailFrames, frame - count), scale * 0.7f, spacing * 1.2f);
    }

    private void Put(SpriteRenderer sr, Dragon d, float s, Sprite art, float scale, float plainSize)
    {
        float end = d.along[d.along.Count - 1];
        if (s < 0f)
        {
            sr.enabled = false;
            return;
        }
        sr.enabled = true;
        Vector2 at = PointAt(d, s), ahead = PointAt(d, Mathf.Min(s + 0.08f, end + 10f)) - PointAt(d, s - 0.08f);
        float angle = Mathf.Atan2(ahead.y, ahead.x) * Mathf.Rad2Deg;
        // drawn facing right; on its way left it's flipped over rather than upside down
        bool left = ahead.x < 0f;
        sr.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, 0f, angle));
        if (art != null)
        {
            sr.sprite = art;
            sr.color = Color.white;
            sr.transform.localScale = new Vector3(scale, left ? -scale : scale, 1f);
        }
        else WeaponFx.Resize(sr, plainSize);
    }

    // Leg Segments are for a line dragon's body; a longer one has its legs at the same places
    // along it
    private bool HasLeg(int segment, int count)
    {
        if (Data.legSegments == null) return false;
        float stretch = count / (float)Mathf.Max(1, Data.segments - 1);
        foreach (int s in Data.legSegments) if (Mathf.RoundToInt(s * stretch) == segment) return true;
        return false;
    }

    private static Sprite Frame(Sprite[] frames, int i) =>
        frames != null && frames.Length > 0 ? frames[((i % frames.Length) + frames.Length) % frames.Length] : null;

    // the point `s` world units along the path; past either end it carries on straight
    private static Vector2 PointAt(Dragon d, float s)
    {
        var p = d.path;
        var len = d.along;
        int n = p.Count;
        if (s <= 0f) return p[0] + (p[1] - p[0]).normalized * s;
        if (s >= len[n - 1]) return p[n - 1] + (p[n - 1] - p[n - 2]).normalized * (s - len[n - 1]);

        int lo = 0, hi = n - 1;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) / 2;
            if (len[mid] <= s) lo = mid; else hi = mid;
        }
        float k = (s - len[lo]) / Mathf.Max(0.0001f, len[hi] - len[lo]);
        return Vector2.Lerp(p[lo], p[hi], k);
    }

    private static void Measure(Dragon d)
    {
        float total = 0f;
        d.along.Add(0f);
        for (int i = 1; i < d.path.Count; i++)
        {
            total += Vector2.Distance(d.path[i - 1], d.path[i]);
            d.along.Add(total);
        }
    }

    // ---------------------------------------------------------------- the line's art

    private void ShowLine(Dragon d, Vector2 from, Vector2 dir, float length)
    {
        if (d.line == null) return;
        d.line.enabled = length > 0.05f;
        if (!d.line.enabled) return;

        d.line.transform.SetPositionAndRotation(from, Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg));
        bool art = Data.lineFrames != null && Data.lineFrames.Length > 0 && Data.lineFrames[0] != null;
        if (art)
        {
            d.line.sprite = Frame(Data.lineFrames, (int)(Time.time * Data.lineFps));
            d.line.drawMode = SpriteDrawMode.Tiled;
            d.line.size = new Vector2(length, d.line.sprite.bounds.size.y);
            d.line.transform.localScale = Vector3.one;
        }
        else d.line.transform.localScale = new Vector3(length, 0.12f / 3f, 1f);
    }

    // where a line through a point, going both ways, leaves a rectangle
    private static void ClipToRect(Vector2 p, Vector2 dir, Vector2 c, float hw, float hh, out Vector2 from, out Vector2 to)
    {
        float tMin = float.NegativeInfinity, tMax = float.PositiveInfinity;
        for (int axis = 0; axis < 2; axis++)
        {
            float o = axis == 0 ? p.x - c.x : p.y - c.y, v = axis == 0 ? dir.x : dir.y, half = axis == 0 ? hw : hh;
            if (Mathf.Abs(v) < 0.0001f) continue;
            float t0 = (-half - o) / v, t1 = (half - o) / v;
            if (t0 > t1) (t0, t1) = (t1, t0);
            tMin = Mathf.Max(tMin, t0);
            tMax = Mathf.Min(tMax, t1);
        }
        from = p + dir * tMin;
        to = p + dir * tMax;
    }

    // ---------------------------------------------------------------- pooling

    private Dragon Take(int count)
    {
        var d = spare.Count > 0 ? spare.Pop() : Build();
        while (d.body.Count < count)
            d.body.Add(Part("Body", Data.sortingOrder - 1));
        d.phase = Random.value * 10f;
        d.head.sortingOrder = Data.sortingOrder + 1;
        d.head.gameObject.SetActive(true);
        d.tail.gameObject.SetActive(true);
        d.line.gameObject.SetActive(true);
        foreach (var b in d.body) b.gameObject.SetActive(true);
        d.Show(false);
        d.line.enabled = false;
        return d;
    }

    private Dragon Build()
    {
        var d = new Dragon
        {
            head = Part("Head", Data.sortingOrder + 1),
            tail = Part("Tail", Data.sortingOrder - 2),
            line = WeaponFx.Make(Fx, "Line", null, WeaponFx.Beam(
                new Color32(0x5a, 0xc8, 0xf0, 0x90), new Color32(0xe0, 0xfb, 0xff, 0xff), new Color32(0x5a, 0xc8, 0xf0, 0x90)),
                Color.white, Data.sortingLayer, Data.sortingOrder - 4),
        };
        return d;
    }

    private SpriteRenderer Part(string name, int order) =>
        WeaponFx.Make(Fx, name, null, WeaponFx.Disc, Data.placeholderColor, Data.sortingLayer, order);

    private void Give(Dragon d)
    {
        d.Show(false);
        d.line.enabled = false;
        spare.Push(d);
    }
}
