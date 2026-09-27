using UnityEngine;
using System.Collections.Generic;

public class AutoAimService : MonoBehaviour
{
    [SerializeField] private float searchRadius = 12f;
    [SerializeField] private LayerMask enemyMask;

    private readonly List<Collider2D> buf = new List<Collider2D>(64);
    private readonly List<(Transform t, float d)> sorted = new List<(Transform, float)>(64);
    private readonly List<Transform> results = new List<Transform>(16);

    private ContactFilter2D filter;
    private bool filterReady;

    // built once and reused; rebuilding per query would undo the point of the shared buffers
    private ContactFilter2D Filter()
    {
        if (filterReady) return filter;

        filter = new ContactFilter2D();
        filter.SetLayerMask(enemyMask);
        filter.useTriggers = Physics2D.queriesHitTriggers;
        filterReady = true;
        return filter;
    }

    public Transform FindTarget(Vector2 origin)
    {
        Physics2D.OverlapCircle(origin, searchRadius, Filter(), buf);

        float best = float.MaxValue;
        Transform t = null;
        for (int i = 0; i < buf.Count; i++)
        {
            var c = buf[i];
            if (!c) continue;

            float d = (c.transform.position - (Vector3)origin).sqrMagnitude;
            if (d < best) { best = d; t = c.transform; }
        }
        return t;
    }

    // note: the returned list is reused between calls, so consume it before querying again
    public List<Transform> FindTargets(Vector2 origin, int maxCount)
    {
        Physics2D.OverlapCircle(origin, searchRadius, Filter(), buf);

        sorted.Clear();
        for (int i = 0; i < buf.Count; i++)
        {
            var c = buf[i];
            if (!c) continue;

            float d = (c.transform.position - (Vector3)origin).sqrMagnitude;
            sorted.Add((c.transform, d));
        }
        sorted.Sort((a, b) => a.d.CompareTo(b.d));

        results.Clear();
        for (int i = 0; i < sorted.Count && results.Count < maxCount; i++)
            results.Add(sorted[i].t);

        return results;
    }
}
