using System;
using System.Collections.Generic;
using UnityEngine;

// Qing Long Xian, the Azure Dragon Line
[CreateAssetMenu(menuName = "Rogue/Weapons/Dragon Line", fileName = "DragonLine")]
public class DragonLineData : WeaponData<DragonLine>
{
    public override AttackClass AttackClass => AttackClass.Magical;

    [Serializable]
    public struct LevelStats
    {
        [Tooltip("damage to an enemy a body segment passes through, before Might. the head hits for Head Multiplier times this")]
        public float damage;
        [Tooltip("seconds between casts, before Cooldown")]
        public float cooldown;
        [Tooltip("dragons per cast, each on its own line")]
        [Min(1)] public int dragons;
    }

    [Header("Levels (the first entry is the unlock; one more pick after the last is the evolution)")]
    public LevelStats[] levels =
    {
        new LevelStats { damage = 10f, cooldown = 5f, dragons = 1 },
        new LevelStats { damage = 12f, cooldown = 5f, dragons = 1 },
        new LevelStats { damage = 12f, cooldown = 4.5f, dragons = 1 },
        new LevelStats { damage = 14f, cooldown = 4.5f, dragons = 2 },
        new LevelStats { damage = 17f, cooldown = 4.5f, dragons = 2 },
        new LevelStats { damage = 17f, cooldown = 4f, dragons = 2 },
        new LevelStats { damage = 20f, cooldown = 4f, dragons = 3 },
        new LevelStats { damage = 24f, cooldown = 3.5f, dragons = 3 },
    };

    [Header("The line and the dragon")]
    [Tooltip("the head hits for this many times the damage")]
    [Min(1f)] public float headMultiplier = 3f;
    [Tooltip("segments behind the head (the tail is the last)")]
    [Min(2)] public int segments = 10;
    [Tooltip("the dragon's length as a share of the screen's height")]
    [Range(0.1f, 1f)] public float lengthOfScreen = 0.4f;
    [Tooltip("seconds the line shows before the dragon comes")]
    [Min(0f)] public float telegraphSeconds = 0.45f;
    [Tooltip("seconds the head takes to cross the screen, before Weapon Speed")]
    [Min(0.1f)] public float crossSeconds = 1.1f;
    [Tooltip("how far the dragon weaves either side of its line, in world units")]
    public float weave = 0.35f;
    [Tooltip("weaves per world unit travelled")]
    public float weaveFrequency = 0.22f;
    [Tooltip("radius the head hits in, before Area")]
    public float headRadius = 0.55f;
    [Tooltip("radius a body segment hits in, before Area")]
    public float bodyRadius = 0.35f;

    [Header("Evolution: the coiling dragon (it flies no more lines, only this)")]
    [Tooltip("seconds between spirals, before Cooldown")]
    public float evolvedCooldown = 15f;
    [Tooltip("damage of the fire it spits, before Might. the head hits for Evolved Head Multiplier times this")]
    public float fireDamage = 50f;
    [Min(1f)] public float evolvedHeadMultiplier = 1.5f;
    [Tooltip("damage of its coils as they shove enemies back, before Might")]
    public float coilDamage = 15f;
    [Tooltip("turns the spiral makes from the middle of the screen to its edge: more turns, a tighter coil")]
    [Min(0.5f)] public float spiralTurns = 4f;
    [Tooltip("world units a second the head travels along the spiral, before Weapon Speed")]
    [Min(1f)] public float spiralSpeed = 20f;
    [Tooltip("segments in the coiling dragon, head to tail: it's much longer than the line dragons")]
    [Min(2)] public int evolvedSegments = 24;
    [Tooltip("how much bigger each of its segments is than a line dragon's")]
    [Min(0.5f)] public float evolvedScale = 1.3f;
    [Tooltip("speed enemies are shoved outward at by the coils")]
    public float pushSpeed = 9f;
    public float pushSeconds = 0.3f;
    [Tooltip("seconds between the spits of fire")]
    [Min(0.02f)] public float fireInterval = 0.12f;
    [Tooltip("how far ahead of the head the fire lands, and how wide it is")]
    public float fireReach = 1.3f;
    public float fireRadius = 0.9f;
    [Tooltip("seconds before the same enemy can be hurt by the same part again in one spiral")]
    public float evolvedRehit = 0.6f;

    [Header("Art (NewSprites/Asesprites/VFX/Weapons/DragonLine; Tools > VFX > Build Weapon FX sets these. placeholder shapes until then)")]
    [Tooltip("the head, facing right, looping")]
    public Sprite[] headFrames = Array.Empty<Sprite>();
    [Tooltip("a body segment, facing right, looping")]
    public Sprite[] bodyFrames = Array.Empty<Sprite>();
    [Tooltip("a body segment with a clawed leg under it, facing right, looping; used for the segments in Leg Segments")]
    public Sprite[] legFrames = Array.Empty<Sprite>();
    [Tooltip("which body segments (0 is the one behind the head) have legs: the shoulders and the hips")]
    public int[] legSegments = { 1, 5 };
    [Tooltip("the tail's end, facing right, looping")]
    public Sprite[] tailFrames = Array.Empty<Sprite>();
    [Min(0.01f)] public float bodyFps = 10f;
    [Tooltip("the line cast before it: one tile repeated along it")]
    public Sprite[] lineFrames = Array.Empty<Sprite>();
    [Min(0.01f)] public float lineFps = 12f;
    [Tooltip("the evolved dragon's fire, played ahead of its head")]
    public GameObject fireFx;
    [Tooltip("played on an enemy the head hits")]
    public GameObject biteFx;
    [Tooltip("art pixels between the centres of two body segments as drawn: the segments are scaled to the dragon's length from this")]
    public float segmentArtPixels = 14f;
    public Color placeholderColor = new Color(0.3f, 0.85f, 0.7f);
    public string sortingLayer = "Aura";
    public int sortingOrder = 8;

    public bool Animated => bodyFrames != null && bodyFrames.Length > 0 && bodyFrames[0] != null;

    // the level table plus the evolution
    public override int LevelCount => levels.Length + 1;
    public override int EvolutionLevel => levels.Length + 1;

    public bool IsEvolved(int level) => level > levels.Length;

    public LevelStats At(int level) =>
        levels == null || levels.Length == 0 ? default : levels[Mathf.Clamp(level - 1, 0, levels.Length - 1)];

    public override void Attributes(int level, StatContext st, List<Attribute> into)
    {
        if (IsEvolved(level))
        {
            Add(into, "Fire damage", N(Hurt(st, fireDamage, AttackClass)));
            Add(into, "Head damage", N(Hurt(st, fireDamage * evolvedHeadMultiplier, AttackClass)));
            Add(into, "Coil damage", N(Hurt(st, coilDamage, AttackClass)));
            Add(into, "Segments", evolvedSegments.ToString());
            Add(into, "Cooldown", Sec(Every(st, evolvedCooldown)));
            return;
        }
        var s = At(level);
        Add(into, "Dragons", s.dragons.ToString());
        Add(into, "Body damage", N(Hurt(st, s.damage, AttackClass)));
        Add(into, "Head damage", N(Hurt(st, s.damage * headMultiplier, AttackClass)));
        Add(into, "Cooldown", Sec(Every(st, s.cooldown)));
    }

    public override string Describe(int level)
    {
        if (IsEvolved(level))
            return $"Evolution: no more lines. Every {evolvedCooldown:0}s a great dragon coils out from the middle of the screen, shoving enemies back and spitting fire for {fireDamage:0}. Its head hits for {fireDamage * evolvedHeadMultiplier:0}.";

        var s = At(level);
        if (level <= 1)
            return $"Every {s.cooldown:0.#}s a line is cast across the screen and an azure dragon flies along it. Its body hits for {s.damage:0}, its head for {s.damage * headMultiplier:0}.";

        var was = At(level - 1);
        var changes = new List<string>();
        if (s.damage > was.damage) changes.Add($"Body hits for {s.damage:0}, head for {s.damage * headMultiplier:0}.");
        if (s.cooldown < was.cooldown) changes.Add($"Every {s.cooldown:0.#}s.");
        if (s.dragons > was.dragons) changes.Add(s.dragons == 2 ? "Two dragons at once." : $"{s.dragons} dragons at once.");
        return changes.Count > 0 ? string.Join(" ", changes) : "A stronger dragon.";
    }
}
