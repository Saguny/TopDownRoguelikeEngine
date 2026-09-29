using System;
using System.Collections.Generic;
using UnityEngine;

// a stage's enemy schedule, the way Vampire Survivors runs its stages: the run is cut into beats
// by the minute, each with its own mix of enemies and a crowd size that is topped straight back up
// whenever the player thins it, plus set pieces at fixed times (wisps streaming across the screen,
// a ring of jiangshi closing in, an elite worth a pile of wen). the DifficultyCurve still decides
// how tough each enemy is; this decides who comes, how many and when
[CreateAssetMenu(menuName = "Rogue/Spawn Timeline")]
public class SpawnTimeline : ScriptableObject
{
    [Serializable]
    public class Pick
    {
        public EnemyArchetype archetype;
        [Min(0f)] public float weight = 1f;
    }

    [Serializable]
    public class Beat
    {
        public string label;
        [Tooltip("run minute this beat takes over from the one before")]
        [Min(0f)] public float fromMinute;
        [Tooltip("whenever fewer than this many are alive, the crowd is refilled right away")]
        [Min(0)] public int minimum = 10;
        [Tooltip("on top of that, this many more arrive every interval, until the crowd reaches the cap")]
        [Min(0)] public int perInterval = 2;
        [Tooltip("seconds between those extra arrivals")]
        [Min(0.05f)] public float interval = 1f;
        [Tooltip("no extra arrivals above this many alive")]
        [Min(1)] public int cap = 60;
        public Pick[] enemies = Array.Empty<Pick>();
    }

    public enum EventKind
    {
        Swarm,      // a stream of enemies crossing the screen through the player, then gone
        Stampede,   // a line of them sweeping across the screen side to side
        Ring,       // a ring just off screen, all closing in at once
        Elite,      // one tough, gold enemy (or a few) that bursts into wen
        Surge,      // the crowd swells for a while
    }

    [Serializable]
    public class Event
    {
        [Tooltip("shown over the player when it starts; empty shows nothing")]
        public string label;
        [Min(0f)] public float atMinute;
        [Tooltip("0 happens once. otherwise it comes back every this many minutes, which keeps endless runs busy")]
        [Min(0f)] public float repeatEveryMinutes;
        public EventKind kind;
        [Tooltip("empty uses enemies from the beat running at the time")]
        public EnemyArchetype archetype;
        [Tooltip("how many enemies (for an elite, how many elites)")]
        [Min(1)] public int count = 12;
        [Tooltip("swarm and stampede: how much faster than usual they cross")]
        [Min(0.1f)] public float speed = 1.6f;
        [Tooltip("health multiplier on top of the difficulty curve. elites want a lot")]
        [Min(0.1f)] public float health = 1f;
        [Tooltip("surge: how much bigger the crowd gets")]
        [Min(1f)] public float crowd = 1.6f;
        [Tooltip("surge: how long it lasts")]
        [Min(0f)] public float seconds = 20f;
    }

    [Tooltip("multiplies every beat's minimum, extra arrivals and cap. the quick way to make the whole stage busier or calmer")]
    [Min(0.1f)] public float crowdScale = 1f;
    [Tooltip("never more alive than this, whatever a beat or surge says. it's the frame rate guard")]
    [Min(1)] public int hardCap = 500;
    [Tooltip("only for the graph above: where the wave lines go (GameLoopController's wave length)")]
    [Min(0.5f)] public float waveMinutes = 3f;

    [Header("Late evolutions (the horde answering a full build of them)")]
    [Tooltip("from this run minute the horde answers evolved weapons harder than SpawnDirector's own evolution pressure: more health for each and all of them counted, easing in over a minute. 0 = never (the director's own all run)")]
    [Min(0f)] public float lateEvoFromMinute = 0f;
    [Tooltip("from then on, each evolved weapon adds this share to every enemy's health (the director's own is 0.1)")]
    [Min(0f)] public float lateEvoHealth = 0.25f;
    [Tooltip("and this many evolutions are answered (the director's own is 4)")]
    [Min(0f)] public float lateEvoMax = 6f;

    public List<Beat> beats = new List<Beat>();
    public List<Event> events = new List<Event>();

    // the beat running at this minute: the latest one that has started. before the first one
    // starts, the first one. the list doesn't have to be in order
    public Beat BeatAt(float minute)
    {
        Beat found = null, earliest = null;
        foreach (var b in beats)
        {
            if (b == null) continue;
            if (earliest == null || b.fromMinute < earliest.fromMinute) earliest = b;
            if (b.fromMinute <= minute && (found == null || b.fromMinute >= found.fromMinute)) found = b;
        }
        return found ?? earliest;
    }

    public static EnemyArchetype PickFrom(Pick[] picks)
    {
        if (picks == null) return null;
        float sum = 0f;
        foreach (var p in picks)
            if (Usable(p)) sum += p.weight;
        if (sum <= 0f) return null;

        float r = UnityEngine.Random.value * sum;
        EnemyArchetype last = null;
        foreach (var p in picks)
        {
            if (!Usable(p)) continue;
            last = p.archetype;
            r -= p.weight;
            if (r <= 0f) return p.archetype;
        }
        return last;
    }

    private static bool Usable(Pick p) => p != null && p.weight > 0f && p.archetype != null && p.archetype.prefab != null;
}
