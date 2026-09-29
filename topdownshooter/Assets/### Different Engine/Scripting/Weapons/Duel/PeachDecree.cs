using System.Collections.Generic;
using UnityEngine;

// the Peach Talismans' duel (BossDuel): a Taoist's decree against the dead. talismans of peach
// wood paper weave out one at a time and slap onto the boss in a ring of eight, each burning as it
// hangs there; with the eighth the ring lights, the bagua seal turns up under it, and the decree
// is carried out: the whole ring goes off at once, and the next begins
public class PeachDecree : DuelWeapon
{
    public override string Title => "Peach Wood Decree";
    public override string Line => "Eight talismans, one seal: by decree, begone";
    public override Color Tint => new Color(1f, 0.55f, 0.5f);
    public override string Pillar => "duel_pillar_peach";

    // a ring: eight talismans 0.3s apart (100 each as they stick, then 70 a second burning through
    // armour), a breath, the seal, and the decree (3200): about 5100 every 3.3s, 1550 a second
    private const int Slots = 8;
    private const float ThrowEvery = 0.3f, Speed = 11f, Impact = 100f, BurnDps = 70f, Tick = 0.25f;
    private const float IgniteAfter = 0.25f, DecreeAfter = 0.55f, Decree = 3200f, Rest = 0.35f;
    private const float RingX = 0.95f, RingY = 0.8f, WaveAmplitude = 0.45f, WaveLength = 2.6f;
    // the ring fills across and round, not one side first
    private static readonly int[] SlotOrder = { 0, 4, 2, 6, 1, 5, 3, 7 };

    private sealed class Talisman
    {
        public SpriteRenderer sr;
        public Afterimage trail;
        public Vector2 pathPos, dir;
        public float travelled, burn, phase;
        public int slot;
        public bool stuck;
    }

    private readonly List<Talisman> ring = new List<Talisman>();
    private readonly Stack<Talisman> spare = new Stack<Talisman>();
    private Sprite[] flight, burning, flaring, sealArt, blast, ash, spark;
    private SpriteRenderer seal;
    private EnemyHealth bound;
    private int thrown, stuckCount;
    private float timer, lit = -1f, age;

    protected override void Awake()
    {
        base.Awake();
        flight = Art("decree_talisman");
        burning = Art("decree_burn");
        flaring = Art("decree_flare");
        sealArt = Art("decree_seal");
        blast = Art("decree_blast");
        ash = Art("decree_ash");
        spark = Art("decree_spark");
        seal = Make("Seal", sealArt, Order - 1);
    }

    private Vector2 SlotAt(int slot)
    {
        float a = (90f + slot * 360f / Slots) * Mathf.Deg2Rad;
        return AimAt(bound) + new Vector2(Mathf.Cos(a) * RingX, Mathf.Sin(a) * RingY);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return; // paused
        age += dt;

        // the ring's on whoever it started on: if they can't be hurt any more it burns away
        if (bound != null && !Hittable(bound)) Scatter();
        if (bound == null)
        {
            bound = Target();
            if (bound == null) return;
        }

        timer += dt;
        if (thrown < Slots && timer >= ThrowEvery)
        {
            timer = 0f;
            Throw(SlotOrder[thrown++]);
        }

        Step(dt);
        if (bound == null || !Hittable(bound)) return;

        // the eighth stuck: the ring lights, the seal comes, and the decree goes off
        if (stuckCount >= Slots)
        {
            if (lit < 0f) lit = 0f;
            float was = lit;
            lit += dt;
            if (was < IgniteAfter && lit >= IgniteAfter) Ignite();
            if (lit >= IgniteAfter)
            {
                seal.transform.position = AimAt(bound);
                seal.sprite = Once(sealArt, (lit - IgniteAfter) / DecreeAfter);
            }
            if (lit >= IgniteAfter + DecreeAfter) Carry();
        }
    }

    private void Throw(int slot)
    {
        var t = spare.Count > 0 ? spare.Pop() : New();
        Vector2 me = transform.position;
        float side = (slot % 2 == 0 ? 1f : -1f) * 0.35f;
        t.slot = slot;
        t.stuck = false;
        t.pathPos = me + new Vector2(side, 0.3f);
        t.dir = (SlotAt(slot) - t.pathPos).normalized;
        t.travelled = 0f;
        t.burn = 0f;
        t.phase = Random.value * 10f;
        t.sr.gameObject.SetActive(true);
        t.sr.transform.SetPositionAndRotation(t.pathPos, Quaternion.identity);
        t.trail.Restart();
        ring.Add(t);
        Sound("decree_throw", me, 0.3f, Random.Range(0.94f, 1.1f));
    }

    private Talisman New()
    {
        var sr = Make("Talisman", flight, Order);
        return new Talisman { sr = sr, trail = new Afterimage(sr, 3, 0.18f, 0.5f, 0.7f) };
    }

    private void Step(float dt)
    {
        float tick = Tick;
        foreach (var t in ring)
        {
            if (t.stuck)
            {
                Vector2 at = SlotAt(t.slot);
                t.sr.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, 0f, (t.slot - Slots * 0.5f) * 4f));
                t.sr.sprite = Loop(lit >= IgniteAfter ? flaring : burning, lit >= IgniteAfter ? 16f : 10f, age + t.phase);
                t.burn += dt;
                while (t.burn >= tick)
                {
                    t.burn -= tick;
                    // a burn, not a hit: it goes through armour
                    Strike(bound, BurnDps * tick, true);
                    if (bound == null || !Hittable(bound)) return;
                }
                continue;
            }

            Vector2 goal = SlotAt(t.slot), toGoal = goal - t.pathPos;
            float dist = toGoal.magnitude, step = Speed * dt;
            if (dist <= Mathf.Max(0.2f, step))
            {
                t.stuck = true;
                t.trail.Hide();
                stuckCount++;
                FxBatch.Play(spark, 22f, goal, 1f, Layer, Order + 2);
                Sound("decree_stick", goal, 0.4f, 0.9f + stuckCount * 0.04f);
                Strike(bound, Impact);
                if (bound == null || !Hittable(bound)) return;
                continue;
            }
            t.dir = toGoal / dist;
            t.pathPos += t.dir * step;
            t.travelled += step;
            float phase = t.travelled / WaveLength * 2f * Mathf.PI;
            float swing = Mathf.Sin(phase) * WaveAmplitude * Mathf.Clamp01(dist / 1.2f) * (t.slot % 2 == 0 ? 1f : -1f);
            var sideways = new Vector2(-t.dir.y, t.dir.x);
            t.sr.transform.SetPositionAndRotation(t.pathPos + sideways * swing, Quaternion.Euler(0f, 0f, Mathf.Cos(phase) * 18f));
            t.sr.sprite = Loop(flight, 14f, age + t.phase);
            t.trail.Record();
        }
    }

    private void Ignite()
    {
        seal.gameObject.SetActive(true);
        seal.transform.localScale = Vector3.one * 1.2f;
        Sound("decree_ignite", AimAt(bound), 0.85f);
        Juice.Shake(0.08f);
    }

    // the decree: the ring goes off as one
    private void Carry()
    {
        Vector2 at = AimAt(bound);
        Strike(bound, Decree);
        FxBatch.Play(blast, 18f, at, 1.6f, Layer, Order + 4);
        Sound("decree_blast", at, 1f, Random.Range(0.97f, 1.03f));
        Juice.Shake(0.28f);
        Juice.Freeze(0.04f);
        Reset(true);
        timer = -Rest;
    }

    // the ring's target is gone or can't be hurt: what's stuck burns away, and it starts again
    private void Scatter()
    {
        Reset(true);
        bound = null;
        timer = 0f;
    }

    private void Reset(bool crumble)
    {
        foreach (var t in ring)
        {
            if (crumble && t.sr.gameObject.activeSelf) FxBatch.Play(ash, 14f, t.sr.transform.position, 1f, Layer, Order + 1);
            t.trail.Hide();
            t.sr.gameObject.SetActive(false);
            spare.Push(t);
        }
        ring.Clear();
        thrown = stuckCount = 0;
        lit = -1f;
        if (seal != null) seal.gameObject.SetActive(false);
    }

    public override void ClearShots()
    {
        Reset(false);
        bound = null;
    }
}
