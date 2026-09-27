using System.Collections.Generic;
using UnityEngine;

// Feijian, flown the way Terraria's Empress of Light flies her prismatic bolts and lances: blades
// appear in a ring round the player and snap away at full speed with no telegraph, swing round in
// an arc onto the thickest part of the horde, cut straight through it and ricochet off the
// screen's edges (and off elites and bosses), faster with every bounce, until their bounces are
// spent. a luminous streak follows each one, so its arc and every ricochet can be read in a crowd.
// evolved (the Sovereign Blade Array), every so often a pair of master blades launches at top
// speed; their bounces spent, they anchor in the screen's borders and a crackling tripwire laser
// burns between the two for a few seconds, then both shatter. each blade only links to its own
// partner, so the arena gets one or two lines across it, not a web
public class FlyingSword : Weapon<FlyingSwordData>
{
    private sealed class Blade
    {
        public SpriteRenderer sr;
        public Afterimage copies;
        public TrailRenderer streak;
        public Vector2 pos, dir, target;
        public float speed, age, pop;
        public int bouncesLeft, bounced;
        public bool hasTarget, cage, spent, embedded;
        public Vector2 view;            // anchored: where on the screen, 0-1
        public Pair pair;
        public readonly Dictionary<EnemyHealth, float> hitAt = new Dictionary<EnemyHealth, float>();
    }

    private sealed class Pair
    {
        public Blade a, b;
        public SpriteRenderer laser;
        public float laserAge = -1f, waited, tick, arcs;
    }

    private const float MaxLife = 8f;
    private const float WorldPpu = 37f / 1.3f;

    private readonly List<Blade> live = new List<Blade>();
    private readonly Stack<Blade> spare = new Stack<Blade>();
    private readonly List<Pair> pairs = new List<Pair>();
    private readonly List<EnemyHealth> touching = new List<EnemyHealth>();
    private readonly List<Vector2> sample = new List<Vector2>(48);
    private Camera cam;
    private float timer, cageTimer, topWall;
    private bool cagePrimed;
    private Sprite plainLaser;

    protected override void OnLevelChanged()
    {
        // the first array comes a moment after the evolution, not a whole cooldown later
        if (Data != null && Data.IsEvolved(Level) && !cagePrimed)
        {
            cagePrimed = true;
            cageTimer = Mathf.Max(0f, Cooldown(Data.cageCooldown) - 0.5f);
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
        bool evolved = Data.IsEvolved(Level);
        topWall = TopWall();

        // with nobody in range it waits, ready, and launches the moment someone shows up
        if (!evolved || Data.evolvedKeepsBlades)
        {
            timer += dt;
            if (timer >= Cooldown(lv.cooldown) && Launch(lv)) timer = 0f;
        }

        if (evolved)
        {
            cageTimer += dt;
            if (cageTimer >= Cooldown(Data.cageCooldown))
            {
                cageTimer = 0f;
                for (int i = 0; i < Data.pairs; i++) LaunchArray();
            }
        }

        float damage = lv.damage * Might;
        for (int i = live.Count - 1; i >= 0; i--)
            if (Step(live[i], dt, damage)) Release(i);
        Lasers(dt);
    }

    // ---------------------------------------------------------------- launching

    // the blades appear round the player, spread round the ring, and snap away. each leaves off to
    // one side of the crowd it's after, so it swings onto it in an arc rather than a straight line
    private bool Launch(FlyingSwordData.LevelStats lv)
    {
        Vector2 me = transform.position;
        if (!FindCrowd(me, out Vector2 crowd)) return false;

        Vector2 toward = (crowd - me).normalized;
        int n = lv.blades;
        float side = Random.value < 0.5f ? 1f : -1f;
        for (int i = 0; i < n; i++)
        {
            // one blade appears in front; several are spaced evenly round the ring
            Vector2 from = me + Rotate(toward, n == 1 ? 0f : 90f + i * 360f / n) * Data.launchRing;
            float swing = (i % 2 == 0 ? side : -side) * Data.launchSwing;
            var b = Take(from, Rotate((crowd - from).normalized, swing), lv.bounces, false, null, Data.speed * SpeedMul);
            b.target = crowd;
            b.hasTarget = true;
        }
        return true;
    }

    // a pair of master blades at top speed, back to back, so they anchor on opposite borders
    private void LaunchArray()
    {
        Vector2 me = transform.position;
        float a = Random.Range(0f, 360f);
        var pair = new Pair();
        float top = Data.maxSpeed * SpeedMul;
        pair.a = Take(me + Rotate(Vector2.right, a) * Data.launchRing, Rotate(Vector2.right, a), Data.cageBounces, true, pair, top);
        pair.b = Take(me + Rotate(Vector2.right, a + 180f) * Data.launchRing, Rotate(Vector2.right, a + 180f + Random.Range(-25f, 25f)), Data.cageBounces, true, pair, top);
        pairs.Add(pair);
    }

    private Blade Take(Vector2 from, Vector2 dir, int bounces, bool cage, Pair pair, float speed)
    {
        var b = spare.Count > 0 ? spare.Pop() : NewBlade();
        b.sr.gameObject.SetActive(true);
        b.pos = from;
        b.dir = dir.normalized;
        b.speed = speed;
        b.age = 0f;
        b.pop = 1f;
        b.bouncesLeft = bounces;
        b.bounced = 0;
        b.hasTarget = false;
        b.cage = cage;
        b.spent = bounces <= 0;
        b.embedded = false;
        b.pair = pair;
        b.hitAt.Clear();
        Place(b);
        b.copies.Restart();
        b.streak.Clear();
        b.streak.emitting = true;
        b.streak.widthMultiplier = Data.trailWidth * AreaMul;

        // the snap: a flash where it appears, pointing the way it goes
        if (Data.launchFx != null) FxOneShot.Play(Data.launchFx, from, FxOneShot.Angle(b.dir), AreaMul);
        live.Add(b);
        return b;
    }

    private Blade NewBlade()
    {
        var sr = WeaponFx.Make(Fx, "Flying Sword", Data.Animated ? Data.bladeFrames[0] : null, WeaponFx.Square, Data.bladeColor, Data.sortingLayer, Data.sortingOrder);

        // the streak: bright at the blade, jade down its length, gone at the end
        var streak = sr.gameObject.AddComponent<TrailRenderer>();
        streak.time = Data.trailSeconds;
        streak.minVertexDistance = 0.06f;
        streak.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.35f, 0.6f), new Keyframe(1f, 0f));
        var fade = new Gradient();
        var body = Data.trailBody;
        fade.SetKeys(
            new[] { new GradientColorKey(Data.trailHead, 0f), new GradientColorKey(body, 0.25f), new GradientColorKey(body, 1f) },
            new[] { new GradientAlphaKey(Data.trailHead.a, 0f), new GradientAlphaKey(body.a, 0.3f), new GradientAlphaKey(0f, 1f) });
        streak.colorGradient = fade;
        streak.sharedMaterial = Data.trailMaterial != null ? Data.trailMaterial : sr.sharedMaterial;
        streak.sortingLayerID = sr.sortingLayerID;
        streak.sortingOrder = sr.sortingOrder - 1;
        streak.numCapVertices = 2;

        return new Blade { sr = sr, streak = streak, copies = new Afterimage(sr, Data.trailLength, Data.trailSpacing, 0.5f, 0.75f) };
    }

    // ---------------------------------------------------------------- flight

    // true once the blade is finished
    private bool Step(Blade b, float dt, float damage)
    {
        b.age += dt;
        b.pop = Mathf.MoveTowards(b.pop, 0f, dt * 10f);
        if (b.embedded)
        {
            b.pos = cam.ViewportToWorldPoint(new Vector3(b.view.x, b.view.y, -cam.transform.position.z));
            Place(b);
            return b.pair == null;
        }
        if (b.age > MaxLife) return true;

        // it swings round onto its crowd; once it's through it, it flies on straight to the edge
        if (b.hasTarget)
        {
            Vector2 to = b.target - b.pos;
            if (to.sqrMagnitude < 0.5f) b.hasTarget = false;
            else b.dir = Vector3.RotateTowards(b.dir, to.normalized, Data.turnRate * Mathf.Deg2Rad * dt, 0f);
        }
        b.pos += b.dir * b.speed * dt;

        // off the screen's edges
        if (Walls(b, out Vector2 normal))
        {
            if (b.spent) return Finish(b);
            Bounce(b, normal, b.pos);
        }

        // through the crowd, and off anything strong
        EnemiesIn(b.pos, Data.hitRadius * AreaMul, touching);
        float now = Time.time;
        foreach (var e in touching)
        {
            if (b.hitAt.TryGetValue(e, out float at) && now - at < Data.rehit) continue;
            b.hitAt[e] = now;
            Hit(e, damage * (1f + Data.bonusPerBounce * b.bounced));

            // only while it's heading into them: one already on its way out isn't turned again
            Vector2 off = b.pos - (Vector2)e.transform.position;
            if (Data.bounceOffStrong && !b.spent && Strong(e) && Vector2.Dot(b.dir, off) < 0f)
            {
                Bounce(b, off.sqrMagnitude > 0.0001f ? off.normalized : -b.dir, e.transform.position);
                b.hitAt[e] = now;   // the bounce forgets who it hit; not this one, it's still inside it
                break;
            }
        }

        Place(b);
        return false;
    }

    // a ricochet: the blade turns off the surface, gets faster, uses a bounce and looks for the
    // next crowd. with its last bounce used it's spent: it flies on to the next edge and ends (or,
    // a master blade, anchors) there
    private void Bounce(Blade b, Vector2 normal, Vector2 at)
    {
        if (Vector2.Dot(b.dir, normal) < 0f) b.dir = Vector2.Reflect(b.dir, normal).normalized;
        b.bounced++;
        b.bouncesLeft--;
        b.speed = Mathf.Min(Data.maxSpeed * SpeedMul, b.speed * (1f + Data.bounceAcceleration));
        b.pop = 1f;
        b.hitAt.Clear();
        if (Data.sparkFx != null) FxOneShot.Play(Data.sparkFx, at, FxOneShot.Angle(b.dir));
        Juice.Shake(b.cage ? 0.04f : 0.02f);

        if (b.bouncesLeft <= 0)
        {
            b.spent = true;
            b.hasTarget = false;
            // a master blade's last leg heads for the border across from its partner's anchor
            if (b.cage && b.pair != null)
            {
                var partner = b.pair.a == b ? b.pair.b : b.pair.a;
                if (partner != null && partner.embedded) AimAcross(b, partner);
            }
            return;
        }

        b.hasTarget = FindCrowd(b.pos, out b.target) && Vector2.Dot(b.target - b.pos, b.dir) > -0.2f;
    }

    // a spent blade reaching an edge: an ordinary one breaks off there, a master blade anchors
    private bool Finish(Blade b)
    {
        if (Data.sparkFx != null) FxOneShot.Play(Data.sparkFx, b.pos, FxOneShot.Angle(b.dir));
        if (!b.cage) return true;
        Anchor(b);
        return false;
    }

    // which edge it crossed, if any: the blade is put back on the edge and `normal` points back in
    private bool Walls(Blade b, out Vector2 normal)
    {
        Vector2 c = cam.transform.position;
        float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;
        normal = Vector2.zero;
        if (b.pos.x < c.x - halfW && b.dir.x < 0f) { b.pos.x = c.x - halfW; normal = Vector2.right; }
        else if (b.pos.x > c.x + halfW && b.dir.x > 0f) { b.pos.x = c.x + halfW; normal = Vector2.left; }
        if (b.pos.y < c.y - halfH && b.dir.y < 0f) { b.pos.y = c.y - halfH; normal += Vector2.up; }
        else if (b.pos.y > topWall && b.dir.y > 0f) { b.pos.y = topWall; normal += Vector2.down; }
        if (normal == Vector2.zero) return false;
        normal.Normalize();
        return true;
    }

    // the top of the play area: the screen's top edge, or just under the level progress bar when
    // it's there, so blades bounce off the bar and anchor below it instead of hiding behind it
    private float TopWall()
    {
        float top = cam.transform.position.y + cam.orthographicSize;
        var bar = ProgressBarGradient.Active;
        // only a bar along the top of the screen counts: anything lower would be a wall of nothing
        if (bar != null && bar.isActiveAndEnabled && bar.TryScreenBottom(out float screenY) && screenY > Screen.height * 0.75f && screenY < Screen.height)
        {
            float barBottom = cam.ScreenToWorldPoint(new Vector3(0f, screenY, -cam.transform.position.z)).y;
            // a little clear of it, so a blade anchored there shows whole
            top = Mathf.Min(top, barBottom - 0.2f);
        }
        return top;
    }

    private static bool Strong(EnemyHealth e) => e.TryGetComponent(out EliteOutline _) || e.TryGetComponent(out BossMarker _);

    // the middle of the thickest crowd near a point: a handful of enemies in range are sampled
    // and the one with the most others close round it wins
    private bool FindCrowd(Vector2 from, out Vector2 crowd)
    {
        crowd = from;
        sample.Clear();
        float r2 = Data.range * Data.range;
        foreach (var go in EnemyRegistry.All)
        {
            if (go == null || !go.TryGetComponent(out EnemyHealth e) || !IsAlive(e)) continue;
            Vector2 p = go.transform.position;
            if ((p - from).sqrMagnitude > r2) continue;
            if (sample.Count < 40) sample.Add(p);
            else if (Random.value < 0.25f) sample[Random.Range(0, sample.Count)] = p;
        }
        if (sample.Count == 0) return false;

        float c2 = Data.clusterRadius * Data.clusterRadius;
        int best = -1;
        Vector2 bestCentre = sample[0];
        foreach (var p in sample)
        {
            int n = 0;
            Vector2 sum = Vector2.zero;
            foreach (var q in sample)
                if ((q - p).sqrMagnitude <= c2) { n++; sum += q; }
            if (n > best) { best = n; bestCentre = sum / n; }
        }
        crowd = bestCentre;
        return true;
    }

    private void Place(Blade b)
    {
        float angle = Mathf.Atan2(b.dir.y, b.dir.x) * Mathf.Rad2Deg;
        bool stuck = b.embedded && Data.embedFrames != null && Data.embedFrames.Length > 0 && Data.embedFrames[0] != null;
        // anchored, its tip is what sits on the edge, not its middle
        Vector2 at = stuck ? b.pos - b.dir * (Data.embedTipPixels / WorldPpu * AreaMul) : b.pos;
        b.sr.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, 0f, angle));

        // a snap of scale on launch and on every ricochet, stretched along the flight
        float pop = b.pop * b.pop;
        var frames = stuck ? Data.embedFrames : Data.bladeFrames;
        if (frames != null && frames.Length > 0 && frames[0] != null)
        {
            b.sr.sprite = frames[(int)((Time.time + b.age) * Data.bladeFps) % frames.Length];
            b.sr.transform.localScale = new Vector3(1f + 0.5f * pop, 1f - 0.2f * pop, 1f) * AreaMul;
        }
        else
        {
            WeaponFx.Resize(b.sr, 0.5f * AreaMul);
            b.sr.transform.localScale = new Vector3(b.sr.transform.localScale.x * 1.6f * (1f + 0.5f * pop), b.sr.transform.localScale.y * 0.4f, 1f);
        }
        if (!b.embedded) b.copies.Record();
    }

    // ---------------------------------------------------------------- evolution: the array

    private void AimAcross(Blade b, Blade partner)
    {
        var across = new Vector2(1f - partner.view.x, 1f - partner.view.y);
        Vector2 goal = cam.ViewportToWorldPoint(new Vector3(across.x, across.y, -cam.transform.position.z));
        Vector2 to = goal - b.pos;
        if (to.sqrMagnitude > 0.01f) b.dir = to.normalized;
    }

    private void Anchor(Blade b)
    {
        b.embedded = true;
        b.copies.Hide();
        b.streak.emitting = false;
        var v = cam.WorldToViewportPoint(b.pos);
        b.view = new Vector2(Mathf.Clamp01(v.x), Mathf.Clamp01(v.y));
        // it points into the border it's stuck in
        b.dir = -b.dir;
        b.pop = 1f;
        Juice.Shake(0.08f);
    }

    private void Lasers(float dt)
    {
        for (int i = pairs.Count - 1; i >= 0; i--)
        {
            var p = pairs[i];
            bool aIn = p.a != null && p.a.embedded, bIn = p.b != null && p.b.embedded;

            if (p.laserAge < 0f)
            {
                // waiting on its partner; one that's gone for good ends the pair
                bool aGone = p.a == null || (!aIn && !live.Contains(p.a)), bGone = p.b == null || (!bIn && !live.Contains(p.b));
                if (aIn || bIn) p.waited += dt;
                if (aGone || bGone || p.waited > Data.partnerWait) { EndPair(i, true); continue; }
                if (aIn && bIn)
                {
                    // the tripwire ignites the moment both are locked in
                    p.laserAge = 0f;
                    p.tick = Data.laserTick;
                    Juice.Shake(0.18f);
                }
                else continue;
            }

            p.laserAge += dt;
            if (p.laserAge >= Data.laserSeconds) { EndPair(i, true); continue; }
            DrawLaser(p);

            Vector2 from = p.a.pos, to = p.b.pos;
            p.tick += dt;
            if (p.tick >= Data.laserTick)
            {
                p.tick = 0f;
                Vector2 mid = (from + to) * 0.5f;
                float length = Vector2.Distance(from, to), angle = Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg;
                EnemiesInBox(mid, new Vector2(length, Data.laserWidth * AreaMul), angle, touching);
                float damage = Data.laserDamage * Might;
                foreach (var e in touching) Hit(e, damage);
            }

            // high voltage: sparks crackling up and down the line
            p.arcs += Data.arcsPerSecond * dt;
            while (p.arcs >= 1f)
            {
                p.arcs -= 1f;
                if (Data.sparkFx != null)
                    FxOneShot.Play(Data.sparkFx, Vector2.Lerp(from, to, Random.value) + Random.insideUnitCircle * Data.laserWidth * 0.3f, Random.Range(0f, 360f), Random.Range(0.5f, 0.9f));
            }
        }
    }

    private void DrawLaser(Pair p)
    {
        if (p.laser == null)
        {
            if (plainLaser == null)
                plainLaser = WeaponFx.Beam(new Color32(0x2a, 0xa0, 0x70, 0xa0), new Color32(0x7a, 0xff, 0xc0, 0xff), new Color32(0xff, 0xff, 0xff, 0xff),
                    new Color32(0x7a, 0xff, 0xc0, 0xff), new Color32(0x2a, 0xa0, 0x70, 0xa0));
            p.laser = WeaponFx.Make(Fx, "Blade Array Tripwire", null, plainLaser, Color.white, Data.sortingLayer, Data.sortingOrder + 1);
        }
        p.laser.enabled = true;
        Vector2 from = p.a.pos, to = p.b.pos;
        float length = Vector2.Distance(from, to);
        p.laser.transform.SetPositionAndRotation(from, Quaternion.Euler(0f, 0f, Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg));

        // it ignites thick, hums with a flicker the whole time, and thins out before it breaks
        float k = p.laserAge / Mathf.Max(0.01f, Data.laserSeconds);
        float swell = k < 0.08f ? Mathf.Lerp(1.8f, 1f, k / 0.08f) : k > 0.88f ? Mathf.Lerp(1f, 0.25f, (k - 0.88f) / 0.12f) : 1f;
        swell *= 1f + ((int)(p.laserAge * 30f) % 3 == 0 ? 0.15f : 0f);
        var frames = Data.laserFrames;
        if (frames != null && frames.Length > 0 && frames[0] != null)
        {
            p.laser.sprite = frames[(int)(p.laserAge * Data.laserFps) % frames.Length];
            p.laser.color = Color.white;
            p.laser.drawMode = SpriteDrawMode.Tiled;
            p.laser.size = new Vector2(length, p.laser.sprite.bounds.size.y);
            p.laser.transform.localScale = new Vector3(1f, AreaMul * swell, 1f);
        }
        else p.laser.transform.localScale = new Vector3(length, Data.laserWidth * AreaMul * swell / 5f, 1f);
    }

    // the tripwire goes out and both anchored blades shatter
    private void EndPair(int index, bool shatter)
    {
        var p = pairs[index];
        if (p.laser != null) Destroy(p.laser.gameObject);
        foreach (var b in new[] { p.a, p.b })
        {
            if (b == null) continue;
            if (shatter && b.embedded && Data.shatterFx != null) FxOneShot.Play(Data.shatterFx, b.sr.transform.position, FxOneShot.Angle(b.dir), AreaMul);
            b.pair = null;
        }
        if (shatter && p.laserAge >= 0f) Juice.Shake(0.1f);
        pairs.RemoveAt(index);
    }

    private void Release(int index)
    {
        var b = live[index];
        b.copies.Hide();
        b.streak.emitting = false;
        b.streak.Clear();
        b.sr.gameObject.SetActive(false);
        b.pair = null;
        spare.Push(b);
        live.RemoveAt(index);
    }

    private static Vector2 Rotate(Vector2 v, float degrees)
    {
        float r = degrees * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }
}
