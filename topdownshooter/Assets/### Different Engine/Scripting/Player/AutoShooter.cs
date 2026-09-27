using UnityEngine;
using UnityEngine.Serialization;
using System.Collections.Generic;

public class AutoShooter : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField, FormerlySerializedAs("bulletPrefab")] public GameObject arrowPrefab;
    [SerializeField] private AutoAimService aimer;
    [Tooltip("played at the player for every arrow loosed, turned the way it flies")]
    [SerializeField] private GameObject looseFx;

    [Header("Base Stats")]
    [Tooltip("seconds between volleys before any upgrades. the scene used to run a fire rate of 1 per second")]
    [SerializeField, Min(0.01f)] public float baseCooldown = 1f;
    [SerializeField, FormerlySerializedAs("baseBulletSpeed")] public float baseArrowSpeed = 12f;
    [SerializeField, FormerlySerializedAs("baseBulletDamage")] public float baseArrowDamage = 10f;

    [Header("Pooling")]
    [SerializeField, Min(0), FormerlySerializedAs("prewarmBullets")] private int prewarmArrows = 48;

    // one volley per frame is the real ceiling anyway; this keeps the pool sane at high frame rates
    private const float MinCooldown = 0.03f;

    private float cooldown;
    private StatContext stats;
    private Collider2D playerCollider;

    // off until the Bow is the starting weapon or gets picked; its level then sets the numbers below
    public bool Armed { get; set; }
    // the Bow firing it, for the run's damage stats
    public Weapon Source { get; set; }
    private int levelArrows = 1;

    // the Bow's level sets these outright; Might, Cooldown, Weapon Speed and the passives ride on top
    public void SetLevelStats(int arrows, float damage, float cooldownSeconds, float speed)
    {
        levelArrows = Mathf.Max(1, arrows);
        baseArrowDamage = damage;
        baseCooldown = Mathf.Max(0.01f, cooldownSeconds);
        baseArrowSpeed = speed;
    }

    private void Awake()
    {
        stats = GetComponent<StatContext>();
        playerCollider = GetComponent<Collider2D>();

        if (arrowPrefab != null) ObjectPool.For(arrowPrefab).Prewarm(prewarmArrows);
    }

    private void Update()
    {
        if (!Armed) return;

        float volleyCooldown = stats ? stats.CooldownFor(UpgradeType.ArrowCooldown, baseCooldown) : baseCooldown;
        volleyCooldown = Mathf.Max(MinCooldown, volleyCooldown);

        cooldown -= Time.deltaTime;
        if (cooldown > 0f) return;

        int extra = stats ? stats.ArrowCountTotal : 0;
        int shots = Mathf.Max(1, levelArrows + extra);

        List<Transform> targets = null;
        if (aimer != null) targets = aimer.FindTargets(transform.position, shots);
        if (targets == null || targets.Count == 0) return;

        Vector2 forward = (targets[0].position - transform.position).normalized;

        for (int i = 0; i < shots; i++)
        {
            Vector2 dir = (i < targets.Count && targets[i] != null)
                ? (targets[i].position - transform.position).normalized
                : forward;

            SpawnArrow(dir);
        }

        cooldown = volleyCooldown;
    }

    private void SpawnArrow(Vector2 dir)
    {
        var pool = ObjectPool.For(arrowPrefab);
        if (pool == null) return;

        var go = pool.Get(transform.position, Quaternion.identity);
        if (looseFx != null) FxOneShot.Play(looseFx, transform.position + (Vector3)(dir * 0.35f), FxOneShot.Angle(dir));
        float speed = baseArrowSpeed * (stats ? stats.arrowSpeedMul * stats.WeaponSpeedMul : 1f);
        float damage = baseArrowDamage * (stats ? stats.arrowDamageMul * stats.OC(UpgradeType.ArrowDamage) * stats.MightMul : 1f);

        var arrow = go.GetComponent<Projectile>();
        if (arrow != null)
        {
            arrow.Configure(
                damage,
                stats ? stats.CritChanceTotal : 0f,
                stats ? stats.CritMultiplierTotal : 2f,
                stats ? stats.PierceTotal : 0,
                Source);

            arrow.Fire(dir, speed);
        }

        var bCol = go.GetComponent<Collider2D>();
        if (bCol != null && playerCollider != null) Physics2D.IgnoreCollision(bCol, playerCollider, true);
    }
}
