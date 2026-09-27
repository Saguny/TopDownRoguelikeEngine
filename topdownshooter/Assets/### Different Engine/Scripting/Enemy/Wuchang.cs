using UnityEngine;

// one of the Wuchang that end a normal run (RunTimeLimit): it drifts straight at the player through
// walls and everything else, faster than they can run however fast they've got, and a touch kills.
// it isn't an enemy, so no weapon aims at it or can hurt it. it keeps coming back for revivals
public class Wuchang : MonoBehaviour
{
    [Tooltip("world units a second at the player's base Move Speed (they run at 6); it keeps pace with Move Speed bonuses")]
    [Min(0.1f)] public float speed = 8.4f;
    [Tooltip("how close counts as a touch")]
    [Min(0.1f)] public float reach = 0.8f;
    [Tooltip("a touch does this much, more than any health there is")]
    public float damage = 65535f;

    private Transform player;
    private PlayerHealth health;
    private StatContext stats;
    private SpriteRenderer sprite;

    private void Start()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            player = p.transform;
            health = p.GetComponent<PlayerHealth>();
            stats = p.GetComponent<StatContext>();
        }
        sprite = GetComponentInChildren<SpriteRenderer>();
    }

    private void Update()
    {
        if (player == null) return;
        Vector2 me = transform.position, to = (Vector2)player.position - me;
        float step = speed * (stats != null ? Mathf.Max(1f, stats.MoveSpeedTotal) : 1f) * Time.deltaTime;
        transform.position += (Vector3)(to.sqrMagnitude > step * step ? to.normalized * step : to);
        if (sprite != null && Mathf.Abs(to.x) > 0.05f) sprite.flipX = to.x < 0f;

        if (health != null && to.sqrMagnitude < reach * reach) health.TakeDamage(damage);
    }
}
