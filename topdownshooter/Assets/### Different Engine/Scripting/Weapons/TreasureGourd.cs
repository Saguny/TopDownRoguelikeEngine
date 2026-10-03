using System.Collections.Generic;
using UnityEngine;

// the Treasure Gourd hovers at the player's shoulder. every so often it picks out the furthest group
// of enemies it can reach, swings round in front of the player to face it, and the cork pops: for
// a moment and a half it pulls the small enemies there together (elites and bosses are too heavy),
// then it sprays a cone of holy fire over the clump, and whatever the fire touched burns for a
// while after. it keeps its mouth on that clump while it's open, however the player moves.
// evolved (the Gourd of Heaven and Earth) it pulls for longer and swallows enemy bullets as well,
// a sphere of plasma gathering in its mouth, and instead of the fire it throws that sphere: it
// bursts on the first enemy it meets, harder and wider for every bullet and enemy it caught, and
// sets everything in the burst burning. and evolved it's bigger, and leaves the player's side: it
// flies the screen on its own, picks out the thickest crowd on it, flies over to it and opens there.
// whichever it is, when the crowd it locked onto dies before it fires, it turns on the nearest
// enemy instead
public class TreasureGourd : Weapon<TreasureGourdData>
{
    private enum Phase { Idle, Fly, Pull, Spray }

    private sealed class Burn
    {
        public float left, tick, dps;
        public SpriteRenderer sr;
        public float phase;
    }

    private sealed class Sphere
    {
        public SpriteRenderer sr;
        public Afterimage trail;
        public Vector2 pos, dir;
        public float travelled, damage, radius, charge;
    }

    private const float WorldPpu = 37f / 1.3f;
    private const float CatchSeconds = 0.1f;   // the fire's first two frames: it reaches out over these
    private const float DieSeconds = 0.18f;    // and its last three, dying back

    private readonly Dictionary<EnemyHealth, Burn> burning = new Dictionary<EnemyHealth, Burn>();
    private readonly List<EnemyHealth> burnKeys = new List<EnemyHealth>();
    private readonly Stack<SpriteRenderer> spareBurns = new Stack<SpriteRenderer>();
    private readonly HashSet<EnemyHealth> scorched = new HashSet<EnemyHealth>();
    private readonly HashSet<EnemyHealth> held = new HashSet<EnemyHealth>();
    private readonly List<EnemyHealth> nearby = new List<EnemyHealth>();
    private readonly List<EnemyBullet> bullets = new List<EnemyBullet>();
    private readonly List<Sphere> spheres = new List<Sphere>();
    private readonly Stack<Sphere> spareSpheres = new Stack<Sphere>();

    private Phase phase = Phase.Idle;
    private float timer, phaseTime, age;
    private Vector2 heading = Vector2.right, aim = Vector2.right, gourdPos;
    private Vector2 target;         // the clump it's locked onto, in the world
    private readonly List<EnemyHealth> group = new List<EnemyHealth>();
    private bool placed;
    private int swallowed;
    private Rigidbody2D body;
    private SpriteRenderer gourd, cone, charge;
    private AudioSource voice;
    private bool voiceHeld;
    // evolved, it flies the screen on its own
    private bool roaming;
    private float size = 1f, wanderLeft;
    private Vector2 flyVelocity, wander;
    private Afterimage flyTrail;

    protected override void Awake()
    {
        base.Awake();
        body = GetComponent<Rigidbody2D>();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (voice != null) voice.Stop();
        phase = Phase.Idle;
    }

    private void Update()
    {
        if (Data == null || Level <= 0) return;
        if (gourd == null) MakeParts();

        // while the game is stopped the pull's long sound waits where it is
        bool stopped = Time.timeScale <= 0f;
        if (stopped != voiceHeld && voice != null)
        {
            voiceHeld = stopped;
            if (stopped) voice.Pause();
            else voice.UnPause();
        }
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        age += dt;

        var lv = Data.At(Level);
        bool evolved = Data.IsEvolved(Level);
        if (evolved != roaming)
        {
            roaming = evolved;
            flyVelocity = Vector2.zero;
            wanderLeft = 0f;
            if (phase == Phase.Fly) phase = Phase.Idle;
        }
        size = roaming ? Data.evolvedSize : 1f;
        TrackHeading(dt);

        switch (phase)
        {
            case Phase.Idle:
                timer += dt;
                // with nobody in reach it waits, ready, and opens as soon as someone comes. roaming,
                // it first flies over to the crowd it picked
                if (timer >= Cooldown(lv.cooldown))
                {
                    if (!roaming) { if (FindGroup(Wide(lv.suctionRadius), out target)) Uncork(evolved); }
                    else if (FindCrowd(lv, out target))
                    {
                        phase = Phase.Fly;
                        phaseTime = 0f;
                        if (flyTrail != null) flyTrail.Restart();
                    }
                }
                break;
            case Phase.Fly:
                phaseTime += dt;
                if ((Standoff(lv) - gourdPos).sqrMagnitude < 0.5f * 0.5f || phaseTime >= Data.flySeconds)
                {
                    if (flyTrail != null) flyTrail.Hide();
                    Uncork(evolved);
                }
                break;
            case Phase.Pull:
                phaseTime += dt;
                Pull(lv, evolved, dt);
                if (phaseTime >= (evolved ? Data.evolvedSuctionSeconds : Data.suctionSeconds))
                {
                    if (evolved) Launch(lv);
                    else StartSpray();
                }
                break;
            case Phase.Spray:
                phaseTime += dt;
                Spray(lv);
                if (phaseTime >= Data.spraySeconds) Close();
                break;
        }

        Burns(dt);
        Spheres(dt, lv);
        if (roaming) Roam(lv, dt);
        Show(lv, evolved, dt);
    }

    private float Wide(float units) => units * AreaMul;

    // where it looks from: the player, or roaming, the gourd itself
    private Vector2 From => roaming ? gourdPos : (Vector2)transform.position;

    // ---------------------------------------------------------------- aiming

    // the way the player is walking (for where it rests at their shoulder). open, it turns to keep
    // its mouth on the clump it locked onto
    private void TrackHeading(float dt)
    {
        Vector2 v = body != null ? body.linearVelocity : Vector2.zero;
        if (v.sqrMagnitude > 0.04f) heading = v.normalized;

        if (phase == Phase.Idle) aim = heading;
        else if (phase == Phase.Fly)
        {
            if (flyVelocity.sqrMagnitude > 0.04f) aim = flyVelocity.normalized;
        }
        else
        {
            Vector2 to = target - From;
            if (to.sqrMagnitude < 0.01f) return;
            float step = Data.turnRate * dt;
            float angle = Vector2.SignedAngle(aim, to);
            aim = Rotate(aim, Mathf.Clamp(angle, -step, step)).normalized;
        }
    }

    // the furthest group of enemies it could pull: every enemy in reach is tried as a direction, and
    // each direction counts who'd be in the cone. of the directions with a real group in them (Min
    // Group or more), the one whose group sits furthest out wins; with no group that size anywhere,
    // the biggest there is. with only bosses and elites in reach (a boss fought alone), the nearest
    // of them: it can't pull them, but it still fires at them. false when there's nobody in reach
    private bool FindGroup(float reach, out Vector2 centre)
    {
        Vector2 me = transform.position;
        centre = me;
        // the cone starts at the mouth, about a unit in front of the player
        float far = reach + 1.1f;
        EnemiesIn(me, far, nearby);
        group.Clear();
        foreach (var e in nearby) if (!Heavy(e)) group.Add(e);
        if (group.Count == 0)
        {
            float closest = float.MaxValue;
            foreach (var e in nearby)
            {
                float d = ((Vector2)e.transform.position - me).sqrMagnitude;
                if (d < closest) { closest = d; centre = e.transform.position; }
            }
            return nearby.Count > 0;
        }

        float half = Data.suctionHalfAngle * 0.85f;
        int step = Mathf.Max(1, group.Count / 48);             // a big crowd: try every few, not all
        int bestCount = 0, bigCount = 0;
        float bestFar = -1f;
        Vector2 best = me, big = me;
        for (int i = 0; i < group.Count; i += step)
        {
            Vector2 dir = ((Vector2)group[i].transform.position - me).normalized;
            if (dir.sqrMagnitude < 0.5f) continue;
            int count = 0;
            Vector2 sum = Vector2.zero;
            foreach (var e in group)
            {
                Vector2 off = (Vector2)e.transform.position - me;
                if (Vector2.Angle(dir, off) > half) continue;
                count++;
                sum += off;
            }
            if (count == 0) continue;
            Vector2 mid = me + sum / count;
            float distance = (mid - me).magnitude;
            if (count >= Data.minGroup && distance > bestFar) { bestFar = distance; bestCount = count; best = mid; }
            if (count > bigCount) { bigCount = count; big = mid; }
        }
        centre = bestCount > 0 ? best : big;
        return true;
    }

    // the nearest enemy within `reach` of `from`, heavy or not (it can't pull those, but it can burn them)
    private bool Nearest(Vector2 from, float reach, out Vector2 at)
    {
        at = from;
        EnemiesIn(from, reach, nearby);
        float closest = float.MaxValue;
        foreach (var e in nearby)
        {
            if (!IsAlive(e)) continue;
            float d = ((Vector2)e.transform.position - from).sqrMagnitude;
            if (d < closest) { closest = d; at = e.transform.position; }
        }
        return closest < float.MaxValue;
    }

    // roaming: the thickest crowd of small enemies on screen, the nearer the better when two are
    // alike; with only heavy ones on screen, the nearest of them. false when the screen's empty
    private bool FindCrowd(TreasureGourdData.LevelStats lv, out Vector2 centre)
    {
        centre = gourdPos;
        Vector2 eye = View(out Vector2 half);
        EnemiesIn(eye, half.magnitude, nearby);
        group.Clear();
        foreach (var e in nearby)
        {
            Vector2 p = e.transform.position;
            if (Mathf.Abs(p.x - eye.x) > half.x || Mathf.Abs(p.y - eye.y) > half.y) continue;
            if (!Heavy(e)) group.Add(e);
        }
        if (group.Count == 0)
        {
            if (!Nearest(gourdPos, half.magnitude * 2f, out centre)) return false;
            return Mathf.Abs(centre.x - eye.x) <= half.x && Mathf.Abs(centre.y - eye.y) <= half.y;
        }

        float r = Wide(lv.suctionRadius) * 0.6f, r2 = r * r;
        int step = Mathf.Max(1, group.Count / 40);
        float bestScore = float.MinValue;
        for (int i = 0; i < group.Count; i += step)
        {
            Vector2 at = group[i].transform.position;
            int count = 0;
            Vector2 sum = Vector2.zero;
            foreach (var e in group)
            {
                Vector2 p = e.transform.position;
                if ((p - at).sqrMagnitude > r2) continue;
                count++;
                sum += p;
            }
            Vector2 mid = sum / count;
            float score = count - 0.15f * (mid - gourdPos).magnitude;
            if (score > bestScore) { bestScore = score; centre = mid; }
        }
        return true;
    }

    // roaming: where it hangs to open on its crowd, off the crowd's edge on the side it came from,
    // kept on screen
    private Vector2 Standoff(TreasureGourdData.LevelStats lv)
    {
        Vector2 away = gourdPos - target;
        away = away.sqrMagnitude > 0.01f ? away.normalized : Vector2.up;
        return InView(target + away * Wide(lv.suctionRadius) * 0.55f, 0.8f);
    }

    // the camera's centre and half its size, in the world
    private static Vector2 View(out Vector2 half)
    {
        var cam = Camera.main;
        if (cam == null || !cam.orthographic) { half = new Vector2(9f, 5f); return cam != null ? (Vector2)cam.transform.position : Vector2.zero; }
        half = new Vector2(cam.orthographicSize * cam.aspect, cam.orthographicSize);
        return cam.transform.position;
    }

    // `p` pulled inside the screen, `margin` units in from its edges
    private static Vector2 InView(Vector2 p, float margin)
    {
        Vector2 eye = View(out Vector2 half);
        half -= new Vector2(margin, margin);
        return new Vector2(Mathf.Clamp(p.x, eye.x - half.x, eye.x + half.x), Mathf.Clamp(p.y, eye.y - half.y, eye.y + half.y));
    }

    // the crowd it locked onto has gone (killed before it fired): it turns on the nearest enemy
    private void Retarget(float reach)
    {
        if (Nearest(From, reach, out Vector2 at)) target = at;
    }

    // about to fire at nothing: it snaps round onto the nearest enemy instead
    private void Reaim(float reach)
    {
        Vector2 mouth = Mouth;
        EnemiesIn(mouth, reach, nearby);
        foreach (var e in nearby)
            if (IsAlive(e) && InCone((Vector2)e.transform.position - mouth, Data.flameHalfAngle + 6f, 0.6f)) return;
        if (!Nearest(From, reach + 1.5f, out Vector2 at)) return;
        target = at;
        Vector2 to = at - From;
        if (to.sqrMagnitude > 0.01f) aim = to.normalized;
    }

    private Vector2 Mouth => gourdPos + aim * (Data.aimMouthPixels / WorldPpu * size);

    // ---------------------------------------------------------------- the pull

    private void Uncork(bool evolved)
    {
        phase = Phase.Pull;
        phaseTime = 0f;
        swallowed = 0;
        held.Clear();
        Vector2 to = target - (Vector2)transform.position;
        aim = to.sqrMagnitude > 0.01f ? to.normalized : heading;
        // it swings round in front straight away, so the pop is where the mouth will be (roaming,
        // it's already where it opens)
        if (!roaming) gourdPos = (Vector2)transform.position + aim * 0.75f;
        else
        {
            to = target - gourdPos;
            aim = to.sqrMagnitude > 0.01f ? to.normalized : aim;
        }
        FxBatch.PlayShot(Data.popFrames, 25f, Mouth, 1f, Data.sortingLayer, Data.sortingOrder + 3);
        OneShot(Data.uncorkSound, Mouth);
        Hold(evolved ? Data.chargeSound : Data.pullSound);
    }

    private void Pull(TreasureGourdData.LevelStats lv, bool evolved, float dt)
    {
        Vector2 mouth = Mouth, gather = mouth + aim * Data.gatherDistance;
        float reach = Wide(lv.suctionRadius);
        EnemiesIn(mouth, reach, nearby);
        Vector2 sum = Vector2.zero;
        int caught = 0;
        foreach (var e in nearby)
        {
            if (Heavy(e) || !InCone((Vector2)e.transform.position - mouth, Data.suctionHalfAngle, 0.8f)) continue;
            sum += (Vector2)e.transform.position;
            caught++;
            if (!e.TryGetComponent(out EnemyMovement move)) continue;
            Vector2 to = gather - (Vector2)e.transform.position;
            float speed = Mathf.Min(Data.pullSpeed, to.magnitude * 4f);
            move.Shove(to.normalized * speed, 0.12f);
            move.ApplySlow(Data.heldSlow, 0.12f);
            if (evolved) held.Add(e);
        }
        // it keeps its mouth on the clump as it gathers, wherever the player walks; the clump killed
        // before it fires, it turns on the nearest enemy left
        if (caught > 0) target = sum / caught;
        else Retarget(reach + 1.5f);

        if (!evolved) return;

        // evolved: enemy bullets in reach are drawn off course into the mouth and swallowed
        bullets.Clear();
        bullets.AddRange(EnemyBullet.Live);
        float r2 = reach * reach;
        foreach (var b in bullets)
        {
            if (b == null || !b.isActiveAndEnabled) continue;
            Vector2 off = (Vector2)b.transform.position - mouth;
            if (off.sqrMagnitude > r2) continue;
            if (off.sqrMagnitude < 0.35f * 0.35f)
            {
                b.Swallow();
                swallowed++;
                FxBatch.PlayShot(Data.absorbFrames, 25f, mouth, 1f, Data.sortingLayer, Data.sortingOrder + 4);
                OneShot(Data.absorbSound, mouth, Random.Range(0.95f, 1.1f) + 0.02f * Mathf.Min(swallowed, 10));
                continue;
            }
            b.PullToward(mouth, Data.bulletPullSpeed);
        }
        // and a boss's danmaku
        int caughtShots = Danmaku.Absorb(mouth, reach, Data.bulletPullSpeed);
        if (caughtShots > 0)
        {
            swallowed += caughtShots;
            FxBatch.PlayShot(Data.absorbFrames, 25f, mouth, 1f, Data.sortingLayer, Data.sortingOrder + 4);
            OneShot(Data.absorbSound, mouth, Random.Range(0.95f, 1.1f) + 0.02f * Mathf.Min(swallowed, 10));
        }
    }

    // elites and bosses are too heavy to be pulled
    private static bool Heavy(EnemyHealth e) =>
        e.TryGetComponent(out BossMarker _) || e.TryGetComponent(out EliteOutline _) || e.TryGetComponent(out SecretBossBehavior _);

    // inside the cone the gourd faces, or close enough to the mouth not to matter
    private bool InCone(Vector2 off, float halfAngle, float close)
    {
        if (off.sqrMagnitude < close * close) return true;
        return Vector2.Angle(aim, off) <= halfAngle;
    }

    // ---------------------------------------------------------------- the fire

    private void StartSpray()
    {
        phase = Phase.Spray;
        phaseTime = 0f;
        scorched.Clear();
        if (voice != null) voice.Stop();
        Reaim(Wide(Data.At(Level).flameLength));
        OneShot(Data.flameSound, Mouth);
    }

    private void Spray(TreasureGourdData.LevelStats lv)
    {
        if (phaseTime > Data.spraySeconds - DieSeconds) return;   // dying back, it no longer burns anything new

        Vector2 mouth = Mouth;
        float reach = Wide(lv.flameLength) * Mathf.Clamp01(phaseTime / CatchSeconds + 0.35f);
        float spread = Mathf.Tan(Data.flameHalfAngle * Mathf.Deg2Rad);
        EnemiesIn(mouth, reach, nearby);
        foreach (var e in nearby)
        {
            if (scorched.Contains(e)) continue;
            Vector2 off = (Vector2)e.transform.position - mouth;
            float along = Vector2.Dot(off, aim);
            if (along < -0.3f || along > reach) continue;
            float across = Mathf.Abs(off.x * aim.y - off.y * aim.x);
            if (across > spread * Mathf.Max(0f, along) + 0.5f) continue;

            scorched.Add(e);
            if (!Hit(e, lv.damage * Might)) Ignite(e, lv);
        }
    }

    private void Close()
    {
        phase = Phase.Idle;
        timer = 0f;
        if (voice != null) voice.Stop();
    }

    // ---------------------------------------------------------------- burning

    private void Ignite(EnemyHealth e, TreasureGourdData.LevelStats lv)
    {
        if (!IsAlive(e)) return;
        if (burning.TryGetValue(e, out var b))
        {
            b.left = Mathf.Max(b.left, lv.burnSeconds);
            b.dps = Mathf.Max(b.dps, lv.burnDps);
            return;
        }
        b = new Burn { left = lv.burnSeconds, dps = lv.burnDps, phase = Random.value * 10f };
        if (burning.Count < Data.maxBurnVisuals && Data.burnFrames != null && Data.burnFrames.Length > 0)
        {
            b.sr = spareBurns.Count > 0 ? spareBurns.Pop()
                : WeaponFx.Make(Fx, "Holy Fire", Data.burnFrames[0], WeaponFx.Disc, Color.white, Data.sortingLayer, Data.sortingOrder + 1);
            b.sr.gameObject.SetActive(true);
        }
        burning.Add(e, b);
    }

    private void Burns(float dt)
    {
        if (burning.Count == 0) return;
        burnKeys.Clear();
        burnKeys.AddRange(burning.Keys);
        float tick = Mathf.Max(0.05f, Data.burnTick);
        foreach (var e in burnKeys)
        {
            var b = burning[e];
            bool done = !IsAlive(e);
            if (!done)
            {
                b.left -= dt;
                b.tick += dt;
                while (!done && b.tick >= tick)
                {
                    b.tick -= tick;
                    // a burn, not a hit, so it goes through armor
                    if (Hit(e, b.dps * tick * Might, true)) done = true;
                }
                if (b.left <= 0f) done = true;
            }
            if (done)
            {
                if (b.sr != null)
                {
                    b.sr.gameObject.SetActive(false);
                    spareBurns.Push(b.sr);
                }
                burning.Remove(e);
                continue;
            }
            if (b.sr != null)
            {
                b.sr.transform.position = e.transform.position + Vector3.up * 0.05f;
                var frames = Data.burnFrames;
                b.sr.sprite = frames[(int)((age + b.phase) * Data.fps) % frames.Length];
            }
        }
    }

    // ---------------------------------------------------------------- the evolution's sphere

    public override void ClearShots()
    {
        foreach (var s in spheres)
        {
            s.trail.Hide();
            s.sr.gameObject.SetActive(false);
            spareSpheres.Push(s);
        }
        spheres.Clear();
        if (voice != null) voice.Stop();
        if (flyTrail != null) flyTrail.Hide();
        phase = Phase.Idle;
        timer = 0f;
    }

    private void Launch(TreasureGourdData.LevelStats lv)
    {
        int fromBullets = Mathf.Min(swallowed, Data.maxCharge);
        int fromEnemies = Mathf.Min(held.Count, Data.maxCharge - fromBullets);
        float full = (fromBullets + fromEnemies) / (float)Data.maxCharge;
        Reaim(Data.sphereRange);

        var s = spareSpheres.Count > 0 ? spareSpheres.Pop() : NewSphere();
        s.sr.gameObject.SetActive(true);
        s.pos = Mouth;
        s.dir = aim;
        s.travelled = 0f;
        s.charge = full;
        s.damage = (Data.sphereDamage + Data.damagePerBullet * fromBullets + Data.damagePerEnemy * fromEnemies) * Might;
        s.radius = Wide(Data.blastRadius) * (1f + Data.blastGrowth * full);
        s.sr.transform.position = s.pos;
        s.sr.transform.localScale = Vector3.one * Mathf.Lerp(Data.sphereScale.x, Data.sphereScale.y, full);
        s.trail.Restart();
        spheres.Add(s);

        OneShot(Data.launchSound, s.pos, Mathf.Lerp(1.08f, 0.9f, full));
        FxBatch.PlayShot(Data.popFrames, 25f, Mouth, 1.4f, Data.sortingLayer, Data.sortingOrder + 3);
        Close();
    }

    private Sphere NewSphere()
    {
        var art = Data.orbFrames != null && Data.orbFrames.Length > 0 ? Data.orbFrames[0] : null;
        var sr = WeaponFx.Make(Fx, "Plasma Sphere", art, WeaponFx.Disc, new Color(0.7f, 0.45f, 1f), Data.sortingLayer, Data.sortingOrder + 2);
        if (art == null) WeaponFx.Resize(sr, 0.6f);
        return new Sphere { sr = sr, trail = new Afterimage(sr, 4, 0.3f, 0.5f, 0.5f) };
    }

    private void Spheres(float dt, TreasureGourdData.LevelStats lv)
    {
        float speed = Data.sphereSpeed * SpeedMul;
        for (int i = spheres.Count - 1; i >= 0; i--)
        {
            var s = spheres[i];
            float step = speed * dt;
            s.pos += s.dir * step;
            s.travelled += step;
            s.sr.transform.position = s.pos;
            if (Data.orbFrames != null && Data.orbFrames.Length > 0)
                s.sr.sprite = Data.orbFrames[(int)(age * Data.fps) % Data.orbFrames.Length];
            s.trail.Record();

            // it bursts on the first enemy it meets, or at the end of its flight
            float touch = 0.3f * s.sr.transform.localScale.x;
            EnemiesIn(s.pos, touch, nearby);
            if (nearby.Count == 0 && s.travelled < Data.sphereRange) continue;

            Burst(s, lv);
            s.trail.Hide();
            s.sr.gameObject.SetActive(false);
            spareSpheres.Push(s);
            spheres.RemoveAt(i);
        }
    }

    private void Burst(Sphere s, TreasureGourdData.LevelStats lv)
    {
        EnemiesIn(s.pos, s.radius, nearby);
        foreach (var e in nearby)
            if (!Hit(e, s.damage)) Ignite(e, lv);

        float scale = s.radius / Mathf.Max(0.01f, Data.blastArtRadius);
        FxBatch.PlayShot(Data.blastFrames, Data.fps, s.pos, scale, Data.sortingLayer, Data.sortingOrder + 5);
        OneShot(Data.blastSound, s.pos, Mathf.Lerp(1.05f, 0.88f, s.charge));
        Juice.Shake(0.12f + 0.12f * s.charge);
    }

    // ---------------------------------------------------------------- roaming (evolved)

    // flying to its crowd it goes fast; open, it hangs where it is; otherwise it drifts about the
    // screen. it never leaves the screen: when the player runs on it's dragged along at the edge
    private void Roam(TreasureGourdData.LevelStats lv, float dt)
    {
        if (!placed) gourdPos = (Vector2)transform.position + new Vector2(-0.55f, 0.7f);
        Vector2 goal;
        float speed;
        switch (phase)
        {
            case Phase.Fly:
                goal = Standoff(lv);
                speed = Data.flySpeed * SpeedMul;
                break;
            case Phase.Idle:
                wanderLeft -= dt;
                if (wanderLeft <= 0f || (wander - gourdPos).sqrMagnitude < 0.3f || InView(wander, 1.2f) != wander)
                {
                    Vector2 eye = View(out Vector2 half);
                    wander = eye + new Vector2(Random.Range(-1f, 1f) * Mathf.Max(0f, half.x - 1.5f), Random.Range(-1f, 1f) * Mathf.Max(0f, half.y - 1.5f));
                    wanderLeft = Random.Range(1.8f, 3.2f);
                }
                goal = wander;
                speed = Data.wanderSpeed;
                break;
            default:
                goal = gourdPos;
                speed = 0f;
                break;
        }
        Vector2 to = goal - gourdPos;
        float d = to.magnitude;
        Vector2 want = d > 0.01f ? to / d * Mathf.Min(speed, d * 4f) : Vector2.zero;
        flyVelocity = Vector2.Lerp(flyVelocity, want, 1f - Mathf.Exp(-dt * (phase == Phase.Fly ? 7f : 3f)));
        gourdPos = InView(gourdPos + flyVelocity * dt, 0.5f);

        if (flyTrail == null) return;
        if (phase == Phase.Fly) flyTrail.Record();
    }

    // ---------------------------------------------------------------- showing it

    private void MakeParts()
    {
        var idle = Data.Animated ? Data.gourdFrames[0] : null;
        gourd = WeaponFx.Make(Fx, "Treasure Gourd", idle, WeaponFx.Disc, new Color(0.9f, 0.65f, 0.25f), Data.sortingLayer, Data.sortingOrder);
        if (idle == null) WeaponFx.Resize(gourd, 0.6f);
        cone = WeaponFx.Make(Fx, "Gourd Cone", null, null, Color.white, Data.sortingLayer, Data.sortingOrder - 1);
        charge = WeaponFx.Make(Fx, "Gourd Charge", null, null, Color.white, Data.sortingLayer, Data.sortingOrder + 2);
        cone.enabled = charge.enabled = false;
        flyTrail = new Afterimage(gourd, 5, 0.35f, 0.45f, 0.7f);

        voice = AudioRouting.Route(new GameObject("Gourd Voice").AddComponent<AudioSource>());
        voice.transform.SetParent(Fx, false);
        voice.playOnAwake = false;
        voice.spatialBlend = 1f;    // like SfxPlayer's voices, so it sits in the mix with the rest
    }

    private void Show(TreasureGourdData.LevelStats lv, bool evolved, float dt)
    {
        Vector2 me = transform.position;
        bool open;

        // at rest it bobs at the shoulder behind the way they face; open, it swings round in front.
        // roaming, it's where Roam flew it
        open = phase == Phase.Pull || phase == Phase.Spray;
        if (!roaming)
        {
            Vector2 want = open
                ? me + aim * 0.75f
                : me + new Vector2(heading.x >= 0f ? -0.55f : 0.55f, 0.7f + Mathf.Sin(age * 2.6f) * 0.06f);
            gourdPos = placed ? Vector2.Lerp(gourdPos, want, 1f - Mathf.Exp(-dt * (open ? 30f : 10f))) : want;
        }
        placed = true;

        var t = gourd.transform;
        t.position = roaming && !open ? gourdPos + Vector2.up * (Mathf.Sin(age * 2.6f) * 0.08f) : gourdPos;
        t.localScale = Vector3.one * size;
        if (open)
        {
            t.rotation = Quaternion.Euler(0f, 0f, FxOneShot.Angle(aim));
            gourd.flipY = aim.x < 0f;
            var frames = Data.aimFrames;
            if (frames != null && frames.Length >= 8)
                gourd.sprite = frames[(phase == Phase.Pull ? 0 : 4) + (int)(age * Data.fps) % 4];
        }
        else
        {
            // flying, it leans into the way it's going
            t.rotation = Quaternion.Euler(0f, 0f, roaming ? Mathf.Clamp(-flyVelocity.x * 2.5f, -25f, 25f) : 0f);
            gourd.flipY = false;
            if (Data.Animated) gourd.sprite = Data.gourdFrames[(int)(age * 7f) % Data.gourdFrames.Length];
        }
        if (voice != null) voice.transform.position = gourdPos;

        // the pull or the fire, laid out from the mouth
        cone.enabled = false;
        if (phase == Phase.Pull && Data.suckFrames != null && Data.suckFrames.Length > 0)
            Lay(Data.suckFrames[(int)(age * 20f) % Data.suckFrames.Length], Data.suckMouthPixels, Wide(lv.suctionRadius), Data.suckArtLength);
        else if (phase == Phase.Spray && Data.flameFrames != null && Data.flameFrames.Length >= 11)
        {
            int frame = phaseTime < CatchSeconds ? Mathf.Min(1, (int)(phaseTime / CatchSeconds * 2f))
                : phaseTime > Data.spraySeconds - DieSeconds ? 8 + Mathf.Min(2, (int)((phaseTime - (Data.spraySeconds - DieSeconds)) / DieSeconds * 3f))
                : 2 + (int)(age * 18f) % 6;
            Lay(Data.flameFrames[frame], Data.flameMouthPixels, Wide(lv.flameLength), Data.flameArtLength);
        }

        // evolved, the sphere gathering in the mouth as it pulls, bigger for everything it caught
        charge.enabled = phase == Phase.Pull && evolved && Data.orbFrames != null && Data.orbFrames.Length > 0;
        if (charge.enabled)
        {
            float grow = Mathf.Clamp01(phaseTime / Mathf.Max(0.1f, Data.evolvedSuctionSeconds));
            float full = Mathf.Min(1f, (swallowed + held.Count) / (float)Data.maxCharge);
            charge.sprite = Data.orbFrames[(int)(age * Data.fps) % Data.orbFrames.Length];
            charge.transform.position = Mouth + aim * (0.2f * size);
            charge.transform.localScale = Vector3.one * Mathf.Lerp(0.25f, Mathf.Lerp(Data.sphereScale.x, Data.sphereScale.y, full), grow);
        }
    }

    // a cone sprite drawn pointing right with its mouth `mouthPixels` left of its centre, laid out
    // from the gourd's mouth along the aim and scaled so it reaches `length`
    private void Lay(Sprite frame, float mouthPixels, float length, float artLength)
    {
        cone.enabled = true;
        cone.sprite = frame;
        float scale = length / Mathf.Max(0.01f, artLength / WorldPpu);
        cone.transform.localScale = Vector3.one * scale;
        cone.transform.position = Mouth + aim * (mouthPixels / WorldPpu * scale);
        cone.transform.rotation = Quaternion.Euler(0f, 0f, FxOneShot.Angle(aim));
        cone.flipY = aim.x < 0f;
    }

    // ---------------------------------------------------------------- sound

    private void OneShot(AudioClip clip, Vector2 at, float pitch = 1f)
    {
        if (clip != null) SfxPlayer.PlayAt(clip, at, Data.soundVolume, pitch);
    }

    // the pull's long sound, on a voice of its own so the horde's hits can't cut it off
    private void Hold(AudioClip clip)
    {
        if (voice == null || clip == null) return;
        voice.Stop();
        voice.clip = clip;
        voice.volume = Data.soundVolume;
        voice.pitch = 1f;
        voice.Play();
    }

    private static Vector2 Rotate(Vector2 v, float degrees)
    {
        float r = degrees * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }
}
