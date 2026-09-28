using UnityEngine;

// Shāo Zhǐ Guǐ, the hell money burners: ghosts bent over braziers of spirit money, a handful of them
// (four to eight) hanging about the screen's edge. they keep their distance, backing off when the
// player comes close and edging sideways round them the rest of the time, and every few seconds one
// straightens up, lights a wad of notes and lobs a fan of three burning embers high over the horde.
// where they come down the ground burns for a moment. so the player can't just circle tightly in
// one spot: the fire goes where they are
[RequireComponent(typeof(EnemyMovement))]
public class HellMoneyBurner : MonoBehaviour
{
    [Header("keeping its distance (in screen half-heights from the player)")]
    [Tooltip("closer than this it backs away")]
    public float near = 0.85f;
    [Tooltip("further than this it walks in as usual")]
    public float far = 1.3f;
    [Tooltip("its speed backing off and sidling round, times its walking speed")]
    public float driftSpeed = 0.9f;

    [Header("the lob")]
    public float interval = 3.6f;
    [Tooltip("seconds it lights the notes before throwing: the warning")]
    public float windUp = 0.5f;
    public int embers = 3;
    [Tooltip("degrees between the embers of a fan")]
    public float fanSpread = 22f;
    [Tooltip("seconds an ember's in the air")]
    public float flight = 0.9f;
    public float poolSeconds = 2f;
    public float poolRadius = 0.75f;
    [Tooltip("a share of the player's max health each half-second in the fire")]
    public float poolDamage = 0.035f;
    [Tooltip("seconds of the player's movement it leads by: aimed at where they're going")]
    public float lead = 0.35f;

    private EnemyMovement move;
    private HqEnemyArt art;
    private float nextLob, throwAt, nextSwap, stuckCheck;
    private bool winding;
    private float side = 1f;
    private Vector2 lastPos, lastPlayer, playerVel;

    private void Awake()
    {
        move = GetComponent<EnemyMovement>();
        art = GetComponent<HqEnemyArt>();
    }

    private void OnEnable()
    {
        winding = false;
        side = Random.value < 0.5f ? -1f : 1f;
        nextLob = Time.time + 1.5f + Random.value * interval;
        nextSwap = Time.time + Random.Range(2f, 4f);
        stuckCheck = Time.time + 0.5f;
        lastPos = transform.position;
        Hq.FindPlayer(out lastPlayer);
    }

    private void Update()
    {
        if (!Hq.FindPlayer(out Vector2 player)) return;
        float now = Time.time, dt = Time.deltaTime;
        if (dt > 0f) playerVel = Vector2.Lerp(playerVel, (player - lastPlayer) / dt, 0.2f);
        lastPlayer = player;

        Vector2 me = transform.position;
        Vector2 off = me - player;
        float dist = off.magnitude;
        Vector2 away = dist > 0.01f ? off / dist : Vector2.up;

        if (winding)
        {
            move.Drive(Vector2.zero, 0.1f);
            move.Face(-away, 0.2f);
            if (now >= throwAt) Throw(me, player);
            return;
        }

        float h = Hq.HalfHeight;
        float speed = move.Speed * driftSpeed;
        if (dist < near * h) move.Drive(away * speed * 1.2f, 0.12f);
        else if (dist <= far * h)
        {
            // sidling round the player; a wall stops it, so it turns back the other way
            if (now >= stuckCheck)
            {
                if ((me - lastPos).sqrMagnitude < 0.04f && move.Driven) side = -side;
                lastPos = me;
                stuckCheck = now + 0.5f;
            }
            if (now >= nextSwap) { side = -side; nextSwap = now + Random.Range(2.5f, 5f); }
            Vector2 round = new Vector2(-away.y, away.x) * side;
            move.Drive(round * speed * 0.6f, 0.12f);
        }
        // further out it walks in on its own

        if (now >= nextLob && dist <= (far + 0.4f) * h && Hq.OnScreen(me, -0.5f))
        {
            winding = true;
            throwAt = now + windUp;
            if (art != null) art.Play("act", windUp + 0.3f);
            Hq.Sound("hq_burner_ignite", me, 0.4f, 0.15f);
        }
    }

    private void Throw(Vector2 me, Vector2 player)
    {
        winding = false;
        nextLob = Time.time + interval * Random.Range(0.85f, 1.15f);
        Vector2 aim = player + playerVel * lead;
        Vector2 to = aim - me;
        float reach = to.magnitude;
        float centre = Hq.Angle(to);
        Vector2 hand = me + new Vector2(0f, 0.45f);
        for (int i = 0; i < embers; i++)
        {
            float a = centre + (i - (embers - 1) * 0.5f) * fanSpread;
            Vector2 land = me + Hq.Dir(a) * reach * Random.Range(0.92f, 1.08f);
            EmberLobs.Lob(hand, land, flight * Random.Range(0.95f, 1.05f), poolSeconds, poolRadius, poolDamage);
        }
        Hq.Sound("hq_burner_throw", me, 0.45f, 0.12f);
    }
}
