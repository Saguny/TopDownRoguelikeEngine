using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Rogue/Weapons/Peach Talismans", fileName = "PeachTalismans")]
public class PeachTalismansData : WeaponData<PeachTalismans>
{
    [Serializable]
    public struct LevelStats
    {
        [Tooltip("burn damage per second while stuck. it goes through armor")]
        public float damagePerSecond;
        [Tooltip("seconds between talismans, before Cooldown")]
        public float interval;
    }

    [Header("Levels (the first entry is the unlock; one more pick after the last is the evolution)")]
    public LevelStats[] levels =
    {
        new LevelStats { damagePerSecond = 2, interval = 1.5f },
        new LevelStats { damagePerSecond = 3, interval = 1.4f },
        new LevelStats { damagePerSecond = 4, interval = 1.3f },
        new LevelStats { damagePerSecond = 6, interval = 1.2f },
        new LevelStats { damagePerSecond = 8, interval = 1.1f },
        new LevelStats { damagePerSecond = 10, interval = 1f },
    };

    [Header("Flight")]
    [Tooltip("before Weapon Speed")]
    public float flightSpeed = 6f;
    [Tooltip("how far the path swings to each side")]
    public float waveAmplitude = 0.6f;
    [Tooltip("distance covered by one full swing")]
    public float waveLength = 3f;
    [Tooltip("how far away an enemy can be for a talisman to be thrown at it. once thrown it flies until it sticks, finding a new target if its own dies")]
    public float range = 12f;
    [Tooltip("fading copies left behind each talisman")]
    [Min(0)] public int trailLength = 3;
    [Tooltip("world distance between the copies")]
    public float trailSpacing = 0.18f;

    [Header("Burning")]
    [Tooltip("seconds a talisman stays stuck")]
    public float stickSeconds = 5f;
    [Tooltip("seconds between burn ticks")]
    public float tickSeconds = 0.5f;
    [Tooltip("speed left to an enemy with a talisman stuck on it: 0.3 = 70% slower. several talismans don't stack")]
    [Range(0f, 1f)] public float stuckSlow = 0.3f;

    [Header("Evolution")]
    [Min(1)] public int evolvedCount = 8;
    [Tooltip("radius of the burst when a burning enemy dies, before Area")]
    public float blastRadius = 1.5f;
    [Tooltip("speed left to enemies caught in the burst: 0.5 = half speed")]
    [Range(0f, 1f)] public float slowFactor = 0.5f;
    public float slowSeconds = 2f;

    [Header("Art (placeholder shapes in these colours until set)")]
    public Sprite talismanSprite;
    [Tooltip("the evolution's burst, played frame by frame at the blast's size. empty = the Burst Sprite below, growing")]
    public Sprite[] burstFrames = Array.Empty<Sprite>();
    public float burstSeconds = 0.35f;
    public Sprite burstSprite;
    public float talismanSize = 0.45f;
    public Color talismanColor = new Color(0.85f, 0.15f, 0.12f);
    public Color burstColor = new Color(1f, 0.55f, 0.45f);
    public string sortingLayer = "Aura";
    public int sortingOrder = 6;

    [Header("Animated art (drawn at its own pixel size; overrides the sprites above when set)")]
    [Tooltip("the talisman in flight, looping")]
    public Sprite[] flightFrames = Array.Empty<Sprite>();
    [Min(0.01f)] public float flightFps = 14f;
    [Tooltip("stuck and burning down: steps of it burning, each a pair of flame flickers (step * 2 + flicker)")]
    public Sprite[] burnFrames = Array.Empty<Sprite>();
    [Min(0.01f)] public float burnFlickerFps = 12f;
    [Tooltip("played where a talisman crumbles to ash once it's done burning")]
    public GameObject ashFx;
    [Tooltip("the blast radius the Burst Frames are drawn for, in world units; they're scaled from it")]
    public float burstArtRadius = 1.5f;

    public bool Animated => flightFrames != null && flightFrames.Length > 0 && flightFrames[0] != null;

    // the level table plus the evolution
    public override int LevelCount => levels.Length + 1;
    public override int EvolutionLevel => levels.Length + 1;

    public bool IsEvolved(int level) => level > levels.Length;

    public LevelStats At(int level) =>
        levels == null || levels.Length == 0 ? default : levels[Mathf.Clamp(level - 1, 0, levels.Length - 1)];

    public override string Describe(int level)
    {
        if (IsEvolved(level))
            return $"Evolution: {evolvedCount} talismans fly out from all around you at once. Enemies they burn to death burst and slow everything near them.";

        var s = At(level);
        return level <= 1
            ? $"Talismans fly to a random enemy and stick, burning {s.damagePerSecond:0.#} a second and slowing it by {(1f - stuckSlow) * 100f:0}%. One every {s.interval:0.#}s."
            : $"Burns {s.damagePerSecond:0.#} a second, one every {s.interval:0.#}s.";
    }
}
