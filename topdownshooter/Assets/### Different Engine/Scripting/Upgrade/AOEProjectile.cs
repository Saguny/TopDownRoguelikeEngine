using UnityEngine;

public class AOEProjectile : MonoBehaviour
{
    [Header("Movement")]
    public float speed = 10f;
    public float lifeTime = 4f;

    [Header("Damage")]
    public float damage = 20f;
    [System.NonSerialized] public StatContext stats;   // set by AOEAttack, so the blast can crit
    [System.NonSerialized] public Weapon source;       // the Meteorite, for the run's damage stats
    public float radius = 2f;

    [Header("VFX")]
    [Tooltip("the blast. one with an FxOneShot is pooled; anything else is made and destroyed after Impact Effect Duration")]
    public GameObject impactEffectPrefab;
    public float impactEffectDuration = 0.5f;
    public float impactVisualScale = 1f;
    [Tooltip("the seal on the ground where it will land, timed to the fall and sized to the blast")]
    public GameObject targetMarkPrefab;
    [Tooltip("the scorch it leaves, sized to the blast")]
    public GameObject craterPrefab;
    [Tooltip("camera shake when it lands")]
    public float impactShake = 0.05f;

    [Header("Visual")]
    public float spriteAngleOffset = 90f;

    [Header("Audio")]
    public AudioClip explosionSound;
    [Range(0f, 1f)] public float explosionVolume = 1f;

    private Vector3 _impactPosition;
    private Vector2 _direction;
    private bool _initialized;
    private GameObject _mark;

    public void Setup(float damageAmount, float radius, Vector3 impactPosition, Vector2 direction, AudioClip splashClip, float splashVol,
        float speedMultiplier = 1f, float visualScale = 1f)
    {
        speed *= Mathf.Max(0.01f, speedMultiplier);
        impactVisualScale *= Mathf.Max(0.01f, visualScale);

        damage = damageAmount;
        this.radius = radius;
        _impactPosition = impactPosition;
        _direction = direction.normalized;
        explosionSound = splashClip;
        explosionVolume = splashVol;
        _initialized = true;

        float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.AngleAxis(angle + spriteAngleOffset, Vector3.forward);

        Destroy(gameObject, lifeTime);

        // the seal closes in over exactly the time the fall takes
        if (targetMarkPrefab != null)
        {
            float seconds = Vector2.Distance(transform.position, impactPosition) / Mathf.Max(0.01f, speed);
            _mark = FxOneShot.Play(targetMarkPrefab, impactPosition, 0f, impactVisualScale);
            if (_mark.TryGetComponent(out Flipbook seal)) seal.PlayOver(seconds);
            if (_mark.TryGetComponent(out FxOneShot shot)) shot.lifetime = seconds + 0.5f;
        }
    }

    private void Update()
    {
        if (!_initialized) return;

        float distanceThisFrame = speed * Time.deltaTime;
        transform.position += (Vector3)(_direction * distanceThisFrame);

        Vector2 toImpact = _impactPosition - transform.position;
        if (Vector2.Dot(toImpact, _direction) <= 0f || toImpact.sqrMagnitude <= distanceThisFrame * distanceThisFrame)
        {
            Explode();
        }
    }

    // no early detonation on contact: the meteor is falling from the sky, so enemies under its
    // flight path aren't in the way. exploding on the first one it crossed made it land short of
    // wherever it was aimed, which threw away the targeting in AOEAttack
    private void Explode()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius);
        foreach (var hit in hits)
        {
            if (hit.TryGetComponent(out EnemyHealth enemy))
            {
                bool crit = false;
                float dealt = stats ? stats.WithCrit(damage * stats.ClassMul(AttackClass.Magical), out crit) : damage;
                enemy.TakeDamage(dealt, DamageKind.Meteor, crit, false, source);
            }
        }

        if (_mark != null)
        {
            ObjectPool.Recycle(_mark);
            _mark = null;
        }

        Vector3 at = _impactPosition;
        if (impactEffectPrefab != null)
        {
            if (impactEffectPrefab.TryGetComponent(out FxOneShot _))
                FxOneShot.PlayShot(impactEffectPrefab, at, 0f, impactVisualScale);
            else
            {
                GameObject fx = Instantiate(impactEffectPrefab, at, Quaternion.identity);
                PlayerShots.Tag(fx, false);
                fx.transform.localScale = Vector3.one * impactVisualScale;
                Destroy(fx, impactEffectDuration);
            }
        }
        // turned by quarters only, so the scorch's pixels stay square
        if (craterPrefab != null) FxOneShot.Play(craterPrefab, at, Random.Range(0, 4) * 90f, impactVisualScale);
        if (impactShake > 0f) Juice.Shake(impactShake);

        if (explosionSound != null)
            SfxPlayer.PlayAt(explosionSound, transform.position, explosionVolume);

        Destroy(gameObject);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
#endif
}
