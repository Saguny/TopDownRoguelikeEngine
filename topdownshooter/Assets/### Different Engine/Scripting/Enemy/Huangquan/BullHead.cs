using System.Collections.Generic;
using UnityEngine;

// Niú Tóu, Ox-Head, the Iron Charger: one of the two guardians of the underworld's gate, stepping
// down off his statue by Huangquan Road when a Final Rush calls him. he marches after the player at
// half their speed; then he stops, flashes red and paws the ground while a lane of red chevrons
// shows the line he'll take (0.8 s), straight at where the player stood when he stopped, and
// charges down it. anything of the horde in his way is trampled flat, its number shown in the
// hostile red-violet rather than the player's colours. he runs on past, skids to a halt wheeling
// round, and comes straight back at them off the skid with a shorter warning; after the second
// charge he marches again. only a wall stops him short: he's dazed a moment, stars round his horns,
// taking 20% more damage. two of them never charge down the same line: a second lane parallel and
// close to one already shown is moved aside, so they come as a pair of lanes
[RequireComponent(typeof(EnemyMovement))]
[RequireComponent(typeof(EnemyHealth))]
public class BullHead : MonoBehaviour
{
    [Header("march")]
    [Tooltip("his walk, as a multiple of the player's base speed")]
    public float march = 0.5f;
    [Tooltip("seconds of marching between charges, a random amount in this range")]
    public Vector2 marchBetween = new Vector2(2.2f, 3.4f);
    [Tooltip("he only charges from this close, units; further off he keeps marching")]
    public float chargeFrom = 11f;

    [Header("charge")]
    public float telegraph = 0.8f;
    [Tooltip("his charge, as a multiple of the player's base speed")]
    public float chargeSpeed = 2.6f;
    [Tooltip("units he carries on past where the player stood")]
    public float overshoot = 4f;
    public float longestCharge = 16f;
    [Tooltip("a share of the player's max health the charge takes if it catches them")]
    public float hitShare = 0.2f;
    [Tooltip("how wide a path he tramples through the horde, units")]
    public float trample = 1.1f;
    [Tooltip("two lanes closer than this and nearly parallel: the second is moved aside by it")]
    public float laneGap = 2.4f;

    [Header("after a charge")]
    [Tooltip("seconds he skids to a halt and wheels round when a charge runs out")]
    public float skid = 0.4f;
    [Tooltip("charges in a row before he marches again: each after the first comes off the skid")]
    [Min(1)] public int chain = 2;
    [Tooltip("the warning before a charge off the skid")]
    public float chainTelegraph = 0.45f;
    [Tooltip("seconds he's dazed after running into a wall")]
    public float wallDaze = 0.7f;
    [Tooltip("damage he takes while dazed, times usual")]
    public float dazedTaken = 1.2f;

    private enum State { March, Telegraph, Charge, Skid, Daze }
    private State state;
    private float until, chargeEnd, warning;
    private Vector2 dir, laneStart;
    private int chargesLeft;
    private bool hitPlayer;
    // the wall check: how far he got over a short window of the charge. the swarm moves him on
    // physics steps, so a frame or two without moving is only a frame without a step, not a wall
    private Vector2 windowFrom;
    private float windowStart;
    private const float Window = 0.15f;

    private EnemyMovement move;
    private EnemyHealth health;
    private HqEnemyArt art;
    private Collider2D body;
    private Collider2D playerBody;
    private SpriteRenderer lane;
    private Sprite[] laneArt, dustArt, crashArt, trampleArt, launchArt, ghostRight, ghostLeft;
    private readonly Sprite[][] crackArt = new Sprite[8][];
    private Vector2 lastCrack;
    private float nextGhost;

    // his hooves, below his middle (the art's centre)
    private const float Feet = 1.45f;
    private const float CrackEvery = 0.7f;      // units of road between cracks
    private const float GhostEvery = 0.06f;     // seconds between afterimages
    private Color rest = Color.white;
    private readonly List<EnemyMovement> under = new List<EnemyMovement>(32);

    // the lanes being shown or run right now, so a second bull picks another
    private static readonly List<BullHead> charging = new List<BullHead>();
    private static float lastTelegraph;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { charging.Clear(); lastTelegraph = -99f; }

    private void Awake()
    {
        move = GetComponent<EnemyMovement>();
        health = GetComponent<EnemyHealth>();
        art = GetComponent<HqEnemyArt>();
        body = GetComponent<Collider2D>();
        laneArt = YamaArt.Strip("Huangquan/charge_lane");
        dustArt = YamaArt.Strip("Huangquan/charge_dust");
        crashArt = YamaArt.Strip("Huangquan/charge_crash");
        trampleArt = YamaArt.Strip("Huangquan/trample");
        launchArt = YamaArt.Strip("Huangquan/charge_launch");
        ghostRight = YamaArt.Strip("Huangquan/bull_ghost_r");
        ghostLeft = YamaArt.Strip("Huangquan/bull_ghost_l");
        for (int k = 0; k < 8; k++) crackArt[k] = YamaArt.Strip("Huangquan/charge_crack_" + k);
        if (laneArt != null && laneArt.Length > 0)
        {
            var go = new GameObject("Charge Lane");
            lane = go.AddComponent<SpriteRenderer>();
            lane.sprite = laneArt[0];
            lane.drawMode = SpriteDrawMode.Tiled;
            lane.sortingLayerName = "Player";
            lane.sortingOrder = -6;
            go.SetActive(false);
        }
    }

    private void OnEnable()
    {
        move.SetSpeed(Hq.PlayerBaseSpeed * march);
        Begin(State.March, Random.Range(marchBetween.x, marchBetween.y) + 1.5f);
    }

    private void OnDisable()
    {
        charging.Remove(this);
        Ghost(false);
        if (lane != null) lane.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (lane != null) Destroy(lane.gameObject);
    }

    private void Begin(State s, float seconds)
    {
        if (state == State.Daze && s != State.Daze) Taken(1f);
        state = s;
        until = Time.time + seconds;
    }

    private void Update()
    {
        float now = Time.time;
        switch (state)
        {
            case State.March:
                if (now >= until && !TryTelegraph(telegraph))
                    until = now + 0.4f;   // too far, or another bull only just began: march on a moment
                break;

            case State.Telegraph:
                move.Drive(Vector2.zero, 0.1f);
                move.Face(dir, 0.2f);
                ShowLane(now);
                // flashing red, faster as the charge comes
                if (art != null && art.Renderer != null)
                {
                    float k = 1f - (until - now) / warning;
                    float blink = Mathf.PingPong(now * Mathf.Lerp(6f, 16f, k), 1f);
                    art.Renderer.color = Color.Lerp(rest, new Color(1f, 0.25f, 0.2f, 1f), blink);
                }
                if (now >= until) StartCharge();
                break;

            case State.Charge:
                Charge(now);
                break;

            case State.Skid:
            {
                // sliding to a halt along the line, dust flying, turning to face them again
                float left = Mathf.Clamp01((until - now) / skid);
                move.Drive(dir * (Hq.PlayerBaseSpeed * chargeSpeed * 0.45f * left * left), 0.1f, ghost: true);
                if (Hq.FindPlayer(out Vector2 p)) move.Face(p - (Vector2)transform.position, 0.1f);
                if (dustArt != null && Random.value < 0.5f * left)
                    FxBatch.Play(dustArt, 18f, (Vector2)transform.position + Vector2.down * Feet + Random.insideUnitCircle * 0.3f, 1f, "Enemy", 1);
                if (now >= until)
                {
                    Ghost(false);
                    if (chargesLeft <= 0 || !TryTelegraph(chainTelegraph)) March();
                }
                break;
            }

            case State.Daze:
                move.Drive(Vector2.zero, 0.1f);
                if (now >= until) March();
                break;
        }
    }

    private void March()
    {
        chargesLeft = 0;
        if (art != null) art.Stop();
        Begin(State.March, Random.Range(marchBetween.x, marchBetween.y));
    }

    private bool TryTelegraph(float seconds)
    {
        if (!Hq.FindPlayer(out Vector2 player)) return false;
        Vector2 me = transform.position;
        Vector2 to = player - me;
        if (to.magnitude > chargeFrom || Time.time - lastTelegraph < 0.6f) return false;
        // a fresh run of charges from the march; off a skid, the next in the run
        if (state == State.March) chargesLeft = chain;
        lastTelegraph = Time.time;
        Vector2 target = Apart(me, player);
        to = target - me;
        dir = to.sqrMagnitude > 0.01f ? to.normalized : Vector2.right;
        // never out through the rush's ring of seals: the lane stops short of it
        float length = Mathf.Min(longestCharge, to.magnitude + overshoot, FinalRushBound.ToEdge(me, dir));
        if (length < 1.5f) return false;
        laneStart = me;
        chargeEnd = length;
        charging.Add(this);
        warning = seconds;
        if (art != null)
        {
            rest = art.Renderer != null ? art.Renderer.color : Color.white;
            art.Play("telegraph", seconds, loop: true);
        }
        Hq.Sound("hq_bull_snort", me, 0.7f, 0.2f);
        Begin(State.Telegraph, seconds);
        return true;
    }

    // where to aim so this lane isn't another bull's: a lane nearly parallel and close to one
    // already shown is moved to the side, away from it
    private Vector2 Apart(Vector2 me, Vector2 target)
    {
        Vector2 d = (target - me).normalized;
        foreach (var other in charging)
        {
            if (other == null || other == this) continue;
            if (Mathf.Abs(Vector2.Dot(d, other.dir)) < 0.87f) continue;         // more than 30° apart: a distinct line
            Vector2 side = new Vector2(-other.dir.y, other.dir.x);
            float gap = Vector2.Dot(target - other.laneStart, side);
            if (Mathf.Abs(gap) >= laneGap) continue;
            target += side * ((gap >= 0f ? 1f : -1f) * laneGap - gap);
            d = (target - me).normalized;
        }
        return target;
    }

    private void ShowLane(float now)
    {
        if (lane == null) return;
        if (!lane.gameObject.activeSelf) lane.gameObject.SetActive(true);
        float k = 1f - Mathf.Clamp01((until - now) / warning);
        lane.sprite = YamaArt.Frame(laneArt, now, 14f);
        float width = lane.sprite.bounds.size.y;
        lane.size = new Vector2(chargeEnd * Mathf.Clamp01(k * 3f), width);
        lane.transform.position = laneStart + Vector2.down * Feet + dir * (lane.size.x * 0.5f);
        lane.transform.rotation = Quaternion.Euler(0f, 0f, Hq.Angle(dir));
        lane.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.45f, 0.95f, k));
    }

    private void StartCharge()
    {
        if (art != null && art.Renderer != null) art.Renderer.color = rest;
        if (lane != null) lane.gameObject.SetActive(false);
        hitPlayer = false;
        chargesLeft--;
        windowFrom = transform.position;
        windowStart = Time.time;
        Ghost(true);
        float speed = Hq.PlayerBaseSpeed * chargeSpeed;
        float seconds = chargeEnd / speed;
        move.Drive(dir * speed, seconds + 0.1f, ghost: true);
        move.Face(dir, seconds + 0.2f);
        if (art != null) art.Play("charge", seconds + 0.1f, loop: true);
        Hq.Sound("hq_bull_charge", transform.position, 0.85f, 0.2f);
        Juice.Shake(0.12f);
        // the road bursting under his hooves as he sets off
        Vector2 hooves = (Vector2)transform.position + Vector2.down * Feet;
        if (launchArt != null) FxBatch.Play(launchArt, 20f, hooves, 1f, "Player", -2);
        lastCrack = hooves;
        nextGhost = Time.time;
        Begin(State.Charge, seconds);
    }

    // through the player rather than shoving them along: the charge's hit is its own
    private void Ghost(bool on)
    {
        if (body == null) return;
        if (playerBody == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerBody = p.GetComponent<Collider2D>();
        }
        if (playerBody != null) Physics2D.IgnoreCollision(body, playerBody, on);
    }

    private void Charge(float now)
    {
        Vector2 me = transform.position;
        float speed = Hq.PlayerBaseSpeed * chargeSpeed;

        // a wall: over a whole window he's got a fifth of the way he should have
        if (now - windowStart >= Window)
        {
            if ((me - windowFrom).magnitude < speed * (now - windowStart) * 0.2f) { Crash(me, true); return; }
            windowFrom = me;
            windowStart = now;
        }

        // the horde in his path goes under his hooves
        under.Clear();
        EnemySwarm.Near(me + dir * 0.3f, trample, under);
        int trampled = 0;
        foreach (var m in under)
        {
            if (m == move || m == null || m.GetComponent<BossMarker>() != null) continue;
            if (!m.TryGetComponent(out EnemyHealth h) || h.IsDead) continue;
            Vector2 at = m.transform.position;
            // what it has left, shown in the hostile colour: it's the bull's kill, not the player's hit
            h.TakeDamage(h.Current, DamageKind.Hostile, ignoreArmor: true);
            if (trampleArt != null && trampled++ < 6) FxBatch.Play(trampleArt, 22f, at, 1f, "Aura", 20);
        }
        if (trampled > 0) Hq.Sound("hq_bull_trample", me, 0.5f, 0.09f);

        Vector2 hooves = me + Vector2.down * Feet;
        if (dustArt != null && Random.value < 0.6f) FxBatch.Play(dustArt, 18f, hooves - dir * 0.6f + Random.insideUnitCircle * 0.25f, 1f, "Enemy", 1);
        // the road splitting behind him, glowing, cooling
        if ((hooves - lastCrack).sqrMagnitude >= CrackEvery * CrackEvery)
        {
            lastCrack = hooves;
            var crack = crackArt[CrackHeading(dir)];
            if (crack != null) FxBatch.Play(crack, 7f, hooves, 1f, "Player", -3);
        }
        // afterimages burning off behind him
        if (now >= nextGhost)
        {
            nextGhost = now + GhostEvery;
            var ghost = dir.x >= 0f ? ghostRight : ghostLeft;
            if (ghost != null) FxBatch.Play(ghost, 18f, me, 1f, "Enemy", 1);
        }

        // the player, caught in the line
        if (!hitPlayer && Hq.FindPlayer(out Vector2 player) && (player - me).sqrMagnitude < 0.9f * 0.9f)
        {
            hitPlayer = true;
            var ph = Hq.PlayerHealth;
            if (ph != null && !ph.IsDead)
            {
                // grows with the run like the road's shots, never more than half their health
                ph.TakeDamage(ph.Max * Mathf.Min(0.5f, Hq.Hurt(hitShare)));
                Juice.Shake(0.3f);
                Hq.Sound("hq_bull_gore", player, 0.8f, 0.2f);
            }
        }

        if (now >= until) Crash(me, false);
    }

    // which of the eight pre-turned cracks runs along a heading (they're turned clockwise on the
    // screen by eighths of a half turn, and a crack runs both ways)
    private static int CrackHeading(Vector2 d)
    {
        float a = -Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        a = ((a % 180f) + 180f) % 180f;
        return Mathf.RoundToInt(a / 22.5f) % 8;
    }

    // the charge over: into a wall (a crash, dazed a moment), or run out (a skid, and round again)
    private void Crash(Vector2 me, bool wall)
    {
        charging.Remove(this);
        move.StopDrive();
        if (!wall)
        {
            Hq.Sound("hq_bull_skid", me, 0.6f, 0.1f);
            if (art != null) art.Stop();
            Begin(State.Skid, skid);
            return;
        }
        Ghost(false);
        if (crashArt != null) FxBatch.Play(crashArt, 20f, me + dir * 0.6f, 1.2f, "Aura", 25);
        Hq.Sound("hq_bull_crash", me, 0.9f, 0.1f, 1f, 0.03f);
        Juice.Shake(0.35f);
        if (art != null) art.Play("stun", wallDaze, loop: true);
        Hq.Sound("hq_bull_dazed", me, 0.5f, 0.3f);
        Taken(dazedTaken);
        Begin(State.Daze, wallDaze);
    }

    // his damage taken while dazed, on top of what his kind takes
    private float takenMul = 1f;
    private void Taken(float mul)
    {
        health.physicalTaken *= mul / takenMul;
        health.magicalTaken *= mul / takenMul;
        takenMul = mul;
    }
}
