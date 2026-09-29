using UnityEngine;

// Mǎ Miàn, Horse-Face, the Lantern Pulse: Ox-Head's partner at the gate, a tall horse-headed
// guardian with a soul lantern hung from his chain whip. he floats at a middle distance, keeping a
// buffer (backing off when the player comes close, drifting in when they get away) and in a slow
// rhythm raises the whip while a ring of light swells round the lantern (0.5 s), then pulses: one
// ring of twelve slow blue spirit orbs, each ring turned half a gap from the last, then 2 s of rest,
// drifting. alone he's a metronome to weave through; beside Ox-Head's charges the rings are what
// leave no easy line to run down
[RequireComponent(typeof(EnemyMovement))]
[RequireComponent(typeof(EnemyHealth))]
public class HorseFace : MonoBehaviour
{
    [Header("keeping a buffer (in screen half-heights from the player)")]
    public float near = 0.5f;
    public float far = 0.9f;
    [Tooltip("his float, as a multiple of the player's base speed")]
    public float floatSpeed = 0.45f;

    [Header("the pulse")]
    public float telegraph = 0.5f;
    public float rest = 2f;
    public int orbs = 12;
    public float orbSpeed = 2.3f;
    public float orbLife = 9f;
    [Tooltip("a share of the player's max health an orb takes")]
    public float orbDamage = 0.07f;
    public float firstPulse = 2f;

    private enum State { Rest, Telegraph }
    private State state;
    private float until, ringAngle, side = 1f, nextSwap;
    private EnemyMovement move;
    private HqEnemyArt art;
    private SpriteRenderer halo;
    private Sprite[] haloArt, pulseArt;

    // where the lantern is held up in his raise (the art's: 1 unit ahead of him and 1.3 up), on
    // whichever side he's facing
    private Vector2 Lantern
    {
        get
        {
            bool left = art != null && art.Renderer != null && art.Renderer.flipX;
            return (Vector2)transform.position + new Vector2(left ? -1.01f : 1.01f, 1.28f);
        }
    }

    private void Awake()
    {
        move = GetComponent<EnemyMovement>();
        art = GetComponent<HqEnemyArt>();
        haloArt = YamaArt.Strip("Huangquan/horse_halo");
        pulseArt = YamaArt.Strip("Huangquan/horse_pulse");
        if (haloArt != null && haloArt.Length > 0)
        {
            var go = new GameObject("Lantern Halo");
            go.transform.SetParent(transform, false);
            halo = go.AddComponent<SpriteRenderer>();
            halo.sortingLayerName = "Aura";
            halo.sortingOrder = 22;
            go.SetActive(false);
        }
    }

    private void OnEnable()
    {
        state = State.Rest;
        until = Time.time + firstPulse + Random.value;
        ringAngle = Random.value * 360f;
        side = Random.value < 0.5f ? -1f : 1f;
        nextSwap = Time.time + Random.Range(2f, 4f);
        move.SetSpeed(Hq.PlayerBaseSpeed * floatSpeed);
    }

    private void Update()
    {
        if (!Hq.FindPlayer(out Vector2 player)) return;
        float now = Time.time;
        Keep(player, now);

        if (state == State.Rest && now >= until)
        {
            state = State.Telegraph;
            until = now + telegraph;
            if (art != null) art.Play("raise", telegraph + 0.2f);
            if (halo != null) halo.gameObject.SetActive(true);
            Hq.Sound("hq_horse_raise", transform.position, 0.7f, 0.15f);
        }
        if (state == State.Telegraph)
        {
            // the ring of light swelling round the lantern
            float k = 1f - Mathf.Clamp01((until - now) / telegraph);
            if (halo != null)
            {
                halo.sprite = YamaArt.Frame(haloArt, now, 16f);
                float s = Mathf.Lerp(0.3f, 1.2f, k * k);
                halo.transform.localScale = new Vector3(s, s, 1f) / Mathf.Max(0.01f, transform.lossyScale.x);
                halo.transform.position = Lantern;
                halo.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.4f, 1f, k));
            }
            if (now >= until) Pulse();
        }
    }

    // floating at his distance, sidling round when he's in it
    private void Keep(Vector2 player, float now)
    {
        Vector2 off = (Vector2)transform.position - player;
        float dist = off.magnitude, h = Hq.HalfHeight;
        Vector2 away = dist > 0.01f ? off / dist : Vector2.up;
        float speed = Hq.PlayerBaseSpeed * floatSpeed;
        if (state == State.Telegraph) { move.Drive(Vector2.zero, 0.1f); return; }
        if (dist < near * h) move.Drive(away * speed * 1.3f, 0.12f);
        else if (dist <= far * h)
        {
            if (now >= nextSwap) { side = -side; nextSwap = now + Random.Range(2.5f, 4.5f); }
            move.Drive(new Vector2(-away.y, away.x) * side * speed * 0.5f + Vector2.up * Mathf.Sin(now * 2f) * 0.2f, 0.12f);
        }
        // further off he floats in on his own walk
    }

    private void Pulse()
    {
        state = State.Rest;
        until = Time.time + rest;
        if (halo != null) halo.gameObject.SetActive(false);
        Vector2 at = Lantern;
        var orb = Shot.Of(BulletType.Orb, BulletColor.Azure, orbSpeed).Life(orbLife).Hurts(Hq.Hurt(orbDamage)).Silent();
        Danmaku.Ring(at, orbs, ringAngle, orb);
        ringAngle += 180f / orbs;
        if (pulseArt != null) FxBatch.Play(pulseArt, 20f, at, 1.4f, "Aura", 23);
        Hq.Sound("hq_horse_pulse", at, 0.8f, 0.1f, 1f, 0.03f);
        Juice.Shake(0.06f);
    }
}
