using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// talismans that weave toward a random enemy, stick to it, burn it and slow it down. a talisman
// flies until it sticks: if its target dies it homes on the nearest other enemy, and with nobody
// left it carries on until it's off the screen. evolved, eight fly out at once and a talisman
// kill bursts, slowing everything around it
public class PeachTalismans : Weapon<PeachTalismansData>
{
    private sealed class Talisman
    {
        public SpriteRenderer sr;
        public Afterimage trail;
        public EnemyHealth target;
        public EnemyMovement move;
        public Vector2 pathPos;     // where it is along its path, before the swing
        public Vector2 dir;         // the way it was last headed
        public Vector2 stuckOffset;
        public float travelled, burnTimer, stuckFor, phase;
        public bool stuck;
    }

    // a stuck talisman renews its slow every frame; this is how long it outlasts the talisman
    private const float SlowLinger = 0.2f;

    private readonly List<Talisman> live = new List<Talisman>();
    private readonly Stack<Talisman> spare = new Stack<Talisman>();
    private readonly List<EnemyHealth> nearby = new List<EnemyHealth>();
    private Camera cam;
    private float timer;

    private void Update()
    {
        if (Data == null || Level <= 0) return;

        float dt = Time.deltaTime;
        if (dt <= 0f) return; // paused
        if (cam == null) cam = Camera.main;

        var lv = Data.At(Level);
        bool evolved = Data.IsEvolved(Level);

        // with nobody in range it waits, ready, and fires as soon as someone shows up
        timer += dt;
        if (timer >= Cooldown(lv.interval) && Fire(evolved)) timer = 0f;

        Step(dt, lv.damagePerSecond * Might, evolved);
    }

    private bool Fire(bool evolved)
    {
        Vector2 me = transform.position;
        int count = evolved ? Data.evolvedCount : 1;
        bool fired = false;

        for (int i = 0; i < count; i++)
        {
            var target = RandomEnemy(me, Data.range);
            if (target == null) break;

            float rad = 2f * Mathf.PI * i / count;
            Vector2 from = evolved ? me + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * 0.6f : me;
            Launch(from, target);
            fired = true;
        }
        return fired;
    }

    private void Launch(Vector2 from, EnemyHealth target)
    {
        var t = spare.Count > 0 ? spare.Pop() : NewTalisman();
        t.sr.gameObject.SetActive(true);
        t.sr.transform.SetPositionAndRotation(from, Quaternion.identity);
        t.phase = Random.value * 10f;
        if (Data.Animated)
        {
            t.sr.transform.localScale = Vector3.one;
            t.sr.sprite = Data.flightFrames[0];
        }
        else WeaponFx.Resize(t.sr, Data.talismanSize);
        t.trail.Restart();

        t.target = target;
        t.move = null;
        t.pathPos = from;
        t.dir = ((Vector2)target.transform.position - from).normalized;
        t.travelled = t.burnTimer = t.stuckFor = 0f;
        t.stuck = false;
        live.Add(t);
    }

    private Talisman NewTalisman()
    {
        var art = Data.Animated ? Data.flightFrames[0] : Data.talismanSprite;
        var sr = WeaponFx.Make(Fx, "Talisman", art, WeaponFx.Square, Data.talismanColor, Data.sortingLayer, Data.sortingOrder);
        return new Talisman { sr = sr, trail = new Afterimage(sr, Data.trailLength, Data.trailSpacing) };
    }

    private void Step(float dt, float dps, bool evolved)
    {
        float speed = Data.flightSpeed * SpeedMul;
        for (int i = live.Count - 1; i >= 0; i--)
        {
            var t = live[i];
            bool wasStuck = t.stuck;
            bool done = t.stuck ? Burn(t, dt, dps, evolved) : Fly(t, dt, speed);
            if (done) Release(i, wasStuck);
        }
    }

    // true once the talisman is finished
    private bool Fly(Talisman t, float dt, float speed)
    {
        float step = speed * dt;

        // its target died on the way: home on whoever is closest now
        if (!IsAlive(t.target)) t.target = NearestEnemy(t.pathPos);

        if (t.target == null)
        {
            // nobody left anywhere: keep going the way it was headed until it's off the screen
            t.pathPos += t.dir * step;
            t.travelled += step;
            Place(t, 1f);
            return cam != null ? !OnScreen(t.pathPos, cam) : t.travelled > 40f;
        }

        Vector2 goal = t.target.transform.position;
        Vector2 toGoal = goal - t.pathPos;
        float dist = toGoal.magnitude;

        if (dist <= Mathf.Max(0.3f, step))
        {
            t.stuck = true;
            t.move = t.target.GetComponent<EnemyMovement>();
            t.stuckOffset = Random.insideUnitCircle * 0.2f;
            t.sr.transform.SetPositionAndRotation(goal + t.stuckOffset, Quaternion.Euler(0f, 0f, Random.Range(-20f, 20f)));
            t.trail.Hide();
            return false;
        }

        t.dir = toGoal / dist;
        t.pathPos += t.dir * step;
        t.travelled += step;
        Place(t, Mathf.Clamp01(dist / 1.5f));
        return false;
    }

    // a sine swing across the path, fading out on the last stretch so it lands on its target
    private void Place(Talisman t, float swingScale)
    {
        float phase = t.travelled / Mathf.Max(0.1f, Data.waveLength) * 2f * Mathf.PI;
        float swing = Mathf.Sin(phase) * Data.waveAmplitude * swingScale;
        var side = new Vector2(-t.dir.y, t.dir.x);
        t.sr.transform.SetPositionAndRotation(t.pathPos + side * swing, Quaternion.Euler(0f, 0f, Mathf.Cos(phase) * 20f));
        if (Data.Animated) t.sr.sprite = Data.flightFrames[(int)((Time.time + t.phase) * Data.flightFps) % Data.flightFrames.Length];
        t.trail.Record();
    }

    // true once the talisman is finished
    private bool Burn(Talisman t, float dt, float dps, bool evolved)
    {
        if (!IsAlive(t.target)) return true;

        t.stuckFor += dt;
        if (t.stuckFor >= Data.stickSeconds) return true;

        Vector2 at = (Vector2)t.target.transform.position + t.stuckOffset;
        t.sr.transform.position = at;
        ShowBurn(t);

        // the enemy crawls while a talisman is on it. the strongest slow wins, so a second
        // talisman on the same enemy changes nothing
        if (t.move != null) t.move.ApplySlow(Data.stuckSlow, SlowLinger);

        float tick = Mathf.Max(0.05f, Data.tickSeconds);
        t.burnTimer += dt;
        while (t.burnTimer >= tick)
        {
            t.burnTimer -= tick;

            // a burn, not a hit, so it goes through armor
            if (Hit(t.target, dps * tick, true))
            {
                if (evolved) Blast(at);
                return true;
            }
        }
        return false;
    }

    // the burn frames: which step of burning down, from how long it has been stuck, flickering
    private void ShowBurn(Talisman t)
    {
        var frames = Data.burnFrames;
        if (frames == null || frames.Length < 2) return;
        int steps = frames.Length / 2;
        int step = Mathf.Min(steps - 1, (int)(t.stuckFor / Mathf.Max(0.01f, Data.stickSeconds) * steps));
        int flicker = (int)((Time.time + t.phase) * Data.burnFlickerFps) % 2;
        t.sr.sprite = frames[step * 2 + flicker];
    }

    private static EnemyHealth NearestEnemy(Vector2 from)
    {
        EnemyHealth best = null;
        float bestD = float.MaxValue;
        foreach (var go in EnemyRegistry.All)
        {
            if (go == null || !go.TryGetComponent(out EnemyHealth e) || !IsAlive(e)) continue;
            float d = ((Vector2)go.transform.position - from).sqrMagnitude;
            if (d < bestD)
            {
                bestD = d;
                best = e;
            }
        }
        return best;
    }

    // evolution: a talisman kill bursts and slows everything close by
    private void Blast(Vector2 at)
    {
        EnemiesIn(at, Data.blastRadius * AreaMul, nearby);
        foreach (var e in nearby)
            if (e.TryGetComponent(out EnemyMovement move)) move.ApplySlow(Data.slowFactor, Data.slowSeconds);

        StartCoroutine(BurstFx(at));
    }

    private IEnumerator BurstFx(Vector2 at)
    {
        var frames = Data.burstFrames;
        bool flipbook = frames != null && frames.Length > 0 && frames[0] != null;
        var sr = WeaponFx.Make(Fx, "Burst", flipbook ? frames[0] : Data.burstSprite, WeaponFx.Ring, Data.burstColor,
            Data.sortingLayer, Data.sortingOrder + 1);
        sr.transform.position = at;

        float diameter = Data.blastRadius * AreaMul * 2f;
        float seconds = Mathf.Max(0.05f, Data.burstSeconds);

        // the frames are drawn for a blast of Burst Art Radius, so one scale fits them all
        if (flipbook) sr.transform.localScale = Vector3.one * (Data.blastRadius * AreaMul / Mathf.Max(0.01f, Data.burstArtRadius));

        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            float k = t / seconds;
            if (flipbook)
            {
                var frame = frames[Mathf.Min(frames.Length - 1, (int)(k * frames.Length))];
                if (frame != null) sr.sprite = frame;
            }
            else
            {
                WeaponFx.Resize(sr, Mathf.Lerp(0.3f, diameter, k));
                WeaponFx.SetAlpha(sr, 1f - k);
            }
            yield return null;
        }
        Destroy(sr.gameObject);
    }

    private void Release(int index, bool crumble)
    {
        var t = live[index];
        if (crumble && Data.ashFx != null) FxOneShot.Play(Data.ashFx, t.sr.transform.position, t.sr.transform.eulerAngles.z);
        t.trail.Hide();
        t.sr.gameObject.SetActive(false);
        spare.Push(t);
        live.RemoveAt(index);
    }
}
