using System;
using System.Collections.Generic;
using UnityEngine;

// Feijian, the flying sword, and its evolution, the Imperial Sword Cage
[CreateAssetMenu(menuName = "Rogue/Weapons/Flying Sword", fileName = "FlyingSword")]
public class FlyingSwordData : WeaponData<FlyingSword>
{
    public override AttackClass AttackClass => AttackClass.Physical;

    [Serializable]
    public struct LevelStats
    {
        [Tooltip("damage of a blade's first hit, before Might")]
        public float damage;
        [Tooltip("blades per throw")]
        [Min(1)] public int blades;
        [Tooltip("bounces off enemies and the screen's edges before it's spent")]
        [Min(0)] public int bounces;
        [Tooltip("how much faster it gets off every wall: 0.1 = 10%")]
        [Min(0f)] public float wallAcceleration;
        [Tooltip("seconds between throws, before Cooldown")]
        public float cooldown;
    }

    [Header("Levels (the first entry is the unlock; one more pick after the last is the evolution)")]
    public LevelStats[] levels =
    {
        new LevelStats { damage = 12f, blades = 1, bounces = 4, wallAcceleration = 0f, cooldown = 1.6f },
        new LevelStats { damage = 12f, blades = 1, bounces = 5, wallAcceleration = 0f, cooldown = 1.6f },
        new LevelStats { damage = 12f, blades = 2, bounces = 5, wallAcceleration = 0f, cooldown = 1.6f },
        new LevelStats { damage = 15f, blades = 2, bounces = 5, wallAcceleration = 0.1f, cooldown = 1.5f },
        new LevelStats { damage = 15f, blades = 2, bounces = 7, wallAcceleration = 0.1f, cooldown = 1.5f },
        new LevelStats { damage = 15f, blades = 3, bounces = 7, wallAcceleration = 0.15f, cooldown = 1.4f },
        new LevelStats { damage = 19f, blades = 3, bounces = 8, wallAcceleration = 0.15f, cooldown = 1.3f },
    };

    [Header("Blades")]
    [Tooltip("before Weapon Speed")]
    public float speed = 13f;
    [Tooltip("the fastest a blade gets, however many walls it bounces off")]
    public float maxSpeed = 30f;
    [Tooltip("extra damage for every bounce it has done: 0.15 = +15% a bounce")]
    [Min(0f)] public float bonusPerBounce = 0.15f;
    [Tooltip("radius it hits in, before Area")]
    public float hitRadius = 0.35f;
    [Tooltip("how far off an enemy it looks for the next one to bounce to")]
    public float bounceRange = 7f;
    [Tooltip("how far from the player it looks for its first enemy")]
    public float range = 12f;
    [Tooltip("seconds before it can hit the enemy it just bounced off again")]
    public float rehit = 0.35f;
    [Tooltip("fading copies left behind each blade")]
    [Min(0)] public int trailLength = 4;
    public float trailSpacing = 0.2f;

    [Header("Evolution: the Imperial Sword Cage")]
    [Tooltip("seconds between pairs, before Cooldown")]
    public float cageCooldown = 4.5f;
    [Tooltip("pairs of swords per throw")]
    [Min(1)] public int pairs = 1;
    [Tooltip("bounces an evolved sword makes before it flies to the edge and embeds there")]
    [Min(0)] public int cageBounces = 6;
    [Tooltip("seconds the laser between a pair lasts")]
    public float laserSeconds = 3f;
    [Tooltip("damage of the laser to each enemy in it, before Might, every Laser Tick")]
    public float laserDamage = 30f;
    public float laserTick = 0.25f;
    [Tooltip("the laser's width, before Area")]
    public float laserWidth = 0.7f;
    [Tooltip("seconds an embedded sword waits for its partner before it gives up")]
    public float partnerWait = 4f;

    [Header("Art (NewSprites/Asesprites/VFX/Weapons/FlyingSword; Tools > VFX > Build Weapon FX sets these. placeholder shapes until then)")]
    [Tooltip("a blade in flight, pointing right, looping")]
    public Sprite[] bladeFrames = Array.Empty<Sprite>();
    [Min(0.01f)] public float bladeFps = 16f;
    [Tooltip("a sword embedded in the screen's edge, pointing right (into the wall), looping")]
    public Sprite[] embedFrames = Array.Empty<Sprite>();
    [Tooltip("the laser: one tile repeated along it")]
    public Sprite[] laserFrames = Array.Empty<Sprite>();
    [Min(0.01f)] public float laserFps = 20f;
    [Tooltip("played where a blade hits an enemy or a wall")]
    public GameObject sparkFx;
    public Color bladeColor = new Color(0.45f, 1f, 0.7f);
    public string sortingLayer = "Aura";
    public int sortingOrder = 6;

    public bool Animated => bladeFrames != null && bladeFrames.Length > 0 && bladeFrames[0] != null;

    // the level table plus the evolution
    public override int LevelCount => levels.Length + 1;
    public override int EvolutionLevel => levels.Length + 1;

    public bool IsEvolved(int level) => level > levels.Length;

    public LevelStats At(int level) =>
        levels == null || levels.Length == 0 ? default : levels[Mathf.Clamp(level - 1, 0, levels.Length - 1)];

    public override string Describe(int level)
    {
        if (IsEvolved(level))
            return $"Evolution: every {cageCooldown:0.#}s a pair of swords bounces about and embeds in the screen's edges, and a laser burns between them for {laserSeconds:0}s.";

        var s = At(level);
        if (level <= 1)
            return $"Throws a jade blade that ricochets off enemies and the screen's edges {s.bounces} times, hitting for {s.damage:0} and {bonusPerBounce * 100f:0}% more with every bounce.";

        var was = At(level - 1);
        var changes = new List<string>();
        if (s.damage > was.damage) changes.Add($"Hits for {s.damage:0}.");
        if (s.blades > was.blades) changes.Add($"+{s.blades - was.blades} blade.");
        if (s.bounces > was.bounces) changes.Add($"{s.bounces} bounces.");
        if (s.wallAcceleration > was.wallAcceleration) changes.Add($"{s.wallAcceleration * 100f:0}% faster off every wall.");
        if (s.cooldown < was.cooldown) changes.Add($"Every {s.cooldown:0.#}s.");
        return changes.Count > 0 ? string.Join(" ", changes) : "A keener blade.";
    }
}
