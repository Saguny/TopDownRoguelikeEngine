using System.Collections.Generic;
using UnityEngine;

// swords circling the player, one per level, cutting whatever they touch. evolved, every interval
// each sword bursts a fan of stars that curve round onto the enemies nearby, spread between them,
// hitting a little harder than a cut
public class SevenStarSwords : Weapon<SevenStarSwordsData>
{
    private sealed class Star
    {
        public SpriteRenderer sr;
        public Afterimage trail;
        public Vector2 dir;
        public EnemyHealth target;
        public float damage, age;
        public int pierceLeft;
        public readonly List<EnemyHealth> hit = new List<EnemyHealth>(4);
    }

    // a star leaves on its fan for this long before it turns in, so a burst still reads as one
    private const float FanSeconds = 0.12f;

    private readonly List<SpriteRenderer> swords = new List<SpriteRenderer>();
    private readonly List<Star> stars = new List<Star>();
    private readonly Stack<Star> spareStars = new Stack<Star>();

    public override void ClearShots()
    {
        for (int i = stars.Count - 1; i >= 0; i--)
        {
            var s = stars[i];
            s.trail.Hide();
            s.sr.gameObject.SetActive(false);
            s.target = null;
            spareStars.Push(s);
        }
        stars.Clear();
    }
    private readonly Dictionary<EnemyHealth, float> nextCut = new Dictionary<EnemyHealth, float>();
    private readonly List<EnemyHealth> touching = new List<EnemyHealth>();
    private readonly List<EnemyHealth> forget = new List<EnemyHealth>();
    private readonly List<EnemyHealth> targets = new List<EnemyHealth>(64);
    private readonly List<float> targetDistance = new List<float>(64);
    private Camera cam;
    private float angle, burstTimer, forgetTimer;

    private void Update()
    {
        if (Data == null || Level <= 0) return;

        float dt = Time.deltaTime;
        if (dt <= 0f) return; // paused
        if (cam == null) cam = Camera.main;

        SyncSwords(Data.SwordsAt(Level));
        angle += Data.orbitSpeed * SpeedMul * dt;
        Cut(Data.At(Level).contactDamage * Might);

        if (Data.IsEvolved(Level))
        {
            burstTimer += dt;
            if (burstTimer >= Cooldown(Data.starInterval))
            {
                burstTimer = 0f;
                Burst(Data.starDamage * Might);
            }
        }

        MoveStars(dt);
        ForgetOldCuts(dt);
    }

    private void SyncSwords(int count)
    {
        while (swords.Count < count)
            swords.Add(WeaponFx.Make(Fx, "Sword", Data.AnimatedSwords ? Data.swordFrames[0] : Data.swordSprite, WeaponFx.Disc, Data.swordColor, Data.sortingLayer, Data.sortingOrder));

        while (swords.Count > count)
        {
            Destroy(swords[swords.Count - 1].gameObject);
            swords.RemoveAt(swords.Count - 1);
        }
    }

    // place the ring of swords and cut everything touching one, each enemy once per contactInterval
    private void Cut(float damage)
    {
        Vector2 centre = transform.position;
        float radius = Data.orbitRadius * AreaMul;
        float size = Data.swordSize * AreaMul;
        float now = Time.time;

        for (int i = 0; i < swords.Count; i++)
        {
            float deg = angle + 360f * i / swords.Count;
            float rad = deg * Mathf.Deg2Rad;
            Vector2 pos = centre + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;

            var sword = swords[i];
            sword.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, 0f, deg - 90f));
            if (Data.AnimatedSwords)
            {
                // each sword a little out of step with the others
                sword.sprite = Data.swordFrames[(int)(now * Data.swordFps + i * 1.7f) % Data.swordFrames.Length];
                sword.transform.localScale = Vector3.one * AreaMul;
            }
            else WeaponFx.Resize(sword, size);

            EnemiesIn(pos, size * 0.5f, touching);
            foreach (var e in touching)
            {
                if (nextCut.TryGetValue(e, out float ready) && now < ready) continue;
                nextCut[e] = now + Data.contactInterval;
                Hit(e, damage);
                SignatureSlow(e);
            }
        }
    }

    // ---- evolution: homing stars ---------------------------------------------------------

    // a burst goes off softly (it's every second and a bit, all run): a breath of air and a gentle
    // glint, one of three takes; a glassy twinkle as each star strikes (Resources/Sfx, made by
    // Tools/SFX/finale.py)
    private static AudioClip[] shootSounds;
    private static AudioClip hitSound;
    private static bool soundsLoaded;
    private float nextHitSound;
    private const float ShootVolume = 0.4f, HitVolume = 0.35f, HitSoundEvery = 0.05f;

    private static void LoadSounds()
    {
        if (soundsLoaded) return;
        soundsLoaded = true;
        shootSounds = new[] { Resources.Load<AudioClip>("Sfx/star_shoot_1"), Resources.Load<AudioClip>("Sfx/star_shoot_2"), Resources.Load<AudioClip>("Sfx/star_shoot_3") };
        hitSound = Resources.Load<AudioClip>("Sfx/star_hit");
    }

    private void Burst(float damage)
    {
        FindTargets();
        LoadSounds();
        var shoot = shootSounds[Random.Range(0, shootSounds.Length)];
        if (shoot != null && swords.Count > 0) SfxPlayer.PlayAt(shoot, transform.position, ShootVolume, Random.Range(0.92f, 1.08f));
        int n = Data.starsPerSword, k = 0;
        for (int i = 0; i < swords.Count; i++)
        {
            Vector2 from = swords[i].transform.position;
            if (Data.launchFx != null) FxOneShot.Play(Data.launchFx, from);

            // each sword's fan is turned a little, so the bursts don't stack into the same lines
            float offset = 360f / n * i / swords.Count;
            for (int j = 0; j < n; j++, k++)
            {
                float rad = (offset + 360f * j / n) * Mathf.Deg2Rad;
                var star = spareStars.Count > 0 ? spareStars.Pop() : NewStar();
                star.sr.gameObject.SetActive(true);
                star.sr.transform.SetPositionAndRotation(from, Quaternion.identity);
                if (Data.AnimatedStars) star.sr.transform.localScale = Vector3.one;
                else WeaponFx.Resize(star.sr, Data.starSize);
                star.trail.Restart();
                star.dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                // the nearest enemies first, one star each, then round again: spread, not piled on one
                star.target = targets.Count > 0 ? targets[k % targets.Count] : null;
                star.damage = damage;
                star.age = 0f;
                star.pierceLeft = Pierce;
                star.hit.Clear();
                stars.Add(star);
            }
        }
    }

    // the live enemies in reach, nearest first
    private void FindTargets()
    {
        targets.Clear();
        targetDistance.Clear();
        Vector2 me = transform.position;
        float r2 = Data.starRange * Data.starRange;
        foreach (var go in EnemyRegistry.All)
        {
            if (go == null || !go.TryGetComponent(out EnemyHealth e) || !IsAlive(e)) continue;
            float d = ((Vector2)go.transform.position - me).sqrMagnitude;
            if (d > r2) continue;
            int at = targetDistance.BinarySearch(d);
            if (at < 0) at = ~at;
            targets.Insert(at, e);
            targetDistance.Insert(at, d);
        }
    }

    private Star NewStar()
    {
        var sr = WeaponFx.Make(Fx, "Star", Data.AnimatedStars ? Data.starFrames[0] : Data.starSprite, WeaponFx.Disc, Data.starColor, Data.sortingLayer, Data.sortingOrder + 1);
        return new Star { sr = sr, trail = new Afterimage(sr, Data.starTrailLength, Data.starTrailSpacing) };
    }

    // a star flies out on its fan, turns onto its enemy and hits it; if that one's gone it takes
    // the nearest other. Armour Piercing lets it go on to more. with nobody left it flies straight
    // until it's off the screen or out of time
    private void MoveStars(float dt)
    {
        float step = Data.starSpeed * SpeedMul * dt;
        Vector2 me = transform.position;

        for (int i = stars.Count - 1; i >= 0; i--)
        {
            var s = stars[i];
            s.age += dt;
            Vector2 pos = s.sr.transform.position;

            if (s.age >= FanSeconds)
            {
                if (!IsAlive(s.target) || s.hit.Contains(s.target)) s.target = Nearest(pos, s.hit);
                if (s.target != null)
                {
                    Vector2 to = (Vector2)s.target.transform.position - pos;
                    if (to.sqrMagnitude > 0.0001f)
                    {
                        float turn = Data.starTurn * (1f + 2f * (s.age - FanSeconds)) * Mathf.Deg2Rad * dt;
                        s.dir = ((Vector2)Vector3.RotateTowards(s.dir, to.normalized, turn, 0f)).normalized;
                    }
                }
            }

            pos += s.dir * step;
            s.sr.transform.position = pos;
            if (Data.AnimatedStars) s.sr.sprite = Data.starFrames[(int)(Time.time * Data.starFps + i) % Data.starFrames.Length];
            s.trail.Record();

            bool gone = s.age >= Data.starLifetime ||
                        (s.target == null && (cam != null ? !OnScreen(pos, cam) : (pos - me).sqrMagnitude > 900f));
            bool done = gone;
            if (!gone)
            {
                EnemiesIn(pos, Data.starSize * 0.5f, touching);
                foreach (var e in touching)
                {
                    if (s.hit.Contains(e)) continue;
                    Hit(e, s.damage);
                    SignatureSlow(e);
                    if (Data.starHitFx != null) FxOneShot.Play(Data.starHitFx, pos);
                    if (hitSound != null && Time.time >= nextHitSound)
                    {
                        nextHitSound = Time.time + HitSoundEvery;
                        SfxPlayer.PlayAt(hitSound, pos, HitVolume, Random.Range(0.9f, 1.3f));
                    }
                    s.hit.Add(e);
                    if (s.pierceLeft-- <= 0) done = true;
                    break;
                }
            }

            if (done)
            {
                s.trail.Hide();
                s.sr.gameObject.SetActive(false);
                s.target = null;
                spareStars.Push(s);
                stars.RemoveAt(i);
            }
        }
    }

    private static EnemyHealth Nearest(Vector2 from, List<EnemyHealth> skip)
    {
        EnemyHealth best = null;
        float bestD = float.MaxValue;
        foreach (var go in EnemyRegistry.All)
        {
            if (go == null || !go.TryGetComponent(out EnemyHealth e) || !IsAlive(e) || skip.Contains(e)) continue;
            float d = ((Vector2)go.transform.position - from).sqrMagnitude;
            if (d < bestD)
            {
                bestD = d;
                best = e;
            }
        }
        return best;
    }

    // dead enemies and long expired timers leave the cut list every couple of seconds
    private void ForgetOldCuts(float dt)
    {
        forgetTimer += dt;
        if (forgetTimer < 2f) return;
        forgetTimer = 0f;

        float now = Time.time;
        forget.Clear();
        foreach (var pair in nextCut)
            if (pair.Key == null || pair.Value < now) forget.Add(pair.Key);
        foreach (var e in forget) nextCut.Remove(e);
    }
}
