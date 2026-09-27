using UnityEngine;

// the root of a pooled one-shot effect (an arrow's hit, a meteor's blast): its Flipbooks play from
// the top each time it comes out of the pool, and after `lifetime` it goes back. Play() is the way
// to show one
public class FxOneShot : MonoBehaviour
{
    [Min(0.02f)] public float lifetime = 0.5f;

    private float age;

    private void OnEnable() => age = 0f;

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= lifetime) ObjectPool.Recycle(gameObject);
    }

    // shows the effect at a point, turned by `angle` degrees and scaled. prefabs without an
    // FxOneShot are only instantiated and left to look after themselves
    public static GameObject Play(GameObject prefab, Vector3 at, float angle = 0f, float scale = 1f)
    {
        if (prefab == null) return null;
        var go = ObjectPool.For(prefab).Get(at, Quaternion.Euler(0f, 0f, angle));
        go.transform.localScale = Vector3.one * scale;
        return go;
    }

    // the angle, in degrees, of a direction
    public static float Angle(Vector2 dir) => Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
}
