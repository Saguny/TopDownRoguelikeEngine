using System.Collections.Generic;
using UnityEngine;

// one-shot particle bursts (an enemy's death puff) without a GameObject each. every effect prefab
// gets one shared particle system that emits its burst wherever it's asked, so hundreds of deaths
// in a frame cost one draw call and no instantiating. prefabs that aren't a single plain particle
// system (children, scripts) are still instantiated the old way
public static class BurstFx
{
    private sealed class Emitter
    {
        public ParticleSystem system;
        public int count;
        public ParticleSystem.EmitParams at;
    }

    private static readonly Dictionary<GameObject, Emitter> emitters = new Dictionary<GameObject, Emitter>();
    private static readonly HashSet<GameObject> unshareable = new HashSet<GameObject>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        emitters.Clear();
        unshareable.Clear();
    }

    public static void Play(GameObject prefab, Vector3 position)
    {
        if (prefab == null) return;

        if (!unshareable.Contains(prefab))
        {
            // the shared system lives in the scene, so a new scene makes a new one
            if (!emitters.TryGetValue(prefab, out var e) || e.system == null)
            {
                e = Make(prefab);
                if (e == null) unshareable.Add(prefab);
                else emitters[prefab] = e;
            }
            if (e != null)
            {
                e.at.position = position;
                e.system.Emit(e.at, e.count);
                return;
            }
        }

        Object.Instantiate(prefab, position, Quaternion.identity);
    }

    private static Emitter Make(GameObject prefab)
    {
        if (!prefab.TryGetComponent(out ParticleSystem _)) return null;
        if (prefab.GetComponentsInChildren<ParticleSystem>(true).Length != 1) return null;
        if (prefab.GetComponentsInChildren<MonoBehaviour>(true).Length > 0) return null;

        var go = Object.Instantiate(prefab);
        go.name = prefab.name + " (shared bursts)";
        go.transform.position = Vector3.zero;
        var ps = go.GetComponent<ParticleSystem>();

        // how many particles one of the prefab's own plays would make
        var emission = ps.emission;
        int count = 0;
        var array = new ParticleSystem.Burst[emission.burstCount];
        emission.GetBursts(array);
        foreach (var b in array)
        {
            float n = b.count.mode == ParticleSystemCurveMode.TwoConstants ? (b.count.constantMin + b.count.constantMax) * 0.5f : b.count.constant;
            count += Mathf.RoundToInt(n * Mathf.Max(1, b.cycleCount));
        }
        var main = ps.main;
        if (emission.rateOverTime.constant > 0f) count += Mathf.RoundToInt(emission.rateOverTime.constant * main.duration);
        count = Mathf.Max(1, count);

        // keep it alive and quiet: it only ever emits when told to
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        main.loop = true;
        main.stopAction = ParticleSystemStopAction.None;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.Max(main.maxParticles, 8000);
        // automatic culling pauses a looping system it thinks is off screen, freezing its particles
        // where they are: blood that never went away
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        emission.enabled = false;
        ps.Play(true);

        return new Emitter { system = ps, count = count, at = new ParticleSystem.EmitParams { applyShapeToPosition = true } };
    }
}
