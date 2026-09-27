using System;
using System.Collections.Generic;
using UnityEngine;

// the Meteorite as one weapon tree. what used to be six separate cards (the unlock, damage,
// amount, radius, cooldown and smartness) are its levels now. the player's AOEAttack still picks
// the spots and drops the meteors; each level sets its numbers outright
[CreateAssetMenu(menuName = "Rogue/Weapons/Meteorite", fileName = "Meteorite")]
public class MeteoriteData : WeaponData<MeteoriteWeapon>
{
    public override AttackClass AttackClass => AttackClass.Magical;

    [Serializable]
    public struct LevelStats
    {
        [Tooltip("meteors per volley")]
        [Min(1)] public int meteors;
        [Tooltip("damage per blast, before Might")]
        public float damage;
        [Tooltip("blast radius, before Area")]
        public float radius;
        [Tooltip("seconds between volleys, before Cooldown")]
        public float cooldown;
        [Tooltip("aim where the most enemies will be, leading moving groups, instead of random spots")]
        public bool smart;
    }

    [Header("Levels (the first entry is the unlock)")]
    public LevelStats[] levels =
    {
        new LevelStats { meteors = 1, damage = 20f, radius = 2f, cooldown = 5f },
        new LevelStats { meteors = 2, damage = 20f, radius = 2f, cooldown = 5f },
        new LevelStats { meteors = 2, damage = 24f, radius = 2.3f, cooldown = 5f },
        new LevelStats { meteors = 2, damage = 24f, radius = 2.3f, cooldown = 4.5f },
        new LevelStats { meteors = 2, damage = 24f, radius = 2.3f, cooldown = 4.5f, smart = true },
        new LevelStats { meteors = 3, damage = 28f, radius = 2.3f, cooldown = 4.5f, smart = true },
        new LevelStats { meteors = 3, damage = 28f, radius = 2.6f, cooldown = 4f, smart = true },
        new LevelStats { meteors = 4, damage = 32f, radius = 2.6f, cooldown = 4f, smart = true },
    };

    public override int LevelCount => levels.Length;

    public LevelStats At(int level) =>
        levels == null || levels.Length == 0 ? default : levels[Mathf.Clamp(level - 1, 0, levels.Length - 1)];

    public override string Describe(int level)
    {
        var s = At(level);
        if (level <= 1)
            return $"Every {s.cooldown:0.#}s, {Meteors(s.meteors)} fall{(s.meteors == 1 ? "s" : "")} from the heavens for {s.damage:0} damage.";

        var was = At(level - 1);
        var changes = new List<string>();
        int more = s.meteors - was.meteors;
        if (more > 0) changes.Add($"+{more} meteorite{(more == 1 ? "" : "s")}.");
        if (s.damage > was.damage) changes.Add($"Blasts hit for {s.damage:0}.");
        if (s.radius > was.radius) changes.Add("Bigger blasts.");
        if (s.cooldown < was.cooldown) changes.Add($"Falls every {s.cooldown:0.#}s.");
        if (s.smart && !was.smart) changes.Add("Meteors aim where they'll catch the most enemies.");
        return changes.Count > 0 ? string.Join(" ", changes) : "Stronger meteors.";
    }

    private static string Meteors(int n) => n == 1 ? "a meteorite" : $"{n} meteorites";
}
