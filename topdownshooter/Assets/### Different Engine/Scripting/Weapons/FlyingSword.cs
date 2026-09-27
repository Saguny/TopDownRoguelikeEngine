using System.Collections.Generic;
using UnityEngine;

// Feijian: a cultivator's flying sword, sent zipping between targets. a fast jade blade that
// ricochets off enemies (on to the next nearest) and off the edges of the screen, gaining damage
// with every bounce and speed off every wall, until its bounces are spent. evolved (the Imperial
// Sword Cage), every so often a pair of swords is loosed that, their bounces spent, embed in the
// screen's edges, and a thick laser burns between the two for a few seconds. each sword only
// ever links to its own partner, so the arena gets one or two lines across it, not a web
public class FlyingSword : Weapon<FlyingSwordData>
{
    private sealed class Blade
    {
        public SpriteRenderer sr;
        public Afterimage trail;
        public Vector2 pos, dir;
        public float speed, age;
        public int bouncesLeft, bounced;
        public EnemyHealth last;
        public float lastAt;
        public bool cage, homing, embedded;
        public Vector2 view;            // embedded: where on the screen, 0-1
        public Pair pair;
    }

    private sealed class Pair
    {
        public Blade a, b;
        public SpriteRenderer laser;
        public float laserAge = -1f, waited, tick;
    }

    private const float MaxLife = 8f;

    private readonly List<Blade> live = new List<Blade>();
    private readonly Stack<Blade> spare = new Stack<Blade>();
    private readonly List<Pair> pairs = new List<Pair>();
    private readonly List<EnemyHealth> touching = new List<EnemyHealth>();
    private Camera cam;
    private float timer, cageTimer, topWall;
    private Sprite plainLaser;

    private void Update()
    {
        if (Data == null || Level <= 0) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return; // paused
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        var lv = Data.At(Level);
        topWall = TopWall();

        // with nobody in range it waits, ready, and throws as soon as someone shows up
        timer += dt;
        if (timer >= Cooldown(lv.cooldown) && Throw(lv)) timer = 0f;

        if (Data.IsEvolved(Level))
        {
            cageTimer += dt;
            if (cageTimer >= Cooldown(Data.cageCooldown))
            {
                cageTimer = 0f;
                for (int i = 0; i < Data.pairs; i++) Cage(lv);
            }
        }

        float damage = lv.damage * Might;
        for (int i = live.Count - 1; i >= 0; i--)
            if (Step(live[i], dt, damage, lv)) Release(i);
        Lasers(dt);
    }

    // ---------------------------------------------------------------- throwing

    private bool Throw(FlyingSwordData.LevelStats lv)
    {
        Vector2 me = transform.position;
        var first = RandomEnemy(me, Data.range);
        if (first == null) return false;

        for (int i = 0; i < lv.blades; i++)
        {
            // the first straight at its enemy, the rest fanned out a little either side
            Vector2 to = ((Vector2)first.transform.position - me).normalized;
            float spread = i == 0 ? 0f : (i % 2 == 1 ? 1f : -1f) * 18f * ((i + 1) / 2);
            Launch(me, Rotate(to, spread), lv.bounces, false, null);
        }
        return true;
    }

    // a pair, loosed back to back, so they land on different edges
    private void Cage(FlyingSwordData.LevelStats lv)
    {
        Vector2 me = transform.position;
        float a = Random.Range(0f, 360f);
        var pair = new Pair();
        pair.a = Launch(me, Rotate(Vector2.right, a), Data.cageBounces, true, pair);
        pair.b = Launch(me, Rotate(Vector2.right, a + 180f + Random.Range(-35f, 35f)), Data.cageBounces, true, pair);
        pairs.Add(pair);
    }

    private Blade Launch(Vector2 from, Vector2 dir, int bounces, bool cage, Pair pair)
    {
        var b = spare.Count > 0 ? spare.Pop() : NewBlade();
        b.sr.gameObject.SetActive(true);
        b.pos = from;
        b.dir = dir.normalized;
        b.speed = Data.speed * SpeedMul;
        b.age = 0f;
        b.bouncesLeft = bounces;
        b.bounced = 0;
        b.last = null;
        b.cage = cage;
        b.homing = false;
        b.embedded = false;
        b.pair = pair;
        b.sr.transform.localScale = Vector3.one * AreaMul;
        Place(b);
        b.trail.Restart();
        live.Add(b);
        return b;
    }

    private Blade NewBlade()
    {
        var sr = WeaponFx.Make(Fx, "Flying Sword", Data.Animated ? Data.bladeFrames[0] : null, WeaponFx.Square, Data.bladeColor, Data.sortingLayer, Data.sortingOrder);
        return new Blade { sr = sr, trail = new Afterimage(sr, Data.trailLength, Data.trailSpacing, 0.55f, 0.7f) };
    }

    // ---------------------------------------------------------------- flight

    // true once the blade is finished
    private bool Step(Blade b, float dt, float damage, FlyingSwordData.LevelStats lv)
    {
        b.age += dt;
        if (b.embedded)
        {
            b.pos = cam.ViewportToWorldPoint(new Vector3(b.view.x, b.view.y, -cam.transform.position.z));
            Place(b);
            return b.pair == null;
        }
        if (b.age > MaxLife) return true;

        b.pos += b.dir * b.speed * dt;

        // off the screen's edges
        if (Walls(b, lv))
        {
            if (Data.sparkFx != null) FxOneShot.Play(Data.sparkFx, b.pos, FxOneShot.Angle(b.dir));
            if (b.cage && b.bouncesLeft < 0)
            {
                Embed(b);
                return false;
            }
            if (!b.cage && b.bouncesLeft < 0) return true;
        }

        // off an enemy, on to the next
        if (!b.homing)
        {
            EnemiesIn(b.pos, Data.hitRadius * AreaMul, touching);
            foreach (var e in touching)
            {
                if (e == b.last && Time.time - b.lastAt < Data.rehit) continue;
                Hit(e, damage * (1f + Data.bonusPerBounce * b.bounced));
                if (Data.sparkFx != null) FxOneShot.Play(Data.sparkFx, e.transform.position, FxOneShot.Angle(b.dir));
                b.last = e;
                b.lastAt = Time.time;
                b.bounced++;
                b.bouncesLeft--;
                if (b.bouncesLeft < 0)
                {
                    // spent: a plain blade is done; a cage sword flies on to the edge to embed
                    if (!b.cage) return true;
                    b.homing = true;
                    break;
                }
                var next = NextTarget(b.pos, e);
                b.dir = next != null
                    ? ((Vector2)next.transform.position - b.pos).normalized
                    : Rotate(((Vector2)b.pos - (Vector2)e.transform.position).normalized, Random.Range(-60f, 60f));
                break;
            }
        }

        Place(b);
        return false;
    }

    // bounces it back in off whichever edge of the screen it crossed. true when it hit one
    private bool Walls(Blade b, FlyingSwordData.LevelStats lv)
    {
        Vector2 c = cam.transform.position;
        float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;
        bool hit = false;
        if (b.pos.x < c.x - halfW && b.dir.x < 0f) { b.dir.x = -b.dir.x; b.pos.x = c.x - halfW; hit = true; }
        else if (b.pos.x > c.x + halfW && b.dir.x > 0f) { b.dir.x = -b.dir.x; b.pos.x = c.x + halfW; hit = true; }
        if (b.pos.y < c.y - halfH && b.dir.y < 0f) { b.dir.y = -b.dir.y; b.pos.y = c.y - halfH; hit = true; }
        else if (b.pos.y > topWall && b.dir.y > 0f) { b.dir.y = -b.dir.y; b.pos.y = topWall; hit = true; }
        if (!hit) return false;

        // a spent cage sword doesn't bounce: it has come to its edge
        if (b.homing) { b.bouncesLeft = -1; return true; }

        b.bounced++;
        b.bouncesLeft--;
        b.speed = Mathf.Min(Data.maxSpeed * SpeedMul, b.speed * (1f + lv.wallAcceleration));
        return true;
    }

    // the top of the play area: the screen's top edge, or just under the level progress bar when
    // it's there, so blades bounce off the bar and embed below it instead of hiding behind it
    private float TopWall()
    {
        float top = cam.transform.position.y + cam.orthographicSize;
        var bar = ProgressBarGradient.Active;
        if (bar != null && bar.isActiveAndEnabled && bar.TryScreenBottom(out float screenY) && screenY > 0f && screenY < Screen.height)
        {
            float barBottom = cam.ScreenToWorldPoint(new Vector3(0f, screenY, -cam.transform.position.z)).y;
            // a little clear of it, so a sword stuck there shows whole
            top = Mathf.Min(top, barBottom - 0.2f);
        }
        return top;
    }

    private EnemyHealth NextTarget(Vector2 from, EnemyHealth not)
    {
        EnemyHealth best = null;
        float bestD = Data.bounceRange * Data.bounceRange;
        foreach (var go in EnemyRegistry.All)
        {
            if (go == null || !go.TryGetComponent(out EnemyHealth e) || e == not || !IsAlive(e)) continue;
            float d = ((Vector2)go.transform.position - from).sqrMagnitude;
            if (d < bestD) { bestD = d; best = e; }
        }
        return best;
    }

    private void Place(Blade b)
    {
        float angle = Mathf.Atan2(b.dir.y, b.dir.x) * Mathf.Rad2Deg;
        bool stuck = b.embedded && Data.embedFrames != null && Data.embedFrames.Length > 0 && Data.embedFrames[0] != null;
        // stuck, its tip is what sits on the edge, not its middle
        Vector2 at = stuck ? b.pos - b.dir * (Data.embedTipPixels / (37f / 1.3f) * AreaMul) : b.pos;
        b.sr.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, 0f, angle));
        var frames = stuck ? Data.embedFrames : Data.bladeFrames;
        if (frames != null && frames.Length > 0 && frames[0] != null)
        {
            b.sr.sprite = frames[(int)((Time.time + b.age) * Data.bladeFps) % frames.Length];
            b.sr.transform.localScale = Vector3.one * AreaMul;
        }
        else
        {
            WeaponFx.Resize(b.sr, 0.5f * AreaMul);
            b.sr.transform.localScale = new Vector3(b.sr.transform.localScale.x * 1.6f, b.sr.transform.localScale.y * 0.4f, 1f);
        }
        if (!b.embedded) b.trail.Record();
    }

    // ---------------------------------------------------------------- evolution: the cage

    private void Embed(Blade b)
    {
        b.embedded = true;
        b.trail.Hide();
        var v = cam.WorldToViewportPoint(b.pos);
        b.view = new Vector2(Mathf.Clamp01(v.x), Mathf.Clamp01(v.y));
        // it points into the edge it's stuck in
        b.dir = -b.dir;
        Juice.Shake(0.05f);
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
                if (aGone || bGone || p.waited > Data.partnerWait) { EndPair(i); continue; }
                if (aIn && bIn) { p.laserAge = 0f; p.tick = Data.laserTick; Juice.Shake(0.12f); }
                else continue;
            }

            p.laserAge += dt;
            if (p.laserAge >= Data.laserSeconds) { EndPair(i); continue; }
            DrawLaser(p);

            p.tick += dt;
            if (p.tick >= Data.laserTick)
            {
                p.tick = 0f;
                Vector2 from = p.a.pos, to = p.b.pos, mid = (from + to) * 0.5f;
                float length = Vector2.Distance(from, to), angle = Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg;
                EnemiesInBox(mid, new Vector2(length, Data.laserWidth * AreaMul), angle, touching);
                float damage = Data.laserDamage * Might;
                foreach (var e in touching) Hit(e, damage);
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
            p.laser = WeaponFx.Make(Fx, "Sword Cage Laser", null, plainLaser, Color.white, Data.sortingLayer, Data.sortingOrder + 1);
        }
        Vector2 from = p.a.pos, to = p.b.pos;
        float length = Vector2.Distance(from, to);
        p.laser.transform.SetPositionAndRotation(from, Quaternion.Euler(0f, 0f, Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg));

        // it flares up thick as it forms and thins out at the end
        float k = p.laserAge / Mathf.Max(0.01f, Data.laserSeconds);
        float swell = k < 0.1f ? Mathf.Lerp(1.5f, 1f, k / 0.1f) : k > 0.85f ? Mathf.Lerp(1f, 0.2f, (k - 0.85f) / 0.15f) : 1f;
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

    private void EndPair(int index)
    {
        var p = pairs[index];
        if (p.laser != null) Destroy(p.laser.gameObject);
        if (p.a != null) p.a.pair = null;
        if (p.b != null) p.b.pair = null;
        pairs.RemoveAt(index);
    }

    private void Release(int index)
    {
        var b = live[index];
        b.trail.Hide();
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
