using System;
using UnityEngine;

// every panel stat for one character: base value + character bonus + global upgrade ranks.
// the stat panel reads it and StatContext applies it at spawn, so the two can't disagree
public sealed class StatSheet
{
    public static readonly int Count = Enum.GetValues(typeof(StatId)).Length;

    public CharacterData Character { get; }
    public StatCatalog Catalog { get; }

    private readonly float[] baseValues = new float[Count];
    private readonly float[] values = new float[Count];

    private StatSheet(StatCatalog catalog, CharacterData character, GlobalUpgradeDef oneMoreRank = null)
    {
        Catalog = catalog;
        Character = character;

        foreach (var def in catalog.stats)
            if (def != null) baseValues[(int)def.id] = def.baseValue;
        Array.Copy(baseValues, values, Count);

        // bonuses on the same stat add up, from the character and from every rank bought
        if (character != null)
            foreach (var b in character.bonuses) values[(int)b.stat] += b.value;

        foreach (var u in catalog.globalUpgrades)
        {
            if (u == null) continue;
            int rank = MetaProgress.RankOf(u);
            if (u == oneMoreRank) rank = Mathf.Min(u.maxRank, rank + 1);
            values[(int)u.stat] += u.perRank * rank;
        }
    }

    // for the select screen: call it again whenever the highlighted character changes. with
    // oneMoreRank, it's the sheet as it would be after buying that upgrade's next rank
    public static StatSheet For(CharacterData character, StatCatalog catalog = null, GlobalUpgradeDef oneMoreRank = null) =>
        new StatSheet(catalog != null ? catalog : StatCatalog.Load(), character, oneMoreRank);

    // what a run starts with: the picked character, or the catalog's first one
    public static StatSheet ForRun()
    {
        var catalog = StatCatalog.Load();
        var character = CharacterSelection.Current != null ? CharacterSelection.Current : catalog.DefaultCharacter;
        // a character that hasn't been bought can't be played, whatever got picked
        if (character != null && !CharacterUnlocks.IsUnlocked(character)) character = catalog.DefaultCharacter;
        return new StatSheet(catalog, character);
    }

    // mid run: the sheet with every value the run has changed read off the player. with a pick,
    // as it would be after taking it (the level up screen's preview)
    public static StatSheet Live(StatContext stats, UpgradeData pick = null)
    {
        var sheet = ForRun();
        if (stats == null) return sheet;
        for (int i = 0; i < Count; i++)
            if (stats.TryLive((StatId)i, pick, out float v)) sheet.values[i] = v;
        return sheet;
    }

    public float this[StatId id] => values[(int)id];
    public float Base(StatId id) => baseValues[(int)id];
    public float Bonus(StatId id) => values[(int)id] - baseValues[(int)id];

    // the value as the panel shows it
    public string Text(StatId id)
    {
        var def = Catalog.Get(id);
        return def != null ? def.Format(this[id]) : this[id].ToString("0.##");
    }

    // 1 when bonuses make the stat better, -1 when worse, 0 when untouched. tint the value by it
    public int Direction(StatId id)
    {
        float bonus = Bonus(id);
        if (Mathf.Abs(bonus) < 0.0001f) return 0;

        var def = Catalog.Get(id);
        bool higherIsBetter = def == null || def.higherIsBetter;
        return (bonus > 0f) == higherIsBetter ? 1 : -1;
    }
}
