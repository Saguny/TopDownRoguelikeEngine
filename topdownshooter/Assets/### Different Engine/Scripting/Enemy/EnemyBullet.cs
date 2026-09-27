using UnityEngine;

// a shot fired at the player (the boss's corpse fire): flies straight, hurts the player when it
// gets within reach and is gone after a while. no physics; one distance check a frame. pooled,
// so Fire is the way to make one
public class EnemyBullet : MonoBehaviour
{
    [Tooltip("how close to the player's body it has to come to hit, world units")]
    [Min(0.01f)] public float radius = 0.16f;
    [Min(0.1f)] public float lifetime = 7f;

    private Vector2 velocity;
    private float damage, age;

    private static PlayerHealth player;
    private static float playerRadius = 0.3f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => player = null;

    public static void Fire(GameObject prefab, Vector2 at, Vector2 velocity, float damage)
    {
        if (prefab == null) return;
        var go = ObjectPool.For(prefab).Get(at, Quaternion.identity);
        if (!go.TryGetComponent(out EnemyBullet b)) return;
        b.velocity = velocity;
        b.damage = damage;
        b.age = 0f;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        if (age >= lifetime)
        {
            ObjectPool.Recycle(gameObject);
            return;
        }

        Vector2 pos = (Vector2)transform.position + velocity * dt;
        transform.position = pos;

        if (player == null)
        {
            player = FindFirstObjectByType<PlayerHealth>();
            if (player == null) return;
            if (player.TryGetComponent(out CircleCollider2D body))
                playerRadius = body.radius * Mathf.Abs(player.transform.lossyScale.x);
        }

        float reach = radius + playerRadius;
        if (((Vector2)player.transform.position - pos).sqrMagnitude <= reach * reach)
        {
            player.TakeDamage(damage);
            ObjectPool.Recycle(gameObject);
        }
    }
}
