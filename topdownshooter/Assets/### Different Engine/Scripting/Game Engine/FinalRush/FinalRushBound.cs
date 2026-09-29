using UnityEngine;

// a Final Rush boss held inside the ring of spirit seals, the way the player is
// (FinalRushPlayerClamp): walking, backing off or knocked back, he stops a little inside them.
// SpawnDirector puts it on each rush boss; it does nothing while there's no arena. the helpers
// are for placing and aiming things inside the ring (spawns, a charge's lane)
[RequireComponent(typeof(Rigidbody2D))]
public class FinalRushBound : MonoBehaviour
{
    // how far inside the player's own limit a boss is held: his body stays clear of the seals
    public const float Inset = 1.2f;

    private Rigidbody2D rb;

    private void Awake() => rb = GetComponent<Rigidbody2D>();

    private void FixedUpdate()
    {
        if (!TryArea(out Vector2 centre, out float radius)) return;
        Vector2 delta = rb.position - centre;
        float d = delta.magnitude;
        if (d <= radius || d < 0.0001f) return;
        Vector2 dir = delta / d;
        rb.position = centre + dir * radius;
        float outward = Vector2.Dot(rb.linearVelocity, dir);
        if (outward > 0f) rb.linearVelocity -= dir * outward;
    }

    // the room a boss has: the ring's centre and how far from it he may go
    public static bool TryArea(out Vector2 centre, out float radius)
    {
        var arena = FinalRushArenaController.Instance;
        if (arena == null || !arena.HasArena)
        {
            centre = default;
            radius = 0f;
            return false;
        }
        centre = arena.Center;
        radius = Mathf.Max(1f, arena.Radius - Inset);
        return true;
    }

    // how far from `from` along `dir` before the edge of that room; infinite without an arena
    public static float ToEdge(Vector2 from, Vector2 dir)
    {
        if (!TryArea(out Vector2 c, out float r)) return float.PositiveInfinity;
        Vector2 m = from - c;
        float b = Vector2.Dot(m, dir), q = m.sqrMagnitude - r * r;
        float disc = b * b - q;
        return disc < 0f ? 0f : Mathf.Max(0f, -b + Mathf.Sqrt(disc));
    }

    public static bool Inside(Vector2 p) => !TryArea(out Vector2 c, out float r) || (p - c).sqrMagnitude <= r * r;
}
