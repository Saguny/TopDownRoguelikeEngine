using UnityEngine;

// one item in the global upgrade shop. every rank bought adds perRank to its stat in every run
[CreateAssetMenu(menuName = "Rogue/Global Upgrade", fileName = "GlobalUpgrade")]
public class GlobalUpgradeDef : ScriptableObject
{
    [Tooltip("saves are keyed by this, so don't change it once players have bought ranks")]
    public string id;
    public string title;
    public Sprite icon;
    [Tooltip("shop text. {0} becomes the amount per level (e.g. +20, +5%), {1} the max level. empty = written from the stat")]
    [TextArea] public string description;

    [Header("Effect")]
    public StatId stat;
    [Tooltip("added to the stat per rank. percent stats use fractions: 0.05 = +5%")]
    public float perRank = 0.05f;
    [Min(1)] public int maxRank = 5;

    [Header("Cost")]
    [Tooltip("price of the first rank; rank n costs this times n to the power 1.5 (rounded to 50)")]
    [Min(0)] public int baseCost = 100;

    // cheap to start, steep to finish: a run that ends at 15:00 (a few envelopes, some 2,500
    // coins) buys a first rank or two; maxing one takes a good many full runs
    public const float Steepness = 1.5f;
    public int CostOfRank(int rank) => Mathf.RoundToInt(baseCost * Mathf.Pow(Mathf.Max(1, rank), Steepness) / 50f) * 50;

    // the amount one rank adds, as the stat panel writes it: +20, +5%, -5%, +0.05x
    public string AmountPerRank(StatCatalog catalog)
    {
        var def = catalog != null ? catalog.Get(stat) : null;
        return def != null ? def.FormatBonus(perRank) : perRank.ToString("+0.##;-0.##");
    }

    public string Describe(StatCatalog catalog)
    {
        if (!string.IsNullOrWhiteSpace(description))
            return description.Contains("{") ? string.Format(description, AmountPerRank(catalog), maxRank) : description;

        var def = catalog != null ? catalog.Get(stat) : null;
        string label = def != null && !string.IsNullOrEmpty(def.label) ? def.label : stat.ToString();
        return $"{label} {AmountPerRank(catalog)} per level.";
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(id)) id = name.ToLowerInvariant();
    }
#endif
}
