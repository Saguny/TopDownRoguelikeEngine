using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(CircleCollider2D))]
public class Aura : MonoBehaviour
{
    [Header("Gameplay")]
    public float radius = 2.5f;
    public float damage = 10f;
    public float damageInterval = 0.5f;
    // the Electrical Aura it's the field of, for the run's damage stats
    [System.NonSerialized] public Weapon source;
    [SerializeField] private LayerMask enemyMask;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string spawnAnimName = "Aura_Spawn";
    [SerializeField] private string loopAnimName = "Aura_Loop";

    [SerializeField] private float _visualScaleBase = 2.5f;

    [Tooltip("the pixel art ring and strikes. when set, the animator above is left alone")]
    [SerializeField] private AuraVisual visual;
    private readonly List<Vector2> _hitPoints = new List<Vector2>(16);

    // each enemy has a cooldown of its own: one walking in is shocked straight away and then every
    // interval while it stays, instead of the whole field pulsing at once. the field is checked a
    // few times a frame's worth apart, not every frame, to keep the physics query cheap
    private const float MinInterval = 0.1f;
    private const float ScanSeconds = 0.05f;

    // radius is the aura's own level; the global Area stat multiplies on top
    private float EffectiveRadius => radius * (_stats ? _stats.AreaMul : 1f);

    private CircleCollider2D _collider;
    private StatContext _stats;
    private float _nextScanTime;
    private readonly List<Collider2D> _hits = new List<Collider2D>(16);
    private ContactFilter2D _filter;
    private bool _filterReady;
    private readonly HashSet<int> _hitThisScan = new HashSet<int>();

    // when each enemy (by instance id) can next be shocked
    private readonly Dictionary<int, float> _nextHitFor = new Dictionary<int, float>(128);
    private readonly List<int> _expired = new List<int>(32);
    private float _nextPruneTime;

    private void Awake()
    {
        _collider = GetComponent<CircleCollider2D>();
        _collider.isTrigger = true;
        _stats = GetComponentInParent<StatContext>();
        if (!visual) visual = GetComponent<AuraVisual>();
        if (!animator && !visual) animator = GetComponentInChildren<Animator>(true);

        if (_visualScaleBase <= 0f)
            _visualScaleBase = radius;
    }

    private void OnEnable()
    {
        _nextScanTime = 0f;
        _nextHitFor.Clear();
        PlaySpawnOnce();
    }

    private void Update()
    {
        _collider.radius = EffectiveRadius;

        if (Time.time >= _nextScanTime)
        {
            _nextScanTime = Time.time + ScanSeconds;
            DamageWithinRadius();
        }
        if (Time.time >= _nextPruneTime) Prune();

        if (visual)
        {
            visual.Show(EffectiveRadius);
            return;
        }

        float scaleFactor = EffectiveRadius / _visualScaleBase;
        if (animator == null || animator.runtimeAnimatorController == null)
            transform.localScale = Vector3.one * scaleFactor;
        else
            animator.transform.localScale = Vector3.one * scaleFactor;
    }

    public void OnRadiusUpgraded()
    {
        if (visual) return;
        if (animator && !string.IsNullOrEmpty(loopAnimName))
            animator.Play(loopAnimName, 0, 0f);
    }

    // built once and reused so the pulse query stays allocation free
    private ContactFilter2D Filter()
    {
        if (_filterReady) return _filter;

        _filter = new ContactFilter2D();
        _filter.SetLayerMask(enemyMask);
        _filter.useTriggers = Physics2D.queriesHitTriggers;
        _filterReady = true;
        return _filter;
    }

    private bool DamageWithinRadius()
    {
        _hitThisScan.Clear();
        _hitPoints.Clear();
        Physics2D.OverlapCircle(transform.position, EffectiveRadius, Filter(), _hits);
        bool hitSomething = false;
        float now = Time.time;
        float interval = Mathf.Max(MinInterval, _stats ? _stats.CooldownFor(UpgradeType.AuraCooldown, damageInterval) : damageInterval);

        for (int i = 0; i < _hits.Count; i++)
        {
            var c = _hits[i];
            if (!c) continue;

            var eh = c.GetComponent<EnemyHealth>();
            if (eh == null) continue;

            // keyed on the enemy, not the collider, so one with two colliders is shocked once
            int id = eh.GetInstanceID();
            if (!_hitThisScan.Add(id)) continue;
            if (_nextHitFor.TryGetValue(id, out float next) && now < next) continue;

            float dealt = damage * (_stats ? _stats.OC(UpgradeType.AuraDamage) * _stats.MightMul * _stats.ClassMul(AttackClass.Magical) : 1f);
            bool crit = false;
            if (_stats) dealt = _stats.WithCrit(dealt, out crit);
            eh.TakeDamage(dealt, DamageKind.Aura, crit, false, source);
            _nextHitFor[id] = now + interval;
            if (visual) _hitPoints.Add(c.transform.position);
            hitSomething = true;
        }

        if (visual && _hitPoints.Count > 0) visual.Strike(_hitPoints);
        return hitSomething;
    }

    // forget enemies whose cooldown ran out long ago (dead, pooled or wandered off)
    private void Prune()
    {
        _nextPruneTime = Time.time + 2f;
        _expired.Clear();
        foreach (var pair in _nextHitFor)
            if (Time.time > pair.Value + 1f) _expired.Add(pair.Key);
        foreach (int id in _expired) _nextHitFor.Remove(id);
    }

    private void PlaySpawnOnce()
    {
        if (visual) return;
        if (animator && !string.IsNullOrEmpty(spawnAnimName))
            animator.Play(spawnAnimName, 0, 0f);
    }

    private void PlayLoopOnce()
    {
        if (animator && !string.IsNullOrEmpty(loopAnimName))
            animator.Play(loopAnimName, 0, 0f);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
#endif
}
