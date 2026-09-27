using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Yu Xian Shan: every cooldown clouds wander across the screen, snowing on the ground under them.
// whatever the snow falls on freezes and takes a hit, and the clouds leave snow piles behind that
// stay a while and freeze whatever walks into them. evolved, a frost tornado touches down somewhere
// on screen away from the player every so often: it drags the horde in, and sprays snow across
// the field that freezes everything near it and heaps up more piles
public class IceCloud : Weapon<IceCloudData>
{
    private sealed class Cloud
    {
        public SpriteRenderer cloud, snow;
        public Vector2 fromView, toView;
        public float age, life, wobble, pileTimer;
    }

    private sealed class Pile
    {
        public SpriteRenderer sr;
        public Vector2 at;
        public float born;
    }

    private readonly List<Cloud> clouds = new List<Cloud>();
    private readonly Stack<Cloud> spareClouds = new Stack<Cloud>();
    private readonly List<Pile> piles = new List<Pile>();
    private readonly Stack<Pile> sparePiles = new Stack<Pile>();
    private readonly Dictionary<EnemyHealth, float> nextSnow = new Dictionary<EnemyHealth, float>();
    private readonly Dictionary<EnemyHealth, float> pileImmuneUntil = new Dictionary<EnemyHealth, float>();
    private readonly List<EnemyHealth> touching = new List<EnemyHealth>();
    private readonly List<EnemyHealth> forget = new List<EnemyHealth>();
    private Camera cam;
    private float timer, tornadoTimer, pileCheck, forgetTimer;
    private bool primed;

    protected override void OnLevelChanged()
    {
        if (primed || Data == null) return;
        primed = true;
        timer = Mathf.Max(0f, Cooldown(Data.At(Level).cooldown) - 1f);
    }

    private void Update()
    {
        if (Data == null || Level <= 0) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return; // paused
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        var lv = Data.At(Level);
        timer += dt;
        if (timer >= Cooldown(lv.cooldown))
        {
            timer = 0f;
            for (int i = 0; i < lv.clouds; i++) Launch(i, lv.clouds);
        }

        if (Data.IsEvolved(Level))
        {
            tornadoTimer += dt;
            if (tornadoTimer >= Cooldown(Data.tornadoCooldown))
            {
                tornadoTimer = 0f;
                StartCoroutine(Tornado());
            }
        }

        MoveClouds(dt, lv);
        TendPiles(dt);
        Forget(dt);
    }

    // ---------------------------------------------------------------- clouds

    private void Launch(int index, int count)
    {
        var c = spareClouds.Count > 0 ? spareClouds.Pop() : NewCloud();
        c.cloud.gameObject.SetActive(true);
        c.snow.gameObject.SetActive(true);

        // in from one side, out of the other, each on its own band of the screen
        bool rightward = Random.value < 0.5f;
        float band = (index + 0.5f) / count;
        float y0 = Mathf.Clamp(Mathf.Lerp(0.2f, 0.8f, band) + Random.Range(-0.12f, 0.12f), 0.15f, 0.85f);
        float y1 = Mathf.Clamp(y0 + Random.Range(-0.25f, 0.25f), 0.15f, 0.85f);
        c.fromView = new Vector2(rightward ? -0.15f : 1.15f, y0);
        c.toView = new Vector2(rightward ? 1.15f : -0.15f, y1);
        c.age = -index * 0.6f;
        c.life = Data.driftSeconds / Mathf.Max(0.1f, SpeedMul);
        c.wobble = Random.value * 10f;
        c.pileTimer = 0f;
        clouds.Add(c);
    }

    private Cloud NewCloud()
    {
        var c = new Cloud
        {
            cloud = WeaponFx.Make(Fx, "Cloud", null, WeaponFx.Disc, Data.cloudColor, Data.cloudLayer, Data.cloudOrder),
            // only the snow itself: no marker on the ground under it
            snow = WeaponFx.Make(Fx, "Snowfall", null, null, Color.white, Data.cloudLayer, Data.cloudOrder - 1),
        };
        return c;
    }

    private void MoveClouds(float dt, IceCloudData.LevelStats lv)
    {
        float radius = lv.radius * AreaMul, now = Time.time;
        float z = -cam.transform.position.z;
        int frame = (int)(now * Data.cloudFps);

        for (int i = clouds.Count - 1; i >= 0; i--)
        {
            var c = clouds[i];
            c.age += dt;
            if (c.age < 0f)
            {
                c.cloud.enabled = c.snow.enabled = false;
                continue;
            }
            if (c.age >= c.life)
            {
                c.cloud.gameObject.SetActive(false);
                c.snow.gameObject.SetActive(false);
                spareClouds.Push(c);
                clouds.RemoveAt(i);
                continue;
            }

            // it rides the screen, so it crosses it whatever the player does, bobbing as it goes
            float k = c.age / c.life;
            var view = Vector2.Lerp(c.fromView, c.toView, k) + new Vector2(0f, Mathf.Sin(now * 0.9f + c.wobble) * 0.03f);
            Vector2 ground = cam.ViewportToWorldPoint(new Vector3(view.x, view.y, z));

            c.cloud.enabled = true;
            c.cloud.transform.position = ground + Vector2.up * Data.cloudHeight;
            c.snow.transform.position = ground;
            if (Data.Animated)
            {
                c.cloud.sprite = Pick(Data.cloudFrames, frame + (int)c.wobble);
                // a little wider than the snow it drops
                float cloudWidth = c.cloud.sprite.bounds.size.x;
                c.cloud.transform.localScale = Vector3.one * (radius * 2.1f / Mathf.Max(0.01f, cloudWidth));
                var snow = Pick(Data.snowFrames, frame + (int)c.wobble);
                c.snow.sprite = snow;
                c.snow.enabled = snow != null;
                c.snow.transform.localScale = Vector3.one * (radius / Mathf.Max(0.01f, Data.snowArtRadius));
            }
            else
            {
                WeaponFx.Resize(c.cloud, radius * 2.2f);
                c.snow.enabled = false;
            }

            Snow(ground, radius, lv, now);

            // and it leaves piles behind it
            c.pileTimer += dt;
            if (c.pileTimer >= Data.pileInterval)
            {
                c.pileTimer = 0f;
                DropPile(ground + Random.insideUnitCircle * radius * 0.8f);
            }
        }
    }

    // everything under the cloud freezes and takes a hit, each enemy once every Snow Tick
    private void Snow(Vector2 ground, float radius, IceCloudData.LevelStats lv, float now)
    {
        EnemiesIn(ground, radius, touching);
        foreach (var e in touching)
        {
            if (nextSnow.TryGetValue(e, out float ready) && now < ready) continue;
            nextSnow[e] = now + Data.snowTick;
            if (!Hit(e, lv.damage * Might)) Frost.Apply(e, lv.freezeSeconds, Data.iceFrames, Data.iceFps);
        }
    }

    // ---------------------------------------------------------------- snow piles

    private void DropPile(Vector2 at)
    {
        if (piles.Count >= Data.maxPiles) Release(0);
        var p = sparePiles.Count > 0 ? sparePiles.Pop() : new Pile
        {
            sr = WeaponFx.Make(Fx, "Snow Pile", null, WeaponFx.Disc, Data.pileColor, Data.pileLayer, Data.pileOrder),
        };
        p.sr.gameObject.SetActive(true);
        p.sr.transform.position = at;
        p.at = at;
        p.born = Time.time;
        piles.Add(p);
    }

    private void TendPiles(float dt)
    {
        float now = Time.time, life = Mathf.Max(0.5f, Data.pileSeconds);
        var frames = Data.pileFrames;
        bool art = frames != null && frames.Length > 0 && frames[0] != null;
        int rest = art ? Mathf.Clamp(Data.pileRestFrame, 0, frames.Length - 1) : 0;
        float meltSeconds = art ? (frames.Length - 1 - rest) / Data.pileFps : 0.4f;

        for (int i = piles.Count - 1; i >= 0; i--)
        {
            var p = piles[i];
            float age = now - p.born;
            if (age >= life)
            {
                Release(i);
                continue;
            }
            if (art)
            {
                // it heaps up, sits, and melts away at the end
                int f = age < life - meltSeconds
                    ? Mathf.Min(rest, (int)(age * Data.pileFps))
                    : rest + Mathf.Min(frames.Length - 1 - rest, (int)((age - (life - meltSeconds)) * Data.pileFps));
                p.sr.sprite = frames[f];
                p.sr.transform.localScale = Vector3.one * AreaMul;
            }
            else
            {
                float grow = Mathf.Clamp01(age / 0.2f) * Mathf.Clamp01((life - age) / meltSeconds);
                WeaponFx.Resize(p.sr, Data.pileRadius * AreaMul * 2f * Mathf.Max(0.05f, grow));
            }
        }

        // whoever walks into a pile freezes; checked a few times a second, not every frame
        pileCheck += dt;
        if (pileCheck < 0.1f) return;
        pileCheck = 0f;
        float radius = Data.pileRadius * AreaMul;
        foreach (var p in piles)
        {
            EnemiesIn(p.at, radius, touching);
            foreach (var e in touching)
            {
                if (Frost.IsFrozen(e)) continue;
                if (pileImmuneUntil.TryGetValue(e, out float until) && now < until) continue;
                pileImmuneUntil[e] = now + Data.pileFreezeSeconds + Data.pileImmunity;
                Frost.Apply(e, Data.pileFreezeSeconds, Data.iceFrames, Data.iceFps);
            }
        }
    }

    private void Release(int index)
    {
        var p = piles[index];
        p.sr.gameObject.SetActive(false);
        sparePiles.Push(p);
        piles.RemoveAt(index);
    }

    // ---------------------------------------------------------------- evolution: the tornado

    private IEnumerator Tornado()
    {
        if (cam == null) yield break;
        var sr = WeaponFx.Make(Fx, "Frost Tornado", null, WeaponFx.Disc, Data.tornadoColor, Data.tornadoLayer, Data.tornadoOrder);
        Vector2 at = TouchDown();
        Vector2 wander = Random.insideUnitCircle.normalized;
        float spray = 0f, life = Mathf.Max(1f, Data.tornadoSeconds);
        // it touches down hard: a shake and a first blast of snow straight away
        Juice.Shake(0.3f);
        Spray(at);

        for (float t = 0f; t < life; t += Time.deltaTime)
        {
            float dt = Time.deltaTime;
            Vector2 me = transform.position;

            // it wanders, and turns away before it can reach the player
            wander = (wander + Random.insideUnitCircle * 0.6f * dt).normalized;
            Vector2 away = at - me;
            if (away.magnitude < Data.tornadoMinDistance) wander = Vector2.Lerp(wander, away.normalized, 4f * dt).normalized;
            at += wander * Data.tornadoSpeed * dt;
            if (((at + wander) - me).magnitude < Data.tornadoMinDistance * 0.8f) at = me + away.normalized * Data.tornadoMinDistance;

            // it winds up out of nothing and unwinds back into it at the end
            float grow = Mathf.Min(Mathf.Clamp01(t / 0.35f), Mathf.Clamp01((life - t) / 0.35f));
            Draw(sr, at, t, 1f - (1f - grow) * (1f - grow));

            // it drags the horde in
            EnemiesIn(at, Data.pullRadius * AreaMul, touching);
            foreach (var e in touching)
                if (e.TryGetComponent(out EnemyMovement move))
                {
                    Vector2 toward = at - (Vector2)e.transform.position;
                    if (toward.sqrMagnitude > 0.04f) move.Shove(toward.normalized * Data.pullSpeed, 0.15f);
                }

            // and sprays its snow across the field
            spray += dt;
            if (spray >= Data.sprayInterval)
            {
                spray = 0f;
                Spray(at);
            }
            yield return null;
        }
        Destroy(sr.gameObject);
    }

    private void Spray(Vector2 at)
    {
        float radius = Data.sprayRadius * AreaMul;
        EnemiesIn(at, radius, touching);
        foreach (var e in touching)
        {
            if (Data.frostBurstFx != null) FxOneShot.Play(Data.frostBurstFx, e.transform.position);
            if (!Hit(e, Data.sprayDamage * Might)) Frost.Apply(e, Data.sprayFreezeSeconds, Data.iceFrames, Data.iceFps);
        }
        for (int i = 0; i < Data.sprayPiles; i++) DropPile(at + Random.insideUnitCircle * radius * 0.8f);

        // gusts of snow bursting all over the field it sprays, not only on the enemies it catches
        if (Data.frostBurstFx != null)
            for (int i = 0; i < Data.sprayGusts; i++)
                FxOneShot.Play(Data.frostBurstFx, at + Random.insideUnitCircle * radius, Random.Range(0f, 360f), Random.Range(0.8f, 1.4f));
        Juice.Shake(0.08f);
    }

    private void Draw(SpriteRenderer sr, Vector2 at, float t, float grow)
    {
        var frame = Pick(Data.tornadoFrames, (int)(t * Data.tornadoFps));
        float scale = AreaMul * Mathf.Max(0.05f, grow);
        if (frame != null)
        {
            sr.sprite = frame;
            sr.color = Color.white;
            sr.transform.localScale = Vector3.one * scale;
            // drawn standing on its base: the base sits on the point
            sr.transform.position = at + Vector2.up * (Data.tornadoBasePixels / frame.pixelsPerUnit) * scale;
        }
        else
        {
            WeaponFx.Resize(sr, 2.2f * scale);
            sr.transform.position = at + Vector2.up * 1.1f * scale;
            sr.transform.rotation = Quaternion.Euler(0f, 0f, t * 720f);
        }
    }

    // somewhere on screen, never on the player: the first random spot far enough from them
    private Vector2 TouchDown()
    {
        Vector2 me = transform.position;
        float z = -cam.transform.position.z;
        for (int tries = 0; tries < 16; tries++)
        {
            Vector2 p = cam.ViewportToWorldPoint(new Vector3(Random.Range(0.15f, 0.85f), Random.Range(0.15f, 0.85f), z));
            if ((p - me).magnitude >= Data.tornadoMinDistance) return p;
        }
        return me + Random.insideUnitCircle.normalized * Data.tornadoMinDistance * 1.2f;
    }

    // ---------------------------------------------------------------- bookkeeping

    private static Sprite Pick(Sprite[] frames, int i) =>
        frames != null && frames.Length > 0 && frames[0] != null ? frames[((i % frames.Length) + frames.Length) % frames.Length] : null;

    // enemies long gone from the snow and the piles are dropped from the timers now and then
    private void Forget(float dt)
    {
        forgetTimer += dt;
        if (forgetTimer < 3f) return;
        forgetTimer = 0f;
        float now = Time.time;
        forget.Clear();
        foreach (var pair in nextSnow) if (pair.Key == null || now > pair.Value + 2f) forget.Add(pair.Key);
        foreach (var e in forget) nextSnow.Remove(e);
        forget.Clear();
        foreach (var pair in pileImmuneUntil) if (pair.Key == null || now > pair.Value + 2f) forget.Add(pair.Key);
        foreach (var e in forget) pileImmuneUntil.Remove(e);
    }
}
