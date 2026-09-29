using System.Collections.Generic;
using UnityEngine;

// the Seven Star Swords' duel (BossDuel): the Northern Dipper's sword formation. the seven hang
// over the player where the Dipper's seven stars would be, joined by faint starlight, every point
// turned on the boss. one by one down the Dipper, bowl to handle, each lunges into him and comes
// back to its star; then the lines flare and all seven go at once, and where they meet the
// Dipper itself is stamped over him
public class DipperFormation : DuelWeapon
{
    public override string Title => "Big Dipper Sword Formation";
    public override string Line => "Seven stars of the Northern Dipper, seven swords";
    public override Color Tint => new Color(0.66f, 0.92f, 1f);
    public override string Pillar => "duel_pillar_azure";

    // a cycle: seven lunges 0.28s apart (380 each), a breath, the seven together (350 each), and
    // back: about 5100 every 3.2s, 1600 a second
    // a cycle runs about 5 s in play (the lunges, every sword home, the gather, the volley and home
    // again): 7 x 520 + 7 x 480 over it is the Sun-Shooter's Bow's pace, which it had fallen a
    // quarter behind (playtest: 970 a second against the Bow's 1330, crits aside)
    private const float LungeEvery = 0.28f, LungeDamage = 520f, VolleyDamage = 480f;
    private const float Gather = 0.45f, OutSpeed = 26f, BackSeconds = 0.36f, VolleyStagger = 0.035f;

    // the Dipper as it hangs over the player, in world units: Dubhe, Merak, Phecda, Megrez (the
    // bowl), Alioth, Mizar, Alkaid (the handle). the order they lunge in
    private static readonly Vector2[] Stars =
    {
        new Vector2(1.1f, 0.5f), new Vector2(1.02f, -0.18f), new Vector2(0.36f, -0.32f), new Vector2(0.3f, 0.32f),
        new Vector2(-0.33f, 0.46f), new Vector2(-0.94f, 0.55f), new Vector2(-1.5f, 0.26f),
    };
    private static readonly int[,] Lines = { { 0, 1 }, { 1, 2 }, { 2, 3 }, { 3, 0 }, { 3, 4 }, { 4, 5 }, { 5, 6 } };
    private static readonly Vector2 Above = new Vector2(0f, 1.7f);

    private enum State { Home, Out, Back }

    private sealed class Sword
    {
        public SpriteRenderer sr;
        public Afterimage trail;
        public State state;
        public Vector2 pos, from, aimOffset, bend;
        public EnemyHealth target;
        public float t, seconds, damage, delay;
        public bool volley;
    }

    private readonly Sword[] swords = new Sword[7];
    private readonly SpriteRenderer[] nodes = new SpriteRenderer[7];
    private readonly SpriteRenderer[] lines = new SpriteRenderer[7];
    private Sprite[] swordArt, nodeArt, hitArt, sealArt;
    private Sprite beam;
    private int next, landed;
    private float timer, age, flare;
    private bool volleying, formed;

    protected override void Awake()
    {
        base.Awake();
        swordArt = Art("dipper_sword");
        nodeArt = Art("dipper_node");
        hitArt = Art("dipper_hit");
        sealArt = Art("dipper_seal");
        beam = WeaponFx.Beam(new Color32(90, 160, 230, 110), new Color32(200, 240, 255, 255), new Color32(90, 160, 230, 110));
        for (int i = 0; i < 7; i++)
        {
            lines[i] = WeaponFx.Make(Fx, "Starlight", beam, beam, Color.white, Layer, Order - 2);
            nodes[i] = Make("Star", nodeArt, Order - 1);
            nodes[i].gameObject.SetActive(true);
            var sr = Make("Sword", swordArt, Order);
            swords[i] = new Sword { sr = sr, trail = new Afterimage(sr, 4, 0.22f, 0.55f, 0.7f) };
        }
    }

    private Vector2 Home(int i) => (Vector2)transform.position + Above + Stars[i] + new Vector2(0f, Mathf.Sin(age * 2.2f + i * 0.9f) * 0.06f);

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return; // paused
        age += dt;
        if (!formed) Form();

        var target = Target();
        timer += dt;
        if (!volleying)
        {
            // one after another down the Dipper, then the breath before the seven go together
            if (next < 7)
            {
                if (target != null && timer >= LungeEvery && swords[next].state == State.Home)
                {
                    Launch(swords[next], target, LungeDamage, false, 0f);
                    Sound("dipper_lunge", swords[next].pos, 0.35f, 0.9f + next * 0.05f);
                    next++;
                    timer = 0f;
                }
            }
            else if (AllHome() && timer >= Gather && target != null)
            {
                volleying = true;
                landed = 0;
                flare = 1f;
                Sound("dipper_volley", transform.position, 0.8f);
                for (int i = 0; i < 7; i++) Launch(swords[i], target, VolleyDamage, true, i * VolleyStagger);
            }
            else if (next >= 7 && !AllHome()) timer = 0f;
        }
        else if (AllHome())
        {
            volleying = false;
            next = 0;
            timer = 0f;
        }

        flare = Mathf.MoveTowards(flare, 0f, dt * 1.6f);
        for (int i = 0; i < 7; i++) Step(swords[i], i, target, dt);
        Draw(target);
    }

    // the first frame: the seven fly out of the player to their stars
    private void Form()
    {
        formed = true;
        for (int i = 0; i < 7; i++)
        {
            var s = swords[i];
            s.sr.gameObject.SetActive(true);
            s.state = State.Back;
            s.from = transform.position;
            s.pos = s.from;
            s.t = -i * 0.06f;
            s.seconds = 0.5f;
            s.bend = new Vector2(Stars[i].x * 0.8f, 0.4f);
            s.trail.Restart();
        }
    }

    private bool AllHome()
    {
        foreach (var s in swords) if (s.state != State.Home) return false;
        return true;
    }

    private void Launch(Sword s, EnemyHealth target, float damage, bool volley, float delay)
    {
        s.state = State.Out;
        s.target = target;
        s.damage = damage;
        s.volley = volley;
        s.delay = delay;
        s.from = s.pos;
        s.t = 0f;
        s.aimOffset = Random.insideUnitCircle * (volley ? 0.15f : 0.35f);
        s.seconds = Mathf.Max(0.08f, (AimAt(target) - s.pos).magnitude / OutSpeed);
        s.trail.Restart();
    }

    private void Step(Sword s, int i, EnemyHealth target, float dt)
    {
        switch (s.state)
        {
            case State.Home:
            {
                s.pos = Home(i);
                Vector2 look = target != null ? AimAt(target) - s.pos : Vector2.up;
                float want = Mathf.Atan2(look.y, look.x) * Mathf.Rad2Deg - 90f;
                float now = s.sr.transform.eulerAngles.z;
                s.sr.transform.SetPositionAndRotation(s.pos, Quaternion.Euler(0f, 0f, Mathf.MoveTowardsAngle(now, want, 540f * dt)));
                break;
            }
            case State.Out:
            {
                if (s.delay > 0f) { s.delay -= dt; s.pos = Home(i); s.sr.transform.position = s.pos; break; }
                if (!Hittable(s.target)) { Return(s, i); break; }
                s.t += dt / s.seconds;
                Vector2 to = AimAt(s.target) + s.aimOffset;
                // it leaves slow and arrives fast
                float k = Mathf.Clamp01(s.t);
                s.pos = Vector2.Lerp(s.from, to, k * k);
                Face(s.sr.transform, to - s.from, 90f);
                s.sr.transform.position = s.pos;
                s.trail.Record();
                if (s.t >= 1f) Land(s, i);
                break;
            }
            case State.Back:
            {
                s.t += dt / s.seconds;
                float k = Mathf.Clamp01(s.t), e = 1f - (1f - k) * (1f - k);
                Vector2 home = Home(i), mid = (s.from + home) * 0.5f + s.bend;
                s.pos = (1f - e) * (1f - e) * s.from + 2f * (1f - e) * e * mid + e * e * home;
                Vector2 look = target != null ? AimAt(target) - s.pos : Vector2.up;
                float want = Mathf.Atan2(look.y, look.x) * Mathf.Rad2Deg - 90f;
                s.sr.transform.SetPositionAndRotation(s.pos, Quaternion.Euler(0f, 0f, Mathf.MoveTowardsAngle(s.sr.transform.eulerAngles.z, want, 900f * dt)));
                s.trail.Record();
                if (k >= 1f)
                {
                    s.state = State.Home;
                    s.trail.Hide();
                }
                break;
            }
        }
        s.sr.sprite = Loop(swordArt, 12f, age + i * 0.37f);
    }

    private void Land(Sword s, int i)
    {
        Strike(s.target, s.damage);
        FxBatch.Play(hitArt, 22f, s.pos, s.volley ? 1.2f : 1f, Layer, Order + 3);
        if (!s.volley) Sound("dipper_hit", s.pos, 0.45f, 0.95f + i * 0.04f);
        else if (++landed == 7)
        {
            // the seven meet: the Dipper stamped over him
            Vector2 at = AimAt(s.target);
            FxBatch.Play(sealArt, 16f, at, 1.5f, Layer, Order + 4);
            Sound("dipper_seal", at, 0.9f);
            Juice.Shake(0.22f);
        }
        Return(s, i);
    }

    private void Return(Sword s, int i)
    {
        s.state = State.Back;
        s.from = s.pos;
        s.t = 0f;
        s.seconds = BackSeconds + i * 0.02f;
        Vector2 side = Home(i) - s.pos;
        s.bend = new Vector2(-side.y, side.x).normalized * (i % 2 == 0 ? 1.1f : -1.1f);
    }

    // the stars and the starlight between them, brighter as the seven gather
    private void Draw(EnemyHealth target)
    {
        float pulse = 0.55f + 0.15f * Mathf.Sin(age * 3f);
        float gather = !volleying && next >= 7 ? Mathf.Clamp01(timer / Gather) : 0f;
        float a = Mathf.Clamp01(Mathf.Max(pulse * 0.6f, Mathf.Max(flare, gather)));
        for (int i = 0; i < 7; i++)
        {
            Vector2 p = (Vector2)transform.position + Above + Stars[i];
            nodes[i].transform.position = p;
            nodes[i].transform.localScale = Vector3.one * (1f + 0.5f * Mathf.Max(flare, gather));
            nodes[i].sprite = Loop(nodeArt, 10f, age + i * 0.29f);

            Vector2 from = (Vector2)transform.position + Above + Stars[Lines[i, 0]], to = (Vector2)transform.position + Above + Stars[Lines[i, 1]];
            var d = to - from;
            var line = lines[i];
            line.transform.SetPositionAndRotation(from, Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg));
            line.transform.localScale = new Vector3(d.magnitude, 1f / YamaArt.WorldPpu, 1f);
            line.color = new Color(1f, 1f, 1f, a * 0.55f);
        }
    }

    public override void ClearShots()
    {
        for (int i = 0; i < 7; i++)
        {
            var s = swords[i];
            if (s == null) continue;
            s.state = State.Home;
            s.trail.Hide();
        }
        volleying = false;
        next = 0;
    }
}
