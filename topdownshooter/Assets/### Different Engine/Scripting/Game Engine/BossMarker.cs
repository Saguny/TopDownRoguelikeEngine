using UnityEngine;

public class BossMarker : MonoBehaviour
{
    private BossIndicatorManager manager;

    // the bosses up right now, newest last, so enemies can steer round one without searching
    private static readonly System.Collections.Generic.List<BossMarker> active = new System.Collections.Generic.List<BossMarker>();
    public static Transform Current => active.Count > 0 ? active[active.Count - 1].transform : null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => active.Clear();

    private void OnEnable() => active.Add(this);

    private void Awake()
    {
        manager = FindFirstObjectByType<BossIndicatorManager>();
    }

    private void Start()
    {
        if (manager != null)
        {
            manager.RegisterBoss(transform);
        }
    }  

    private void OnDisable()
    {
        active.Remove(this);
        if (manager != null)
        {
            manager.UnregisterBoss(transform);
        }
    }
}
