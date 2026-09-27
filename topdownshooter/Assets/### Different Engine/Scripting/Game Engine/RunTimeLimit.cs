using UnityEngine;

// a normal run's time limit, like Vampire Survivors' Reaper: the final boss comes at 45 minutes, and
// if it's still standing when the clock reaches the limit, the horde is swept away and the Wuchang
// come for the player, the white one first, then one
// more every minute, white and black in turn. they can't be hurt and one touch kills. endless
// runs have no limit. GameLoopController adds it; the Wuchang prefabs come from the VfxLibrary
public class RunTimeLimit : MonoBehaviour
{
    [Tooltip("on the run clock. the final boss comes at 45:00; this is how long it gets")]
    [Min(1f)] public float limitMinutes = 55f;
    [Tooltip("another one comes every this many seconds after the first")]
    [Min(5f)] public float every = 60f;
    [Tooltip("how far from the player they appear")]
    [Min(1f)] public float appearDistance = 13f;

    private float runTime;
    private float nextAt = -1f;
    private int sent;

    public bool TimeIsUp => sent > 0;

    private void OnEnable() => GameEvents.OnRunTimeChanged += OnRunTime;
    private void OnDisable() => GameEvents.OnRunTimeChanged -= OnRunTime;

    private void OnRunTime(float seconds)
    {
        runTime = seconds;
        if (GameMode.IsEndless) return;
        if (sent == 0 && runTime >= limitMinutes * 60f) TimeUp();
        else if (sent > 0 && runTime >= nextAt) Send();
    }

    private void TimeUp()
    {
        var director = FindFirstObjectByType<SpawnDirector>();
        if (director != null) director.EndOfTime();
        Juice.Shake(0.6f);
        Send();
    }

    private void Send()
    {
        nextAt = runTime + every;
        var lib = VfxLibrary.Get;
        var prefab = lib == null ? null : sent % 2 == 0 ? lib.wuchangBai : lib.wuchangHei != null ? lib.wuchangHei : lib.wuchangBai;
        sent++;

        var player = GameObject.FindGameObjectWithTag("Player");
        if (prefab == null || player == null) return;

        // off screen, from a random side
        float a = Random.value * Mathf.PI * 2f;
        Vector3 at = player.transform.position + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * appearDistance;
        at.z = prefab.transform.position.z;
        Instantiate(prefab, at, Quaternion.identity);
    }

    // dev tools: the limit reached now, whatever the clock says
    public void TimeUpNow()
    {
        if (sent == 0) TimeUp();
    }
}
