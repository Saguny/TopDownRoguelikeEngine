using System.Collections.Generic;
using UnityEngine;

public class ObjectPool : MonoBehaviour
{
    [SerializeField] private GameObject prefab;
    [SerializeField] private int warmup = 32;

    private readonly Queue<GameObject> q = new();
    private static readonly Dictionary<GameObject, ObjectPool> pools = new();

    public GameObject Prefab => prefab;

    // statics survive play sessions when domain reload is off, so wipe the registry explicitly
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => pools.Clear();

    private void Awake()
    {
        if (prefab == null) return;
        pools[prefab] = this;
        Prewarm(warmup);
    }

    private void OnDestroy()
    {
        if (prefab != null && pools.TryGetValue(prefab, out var existing) && existing == this)
            pools.Remove(prefab);
    }

    // gets the pool for a prefab, spinning one up on demand so nothing has to be wired in the scene
    public static ObjectPool For(GameObject prefab)
    {
        if (prefab == null) return null;
        if (pools.TryGetValue(prefab, out var pool) && pool != null) return pool;

        var host = new GameObject($"Pool [{prefab.name}]");
        pool = host.AddComponent<ObjectPool>();
        pool.prefab = prefab;
        pools[prefab] = pool;
        return pool;
    }

    // sends an instance back to whichever pool handed it out, or destroys it if it was never pooled
    public static void Recycle(GameObject go)
    {
        if (go == null) return;

        if (go.TryGetComponent(out PooledObject tag) && tag.Owner != null)
            tag.Owner.Release(go);
        else
            Destroy(go);
    }

    public void Prewarm(int count)
    {
        if (prefab == null) return;

        for (int i = q.Count; i < count; i++)
        {
            var go = Instantiate(prefab, transform);
            go.SetActive(false);
            var tag = Tag(go);
            tag.InPool = true;
            q.Enqueue(go);
        }
    }

    public GameObject Get(Vector3 pos, Quaternion rot)
    {
        GameObject go = null;
        while (go == null && q.Count > 0) go = q.Dequeue();  // skip anything destroyed behind our back

        if (go == null) go = Instantiate(prefab, transform);

        var tag = Tag(go);
        tag.InPool = false;

        go.transform.SetPositionAndRotation(pos, rot);
        go.SetActive(true);
        return go;
    }

    public void Release(GameObject go)
    {
        if (go == null) return;

        var tag = Tag(go);
        if (tag.InPool) return;  // already parked, never enqueue twice
        tag.InPool = true;

        go.SetActive(false);
        go.transform.SetParent(transform, false);
        q.Enqueue(go);
    }

    private PooledObject Tag(GameObject go)
    {
        if (!go.TryGetComponent(out PooledObject tag))
            tag = go.AddComponent<PooledObject>();

        tag.Owner = this;
        return tag;
    }
}
