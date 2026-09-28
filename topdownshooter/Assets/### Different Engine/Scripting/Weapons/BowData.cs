using System;
using System.Collections.Generic;
using UnityEngine;

// the Bow as one weapon tree: a card that unlocks it, then a card per level. the player's
// AutoShooter still does the aiming and firing; each level sets its numbers outright
[CreateAssetMenu(menuName = "Rogue/Weapons/Bow", fileName = "Bow")]
public class BowData : WeaponData<BowWeapon>
{
    [Serializable]
    public struct LevelStats
    {
        [Tooltip("arrows per volley, each at its own target")]
        [Min(1)] public int arrows;
        [Tooltip("damage per arrow, before Might")]
        public float damage;
        [Tooltip("seconds between volleys, before Cooldown")]
        public float cooldown;
        [Tooltip("arrow speed, before Weapon Speed")]
        public float speed;
    }

    [Header("Levels (the first entry is the unlock)")]
    public LevelStats[] levels =
    {
        new LevelStats { arrows = 1, damage = 10.5f, cooldown = 1.4f, speed = 12f },
        new LevelStats { arrows = 2, damage = 10.5f, cooldown = 1.4f, speed = 12f },
        new LevelStats { arrows = 2, damage = 14f, cooldown = 1.3f, speed = 12f },
        new LevelStats { arrows = 3, damage = 14f, cooldown = 1.25f, speed = 12f },
        new LevelStats { arrows = 3, damage = 18f, cooldown = 1.2f, speed = 14f },
        new LevelStats { arrows = 4, damage = 18f, cooldown = 1.15f, speed = 14f },
        new LevelStats { arrows = 4, damage = 23f, cooldown = 1.1f, speed = 15f },
        new LevelStats { arrows = 5, damage = 26f, cooldown = 1f, speed = 16f },
    };

    [Header("Evolution (one more pick after the last level)")]
    [Tooltip("seconds between volleys once evolved. it ignores Cooldown: the bow simply never stops")]
    [Min(0.03f)] public float evolvedInterval = 0.1f;
    [Tooltip("the stream is one arrow a shot, each hitting for this share of the last level's damage: lighter arrows, but never a pause between them")]
    [Range(0.1f, 1f)] public float evolvedDamageMul = 0.4f;
    [Tooltip("the shimmer the evolved arrows' outline runs through")]
    public Color[] outlineColors =
    {
        new Color(1f, 0.93f, 0.55f),
        new Color(1f, 1f, 1f),
        new Color(0.6f, 0.95f, 1f),
        new Color(1f, 0.75f, 0.3f),
    };
    [Min(0.1f)] public float outlineShimmerSpeed = 10f;

    // the level table plus the evolution
    public override int LevelCount => levels.Length + 1;
    public override int EvolutionLevel => levels.Length + 1;
    public override AttackClass AttackClass => AttackClass.Physical;

    public bool IsEvolved(int level) => level > levels.Length;

    public LevelStats At(int level) =>
        levels == null || levels.Length == 0 ? default : levels[Mathf.Clamp(level - 1, 0, levels.Length - 1)];

    public override void Attributes(int level, StatContext st, List<Attribute> into)
    {
        var s = At(level);
        int extra = st != null ? st.ArrowCountTotal : 0;
        float dmg = s.damage * (IsEvolved(level) ? evolvedDamageMul : 1f) * (st != null ? st.arrowDamageMul : 1f);
        Add(into, "Arrows", IsEvolved(level) ? "1" : (s.arrows + extra).ToString());
        Add(into, "Damage", N(Hurt(st, dmg, AttackClass)));
        Add(into, "Cooldown", IsEvolved(level) ? Sec(evolvedInterval) + " (none)" : Sec(Every(st, s.cooldown, UpgradeType.ArrowCooldown)));
        Add(into, "Speed", N(s.speed * (st != null ? st.arrowSpeedMul * st.WeaponSpeedMul : 1f)));
        if (st != null && st.PierceTotal > 0) Add(into, "Pierce", st.PierceTotal.ToString());
    }

    public override string Describe(int level)
    {
        if (IsEvolved(level))
            return "Evolution: the bow never has to draw again. A single stream of light arrows pours out without a cooldown, each wrapped in a shining outline.";

        var s = At(level);
        if (level <= 1)
            return $"Every {s.cooldown:0.##}s, shoots {Arrows(s.arrows)} at the nearest enemies for {s.damage:0.#} damage.";

        var was = At(level - 1);
        var changes = new List<string>();
        if (s.arrows > was.arrows) changes.Add($"+{Arrows(s.arrows - was.arrows)}.");
        if (s.damage > was.damage) changes.Add($"Arrows hit for {s.damage:0.#}.");
        if (s.cooldown < was.cooldown) changes.Add($"Shoots every {s.cooldown:0.##}s.");
        if (s.speed > was.speed) changes.Add("Faster arrows.");
        return changes.Count > 0 ? string.Join(" ", changes) : "A stronger bow.";
    }

    private static string Arrows(int n) => n == 1 ? "1 arrow" : $"{n} arrows";
}
