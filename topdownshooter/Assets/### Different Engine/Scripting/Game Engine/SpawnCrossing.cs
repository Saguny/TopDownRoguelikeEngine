using UnityEngine;

// an enemy from a swarm or stampede, on its way straight across the screen instead of chasing.
// it leaves quietly (no kill, no drop) once it's out the far side. if something stops it getting
// through, like a wall or the edge of the arena, it gives up and chases the player like the rest
public class SpawnCrossing : MonoBehaviour
{
    // crossers don't count toward the timeline's crowd, or a big swarm would hold back the refill
    public static int Live { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Live = 0;

    private Vector2 heading;
    private Camera cam;
    private EnemyMovement movement;
    private Rigidbody2D body;
    private float giveUpAt;
    private float stuckFor;
    private bool counted;

    public static void Send(GameObject enemy, Vector2 heading, float speedMul, Camera cam, float seconds)
    {
        if (!enemy.TryGetComponent(out EnemyMovement movement)) return;
        movement.SetHeading(heading, speedMul);

        var crossing = enemy.AddComponent<SpawnCrossing>();
        crossing.heading = heading.normalized;
        crossing.cam = cam;
        crossing.movement = movement;
        crossing.body = enemy.GetComponent<Rigidbody2D>();
        crossing.giveUpAt = Time.time + seconds;
        crossing.counted = true;
        Live++;
    }

    private void Update()
    {
        if (cam == null || movement == null || Time.time >= giveUpAt)
        {
            GiveUp();
            return;
        }

        // gone once it's this far past the edge of the screen on the side it was heading for
        Vector2 fromCentre = (Vector2)transform.position - (Vector2)cam.transform.position;
        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        float reach = Mathf.Abs(heading.x) * halfW + Mathf.Abs(heading.y) * halfH;
        if (Vector2.Dot(fromCentre, heading) > reach + 2f)
        {
            ObjectPool.Recycle(gameObject);
            return;
        }

        // blocked: barely moving along its heading for a while
        float along = body != null ? Vector2.Dot(body.linearVelocity, heading) : 1f;
        stuckFor = along < 0.25f ? stuckFor + Time.deltaTime : 0f;
        if (stuckFor > 1.2f) GiveUp();
    }

    private void GiveUp()
    {
        if (movement != null) movement.ClearHeading();
        Destroy(this);
    }

    // a crosser that dies or leaves goes back to the pool; the next life isn't a crosser
    private void OnDisable()
    {
        if (movement != null) movement.ClearHeading();
        Destroy(this);
    }

    private void OnDestroy()
    {
        if (counted) Live--;
        counted = false;
    }
}
