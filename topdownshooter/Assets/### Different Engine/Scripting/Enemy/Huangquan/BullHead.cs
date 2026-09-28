using System.Collections.Generic;
using UnityEngine;

// Niú Tóu, Ox-Head, the Iron Charger: one of the two guardians of the underworld's gate, stepping
// down off his statue by Huangquan Road when a Final Rush calls him. he marches after the player at
// half their speed; then he stops, flashes red and paws the ground while a lane of red chevrons
// shows the line he'll take (0.8 s), straight at where the player stood when he stopped, and
// charges down it. anything of the horde in his way is trampled flat, its number shown in the
// hostile red-violet rather than the player's colours. he runs on past, and when he
// hits a wall or the charge runs out he's stunned for 1.5 s, stars round his horns, taking 20% more
// damage: the moment to hit him. two of them never charge down the same line: a second lane
// parallel and close to one already shown is moved aside, so they come as a pair of lanes
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

    [Header("stun")]
    public float stun = 1.5f;
    [Tooltip("damage he takes while stunned, times usual")]
    public float stunnedTaken = 1.2f;

    private enum State { March, Telegraph, Charge, Stun }
    private State state;
    private float until, chargeEnd;
    private Vector2 dir, laneStart, lastPos;
    private int stalls;
    private bool hitPlayer;

    private EnemyMovement move;
    private EnemyHealth health;
    private HqEnemyArt art;
    private Collider2D body;
    private Collider2D playerBody;
    private SpriteRenderer lane;
    private Sprite[] laneArt, dustArt, crashArt, trampleArt;
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
        if (state == State.Stun && s != State.Stun) Taken(1f);
        state = s;
        until = Time.time + seconds;
    }

    private void Update()
    {
        float now = Time.time;
        switch (state)
        {
            case State.March:
                if (now >= until) TryTelegraph();
                break;

            case State.Telegraph:
                move.Drive(Vector2.zero, 0.1f);
                move.Face(dir, 0.2f);
                ShowLane(now);
                // flashing red, faster as the charge comes
                if (art != null && art.Renderer != null)
                {
                    float k = 1f - (until - now) / telegraph;
                    float blink = Mathf.PingPong(now * Mathf.Lerp(6f, 16f, k), 1f);
                    art.Renderer.color = Color.Lerp(rest, new Color(1f, 0.25f, 0.2f, 1f), blink);
                }
                if (now >= until) StartCharge();
                break;

            case State.Charge:
                Charge(now);
                break;

            case State.Stun:
                move.Drive(Vector2.zero, 0.1f);
                if (now >= until)
                {
                    Begin(State.March, Random.Range(marchBetween.x, marchBetween.y));
                    if (art != null) art.Stop();
                }
                break;
        }
    }

    private void TryTelegraph()
    {
        if (!Hq.FindPlayer(out Vector2 player)) return;
        Vector2 me = transform.position;
        Vector2 to = player - me;
        // too far to charge, or another bull only just began: march on a moment
        if (to.magnitude > chargeFrom || Time.time - lastTelegraph < 0.6f)
        {
            until = Time.time + 0.4f;
            return;
        }
        lastTelegraph = Time.time;
        Vector2 target = Apart(me, player);
        to = target - me;
        dir = to.sqrMagnitude > 0.01f ? to.normalized : Vector2.right;
        float length = Mathf.Min(longestCharge, to.magnitude + overshoot);
        laneStart = me;
        chargeEnd = length;
        charging.Add(this);
        if (art != null)
        {
            rest = art.Renderer != null ? art.Renderer.color : Color.white;
            art.Play("telegraph", telegraph, loop: true);
        }
        Hq.Sound("hq_bull_snort", me, 0.7f, 0.2f);
        Begin(State.Telegraph, telegraph);
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
        float k = 1f - Mathf.Clamp01((until - now) / telegraph);
        lane.sprite = YamaArt.Frame(laneArt, now, 14f);
        float width = lane.sprite.bounds.size.y;
        lane.size = new Vector2(chargeEnd * Mathf.Clamp01(k * 3f), width);
        lane.transform.position = laneStart + dir * (lane.size.x * 0.5f);
        lane.transform.rotation = Quaternion.Euler(0f, 0f, Hq.Angle(dir));
        lane.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.45f, 0.95f, k));
    }

    private void StartCharge()
    {
        if (art != null && art.Renderer != null) art.Renderer.color = rest;
        if (lane != null) lane.gameObject.SetActive(false);
        hitPlayer = false;
        stalls = 0;
        lastPos = transform.position;
        Ghost(true);
        float speed = Hq.PlayerBaseSpeed * chargeSpeed;
        float seconds = chargeEnd / speed;
        move.Drive(dir * speed, seconds + 0.1f, ghost: true);
        move.Face(dir, seconds + 0.2f);
        if (art != null) art.Play("charge", seconds + 0.1f, loop: true);
        Hq.Sound("hq_bull_charge", transform.position, 0.85f, 0.2f);
        Juice.Shake(0.08f);
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

        // a wall: he's moved almost nothing for a couple of frames though he's charging
        float moved = (me - lastPos).magnitude;
        lastPos = me;
        stalls = moved < speed * Time.deltaTime * 0.3f ? stalls + 1 : 0;
        if (stalls >= 2) { Crash(me, true); return; }

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

        if (dustArt != null && Random.value < 0.5f) FxBatch.Play(dustArt, 18f, me - dir * 0.5f + Random.insideUnitCircle * 0.2f, 1f, "Enemy", 1);

        // the player, caught in the line
        if (!hitPlayer && Hq.FindPlayer(out Vector2 player) && (player - me).sqrMagnitude < 0.9f * 0.9f)
        {
            hitPlayer = true;
            var ph = Hq.PlayerHealth;
            if (ph != null && !ph.IsDead)
            {
                ph.TakeDamage(ph.Max * hitShare);
                Juice.Shake(0.3f);
                Hq.Sound("hq_bull_gore", player, 0.8f, 0.2f);
            }
        }

        if (now >= until) Crash(me, false);
    }

    // the charge over: into a wall (a crash), or run out (a skid). stunned either way
    private void Crash(Vector2 me, bool wall)
    {
        charging.Remove(this);
        Ghost(false);
        move.StopDrive();
        if (wall)
        {
            if (crashArt != null) FxBatch.Play(crashArt, 20f, me + dir * 0.6f, 1.2f, "Aura", 25);
            Hq.Sound("hq_bull_crash", me, 0.9f, 0.1f, 1f, 0.03f);
            Juice.Shake(0.35f);
        }
        else Hq.Sound("hq_bull_skid", me, 0.6f, 0.1f);
        if (art != null) art.Play("stun", stun, loop: true);
        Taken(stunnedTaken);
        Begin(State.Stun, stun);
    }

    // his damage taken while stunned, on top of what his kind takes
    private float takenMul = 1f;
    private void Taken(float mul)
    {
        health.physicalTaken *= mul / takenMul;
        health.magicalTaken *= mul / takenMul;
        takenMul = mul;
    }
}
