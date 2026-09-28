using System;
using System.Collections.Generic;
using UnityEngine;

// the Cinnabar Ink Brush: a giant calligraphy brush that follows the player, painting a trail of
// burning cinnabar ink on the ground wherever they walk. evolved (the Calligraphic Seal Grid),
// closing a loop with the trail stamps a seal on it and the whole inside goes up
[CreateAssetMenu(menuName = "Rogue/Weapons/Cinnabar Ink Brush", fileName = "CinnabarInkBrush")]
public class CinnabarInkBrushData : WeaponData<CinnabarInkBrush>
{
    public override AttackClass AttackClass => AttackClass.Magical;

    [Serializable]
    public struct LevelStats
    {
        [Tooltip("seconds the ink stays on the ground")]
        public float seconds;
        [Tooltip("how wide the trail is, in world units, before Area")]
        public float width;
        [Tooltip("burn damage to an enemy on the ink, every Tick Seconds, before Might. it goes through armor")]
        public float damage;
    }

    [Header("Levels (the first entry is the unlock; one more pick after the last is the evolution)")]
    public LevelStats[] levels =
    {
        new LevelStats { seconds = 2f, width = 0.55f, damage = 1.8f },
        new LevelStats { seconds = 2.5f, width = 0.55f, damage = 2.4f },
        new LevelStats { seconds = 2.5f, width = 0.65f, damage = 3f },
        new LevelStats { seconds = 3f, width = 0.65f, damage = 3.6f },
        new LevelStats { seconds = 3f, width = 0.75f, damage = 4.8f },
        new LevelStats { seconds = 3.5f, width = 0.8f, damage = 6f },
        new LevelStats { seconds = 4f, width = 0.9f, damage = 7.2f },
    };

    [Header("Ink")]
    [Tooltip("seconds between burns; an enemy on the ink is burnt once a tick however much of it they touch")]
    [Min(0.05f)] public float tickSeconds = 0.3f;
    [Tooltip("world distance between the blots the trail is painted from")]
    [Min(0.05f)] public float spacing = 0.2f;
    [Tooltip("the brush presses harder and lighter as it goes: 0.25 = the width swings by a quarter")]
    [Range(0f, 0.6f)] public float pressure = 0.24f;
    [Tooltip("parts of the ink's life it spends wet and burning, then drying; after that it's dry")]
    [Range(0f, 1f)] public float wetShare = 0.45f;
    [Range(0f, 1f)] public float dryingShare = 0.25f;
    [Tooltip("the last part of its life it shrinks and fades out")]
    [Range(0f, 1f)] public float fadeShare = 0.18f;
    [Tooltip("flames licking up off the wet ink: how many a second for every world unit of it")]
    [Min(0f)] public float flamesPerSecond = 0.9f;

    [Header("Brush")]
    [Tooltip("where the brush's tip trails, from the player's position: this far behind them")]
    public float trailBehind = 0.35f;
    [Tooltip("and this far down, at their feet")]
    public float feetBelow = 0.65f;
    [Tooltip("how quickly the brush catches up with where it should be")]
    [Min(0.1f)] public float brushFollow = 14f;
    [Tooltip("degrees the brush leans into the way the player walks")]
    public float brushLean = 32f;

    [Header("Evolution: Calligraphic Seal Grid")]
    [Tooltip("the smallest area a closed loop has to hold to go off, in square world units")]
    [Min(0.1f)] public float minLoopArea = 2.5f;
    [Tooltip("damage to bosses caught inside, before Might; other enemies inside are wiped out")]
    public float loopDamage = 400f;
    [Tooltip("share of an elite's health a blast takes")]
    [Range(0f, 1f)] public float eliteShare = 0.35f;
    [Tooltip("seconds before another loop can go off")]
    [Min(0f)] public float loopCooldown = 1.2f;
    [Tooltip("world distance between the ink blasts filling a loop")]
    [Min(0.3f)] public float blastSpacing = 1.1f;
    [Min(1)] public int maxBlasts = 40;
    [Tooltip("seconds the blasts take to ripple out from the seal to the far edge")]
    public float rippleSeconds = 0.35f;
    [Tooltip("screen shake when a loop goes off")]
    public float loopShake = 0.3f;
    public AudioClip loopSound;
    [Range(0f, 1f)] public float loopVolume = 1f;

    [Header("Art (NewSprites/Asesprites/VFX/Weapons/CinnabarInkBrush; Tools > VFX > Build Cinnabar Ink Brush sets these)")]
    [Tooltip("the trail's blots: four shapes, each wet, wet (flicker), drying, dry and its rim")]
    public Sprite[] dabFrames = Array.Empty<Sprite>();
    [Tooltip("the world size the blots' stroke is drawn at, before the level's width stretches it")]
    public float dabArtWidth = 14f / 28.46f;
    public Sprite[] brushFrames = Array.Empty<Sprite>();
    [Min(0.1f)] public float brushFps = 10f;
    [Tooltip("art pixels from the brush's canvas centre down to its tip")]
    public float brushTipPixels = 16.5f;
    public Sprite[] flameFrames = Array.Empty<Sprite>();
    [Min(0.1f)] public float flameFps = 16f;
    public Sprite[] sealFrames = Array.Empty<Sprite>();
    [Min(0.1f)] public float sealFps = 20f;
    [Tooltip("the loop size in world units the seal is drawn for; bigger loops get a bigger seal")]
    public float sealArtSize = 2.5f;
    public Sprite[] blastFrames = Array.Empty<Sprite>();
    [Min(0.1f)] public float blastFps = 25f;
    [Tooltip("the trail's layer: on the ground, under everyone")]
    public string trailLayer = "Player";
    public int trailOrder = -10;
    [Tooltip("the brush and the flames")]
    public string brushLayer = "Player";
    public int brushOrder = 0;
    [Tooltip("the seal and the blasts, over everything")]
    public string blastLayer = "Aura";
    public int blastOrder = 8;

    public const int DabStates = 5;
    public bool Ready => dabFrames != null && dabFrames.Length >= DabStates && dabFrames[0] != null;

    public override int LevelCount => levels.Length + 1;
    public override int EvolutionLevel => levels.Length + 1;
    public bool IsEvolved(int level) => level > levels.Length;

    public LevelStats At(int level) =>
        levels == null || levels.Length == 0 ? default : levels[Mathf.Clamp(level - 1, 0, levels.Length - 1)];

    public override void Attributes(int level, StatContext st, List<Attribute> into)
    {
        var s = At(level);
        Add(into, "Burn damage", N(Hurt(st, s.damage, AttackClass)));
        Add(into, "Burns every", Sec(tickSeconds));
        Add(into, "Ink lasts", Sec(s.seconds));
        Add(into, "Stroke width", N(Wide(st, s.width)));
        if (IsEvolved(level)) Add(into, "Seal damage", N(Hurt(st, loopDamage, AttackClass)));
    }

    public override string Describe(int level)
    {
        if (IsEvolved(level))
            return "Evolution: close a loop with the ink and a seal is stamped on it. Everything inside goes up in flames.";

        var s = At(level);
        if (level <= 1)
            return $"A brush paints burning cinnabar behind you. Enemies on the ink take {s.damage:0.#} every {tickSeconds:0.#}s. It lasts {s.seconds:0.#}s.";

        var was = At(level - 1);
        var changes = new List<string>();
        if (s.seconds > was.seconds) changes.Add($"Ink lasts {s.seconds:0.#}s.");
        if (s.width > was.width) changes.Add("A wider stroke.");
        if (s.damage > was.damage) changes.Add($"Burns for {s.damage:0.#}.");
        return changes.Count > 0 ? string.Join(" ", changes) : "A stronger brush.";
    }
}
