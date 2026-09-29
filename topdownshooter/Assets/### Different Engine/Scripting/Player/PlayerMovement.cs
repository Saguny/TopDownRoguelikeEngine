using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0f)] private float _speed = 6f;
    [SerializeField, Range(0f, 0.5f)] private float _accelTime = 0.08f; // smoothing time

    [Header("Visuals")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private Animator _animator;
    [Tooltip("which way the character art faces when not flipped. the Qing warrior faces right; the old robot faced left")]
    [SerializeField] private bool _artFacesRight = true;

    private Rigidbody2D _rb;
    private StatContext _stats;
    private Vector2 _moveInput;                 // raw input from WASD/Stick
    private Vector2 _velocitySmoothRef;         // ref for SmoothDamp
    private Vector3 _originalScale;
    private Vector3 _originalFirePointLocalPos;
    private int _facingSign = -1;                // 1 = right, -1 = left
    private Vector2 _lastPosition;              // for the run's distance walked

    // something carrying the player along whatever they do, units a second (Meng Po's river of
    // forgetting): added to their own movement
    public static Vector2 Drift;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Drift = Vector2.zero;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _stats = GetComponent<StatContext>();
        _rb.gravityScale = 0f;
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        _rb.freezeRotation = true;
        _lastPosition = _rb.position;

        _originalScale = transform.localScale;

        if (firePoint != null)
            _originalFirePointLocalPos = firePoint.localPosition;
    }

    private void FixedUpdate()
    {
        // the run's distance walked: how far the body really got, walls and all. more than a
        // unit in one step is a teleport, not walking
        Vector2 at = _rb.position;
        float moved = (at - _lastPosition).magnitude;
        if (moved < 1f) RunStats.Walked(moved);
        _lastPosition = at;

        // normalize so diagonals aren't faster
        Vector2 dir = _moveInput.sqrMagnitude > 1f ? _moveInput.normalized : _moveInput;

        // target velocity and smooth acceleration/deceleration
        Vector2 desiredVel = dir * (_speed * (_stats ? _stats.MoveSpeedTotal : 1f)) + Drift;
        _rb.linearVelocity = Vector2.SmoothDamp(_rb.linearVelocity, desiredVel, ref _velocitySmoothRef, _accelTime);

        // flip from velocity when there's horizontal motion (their own, not a drift carrying them)
        float ownX = _rb.linearVelocity.x - Drift.x;
        if (Mathf.Abs(ownX) > 0.001f)
        {
            // mirror the art only when moving against the way it's drawn
            _facingSign = (ownX > 0) == _artFacesRight ? 1 : -1;
            transform.localScale = new Vector3(Mathf.Abs(_originalScale.x) * _facingSign, _originalScale.y, _originalScale.z);

            if (firePoint != null)
            {
                Vector3 lp = _originalFirePointLocalPos;
                lp.x *= _facingSign;
                firePoint.localPosition = lp;
            }
        }
    }

    // new input system callback (Player Input component → Actions: Move)
    private void OnMove(InputValue value)
    {
        _moveInput = value.Get<Vector2>();

        if (_animator != null)
        {
            bool isMoving = _moveInput.sqrMagnitude > 0.001f;
            _animator.SetBool("IsRunning", isMoving);
        }
    }
}
