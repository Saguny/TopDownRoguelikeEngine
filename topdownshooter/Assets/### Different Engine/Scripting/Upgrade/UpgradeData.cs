using UnityEngine;

// what a level up card is: a weapon to take or level up, or a passive that helps every weapon
public enum UpgradeCategory { Weapon, Passive }

[CreateAssetMenu(menuName = "Rogue/Upgrade Data", fileName = "Upgrade_")]
public class UpgradeData : ScriptableObject
{
    [Header("Identity")]
    public string title;
    public UpgradeType type;
    public Sprite icon;
    [Tooltip("optional: the icon's idle loop on the level up card. empty shows the icon still")]
    public Sprite[] iconFrames = System.Array.Empty<Sprite>();
    [Min(1f)] public float iconFps = 6f;

    [Header("Pool")]
    [Tooltip("untick to keep this upgrade out of level up offers without deleting it")]
    public bool includeInPool = true;

    [Header("Effect")]
    public float value = 1.10f;
    public bool additive = false;

    [TextArea] public string description;

    [Header("Leveling")]
    [SerializeField, Min(0)] private int level = 0;
    [SerializeField, Min(1)] private int maxLevel = 5;

    // endless: once maxed, keep offering this with diminishing returns. only enable it for stats
    // the enemy curve directly counters (damage, fire rate, health). never for radius, speed,
    // arrow count or pierce, since those change the geometry of the fight and are exactly what
    // lets a build stand still and clear the screen
    [Header("Overcharge (endless)")]
    public bool overcharge = false;
    [Tooltip("the most overcharge can ever add, as a fraction. 1 = up to +100%")]
    [Min(0f)] public float overchargeCeiling = 1f;
    [Tooltip("overcharge picks needed to reach half the ceiling")]
    [Min(1f)] public float overchargeHalfStacks = 8f;

    [System.NonSerialized] private int overchargeStacks;

    public int Level => level;
    public virtual UpgradeCategory Category => UpgradeCategory.Passive;

    // whether holding it fills one of the run's weapon or passive slots. an ability every
    // character carries (the Command Token) doesn't
    public virtual bool TakesSlot => true;

    // what the level up card says it is, e.g. Passive, or Weapon (Magical)
    public virtual string CategoryLabel => Category.ToString();

    // what the level up card shows for the next pick; weapons show their evolved art on the evolution
    public virtual Sprite CardIcon => icon;
    public virtual Sprite[] CardIconFrames => iconFrames;
    // what it shows once held (the level up screen's loadout); an evolved weapon shows its evolved art
    public virtual Sprite HeldIcon => icon;
    // weapons take their level count from their level table instead
    public virtual int MaxLevel => maxLevel;
    public bool IsAtCap => level >= MaxLevel;

    public int OverchargeStacks => overchargeStacks;
    // overcharge is an endless mode feature; in a normal run a maxed upgrade is simply done
    public bool IsOvercharging => IsAtCap && overcharge && GameMode.IsEndless;
    public bool CanOffer => !IsAtCap || (overcharge && GameMode.IsEndless);

    // hyperbolic stacking: every pick adds the same raw stack, the payoff shrinks, and the total
    // can never pass the ceiling. bounded player power against enemy health that doubles forever
    // is what guarantees a wall exists; skill and build only decide where it is
    public float OverchargeBonus(int stacks) => overchargeCeiling * stacks / (stacks + overchargeHalfStacks);
    public float OverchargeBonus() => OverchargeBonus(overchargeStacks);

    public void AddOverchargeStack()
    {
        overchargeStacks++;
    }

    public void LevelUp()
    {
        if (level < MaxLevel) level++;
    }

    public string GetBaseTitle()
    {
        return string.IsNullOrEmpty(title) ? "Upgrade" : title;
    }

    public virtual string GetLevelProgress()
    {
        return IsOvercharging ? $"OC {overchargeStacks + 1}" : $"Lv. {level + 1}";
    }

    public string GetDisplayTitle()
    {
        return IsOvercharging ? "Overcharge: " + GetBaseTitle() : GetBaseTitle();
    }

    // overcharge states the real, shrinking gain so diminishing returns are visible, not hidden.
    // weapons write theirs from their level table
    public virtual string GetDisplayDescription()
    {
        if (!IsOvercharging) return description ?? string.Empty;

        float now = OverchargeBonus(overchargeStacks);
        float next = OverchargeBonus(overchargeStacks + 1);
        return $"+{(next - now) * 100f:0.#}% more\n(total +{next * 100f:0}% of +{overchargeCeiling * 100f:0}% max)";
    }

    public void ResetLevel()
    {
        level = 0;
        overchargeStacks = 0;
    }
}
