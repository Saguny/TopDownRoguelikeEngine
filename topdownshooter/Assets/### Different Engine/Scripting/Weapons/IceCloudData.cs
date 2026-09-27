using System;
using System.Collections.Generic;
using UnityEngine;

// Yu Xian Shan, the ice clouds
[CreateAssetMenu(menuName = "Rogue/Weapons/Ice Cloud", fileName = "IceCloud")]
public class IceCloudData : WeaponData<IceCloud>
{
    public override AttackClass AttackClass => AttackClass.Magical;

    [Serializable]
    public struct LevelStats
    {
        [Tooltip("damage to each enemy the snow freezes, before Might")]
        public float damage;
        [Tooltip("clouds per drift")]
        [Min(1)] public int clouds;
        [Tooltip("radius of the snowfall under a cloud, before Area")]
        public float radius;
        [Tooltip("seconds an enemy caught in the snowfall stays frozen")]
        public float freezeSeconds;
        [Tooltip("seconds between drifts, before Cooldown")]
        public float cooldown;
    }

    [Header("Levels (the first entry is the unlock; one more pick after the last is the evolution)")]
    public LevelStats[] levels =
    {
        new LevelStats { damage = 6f, clouds = 1, radius = 1.6f, freezeSeconds = 1.5f, cooldown = 10f },
        new LevelStats { damage = 8f, clouds = 1, radius = 1.6f, freezeSeconds = 1.5f, cooldown = 10f },
        new LevelStats { damage = 8f, clouds = 2, radius = 1.6f, freezeSeconds = 1.5f, cooldown = 10f },
        new LevelStats { damage = 8f, clouds = 2, radius = 2f, freezeSeconds = 2f, cooldown = 10f },
        new LevelStats { damage = 11f, clouds = 2, radius = 2f, freezeSeconds = 2f, cooldown = 8.5f },
        new LevelStats { damage = 11f, clouds = 3, radius = 2.2f, freezeSeconds = 2f, cooldown = 8.5f },
    };

    [Header("Clouds")]
    [Tooltip("seconds a cloud takes to drift across the screen, before Weapon Speed")]
    [Min(1f)] public float driftSeconds = 7f;
    [Tooltip("seconds between the snow's hits on the same enemy while it stays under a cloud")]
    [Min(0.1f)] public float snowTick = 1f;
    [Tooltip("how high above the ground the cloud floats, in world units: the snow falls from it to the ring it covers")]
    public float cloudHeight = 1.6f;

    [Header("Snow piles")]
    [Tooltip("seconds between piles a cloud leaves on the ground")]
    [Min(0.05f)] public float pileInterval = 0.7f;
    [Tooltip("seconds a pile lasts before it melts")]
    public float pileSeconds = 5f;
    [Tooltip("seconds an enemy that walks into a pile is frozen for")]
    public float pileFreezeSeconds = 3f;
    [Tooltip("radius of a pile, before Area")]
    public float pileRadius = 0.45f;
    [Tooltip("seconds after a pile's freeze wears off before a pile can freeze the same enemy again")]
    public float pileImmunity = 1f;
    [Min(1)] public int maxPiles = 60;

    [Header("Evolution: the frost tornado")]
    [Tooltip("seconds between tornadoes, before Cooldown")]
    public float tornadoCooldown = 20f;
    [Tooltip("seconds a tornado lasts")]
    public float tornadoSeconds = 6f;
    [Tooltip("it never touches down closer to the player than this")]
    public float tornadoMinDistance = 3.5f;
    [Tooltip("how far it pulls enemies in from, before Area")]
    public float pullRadius = 5f;
    [Tooltip("speed enemies are dragged toward it at")]
    public float pullSpeed = 3.5f;
    [Tooltip("how far its snow sprays, before Area: every enemy inside is frozen each spray")]
    public float sprayRadius = 7f;
    [Tooltip("seconds between sprays")]
    public float sprayInterval = 1.2f;
    [Tooltip("damage of each spray to the enemies it freezes, before Might")]
    public float sprayDamage = 15f;
    public float sprayFreezeSeconds = 2f;
    [Tooltip("snow piles each spray leaves around it")]
    [Min(0)] public int sprayPiles = 5;
    [Tooltip("how fast the tornado wanders")]
    public float tornadoSpeed = 1.2f;

    [Header("Art (NewSprites/Asesprites/VFX/Weapons/IceCloud; Tools > VFX > Build Weapon FX sets these. placeholder shapes until then)")]
    [Tooltip("a cloud drifting, looping")]
    public Sprite[] cloudFrames = Array.Empty<Sprite>();
    [Tooltip("the snow falling from it, looping, drawn for a snowfall of Snow Art Radius")]
    public Sprite[] snowFrames = Array.Empty<Sprite>();
    [Min(0.01f)] public float cloudFps = 8f;
    public float snowArtRadius = 1.6f;
    [Tooltip("a pile building up (played once) then sitting there")]
    public Sprite[] pileFrames = Array.Empty<Sprite>();
    [Tooltip("which frame of Pile Frames it rests on once built; the frames after it melt it away at the end")]
    public int pileRestFrame = 3;
    [Min(0.01f)] public float pileFps = 12f;
    [Tooltip("the ice over a frozen enemy")]
    public Sprite[] iceFrames = Array.Empty<Sprite>();
    [Min(0.01f)] public float iceFps = 16f;
    public Sprite[] tornadoFrames = Array.Empty<Sprite>();
    [Min(0.01f)] public float tornadoFps = 14f;
    [Tooltip("played where a spray freezes an enemy")]
    public GameObject frostBurstFx;
    public Color cloudColor = new Color(0.85f, 0.93f, 1f, 0.8f);
    public Color pileColor = new Color(0.95f, 0.98f, 1f, 0.9f);
    public Color tornadoColor = new Color(0.7f, 0.9f, 1f, 0.6f);
    [Tooltip("the clouds float over everything but the HUD; the piles lie on the floor")]
    public string cloudLayer = "Default";
    public int cloudOrder = 40;
    public string pileLayer = "Player";
    public int pileOrder = -5;
    public string tornadoLayer = "Aura";
    public int tornadoOrder = 9;

    public bool Animated => cloudFrames != null && cloudFrames.Length > 0 && cloudFrames[0] != null;

    // the level table plus the evolution
    public override int LevelCount => levels.Length + 1;
    public override int EvolutionLevel => levels.Length + 1;

    public bool IsEvolved(int level) => level > levels.Length;

    public LevelStats At(int level) =>
        levels == null || levels.Length == 0 ? default : levels[Mathf.Clamp(level - 1, 0, levels.Length - 1)];

    public override string Describe(int level)
    {
        if (IsEvolved(level))
            return $"Evolution: every {tornadoCooldown:0}s a frost tornado touches down away from you, dragging enemies in and spraying snow across the field that freezes them and leaves more piles.";

        var s = At(level);
        if (level <= 1)
            return $"Every {s.cooldown:0}s a cloud drifts over the screen, snowing: enemies under it freeze for {s.freezeSeconds:0.#}s and take {s.damage:0}. It leaves snow piles that freeze whoever walks in for {pileFreezeSeconds:0}s.";

        var was = At(level - 1);
        var changes = new List<string>();
        if (s.damage > was.damage) changes.Add($"The snow hits for {s.damage:0}.");
        if (s.clouds > was.clouds) changes.Add(s.clouds == 2 ? "Two clouds." : $"{s.clouds} clouds.");
        if (s.radius > was.radius) changes.Add("Wider snowfall.");
        if (s.freezeSeconds > was.freezeSeconds) changes.Add($"Freezes for {s.freezeSeconds:0.#}s.");
        if (s.cooldown < was.cooldown) changes.Add($"Every {s.cooldown:0.#}s.");
        return changes.Count > 0 ? string.Join(" ", changes) : "Colder clouds.";
    }
}
