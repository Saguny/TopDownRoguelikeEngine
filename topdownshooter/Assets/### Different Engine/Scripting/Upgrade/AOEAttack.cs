using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class AOEAttack : MonoBehaviour
{
    [Header("AOE Attack Settings")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float attackInterval = 5f;
    [SerializeField] private float attackRange = 8f;
    [SerializeField] private float damage = 20f;
    [SerializeField] private int projectileCount = 1;

    [Header("Audio")]
    [SerializeField] private AudioClip projectileSound;
    [Range(0f, 1f)][SerializeField] private float projectileVolume = 0.8f;
    [SerializeField] private AudioClip splashSound;
    [Range(0f, 1f)][SerializeField] private float splashVolume = 0.8f;

    [Header("Meteor Targeting")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Vector2 viewportXRange = new Vector2(0.1f, 0.9f);
    [SerializeField] private Vector2 viewportYRange = new Vector2(0.2f, 0.9f);
    [SerializeField] private float spawnYOffset = 2f;
    [SerializeField] private float meteorTiltFromVerticalDeg = 16.786f;

    [Header("Smart Targeting")]
    [Tooltip("aim at the densest on-screen groups from the start. off: meteors land on enemies picked at random until the Meteor Smartness upgrade is picked")]
    [SerializeField] private bool smartFromStart;
    private bool smartTargeting;

    public bool IsSmart => smartTargeting;

    // the Meteor Smartness upgrade: from now on meteors aim where they'll catch the most enemies
    public void MakeSmart() => smartTargeting = true;

    // the Meteorite dropping them, for the run's damage stats
    public Weapon Source { get; set; }
    [Tooltip("how far to lead moving groups, as a fraction of the meteor's real flight time")]
    [SerializeField, Range(0f, 1.5f)] private float leadFactor = 1f;

    private readonly List<Vector2> _enemyPos = new List<Vector2>();
    private readonly List<Vector2> _enemyVel = new List<Vector2>();
    private readonly List<bool> _covered = new List<bool>();

    // meteors need time to fall and read; below this a volley lands before the last one clears
    private const float MinInterval = 0.75f;

    private AudioSource _audio;
    private StatContext _stats;
    private float _meteorSpeed = 10f;
    private float _authoredRange;

    private float AreaMul => _stats ? _stats.AreaMul : 1f;
    private float SpeedMul => _stats ? _stats.WeaponSpeedMul : 1f;

    // attackRange carries the meteor's own radius upgrades; the global Area stat multiplies on top
    private float EffectiveRange => attackRange * AreaMul;
    private float _timer;
    private bool _active;

    private void Awake()
    {
        _stats = GetComponentInParent<StatContext>();
        _authoredRange = Mathf.Max(0.01f, attackRange);
        smartTargeting = smartFromStart;

        if (projectilePrefab != null && projectilePrefab.TryGetComponent(out AOEProjectile meteor))
            _meteorSpeed = Mathf.Max(0.1f, meteor.speed);
        _audio = GetComponent<AudioSource>();
        if (_audio == null)
            _audio = AudioRouting.Route(gameObject.AddComponent<AudioSource>());

        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    private void Update()
    {
        if (!_active) return;

        float interval = _stats ? _stats.CooldownFor(UpgradeType.AOEAttackCooldown, attackInterval) : attackInterval;

        _timer += Time.deltaTime;
        if (_timer >= Mathf.Max(MinInterval, interval))
        {
            _timer = 0f;
            FireProjectiles();
        }
    }

    public bool IsActive => _active;

    // the Meteorite's level sets these outright; the global Area, Cooldown and Weapon Speed stats
    // still ride on top
    public void Configure(float interval, float damageAmount, float range, int count, bool smart)
    {
        attackInterval = Mathf.Max(0.5f, interval);
        damage = damageAmount;
        attackRange = range;
        projectileCount = Mathf.Max(1, count);
        smartTargeting = smart || smartFromStart;
    }

    public void Activate()
    {
        _active = true;
        _timer = 0f;
    }

    public void Deactivate()
    {
        _active = false;
    }

    public void Upgrade(float intervalMultiplier, float damageMultiplier, float rangeMultiplier, int extraProjectiles)
    {
        attackInterval = Mathf.Max(0.5f, attackInterval * intervalMultiplier);
        damage *= damageMultiplier;
        attackRange *= rangeMultiplier;
        projectileCount = Mathf.Max(1, projectileCount + extraProjectiles);
    }


    private void FireProjectiles()
    {
        if (projectilePrefab == null || targetCamera == null) return;

        float worldAngleDeg = -90f + meteorTiltFromVerticalDeg;
        float worldAngleRad = worldAngleDeg * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Cos(worldAngleRad), Mathf.Sin(worldAngleRad)).normalized;

        Rect view = ViewRect();
        GatherTargets(view);

        for (int i = 0; i < projectileCount; i++)
        {
            // smart, on the thickest group it can catch; before that, on an enemy picked at random
            // (a random spot on the screen mostly hit nothing); with nobody on screen, anywhere
            Vector3 impactPos = _enemyPos.Count == 0 ? RandomPoint(view)
                : smartTargeting ? AimAtCluster(view, dir)
                : RandomEnemy(view, dir);

            float topY = targetCamera.transform.position.y + targetCamera.orthographicSize;
            float spawnY = topY + spawnYOffset;
            float t = (spawnY - impactPos.y) / (-dir.y);

            Vector3 spawnPos = impactPos - (Vector3)(dir * t);
            spawnPos.z = 0f;

            GameObject proj = Instantiate(
                projectilePrefab,
                spawnPos,
                Quaternion.AngleAxis(worldAngleDeg, Vector3.forward)
            );
            PlayerShots.Tag(proj, false);

            if (proj.TryGetComponent(out AOEProjectile aoe))
            {
                aoe.stats = _stats;
                aoe.source = Source;
                float dealt = damage * (_stats ? _stats.OC(UpgradeType.AOEAttackDamage) * _stats.MightMul : 1f);
                // the explosion is drawn to match the real blast, so radius upgrades are visible
                aoe.Setup(dealt, EffectiveRange, impactPos, dir, splashSound, splashVolume,
                    SpeedMul, EffectiveRange / _authoredRange);
            }
        }

        if (projectileSound != null)
            _audio.PlayOneShot(projectileSound, projectileVolume);
    }

    // the same on-screen band the random targeting always used, in world space, so meteors
    // still land where the player can see them
    private Rect ViewRect()
    {
        float z = Mathf.Abs(targetCamera.transform.position.z);
        Vector3 min = targetCamera.ViewportToWorldPoint(new Vector3(viewportXRange.x, viewportYRange.x, z));
        Vector3 max = targetCamera.ViewportToWorldPoint(new Vector3(viewportXRange.y, viewportYRange.y, z));
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private Vector3 RandomPoint(Rect view)
    {
        return new Vector3(
            UnityEngine.Random.Range(view.xMin, view.xMax),
            UnityEngine.Random.Range(view.yMin, view.yMax),
            0f);
    }

    // an enemy on screen picked at random, led by where it'll be when the meteor lands
    private Vector3 RandomEnemy(Rect view, Vector2 dir)
    {
        int i = UnityEngine.Random.Range(0, _enemyPos.Count);
        float topY = targetCamera.transform.position.y + targetCamera.orthographicSize;
        float flightTime = (topY + spawnYOffset - _enemyPos[i].y) / (-dir.y) / (_meteorSpeed * SpeedMul);
        Vector2 aim = _enemyPos[i] + _enemyVel[i] * flightTime * leadFactor;
        return new Vector3(Mathf.Clamp(aim.x, view.xMin, view.xMax), Mathf.Clamp(aim.y, view.yMin, view.yMax), 0f);
    }

    private void GatherTargets(Rect view)
    {
        _enemyPos.Clear();
        _enemyVel.Clear();
        _covered.Clear();

        foreach (var e in EnemyRegistry.All)
        {
            if (e == null) continue;

            Vector2 p = e.transform.position;
            if (!view.Contains(p)) continue;

            _enemyPos.Add(p);
            _enemyVel.Add(e.TryGetComponent(out Rigidbody2D rb) ? rb.linearVelocity : Vector2.zero);
            _covered.Add(false);
        }
    }

    // greedy max coverage: pick the spot whose blast catches the most enemies not already claimed
    // by an earlier meteor this volley, so a multi-meteor volley spreads over separate groups
    // instead of stacking on one. ties go to the group nearest the player, since those are the
    // enemies about to reach them
    private Vector3 AimAtCluster(Rect view, Vector2 dir)
    {
        float r2 = EffectiveRange * EffectiveRange;
        Vector2 playerPos = transform.position;
        bool anyFree = _covered.Contains(false);

        int best = -1;
        int bestCount = 0;
        float bestDist = float.MaxValue;

        for (int i = 0; i < _enemyPos.Count; i++)
        {
            if (anyFree && _covered[i]) continue;

            int count = 0;
            for (int j = 0; j < _enemyPos.Count; j++)
            {
                if (anyFree && _covered[j]) continue;
                if ((_enemyPos[j] - _enemyPos[i]).sqrMagnitude <= r2) count++;
            }

            float dist = (_enemyPos[i] - playerPos).sqrMagnitude;
            if (count > bestCount || (count == bestCount && dist < bestDist))
            {
                best = i;
                bestCount = count;
                bestDist = dist;
            }
        }

        // centre the blast on the whole group rather than on the enemy that seeded it
        Vector2 sum = Vector2.zero;
        Vector2 vel = Vector2.zero;
        int members = 0;

        for (int j = 0; j < _enemyPos.Count; j++)
        {
            if ((_enemyPos[j] - _enemyPos[best]).sqrMagnitude > r2) continue;

            sum += _enemyPos[j];
            vel += _enemyVel[j];
            members++;
            _covered[j] = true;
        }

        Vector2 centre = sum / members;

        // the group keeps walking while the meteor falls, so aim where it will be on impact
        float topY = targetCamera.transform.position.y + targetCamera.orthographicSize;
        float travel = (topY + spawnYOffset - centre.y) / (-dir.y);
        float flightTime = travel / (_meteorSpeed * SpeedMul);

        Vector2 aim = centre + (vel / members) * flightTime * leadFactor;

        return new Vector3(
            Mathf.Clamp(aim.x, view.xMin, view.xMax),
            Mathf.Clamp(aim.y, view.yMin, view.yMax),
            0f);
    }
}
