using UnityEngine;

// one of the Wuchang that end a normal run (RunTimeLimit): it drifts straight at the player through
// walls and everything else, faster than they can run however fast they've got, and the first to get
// within its chain's reach takes them (SoulTaking): the run ends there, survived. it isn't an enemy,
// so no weapon aims at it or can hurt it, and a Revival doesn't save anyone from it
public class Wuchang : MonoBehaviour
{
    [Tooltip("world units a second at the player's base Move Speed (they run at 6); it keeps pace with Move Speed bonuses")]
    [Min(0.1f)] public float speed = 8.4f;
    [Tooltip("how close it comes before it throws its chain")]
    [Min(0.1f)] public float reach = 3.2f;

    private Transform player;
    private StatContext stats;
    private SpriteRenderer sprite;

    private void Start()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            player = p.transform;
            stats = p.GetComponent<StatContext>();
        }
        sprite = GetComponentInChildren<SpriteRenderer>();
    }

    private void Update()
    {
        if (player == null) return;
        Vector2 me = transform.position, to = (Vector2)player.position - me;
        if (sprite != null && Mathf.Abs(to.x) > 0.05f && !SoulTaking.Running) sprite.flipX = to.x < 0f;
        // one of them is taking the player: the others stand and watch
        if (SoulTaking.Running) return;
        if (to.sqrMagnitude < reach * reach)
        {
            SoulTaking.Begin(this, player);
            return;
        }
        float step = speed * (stats != null ? Mathf.Max(1f, stats.MoveSpeedTotal) : 1f) * Time.deltaTime;
        transform.position += (Vector3)(to.sqrMagnitude > step * step ? to.normalized * step : to);
    }
}
