using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
public class MagnetArea : MonoBehaviour
{
    [SerializeField] private float baseRadius = 1.5f;

    private CircleCollider2D cc;
    private StatContext stats;

    private void Awake()
    {
        cc = GetComponent<CircleCollider2D>();

        // the magnet needs a circle collider of its own, on a child. next to a Rigidbody2D that
        // circle is the body, and turning it into a trigger stops every contact hit from landing
        if (!cc.isTrigger && TryGetComponent<Rigidbody2D>(out _))
        {
            Debug.LogError($"MagnetArea on '{name}' would turn its body collider into a trigger. Put it on a child object with its own CircleCollider2D.", this);
            enabled = false;
            return;
        }

        cc.isTrigger = true;
        stats = GetComponentInParent<StatContext>();
    }

    // the magnet's reach in world units, for PickupSystem
    public float Radius => (cc != null ? cc.radius : baseRadius) * Mathf.Abs(transform.lossyScale.x);

    private void Update()
    {
        float mul = stats ? stats.PickupRadiusTotal : 1f;
        cc.radius = baseRadius * mul;
    }

    // no trigger callback: wen is pulled by PickupSystem reading Radius, and heals were never
    // magnetised (they wait to be walked over)
}
