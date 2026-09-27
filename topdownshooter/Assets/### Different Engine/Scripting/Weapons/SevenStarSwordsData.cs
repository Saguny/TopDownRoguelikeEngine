using System.Collections.Generic;
using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Rogue/Weapons/Seven Star Swords", fileName = "SevenStarSwords")]
public class SevenStarSwordsData : WeaponData<SevenStarSwords>
{
    public override AttackClass AttackClass => AttackClass.Physical;

    [Serializable]
    public struct LevelStats
    {
        [Tooltip("damage when a sword touches an enemy")]
        public float contactDamage;
    }

    [Header("Levels (one sword per level; one more pick after the last is the evolution)")]
    public LevelStats[] levels =
    {
        new LevelStats { contactDamage = 5 },
        new LevelStats { contactDamage = 8 },
        new LevelStats { contactDamage = 10 },
        new LevelStats { contactDamage = 12 },
        new LevelStats { contactDamage = 15 },
        new LevelStats { contactDamage = 18 },
        new LevelStats { contactDamage = 20 },
    };

    [Header("Swords")]
    [Tooltip("distance from the player, before Area")]
    public float orbitRadius = 1.8f;
    [Tooltip("degrees per second the ring turns, before Weapon Speed")]
    public float orbitSpeed = 120f;
    [Tooltip("world size of one sword, before Area")]
    public float swordSize = 0.7f;
    [Tooltip("seconds before the swords can cut the same enemy again")]
    public float contactInterval = 0.5f;

    [Header("Evolution: homing stars (the swords have none before it)")]
    [Tooltip("seconds between bursts, before Cooldown")]
    public float starInterval = 1.3f;
    [Tooltip("base damage of each star, before Might")]
    public float starDamage = 10f;
    [Min(1)] public int starsPerSword = 8;
    [Tooltip("before Weapon Speed")]
    public float starSpeed = 7f;
    [Tooltip("degrees a second a star turns onto its enemy. it turns tighter the longer it flies, so it can't circle one forever")]
    public float starTurn = 240f;
    [Tooltip("how far from the player a burst looks for enemies to home in on")]
    public float starRange = 12f;
    [Tooltip("seconds a star flies before it fades out")]
    public float starLifetime = 3f;
    public float starSize = 0.3f;
    [Tooltip("fading copies left behind each star")]
    [Min(0)] public int starTrailLength = 4;
    [Tooltip("world distance between the copies")]
    public float starTrailSpacing = 0.14f;

    [Header("Art (placeholder shapes in these colours until set)")]
    [Tooltip("drawn pointing up; each sword is turned so its tip faces outward")]
    public Sprite swordSprite;
    public Sprite starSprite;
    public Color swordColor = new Color(0.55f, 0.75f, 1f);
    public Color starColor = new Color(0.8f, 0.55f, 1f);
    public string sortingLayer = "Aura";
    public int sortingOrder = 5;

    [Header("Animated art (drawn at its own pixel size; overrides the sprites above when set)")]
    [Tooltip("a sword hovering, looping; grows with Area")]
    public Sprite[] swordFrames = Array.Empty<Sprite>();
    [Min(0.01f)] public float swordFps = 12f;
    public Sprite[] starFrames = Array.Empty<Sprite>();
    [Min(0.01f)] public float starFps = 20f;
    [Tooltip("played where a star hits")]
    public GameObject starHitFx;
    [Tooltip("played at each sword as it looses its stars")]
    public GameObject launchFx;

    public bool AnimatedSwords => swordFrames != null && swordFrames.Length > 0 && swordFrames[0] != null;
    public bool AnimatedStars => starFrames != null && starFrames.Length > 0 && starFrames[0] != null;

    // one sword per level, then the evolution
    public override int LevelCount => levels.Length + 1;
    public override int EvolutionLevel => levels.Length + 1;

    public bool IsEvolved(int level) => level > levels.Length;

    public int SwordsAt(int level) => Mathf.Clamp(level, 1, Mathf.Max(1, levels.Length));

    public LevelStats At(int level) =>
        levels == null || levels.Length == 0 ? default : levels[Mathf.Clamp(level - 1, 0, levels.Length - 1)];

    public override void Attributes(int level, StatContext st, List<Attribute> into)
    {
        var s = At(level);
        Add(into, "Swords", SwordsAt(level).ToString());
        Add(into, "Cut damage", N(Hurt(st, s.contactDamage, AttackClass)));
        Add(into, "Orbit radius", N(Wide(st, orbitRadius)));
        if (IsEvolved(level))
        {
            Add(into, "Star damage", N(Hurt(st, starDamage, AttackClass)));
            Add(into, "Stars per sword", starsPerSword.ToString());
            Add(into, "Stars every", Sec(Every(st, starInterval)));
        }
    }

    public override string Describe(int level)
    {
        if (IsEvolved(level))
            return $"Evolution: every {starInterval:0.#}s each sword bursts {starsPerSword} stars that home in on enemies for {starDamage:0}.";

        var s = At(level);
        return level <= 1
            ? $"A sword circles you and cuts through enemies for {s.contactDamage:0}."
            : $"+1 sword ({SwordsAt(level)} in all). Swords cut for {s.contactDamage:0}.";
    }
}
