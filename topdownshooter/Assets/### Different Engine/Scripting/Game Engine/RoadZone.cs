using System.Collections.Generic;
using UnityEngine;

// the road keeps moving. the arena (its walls and the edge art) travels along the road at a steady
// pace, so the player has to keep moving through the world instead of holding one spot. it halts
// for a final rush (the roadblock) and for good once the final boss arrives. the ground is an
// endless tiling of the street tile, so there is always more road ahead.
// it was made for the old street map and only runs when placed in a scene by hand; it never moves
// a playfield (the courtyard and the maps after it are fixed arenas)
public class RoadZone : MonoBehaviour
{
    [Header("Travel")]
    [Tooltip("sideways by default: the screen is wider than it is tall, so more of the road ahead is visible")]
    [SerializeField] private Vector2 direction = Vector2.right;
    [Tooltip("world units per second")]
    [SerializeField, Min(0f)] private float speed = 0.5f;
    [SerializeField, Min(0f)] private float speedPerWave = 0.05f;
    [SerializeField, Min(0f)] private float maxSpeed = 1.2f;

    [Header("Carried with the arena")]
    [Tooltip("objects that travel with the walls. when empty, the Boundary object next to the ground is used")]
    [SerializeField] private Transform[] carry;

    [Header("Stragglers")]
    [Tooltip("enemies this far behind the rear wall are removed")]
    [SerializeField, Min(0f)] private float cullBehind = 4f;

    public static RoadZone Instance { get; private set; }

    public Vector2 Direction => direction.sqrMagnitude > 0f ? direction.normalized : Vector2.right;
    public float CurrentSpeed => Mathf.Min(maxSpeed, speed + speedPerWave * Mathf.Max(0, wave - 1));

    private SpawnDirector director;
    private Rigidbody2D body;
    private int wave = 1;
    private bool halted;
    private bool finished;
    private float nextCull;
    private readonly List<GameObject> stragglers = new List<GameObject>();

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnEnable()
    {
        GameEvents.OnWaveStarted += HandleWaveStarted;
        GameEvents.OnFinalRushStarted += HandleRushStarted;
        GameEvents.OnFinalRushEnded += HandleRushEnded;
        GameEvents.OnFinalBossStarted += HandleFinalBoss;
    }

    private void OnDisable()
    {
        GameEvents.OnWaveStarted -= HandleWaveStarted;
        GameEvents.OnFinalRushStarted -= HandleRushStarted;
        GameEvents.OnFinalRushEnded -= HandleRushEnded;
        GameEvents.OnFinalBossStarted -= HandleFinalBoss;
    }

    private void HandleWaveStarted(int w) => wave = w;

    // the final rush clamps the player inside a circle; if the rear wall kept coming it would
    // crush them against it, so the road stops for the roadblock
    private void HandleRushStarted(int w, int quota) => halted = true;
    private void HandleRushEnded(int w) => halted = false;

    private void HandleFinalBoss() => finished = true;

    private void Start()
    {
        if (Playfield.Active != null)
        {
            enabled = false;
            return;
        }

        director = FindFirstObjectByType<SpawnDirector>();
        var walls = director != null ? director.MapBounds : null;
        if (walls == null)
        {
            enabled = false;
            return;
        }

        Transform groundRoot = null;
        Vector3 centre = Vector3.zero;
        int count = 0;

        foreach (var wall in walls)
        {
            if (wall == null) continue;
            centre += wall.bounds.center;
            count++;
            if (groundRoot == null) groundRoot = wall.transform.parent;
        }

        if (count == 0)
        {
            enabled = false;
            return;
        }

        transform.position = centre / count;

        body = gameObject.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        // the walls become one moving compound collider on this body, so they push the player
        // and enemies along instead of teleporting through them
        foreach (var wall in walls)
            if (wall != null) wall.transform.SetParent(transform, true);

        if (carry == null || carry.Length == 0) carry = DefaultCarry(groundRoot);
        foreach (var t in carry)
            if (t != null) t.SetParent(transform, true);

        if (groundRoot != null) GroundTiler.Install(groundRoot);
    }

    private static Transform[] DefaultCarry(Transform groundRoot)
    {
        var parent = groundRoot != null ? groundRoot.parent : null;
        var boundary = parent != null ? parent.Find("Boundary") : null;
        return boundary != null ? new[] { boundary } : new Transform[0];
    }

    private void FixedUpdate()
    {
        if (body == null || halted || finished) return;
        body.MovePosition(body.position + Direction * CurrentSpeed * Time.fixedDeltaTime);
    }

    private void Update()
    {
        if (Time.time < nextCull) return;
        nextCull = Time.time + 1f;
        CullStragglers();
    }

    // an enemy that ends up behind the rear wall can never reach the player again, but it still
    // counts toward the spawner's alive total, so left alone they would slowly starve spawning
    private void CullStragglers()
    {
        if (director == null) return;

        Rect r = director.PlayRect;
        if (r.width > 1000f || r.height > 1000f) return;

        Vector2 dir = Direction;
        float halfDepth = Mathf.Abs(dir.x) * r.width * 0.5f + Mathf.Abs(dir.y) * r.height * 0.5f;
        float rear = Vector2.Dot(r.center, dir) - halfDepth - cullBehind;

        stragglers.Clear();
        foreach (var enemy in EnemyRegistry.All)
        {
            if (enemy == null) continue;
            if (Vector2.Dot(enemy.transform.position, dir) < rear) stragglers.Add(enemy);
        }

        foreach (var enemy in stragglers) Destroy(enemy);
    }
}
