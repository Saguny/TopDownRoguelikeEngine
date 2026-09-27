#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using Debug = UnityEngine.Debug;

// a repeatable bullet hell worst case, to measure what a horde costs: 2000 enemies spawned at once,
// all killed in one frame, spawned again from the pools, the spawner's refill, then 7000 wen
// dropped and all picked up in one go. the spawner is paused while it runs so each step is measured
// on its own, and the steady states say where the frame goes (physics, scripts, animation,
// rendering). results go to the console and to Last. dev tools (F1) has a button for it; Tools >
// Performance runs it from the editor
public class StressTest : MonoBehaviour
{
    public const int Enemies = 2000;
    public const int Wen = 7000;

    public static string Last { get; private set; } = string.Empty;
    public static bool Running { get; private set; }

    public static void Begin(System.Action done = null)
    {
        if (Running) return;
        var host = new GameObject("StressTest");
        var test = host.AddComponent<StressTest>();
        test.StartCoroutine(test.Run(done));
    }

#if UNITY_EDITOR
    public const string PendingKey = "StressTest.Pending";

    // started from the editor menu: play mode begins, the game settles for a moment, the test
    // runs, and play mode ends again
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RunIfPending()
    {
        if (!UnityEditor.SessionState.GetBool(PendingKey, false)) return;
        UnityEditor.SessionState.EraseBool(PendingKey);
        var host = new GameObject("StressTest (waiting)");
        host.AddComponent<StressTest>().StartCoroutine(AfterSettling(host));
    }

    private static IEnumerator AfterSettling(GameObject host)
    {
        if (FindFirstObjectByType<SpawnDirector>() == null)
        {
            DontDestroyOnLoad(host);
            UnityEngine.SceneManagement.SceneManager.LoadScene("Game");
            for (float waited = 0f; FindFirstObjectByType<SpawnDirector>() == null && waited < 15f; waited += Time.unscaledDeltaTime)
                yield return null;
        }
        yield return new WaitForSecondsRealtime(3f);
        Destroy(host);
        Begin(() => UnityEditor.EditorApplication.isPlaying = false);
    }
#endif

    // where the frame goes: the player loop's big stages, read off the profiler
    private static readonly (string marker, string label)[] Stages =
    {
        ("FixedUpdate.Physics2DFixedUpdate", "physics"),
        ("FixedUpdate.ScriptRunBehaviourFixedUpdate", "fixed scripts"),
        ("Update.ScriptRunBehaviourUpdate", "scripts"),
        ("PreLateUpdate.ScriptRunBehaviourLateUpdate", "late scripts"),
        ("PreLateUpdate.DirectorUpdateAnimationBegin", "animation"),
        ("PreLateUpdate.DirectorUpdateAnimationEnd", "animation end"),
        ("PostLateUpdate.UpdateAllRenderers", "renderers"),
        ("PostLateUpdate.FinishFrameRendering", "rendering"),
    };

    private readonly StringBuilder log = new StringBuilder();
    private readonly List<(string label, ProfilerRecorder rec)> recorders = new List<(string, ProfilerRecorder)>();
    private readonly List<Behaviour> heldFire = new List<Behaviour>();
    private int deaths;

    private void CountDeath(int n) => deaths += n;

    private IEnumerator Run(System.Action done)
    {
        Running = true;
        log.Clear();
        var director = FindFirstObjectByType<SpawnDirector>();
        var inventory = FindFirstObjectByType<PlayerInventory>();
        var health = inventory != null ? inventory.GetComponent<PlayerHealth>() : null;
        var cam = Camera.main;
        if (director == null || inventory == null || cam == null)
        {
            Finish("no game scene to test in", done);
            yield break;
        }

        var prefabs = director.Timeline != null
            ? director.Timeline.beats.SelectMany(b => b.enemies).Where(p => p?.archetype?.prefab != null).Select(p => p.archetype.prefab).Distinct().ToList()
            : new List<GameObject>();
        if (prefabs.Count == 0)
        {
            Finish("no enemy prefabs in the spawn timeline", done);
            yield break;
        }
        var wenPrefab = prefabs.Select(p => p.TryGetComponent(out EnemyHealth h) ? h.WenDropPrefab : null).FirstOrDefault(p => p != null);

        StartRecorders();

        // keep the run from dying, levelling up or paying real coins while it's measured
        int coinsBefore = Coins.Balance;
        int wenForUpgrade = inventory.wenForUpgrade;
        inventory.wenForUpgrade = int.MaxValue / 2;
        if (health != null) health.godMode = true;
        HoldFire(inventory.gameObject);
        GameEvents.OnEnemyKilled += CountDeath;
        director.enabled = false;
        KillAll(DamageKind.Silent);
        PickupSystem.ClearAll();
        yield return Frames(20);

        var sw = new Stopwatch();
        Vector2 at = inventory.transform.position;
        float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;

        yield return Measure("idle, nothing spawned", 60);

        // ---- a horde in one frame, cold (most of it has to be made)
        sw.Restart();
        SpawnWave(prefabs, at, halfW, halfH);
        sw.Stop();
        log.AppendLine($"spawn {Enemies} enemies in one frame (cold): {sw.Elapsed.TotalMilliseconds:0} ms of script");
        yield return Measure("the frames after the spawn", 5);
        yield return Measure($"{Enemies} enemies alive (steady)", 120, true);
#if UNITY_EDITOR
        ScreenCapture.CaptureScreenshot(Shot("stress_horde.png"));
        yield return Frames(3);
#endif

        // ---- all of them killed in one frame
        int count = EnemyRegistry.Count;
        sw.Restart();
        KillAll(DamageKind.Weapon);
        sw.Stop();
        log.AppendLine($"kill {count} in one frame: {sw.Elapsed.TotalMilliseconds:0} ms of script");
        yield return Measure("the frames after the mass kill", 10);
        PickupSystem.ClearAll();
        yield return Frames(10);

        // ---- the same horde again, now from the pools
        sw.Restart();
        SpawnWave(prefabs, at, halfW, halfH);
        sw.Stop();
        log.AppendLine($"respawn {Enemies} from the pools: {sw.Elapsed.TotalMilliseconds:0} ms of script");
        yield return Measure("the frames after the respawn", 5);
        KillAll(DamageKind.Silent);
        PickupSystem.ClearAll();
        yield return Frames(10);

        // ---- the spawner topping the crowd back up
        director.enabled = true;
        yield return Measure("spawner refilling after it", 120);
        director.enabled = false;
        KillAll(DamageKind.Silent);
        PickupSystem.ClearAll();
        yield return Frames(20);

        // ---- a carpet of wen, then all of it picked up at once
        if (wenPrefab != null)
        {
            sw.Restart();
            for (int i = 0; i < Wen; i++) PickupSystem.Drop(wenPrefab, at + Random.insideUnitCircle * 22f);
            sw.Stop();
            log.AppendLine($"drop {Wen} wen in one frame: {sw.Elapsed.TotalMilliseconds:0} ms of script");
            log.AppendLine($"    {PickupSystem.Count} wen on the ground");
            yield return Measure($"{Wen} wen lying around (steady)", 90, true);
#if UNITY_EDITOR
            ScreenCapture.CaptureScreenshot(Shot("stress_wen.png"));
            log.AppendLine($"    screenshots: {Shot("")}");
            yield return Frames(3);
#endif
            int wenBefore = RunStats.WenPickedUp;
            PickupSystem.GatherAllTo(inventory.transform.position);
            yield return Measure("picking all of them up at once", 10);
            log.AppendLine($"    {PickupSystem.Count} left on the ground, {RunStats.WenPickedUp - wenBefore} wen collected");
        }

        // ---- put everything back
        inventory.wenForUpgrade = wenForUpgrade;
        if (health != null) health.godMode = false;
        GameEvents.OnEnemyKilled -= CountDeath;
        foreach (var b in heldFire) if (b != null) b.enabled = true;
        heldFire.Clear();
        Coins.Set(coinsBefore);
        director.enabled = true;
        Finish(null, done);
    }

#if UNITY_EDITOR
    public static string Shot(string file) => System.IO.Path.Combine(Application.temporaryCachePath, file);
#endif

    // the player's weapons switched off for the run, so the horde only dies when the test says so
    private void HoldFire(GameObject player)
    {
        heldFire.Clear();
        foreach (var b in player.GetComponentsInChildren<Behaviour>(true))
            if (b.enabled && (b is Weapon || b is AutoShooter || b is Aura || b is AOEAttack))
            {
                b.enabled = false;
                heldFire.Add(b);
            }
        log.AppendLine($"holding fire: {heldFire.Count} weapon parts off");
    }

    private static void KillAll(DamageKind kind)
    {
        foreach (var go in EnemyRegistry.All.ToList())
            if (go != null && go.TryGetComponent(out EnemyHealth e)) e.TakeDamage(1e9f, kind, false, true);
    }

    // spread over the screen and a ring around it, the way a horde closes in
    private static void SpawnWave(List<GameObject> prefabs, Vector2 at, float halfW, float halfH)
    {
        for (int i = 0; i < Enemies; i++)
        {
            var p = at + new Vector2(Random.Range(-halfW, halfW), Random.Range(-halfH, halfH)) * 1.4f;
            ObjectPool.For(prefabs[i % prefabs.Count]).Get(p, Quaternion.identity);
        }
    }

    private IEnumerator Frames(int n)
    {
        for (int i = 0; i < n; i++) yield return null;
    }

    private void StartRecorders()
    {
        recorders.Clear();
        var handles = new List<ProfilerRecorderHandle>();
        ProfilerRecorderHandle.GetAvailable(handles);
        foreach (var (marker, label) in Stages)
        {
            foreach (var h in handles)
            {
                if (ProfilerRecorderHandle.GetDescription(h).Name != marker) continue;
                var rec = new ProfilerRecorder(h, 1);
                rec.Start();
                recorders.Add((label, rec));
                break;
            }
        }
    }

    // frame times over the next n frames: the worst, the average, draw calls, and with breakdown,
    // the average time of each stage of the frame
    private IEnumerator Measure(string label, int frames, bool breakdown = false)
    {
        float worst = 0f, total = 0f;
        int batches = 0, setPass = 0;
        int aliveBefore = EnemyRegistry.Count, deathsBefore = deaths;
        var stageTotals = new double[recorders.Count];
        for (int i = 0; i < frames; i++)
        {
            yield return null;
            float ms = Time.unscaledDeltaTime * 1000f;
            worst = Mathf.Max(worst, ms);
            total += ms;
            for (int k = 0; k < recorders.Count; k++) stageTotals[k] += recorders[k].rec.LastValue * 1e-6;
#if UNITY_EDITOR
            batches = Mathf.Max(batches, UnityEditor.UnityStats.batches);
            setPass = Mathf.Max(setPass, UnityEditor.UnityStats.setPassCalls);
#endif
        }
        log.Append($"{label}: worst {worst:0.0} ms, average {total / frames:0.0} ms");
        if (batches > 0) log.Append($", up to {batches} batches, {setPass} set pass");
        log.Append($"  [alive {aliveBefore} -> {EnemyRegistry.Count}, {deaths - deathsBefore} died]");
        log.AppendLine();
        if (breakdown && recorders.Count > 0)
        {
            log.Append("    per frame:");
            for (int k = 0; k < recorders.Count; k++)
                log.Append($" {recorders[k].label} {stageTotals[k] / frames:0.00}");
            log.AppendLine(" (ms)");
        }
    }

    private void Finish(string error, System.Action done)
    {
        GameEvents.OnEnemyKilled -= CountDeath;
        foreach (var (_, rec) in recorders) rec.Dispose();
        recorders.Clear();
        Last = error ?? log.ToString();
        Debug.Log("STRESS " + (error != null ? "failed: " + error : "results\n" + Last));
        Running = false;
        done?.Invoke();
        Destroy(gameObject);
    }
}
#endif
