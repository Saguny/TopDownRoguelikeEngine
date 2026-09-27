using System;
using UnityEngine;

// one entry per stat on the character panel. assets store these as numbers, so only ever add
// new ones at the end
public enum StatId
{
    MaxHealth,
    Recovery,
    Armor,
    MoveSpeed,
    Might,
    Cooldown,
    Area,
    WeaponSpeed,
    ArrowCount,
    Pierce,
    CritChance,
    CritDamage,
    Magnet,
    Growth,
    Greed,
    Revival,
    Reroll,
    Skip,
    Banish,
    ArmourPierce
}

public enum StatGroup
{
    Body,
    Martial,
    Fortune,
    Strategy
}

// how a value reads on the panel. percent style stats are stored as fractions, 0.1 = 10%
public enum StatFormat
{
    Number,     // 100, 0.50
    Percent,    // +10%
    Reduction,  // -10%, for stats where a bigger value means less of something, like cooldown
    Multiplier, // x2.0
    Count       // +1
}

[Serializable]
public struct StatBonus
{
    public StatId stat;
    [Tooltip("added to the stat. percent stats use fractions: 0.1 = +10%")]
    public float value;
}
