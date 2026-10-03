using System.Collections.Generic;
using UnityEngine;

// the Bow's duel (BossDuel): Hou Yi's bow, that shot nine of the ten suns out of the sky. a thin
// unbroken stream of light arrows pours into the boss, and every couple of seconds a sun gathers
// on the string and goes: the sunshot, bursting on him and knocking a three-legged crow, the
// sun's spirit, tumbling out of it. every ninth is the Ninth Sun, and it hits for three
public class SunShooterBow : DuelWeapon
{
    public override string Title => "Sun-Shooter's Bow";
    public override string Line => "Hou Yi's bow, that shot nine suns from the sky";
    public override Color Tint => new Color(1f, 0.84f, 0.4f);
    public override string Pillar => "duel_pillar_gold";

    // the stream: 100 every 0.12s, about 830 a second
    private const float StreamEvery = 0.12f, StreamDamage = 100f, StreamSpeed = 22f, StreamTurn = 540f, StreamLife = 1.3f;
    // the sunshot: 1450 every 2.4s, three times that on the ninth, about 740 a second
    private const float SunEvery = 2.4f, SunCharge = 0.6f, SunDamage = 1450f, SunSpeed = 26f, SunTurn = 900f, SunLife = 1.6f;
    private const float NinthMul = 3f;
    private const float CrowSeconds = 1.2f;

    private sealed class Arrow
    {
        public SpriteRenderer sr;
        public Afterimage trail;
        public Vector2 pos, dir;
        public EnemyHealth target;
        public float age, damage;
        public bool sun, ninth;
    }

    private sealed class Crow
    {
        public SpriteRenderer sr;
        public Vector2 pos, vel;
        public float age, spin;
    }

    private readonly List<Arrow> arrows = new List<Arrow>();
    private readonly Stack<Arrow> spareDarts = new Stack<Arrow>(), spareSuns = new Stack<Arrow>();
    private readonly List<Crow> crows = new List<Crow>();
    private readonly Stack<Crow> spareCrows = new Stack<Crow>();

    private Sprite[] dart, sunArrow, charge, spark, burst, crow;
    private SpriteRenderer charging;
    private float streamTimer, sunTimer, age;
    private int suns, loosed;

    protected override void Awake()
    {
        base.Awake();
        dart = Art("sun_dart");
        sunArrow = Art("sun_arrow");
        charge = Art("sun_charge");
        spark = Art("sun_spark");
        burst = Art("sun_burst");
        crow = Art("sun_crow");
        charging = Make("Sun", charge, Order + 2);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return; // paused
        age += dt;

        var target = Target();
        if (target != null)
        {
            streamTimer += dt;
            if (streamTimer >= StreamEvery)
            {
                streamTimer -= StreamEvery;
                Loose(target, false);
            }

            sunTimer += dt;
            if (sunTimer >= SunEvery)
            {
                sunTimer = 0f;
                Loose(target, true);
            }
        }
        else
        {
            // nobody to shoot: the string waits drawn, short of the sun
            streamTimer = Mathf.Min(streamTimer, StreamEvery);
            sunTimer = Mathf.Min(sunTimer, SunEvery - SunCharge - 0.01f);
        }

        Charge(target);
        Fly(dt);
        Fall(dt);
    }

    // the sun gathering on the string for its last stretch before it goes, a ninth one bigger
    private void Charge(EnemyHealth target)
    {
        float k = (sunTimer - (SunEvery - SunCharge)) / SunCharge;
        bool on = target != null && k >= 0f;
        if (on && !charging.gameObject.activeSelf)
        {
            charging.gameObject.SetActive(true);
            Sound("sun_charge", transform.position, 0.45f, (suns + 1) % 9 == 0 ? 0.85f : 1f);
        }
        if (!on)
        {
            if (charging.gameObject.activeSelf) charging.gameObject.SetActive(false);
            return;
        }
        Vector2 me = transform.position, dir = (AimAt(target) - me).normalized;
        bool ninth = (suns + 1) % 9 == 0;
        charging.transform.position = me + dir * 0.6f;
        charging.transform.localScale = Vector3.one * Mathf.Lerp(0.4f, ninth ? 1.5f : 1f, k);
        charging.sprite = Once(charge, k);
    }

    private void Loose(EnemyHealth target, bool sun)
    {
        Vector2 me = transform.position, to = AimAt(target);
        Vector2 dir = (to - me).normalized;
        if (!sun) dir = Rotate(dir, Random.Range(-3f, 3f));

        var a = sun ? (spareSuns.Count > 0 ? spareSuns.Pop() : New(true)) : (spareDarts.Count > 0 ? spareDarts.Pop() : New(false));
        a.sun = sun;
        a.pos = me + dir * 0.45f;
        a.dir = dir;
        a.target = target;
        a.age = 0f;
        a.damage = StreamDamage;
        a.ninth = false;
        if (sun)
        {
            suns++;
            a.ninth = suns % 9 == 0;
            a.damage = SunDamage * (a.ninth ? NinthMul : 1f);
            a.sr.transform.localScale = Vector3.one * (a.ninth ? 1.4f : 1f);
            Sound("sun_shot", me, a.ninth ? 1f : 0.75f, a.ninth ? 0.85f : Random.Range(0.97f, 1.03f));
            Juice.Shake(a.ninth ? 0.12f : 0.05f);
        }
        else if (loosed++ % 2 == 0) Sound("sun_loose", me, 0.16f, Random.Range(0.92f, 1.12f));

        a.sr.gameObject.SetActive(true);
        a.sr.transform.position = a.pos;
        Face(a.sr.transform, dir);
        a.trail.Restart();
        arrows.Add(a);
    }

    private Arrow New(bool sun)
    {
        var sr = Make(sun ? "Sunshot" : "Arrow", sun ? sunArrow : dart, Order + (sun ? 1 : 0));
        return new Arrow { sr = sr, trail = new Afterimage(sr, sun ? 5 : 2, sun ? 0.3f : 0.22f, sun ? 0.7f : 0.45f, 0.5f) };
    }

    private void Fly(float dt)
    {
        for (int i = arrows.Count - 1; i >= 0; i--)
        {
            var a = arrows[i];
            a.age += dt;
            bool live = Hittable(a.target);
            if (live)
            {
                Vector2 to = AimAt(a.target) - a.pos;
                float turn = (a.sun ? SunTurn : StreamTurn) * (1f + 3f * a.age) * Mathf.Deg2Rad * dt;
                if (to.sqrMagnitude > 0.0001f) a.dir = ((Vector2)Vector3.RotateTowards(a.dir, to.normalized, turn, 0f)).normalized;
            }
            a.pos += a.dir * (a.sun ? SunSpeed : StreamSpeed) * dt;
            a.sr.transform.position = a.pos;
            Face(a.sr.transform, a.dir);
            a.sr.sprite = Loop(a.sun ? sunArrow : dart, 16f, age + i * 0.13f);
            a.trail.Record();

            if (live && Touches(a.target, a.pos, a.sun ? 0.35f : 0.2f))
            {
                Land(a);
                Drop(i);
            }
            else if (a.age >= (a.sun ? SunLife : StreamLife)) Drop(i);
        }
    }

    private void Land(Arrow a)
    {
        Strike(a.target, a.damage);
        if (!a.sun)
        {
            FxBatch.PlayShot(spark, 24f, a.pos, 1f, Layer, Order + 3);
            return;
        }
        FxBatch.PlayShot(burst, 18f, a.pos, a.ninth ? 2f : 1.3f, Layer, Order + 4);
        Sound(a.ninth ? "sun_ninth" : "sun_hit", a.pos, a.ninth ? 1f : 0.8f, Random.Range(0.96f, 1.04f));
        Juice.Shake(a.ninth ? 0.35f : 0.12f);
        Knock(a.pos, a.dir);
        if (a.ninth)
        {
            Knock(a.pos, Rotate(a.dir, 40f));
            Knock(a.pos, Rotate(a.dir, -40f));
            Juice.Freeze(0.05f);
            var screen = YamaScreen.Get();
            if (screen != null) screen.Flash(new Color(1f, 0.86f, 0.5f), 0.25f, 0.4f);
        }
    }

    // a three-legged crow knocked out of the sun, tumbling away
    private void Knock(Vector2 at, Vector2 dir)
    {
        if (crow == null) return;
        var c = spareCrows.Count > 0 ? spareCrows.Pop() : new Crow { sr = Make("Crow", crow, Order + 5) };
        c.pos = at;
        c.vel = new Vector2(dir.x * 2.5f + Random.Range(-1.5f, 1.5f), Random.Range(3f, 4.5f));
        c.spin = Random.Range(-420f, 420f);
        c.age = 0f;
        c.sr.gameObject.SetActive(true);
        c.sr.color = Color.white;
        crows.Add(c);
    }

    private void Fall(float dt)
    {
        for (int i = crows.Count - 1; i >= 0; i--)
        {
            var c = crows[i];
            c.age += dt;
            c.vel.y -= 11f * dt;
            c.pos += c.vel * dt;
            c.sr.transform.SetPositionAndRotation(c.pos, Quaternion.Euler(0f, 0f, c.spin * c.age));
            c.sr.sprite = Loop(crow, 14f, c.age);
            c.sr.color = new Color(1f, 1f, 1f, Mathf.Clamp01((CrowSeconds - c.age) / 0.4f));
            if (c.age < CrowSeconds) continue;
            c.sr.gameObject.SetActive(false);
            spareCrows.Push(c);
            crows.RemoveAt(i);
        }
    }

    private void Drop(int i)
    {
        var a = arrows[i];
        a.trail.Hide();
        a.sr.gameObject.SetActive(false);
        a.target = null;
        (a.sun ? spareSuns : spareDarts).Push(a);
        arrows.RemoveAt(i);
    }

    public override void ClearShots()
    {
        for (int i = arrows.Count - 1; i >= 0; i--) Drop(i);
        if (charging != null) charging.gameObject.SetActive(false);
    }

    private static Vector2 Rotate(Vector2 v, float degrees) => Quaternion.Euler(0f, 0f, degrees) * v;
}
