using System.Collections.Generic;
using UnityEngine;

// a playable character: what the select screen shows and what the run starts with
[CreateAssetMenu(menuName = "Rogue/Character", fileName = "Character")]
public class CharacterData : ScriptableObject
{
    public string displayName = "Archer";
    public Sprite portrait;
    [TextArea] public string description;

    [Header("Look")]
    [Tooltip("an Animator Override Controller made from _PlayerAnim, with this character's idle and run clips. empty keeps the current player")]
    public RuntimeAnimatorController animations;

    [Header("Starting weapon")]
    [Tooltip("the weapon's upgrade asset: Bow, Electrical Aura, Meteorite or any weapon. empty means the bow")]
    public UpgradeData startingWeapon;

    [Header("Signature")]
    [Tooltip("how many times as hard the starting weapon hits, e.g. 2 for double damage")]
    [Min(0f)] public float signatureDamage = 1f;
    [Tooltip("how much the starting weapon's hits slow an enemy, 0.3 = 30% slower. Seven Star Swords: its stars")]
    [Range(0f, 0.95f)] public float signatureSlow;
    [Tooltip("how long that slow lasts, in seconds")]
    [Min(0f)] public float signatureSlowSeconds = 2f;

    [Header("Unlock")]
    [Tooltip("owned from the first time the game is played. the catalog's first character always is")]
    public bool startsUnlocked;

    [Header("Stat bonuses")]
    [Tooltip("added on top of each stat's base value. percent stats use fractions: 0.1 = +10%")]
    public List<StatBonus> bonuses = new List<StatBonus>();

    public float BonusFor(StatId id)
    {
        float total = 0f;
        foreach (var b in bonuses)
            if (b.stat == id) total += b.value;
        return total;
    }

    // for the panel footer
    public string StartingWeaponName() => startingWeapon != null ? startingWeapon.GetBaseTitle() : "Bow";

    // the signature perks as lines for the bonus list, e.g. "x2 Peach Talismans Damage"
    public IEnumerable<string> PerkLines()
    {
        if (Mathf.Abs(signatureDamage - 1f) > 0.001f)
            yield return $"x{signatureDamage:0.##} Weapon Damage";
        if (signatureSlow > 0.001f)
            yield return $"Hits slow {Mathf.RoundToInt(signatureSlow * 100f)}%";
    }
}
