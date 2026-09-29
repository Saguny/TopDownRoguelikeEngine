using System.Collections.Generic;
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

    // every bullet in flight, for anything that catches them (the Gourd of Heaven and Earth)
    private static readonly List<EnemyBullet> live = new List<EnemyBullet>();
    public static IReadOnlyList<EnemyBullet> Live => live;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        player = null;
        live.Clear();
    }

    private void OnEnable()
    {
        live.Add(this);
        EnemyShots.Register(Shots);
    }
    private void OnDisable() => live.Remove(this);

    public float Damage => damage;

    // drawn off its course toward a point, as fast as `speed`, instead of flying on
    public void PullToward(Vector2 point, float speed)
    {
        Vector2 to = point - (Vector2)transform.position;
        velocity = Vector2.MoveTowards(velocity, to.normalized * speed, speed * 6f * Time.deltaTime);
    }

    // caught and gone, without hurting anyone
    public void Swallow() => ObjectPool.Recycle(gameObject);

    // all of them, for a clear of every enemy shot (EnemyShots)
    private static readonly Clearer Shots = new Clearer();
    private sealed class Clearer : IEnemyShots
    {
        public int ClearWithin(Vector2 centre, float radius, bool drops)
        {
            int n = 0;
            // a swallowed bullet leaves the list as it goes
            for (int i = live.Count - 1; i >= 0; i--)
            {
                if (i >= live.Count) continue;
                var b = live[i];
                if (b == null || !EnemyShots.Within(b.transform.position, centre, radius)) continue;
                EnemyShots.Sparkle(b.transform.position);
                b.Swallow();
                n++;
            }
            return n;
        }
    }

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
