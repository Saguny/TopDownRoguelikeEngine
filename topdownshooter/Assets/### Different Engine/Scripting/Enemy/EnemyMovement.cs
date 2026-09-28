using UnityEngine;

// one enemy's walk: toward the player (or along a crossing heading), round the boss and round
// obstacles, kept a little apart from its neighbours. it has no FixedUpdate of its own: EnemySwarm
// steps every enemy in one loop, which is what lets a couple of thousand of them move at once
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerAwareness))]
public class EnemyMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float _speed = 3f;
    private float _baseSpeed;

    [Header("Boss Avoidance")]
    [SerializeField] private float _bossAvoidRadius = 2.5f;
    [SerializeField, Range(0f, 3f)] private float _bossAvoidWeight = 1.5f;

    [Header("Obstacle Avoidance")]
    [Tooltip("unused since obstacles come from the swarm's map of the arena; kept so prefabs keep their value")]
    [SerializeField] private LayerMask _obstacleMask;
    [SerializeField] private float _avoidanceRadius = 0.5f;
    [Tooltip("how far ahead an enemy looks for a wall or prop to step round")]
    [SerializeField] private float _avoidanceDistance = 2f;
    [SerializeField, Range(0f, 3f)] private float _avoidanceWeight = 1.5f;
    [SerializeField, Range(0f, 1f)] private float _directionSmoothFactor = 0.2f;

    [Header("Visuals")]
    [SerializeField] private bool _spriteFacesLeftByDefault = true;
    // toggle this: true if your art faces left, false if it faces right

    private Rigidbody2D _rigidbody;
    private Animator _animator;
    private SpriteRenderer _sr;

    private Vector2 _smoothedDirection = Vector2.zero;
    private float _avoidanceSide = 0f;
    private float _radius = -1f;
    private bool _running, _runningKnown;
    private bool _facingRight, _facingKnown;

    internal int swarmIndex = -1;
    internal SpriteRenderer Sprite => _sr;

    // its shadow on the floor, sized from its sprite the first time the swarm draws it
    internal bool shadowKnown, castsShadow;
    internal float shadowWidth, feetOffset;
    internal Rigidbody2D Body => _rigidbody;

    private static readonly int HashIsRunning = Animator.StringToHash("IsRunning");

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
        _animator = GetComponent<Animator>();
        if (_animator != null) _animator.enabled = true;

        _sr = GetComponentInChildren<SpriteRenderer>();
        _baseSpeed = _speed;

        // initial flip fix
        if (_sr != null)
            _sr.flipX = _spriteFacesLeftByDefault;
    }

    // back from the pool: no heading, slow or momentum left over from the last life
    private void OnEnable()
    {
        ClearHeading();
        _slowFactor = 1f;
        _slowUntil = 0f;
        _shove = Vector2.zero;
        _shoveUntil = 0f;
        _knock = Vector2.zero;
        _knockUntil = _nextKnock = 0f;
        _driveUntil = 0f;
        _hasteUntil = 0f;
        _haste = 1f;
        _smoothedDirection = Vector2.zero;
        _avoidanceSide = 0f;
        _radius = -1f;
        _runningKnown = _facingKnown = false;
        if (_rigidbody != null) _rigidbody.linearVelocity = Vector2.zero;
        shadowKnown = false;
        EnemySwarm.Join(this);
    }

    private void OnDisable() => EnemySwarm.Leave(this);

    // how much room this enemy takes, for keeping neighbours apart. read once per life, after
    // the spawner has had its say (an elite is bigger)
    internal float Radius
    {
        get
        {
            if (_radius < 0f)
            {
                _radius = TryGetComponent(out CircleCollider2D c) ? c.radius * Mathf.Abs(transform.lossyScale.x) : _avoidanceRadius;
                _radius = Mathf.Max(0.1f, _radius);
            }
            return _radius;
        }
    }

    // one physics step, run by EnemySwarm. push is the nudge away from crowding neighbours
    internal void Step(Vector2 position, Vector2 player, Vector2 push, EnemySwarm.ObstacleMap obstacles, float now)
    {
        Vector2 direction;
        if (HasHeading)
        {
            direction = _heading;
        }
        else
        {
            direction = player - position;
            float sq = direction.sqrMagnitude;
            direction = sq > 0.0001f ? direction / Mathf.Sqrt(sq) : Vector2.zero;
            direction = ApplyBossAvoidance(direction, position);
        }

        if (obstacles != null) direction = obstacles.Steer(position, direction, _avoidanceDistance, ref _avoidanceSide);
        UpdateSmoothedDirection(direction);

        _lastPlayer = player;
        // rooted (a spider lily): it never moves, whatever pushes it
        if (Rooted)
        {
            _rigidbody.linearVelocity = Vector2.zero;
            return;
        }
        Vector2 velocity = Vector2.zero;
        float slowNow = now < _slowUntil ? _slowFactor : 1f;
        // knocked back: its own walk stops for the moment while the hit carries it away
        if (now < _knockUntil) velocity = _knock;
        // driven by its own behaviour (a dash, a charge, keeping its distance): that instead of the walk
        else if (now < _driveUntil)
        {
            velocity = _drive * slowNow;
            if (_driveGhost) push = Vector2.zero;
        }
        else if (_smoothedDirection.sqrMagnitude >= 0.001f)
        {
            float haste = now < _hasteUntil ? _haste : 1f;
            velocity = _smoothedDirection.normalized * (_speed * slowNow * haste * _headingSpeedMul);
        }
        velocity += push;
        if (now < _shoveUntil) velocity += _shove;
        _rigidbody.linearVelocity = velocity;

        // only touch the renderer and animator when something actually changes
        if (_sr != null && Mathf.Abs(_smoothedDirection.x) >= 0.01f)
        {
            bool right = _smoothedDirection.x > 0f;
            if (!_facingKnown || right != _facingRight)
            {
                _facingRight = right;
                _facingKnown = true;
                _sr.flipX = _spriteFacesLeftByDefault ? right : !right;
            }
        }

        bool running = velocity.sqrMagnitude > 0.01f;
        if ((!_runningKnown || running != _running) && _animator != null && _animator.isActiveAndEnabled)
        {
            _running = running;
            _runningKnown = true;
            _animator.SetBool(HashIsRunning, running);
        }
    }

    private Vector2 ApplyBossAvoidance(Vector2 toPlayerDir, Vector2 enemyPos)
    {
        // whichever boss is up right now; no scene search per enemy
        var boss = BossMarker.Current;
        if (boss == null || toPlayerDir.sqrMagnitude < 0.0001f)
            return toPlayerDir;

        Vector2 toBoss = (Vector2)boss.position - enemyPos;
        float distToBoss = toBoss.magnitude;
        if (distToBoss > _bossAvoidRadius || distToBoss <= Mathf.Epsilon)
            return toPlayerDir;

        Vector2 aroundDir = Vector2.Perpendicular(toBoss).normalized;
        if (Vector2.Dot(aroundDir, toPlayerDir) < 0f)
            aroundDir = -aroundDir;

        float t = 1f - Mathf.Clamp01(distToBoss / _bossAvoidRadius);
        Vector2 blended = toPlayerDir * (1f - t) + aroundDir * (t * _bossAvoidWeight);
        return blended.sqrMagnitude < 0.0001f ? toPlayerDir : blended.normalized;
    }

    private void UpdateSmoothedDirection(Vector2 targetDirection)
    {
        if (targetDirection.sqrMagnitude > 0.0001f)
        {
            Vector2 targetNorm = targetDirection.normalized;
            _smoothedDirection = _smoothedDirection.sqrMagnitude < 0.0001f
                ? targetNorm
                : Vector2.Lerp(_smoothedDirection, targetNorm, _directionSmoothFactor);
        }
        else
        {
            _smoothedDirection = Vector2.zero;
        }
    }

    // a fixed direction from the spawner (a swarm crossing the screen) instead of chasing the
    // player. obstacles are still walked around
    private Vector2 _heading;
    private float _headingSpeedMul = 1f;

    public bool HasHeading => _heading.sqrMagnitude > 0f;

    public void SetHeading(Vector2 heading, float speedMul = 1f)
    {
        _heading = heading.normalized;
        _headingSpeedMul = Mathf.Max(0.1f, speedMul);
        _smoothedDirection = _heading;
    }

    public void ClearHeading()
    {
        _heading = Vector2.zero;
        _headingSpeedMul = 1f;
    }

    // a temporary slow from a weapon. the strongest one running wins, and it wears off by itself
    private float _slowFactor = 1f;
    private float _slowUntil;

    private Vector2 _shove;
    private float _shoveUntil;

    // an outside force for a moment, on top of its own walk: a dragon's coils pushing it back, a
    // tornado pulling it in. it still moves under a freeze. a stronger shove replaces a weaker one
    public void Shove(Vector2 velocity, float seconds)
    {
        if (Time.time >= _shoveUntil || velocity.sqrMagnitude >= _shove.sqrMagnitude)
        {
            _shove = velocity;
            _shoveUntil = Time.time + Mathf.Max(0f, seconds);
        }
    }

    // knockback, the way Vampire Survivors does it: a hit that doesn't kill pushes the enemy a
    // little way back from the player, its own walk stopped for the moment. how much it takes is
    // its kind's (EnemyArchetype's Knockback Resist, set by the spawner: a boss takes none); after
    // a knock it can't be knocked again for a beat, so a fast weapon slows the horde, not pins it
    public const float KnockSpeed = 4.5f;      // units a second at full strength
    public const float KnockSeconds = 0.1f;
    public const float KnockRest = 0.25f;

    [Tooltip("share of every knockback it shrugs off: 0 is pushed the full way, 1 not at all. the spawner sets it from the enemy's archetype")]
    [Range(0f, 1f)] public float knockbackResist;
    private Vector2 _knock, _lastPlayer;
    private float _knockUntil, _nextKnock;

    public void Knock(float strength)
    {
        if (Rooted) return;
        float k = strength * (1f - knockbackResist);
        float now = Time.time;
        if (k <= 0.01f || now < _nextKnock) return;
        Vector2 away = (Vector2)transform.position - _lastPlayer;
        away = away.sqrMagnitude > 0.0001f ? away.normalized : UnityEngine.Random.insideUnitCircle.normalized;
        _knock = away * (KnockSpeed * k);
        _knockUntil = now + KnockSeconds;
        _nextKnock = now + KnockRest;
    }

    // ---- for enemies with a mind of their own (Huangquan Road's): rooted, driven, hasted

    // never moves: no walk, no push, no knockback
    public bool Rooted { get; set; }

    // its walking speed right now (the run's curve and its kind's), for behaviours that move it themselves
    public float Speed => _speed;

    private Vector2 _drive;
    private float _driveUntil;
    private bool _driveGhost;

    // moves at `velocity` for `seconds` instead of walking to the player. ghost: through the crowd,
    // not pushed apart from it (a paper servant slicing through). a zero velocity holds it still
    public void Drive(Vector2 velocity, float seconds, bool ghost = false)
    {
        _drive = velocity;
        _driveUntil = Time.time + Mathf.Max(0f, seconds);
        _driveGhost = ghost;
    }

    public bool Driven => Time.time < _driveUntil;
    public void StopDrive() => _driveUntil = 0f;

    private float _haste = 1f, _hasteUntil;

    // walks faster for a moment (a soul lantern's aura); the strongest running wins
    public void Haste(float factor, float seconds)
    {
        if (Time.time >= _hasteUntil || factor > _haste) _haste = factor;
        _hasteUntil = Mathf.Max(_hasteUntil, Time.time + seconds);
    }

    public void ApplySlow(float factor, float seconds)
    {
        factor = Mathf.Clamp01(factor);
        if (Time.time >= _slowUntil || factor < _slowFactor) _slowFactor = factor;
        _slowUntil = Mathf.Max(_slowUntil, Time.time + seconds);
    }

    public void SetSpeedMultiplier(float mult)
    {
        _speed = _baseSpeed * mult;
    }
}
