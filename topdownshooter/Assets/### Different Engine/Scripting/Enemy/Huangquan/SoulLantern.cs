using System.Collections.Generic;
using UnityEngine;

// Yǐn Hún Dēng, the soul guiding lantern: a paper lantern that leads the dead down the road, rare,
// floating away from the player as they come for it. round it a green glow speeds up every mob
// in reach, and each second it sends out a slow wisp of green soul-fire that follows the player.
// left alone it makes the whole swarm worse; hunted down, it goes out in a burst of green and a
// shower of wen. it shows at the screen's edge like a boss does, so it can be found
[RequireComponent(typeof(EnemyMovement))]
public class SoulLantern : MonoBehaviour
{
    [Header("floating away (in screen half-heights from the player)")]
    [Tooltip("closer than this it floats away")]
    public float flee = 1.5f;
    [Tooltip("further than this it drifts back in, so it isn't lost")]
    public float lost = 2.4f;
    [Tooltip("its speed floating away, times its walking speed")]
    public float fleeSpeed = 1f;

    [Header("the aura")]
    public float auraRadius = 4.5f;
    [Tooltip("the swarm's speed in the aura, times its own")]
    public float haste = 1.4f;

    [Header("the wisps")]
    public float wispEvery = 1f;
    public float wispSpeed = 1.7f;
    [Tooltip("degrees a second it turns after the player")]
    public float wispTurn = 80f;
    [Tooltip("seconds it follows before flying on straight")]
    public float wispHoming = 5f;
    [Tooltip("a share of the player's max health a wisp takes")]
    public float wispDamage = 0.04f;

    private const float AuraTick = 0.25f;

    private static BossIndicatorManager indicator;
    private readonly List<EnemyMovement> near = new List<EnemyMovement>(128);
    private EnemyMovement move;
    private EnemyHealth health;
    private SpriteRenderer aura;
    private Sprite[] auraArt, moteArt;
    private float nextAura, nextWisp, stuckCheck, side = 1f;
    private Vector2 lastPos;
    private bool shown;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => indicator = null;

    private void Awake()
    {
        move = GetComponent<EnemyMovement>();
        health = GetComponent<EnemyHealth>();
        auraArt = YamaArt.Strip("Huangquan/lantern_aura");
        moteArt = YamaArt.Strip("Huangquan/haste_mote");
        if (auraArt != null && auraArt.Length > 0)
        {
            var go = new GameObject("Lantern Aura");
            go.transform.SetParent(transform, false);
            aura = go.AddComponent<SpriteRenderer>();
            aura.sortingLayerName = "Player";
            aura.sortingOrder = -6;
        }
    }

    private void OnEnable()
    {
        nextAura = Time.time;
        nextWisp = Time.time + 1.2f;
        stuckCheck = Time.time + 0.5f;
        lastPos = transform.position;
        side = Random.value < 0.5f ? -1f : 1f;
        shown = false;
        if (health != null) health.Died += OnDied;
    }

    private void Start() => Show();

    // the edge-of-screen marker, like a boss's
    private void Show()
    {
        if (shown) return;
        if (indicator == null) indicator = FindFirstObjectByType<BossIndicatorManager>();
        if (indicator == null) return;
        indicator.RegisterBoss(transform);
        shown = true;
    }

    private void OnDisable()
    {
        if (health != null) health.Died -= OnDied;
        if (shown && indicator != null) indicator.UnregisterBoss(transform);
        shown = false;
    }

    private void Update()
    {
        Show();
        if (!Hq.FindPlayer(out Vector2 player)) return;
        float now = Time.time;
        Vector2 me = transform.position;
        Vector2 off = me - player;
        float dist = off.magnitude, h = Hq.HalfHeight;
        Vector2 away = dist > 0.01f ? off / dist : Vector2.up;

        if (dist < flee * h)
        {
            // away, sliding along a wall when it backs into one
            if (now >= stuckCheck)
            {
                if ((me - lastPos).sqrMagnitude < 0.05f) side = -side;
                lastPos = me;
                stuckCheck = now + 0.5f;
            }
            Vector2 slide = new Vector2(-away.y, away.x) * side * 0.6f;
            move.Drive((away + slide).normalized * move.Speed * fleeSpeed, 0.12f);
        }
        else if (dist <= lost * h)
            move.Drive(new Vector2(0f, Mathf.Sin(now * 1.7f) * 0.3f), 0.12f);    // hanging there, bobbing
        // further off it drifts back in on its own walk

        if (aura != null)
        {
            aura.sprite = YamaArt.Frame(auraArt, now, 8f);
            float s = auraRadius * 2f / Mathf.Max(0.01f, aura.sprite.bounds.size.x);
            aura.transform.localScale = new Vector3(s, s, 1f) / Mathf.Max(0.01f, transform.lossyScale.x);
        }

        if (now >= nextAura)
        {
            nextAura = now + AuraTick;
            Hasten(me);
        }
        if (now >= nextWisp)
        {
            nextWisp = now + wispEvery;
            if (dist < 18f) Wisp(me);
        }
    }

    private void Hasten(Vector2 me)
    {
        near.Clear();
        EnemySwarm.Near(me, auraRadius, near);
        int motes = 0;
        foreach (var m in near)
        {
            if (m == move) continue;
            m.Haste(haste, AuraTick + 0.15f);
            // a few green sparks off the mobs it's quickening, so it reads
            if (moteArt != null && motes < 4 && Random.value < 0.15f)
            {
                motes++;
                FxBatch.Play(moteArt, 14f, (Vector2)m.transform.position + Random.insideUnitCircle * 0.2f, 1f, "Aura", 12);
            }
        }
    }

    private void Wisp(Vector2 me)
    {
        Vector2 at = me + new Vector2(0f, 0.3f);
        var wisp = Shot.Of(BulletType.Orb, BulletColor.Jade, wispSpeed)
            .Home(wispTurn, wispHoming).Life(wispHoming + 3f).Hurts(wispDamage).Silent();
        Danmaku.Fire(at, Danmaku.AimAt(at) + Random.Range(-40f, 40f), wisp);
        var puff = YamaArt.Strip("Huangquan/wisp_puff");
        if (puff != null) FxBatch.Play(puff, 16f, at, 1f, "Aura", 41);
        Hq.Sound("hq_wisp", at, 0.3f, 0.25f);
    }

    private void OnDied(EnemyHealth h)
    {
        if (h.Purged) return;
        Hq.Sound("hq_lantern_out", transform.position, 0.8f, 0.1f, 1f, 0.02f);
        Juice.Shake(0.2f);
        // the soul-fire round it goes out with it
        Danmaku.CancelNear(transform.position, 9f);
    }
}
