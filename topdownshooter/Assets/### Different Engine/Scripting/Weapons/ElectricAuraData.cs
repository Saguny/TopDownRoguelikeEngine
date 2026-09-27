using System;
using System.Collections.Generic;
using UnityEngine;

// the Electrical Aura as one weapon tree. its damage and radius cards are its levels now. the
// Aura under the player still does the shocking; each level sets its numbers outright
[CreateAssetMenu(menuName = "Rogue/Weapons/Electrical Aura", fileName = "Electrical Aura")]
public class ElectricAuraData : WeaponData<ElectricAuraWeapon>
{
    public override AttackClass AttackClass => AttackClass.Magical;

    [Serializable]
    public struct LevelStats
    {
        [Tooltip("damage to everything inside per pulse, before Might")]
        public float damage;
        [Tooltip("radius, before Area")]
        public float radius;
        [Tooltip("seconds between pulses, before Cooldown")]
        public float interval;
    }

    [Header("Levels (the first entry is the unlock)")]
    public LevelStats[] levels =
    {
        new LevelStats { damage = 10f, radius = 1.65f, interval = 0.5f },
        new LevelStats { damage = 12f, radius = 1.65f, interval = 0.5f },
        new LevelStats { damage = 12f, radius = 1.9f, interval = 0.5f },
        new LevelStats { damage = 15f, radius = 1.9f, interval = 0.5f },
        new LevelStats { damage = 15f, radius = 2.2f, interval = 0.5f },
        new LevelStats { damage = 15f, radius = 2.2f, interval = 0.42f },
        new LevelStats { damage = 19f, radius = 2.5f, interval = 0.42f },
        new LevelStats { damage = 24f, radius = 2.8f, interval = 0.4f },
    };

    public override int LevelCount => levels.Length;

    public LevelStats At(int level) =>
        levels == null || levels.Length == 0 ? default : levels[Mathf.Clamp(level - 1, 0, levels.Length - 1)];

    public override void Attributes(int level, StatContext st, List<Attribute> into)
    {
        var s = At(level);
        Add(into, "Damage", N(Hurt(st, s.damage, AttackClass)));
        Add(into, "Radius", N(Wide(st, s.radius)));
        Add(into, "Hits each enemy every", Sec(Every(st, s.interval, UpgradeType.AuraCooldown)));
    }

    public override string Describe(int level)
    {
        var s = At(level);
        if (level <= 1)
            return $"A field around you shocks every enemy inside it for {s.damage:0} every {s.interval:0.##}s.";

        var was = At(level - 1);
        var changes = new List<string>();
        if (s.damage > was.damage) changes.Add($"Shocks for {s.damage:0}.");
        if (s.radius > was.radius) changes.Add("Bigger field.");
        if (s.interval < was.interval) changes.Add($"Shocks every {s.interval:0.##}s.");
        return changes.Count > 0 ? string.Join(" ", changes) : "A stronger aura.";
    }
}
