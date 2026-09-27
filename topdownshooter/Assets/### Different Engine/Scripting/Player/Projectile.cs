using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Projectile : MonoBehaviour
{
    [SerializeField] private float damage = 10f;
    [SerializeField, Min(0.1f)] private float maxLifetime = 5f;
    [Tooltip("which way the sprite points when unrotated; Fire turns that side toward where it flies")]
    [SerializeField] private bool artPointsRight = true;
    [Tooltip("played where it hits, turned the way it was flying")]
    [SerializeField] private GameObject hitFx;

    private Rigidbody2D rb;
    private bool fired;
    private Vector2 dir;
    private float spd;
    private float life;
    private bool despawning;

    private float critChance;
    private float critMultiplier = 2f;
    private int pierceLeft;
    private Weapon source;      // the Bow, for the run's damage stats

    // a piercing arrow overlaps the same enemy for several frames, so without this it
    // would burn its whole pierce budget on one target
    private readonly HashSet<int> hitAlready = new HashSet<int>();

    public void SetDamage(float v) { damage = v; }

    public void Configure(float dmg, float chance, float multiplier, int pierce, Weapon from = null)
    {
        damage = dmg;
        source = from;
        critChance = chance;
        critMultiplier = multiplier;
        pierceLeft = Mathf.Max(0, pierce);
        hitAlready.Clear();
    }

    public void Fire(Vector2 direction, float speed)
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        dir = direction.sqrMagnitude > 0 ? direction.normalized : Vector2.right;
        spd = speed;
        fired = true;
        despawning = false;
        life = 0f;
        rb.gravityScale = 0f;
        rb.linearVelocity = dir * spd;

        // point the arrow along its flight; physics mustn't spin it on a bump
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + (artPointsRight ? 0f : 180f);
        rb.freezeRotation = true;
        rb.angularVelocity = 0f;
        rb.rotation = angle;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
    }

    private void OnDisable()
    {
        // clear flight state so a recycled arrow can't drift off before Fire sets it up again
        fired = false;
        life = 0f;
        despawning = false;
        hitAlready.Clear();
        if (rb != null) rb.linearVelocity = Vector2.zero;
    }

    // an arrow that never hits anything used to fly forever and was never reclaimed
    private void Update()
    {
        if (!fired) return;

        life += Time.deltaTime;
        if (life >= maxLifetime) Despawn();
    }

    private void OnCollisionEnter2D(Collision2D c) => TryHit(c.collider);

    private void OnTriggerEnter2D(Collider2D c) => TryHit(c);

    private void TryHit(Collider2D c)
    {
        if (despawning) return;

        var eh = c.GetComponent<EnemyHealth>();
        if (eh == null) return;

        int id = eh.GetInstanceID();
        if (!hitAlready.Add(id)) return;

        bool crit = critChance > 0f && Random.value < critChance;
        float dealt = crit ? damage * critMultiplier : damage;

        eh.TakeDamage(dealt, DamageKind.Arrow, crit, false, source);
        if (hitFx != null) FxOneShot.Play(hitFx, transform.position, FxOneShot.Angle(dir));

        if (pierceLeft > 0)
        {
            pierceLeft--;
            return;
        }

        Despawn();
    }

    private void Despawn()
    {
        if (despawning) return;
        despawning = true;
        ObjectPool.Recycle(gameObject);
    }
}
