using UnityEngine;

// a normal run's time limit, like Vampire Survivors' Reaper: a normal run lasts 30 minutes. the final
// boss comes after the sixth 3 minute wave, by 25 minutes at the latest (GameLoopController); beaten
// or not, when the clock reaches the limit the horde is swept away and the Wuchang come for the
// player, the white one first, then one more every minute, white and black in turn. they can't be
// hurt, and the first to reach the player takes them (SoulTaking): the run is over, survived.
// endless runs have no limit. GameLoopController adds it; the Wuchang prefabs come from the VfxLibrary
public class RunTimeLimit : MonoBehaviour
{
    [Tooltip("on the run clock, which counts the real time played. the final boss comes by 25:00; this is how long it gets")]
    [Min(1f)] public float limitMinutes = 30f;
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
        // the run is decided: the sweep's kills don't open level ups
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null && p.TryGetComponent(out PlayerInventory inventory)) inventory.LockLeveling();
        Juice.Shake(0.6f);
        Send();
    }

    private void Send()
    {
        if (SoulTaking.Running) return;
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
