using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// one row on the character stat panel
[Serializable]
public class StatDef
{
    public StatId id;
    public string label;
    public Sprite icon;
    public StatGroup group;
    public StatFormat format;
    [Tooltip("the value before any character or global upgrade bonus")]
    public float baseValue;
    [Tooltip("decimals shown for Number stats, e.g. 2 for Recovery 0.50")]
    [Min(0)] public int decimals;
    [Tooltip("off for stats where a bigger number is worse, so the panel tints them the other way")]
    public bool higherIsBetter = true;
    [Tooltip("leave off until the stat does something in game")]
    public bool showInPanel = true;
    [TextArea] public string tooltip;

    // panel text: "-" at zero like Vampire Survivors, otherwise "100", "+10%", "-5%", "x2.0", "+1"
    public string Format(float value)
    {
        if (Mathf.Abs(value) < 0.0001f) return "-";

        var c = CultureInfo.InvariantCulture;
        switch (format)
        {
            case StatFormat.Percent: return (value > 0f ? "+" : "") + (value * 100f).ToString("0.#", c) + "%";
            case StatFormat.Reduction: return (value > 0f ? "-" : "+") + Mathf.Abs(value * 100f).ToString("0.#", c) + "%";
            case StatFormat.Multiplier: return "x" + value.ToString("0.0#", c);
            case StatFormat.Count: return (value > 0f ? "+" : "") + value.ToString("0", c);
            default: return value.ToString(decimals > 0 ? "0." + new string('0', decimals) : "0", c);
        }
    }

    // a bonus rather than a total, always signed: "+20" Max Health, "+0.5x" Crit Damage, "+10%" Might
    public string FormatBonus(float value)
    {
        var c = CultureInfo.InvariantCulture;
        string sign = value > 0f ? "+" : "";
        switch (format)
        {
            case StatFormat.Number: return sign + value.ToString(decimals > 0 ? "0." + new string('0', decimals) : "0.##", c);
            case StatFormat.Multiplier: return sign + value.ToString("0.0#", c) + "x";
            default: return Format(value);
        }
    }
}

// everything the character select screen needs in one place: the panel rows in order, the
// characters, and the global upgrades whose ranks feed the stat sheet. it lives in Resources so
// the game scene can find it without a reference
[CreateAssetMenu(menuName = "Rogue/Stat Catalog", fileName = "StatCatalog")]
public class StatCatalog : ScriptableObject
{
    public const string ResourcePath = "StatCatalog";

    [Tooltip("header text for each group, in StatGroup order")]
    public string[] groupLabels = { "Body", "Martial", "Fortune", "Strategy" };

    [Tooltip("panel rows, top to bottom")]
    public List<StatDef> stats = new List<StatDef>();

    [Tooltip("select screen order. the first one plays when nothing has been picked")]
    public List<CharacterData> characters = new List<CharacterData>();

    public List<GlobalUpgradeDef> globalUpgrades = new List<GlobalUpgradeDef>();

    public CharacterData DefaultCharacter => characters.Count > 0 ? characters[0] : null;

    public string GroupLabel(StatGroup group) =>
        (int)group < groupLabels.Length ? groupLabels[(int)group] : group.ToString();

    public StatDef Get(StatId id)
    {
        foreach (var s in stats)
            if (s != null && s.id == id) return s;
        return null;
    }

    // the rows the panel should draw, in order
    public IEnumerable<StatDef> VisibleStats()
    {
        foreach (var s in stats)
            if (s != null && s.showInPanel) yield return s;
    }

    private static StatCatalog cached;

    // the asset from Resources. if it's missing this hands back an empty catalog, which leaves
    // every stat exactly as the scene sets it, so a run never breaks over a missing file
    public static StatCatalog Load()
    {
        if (cached != null) return cached;

        cached = Resources.Load<StatCatalog>(ResourcePath);
        if (cached == null)
        {
            Debug.LogWarning("no StatCatalog found in a Resources folder, stats stay at their scene values");
            cached = CreateInstance<StatCatalog>();
        }
        return cached;
    }
}
