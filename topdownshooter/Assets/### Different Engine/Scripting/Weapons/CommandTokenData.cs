using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Rogue/Weapons/Command Token", fileName = "CommandToken")]
public class CommandTokenData : WeaponData<CommandToken>
{
    public override AttackClass AttackClass => AttackClass.Magical;

    [Serializable]
    public struct LevelStats
    {
        [Tooltip("damage to every enemy on screen")]
        public float damage;
        [Tooltip("seconds between shockwaves, before Cooldown")]
        public float cooldown;
        [Tooltip("seconds the enemies it doesn't kill are stunned for. 0 = none")]
        public float stunSeconds;
        [Tooltip("the shockwave also pulls every piece of qi on the map to the player")]
        public bool pullsWen;
        [Tooltip("on top of the damage, this share of each ordinary enemy's max health (not bosses or elites), so a late level still clears a tough horde. 0 = none")]
        [Range(0f, 1f)] public float healthShare;
    }

    // a screen clear, so its levels buy utility rather than damage: flat damage fades from a wipe
    // to a softener as enemy health grows, while the stun and the qi pull stay useful. the late
    // levels (an ability, it's still offered with every slot full) cut a share of each ordinary
    // enemy's health, so it keeps up with the horde however tough it gets
    [Header("Levels (the first entry is the unlock)")]
    public LevelStats[] levels =
    {
        new LevelStats { damage = 40, cooldown = 60f },
        new LevelStats { damage = 40, cooldown = 50f },
        new LevelStats { damage = 40, cooldown = 50f, stunSeconds = 1.5f },
        new LevelStats { damage = 40, cooldown = 50f, stunSeconds = 1.5f, pullsWen = true },
        new LevelStats { damage = 40, cooldown = 45f, stunSeconds = 1.5f, pullsWen = true, healthShare = 0.25f },
        new LevelStats { damage = 40, cooldown = 45f, stunSeconds = 2f, pullsWen = true, healthShare = 0.4f },
        new LevelStats { damage = 40, cooldown = 40f, stunSeconds = 2f, pullsWen = true, healthShare = 0.55f },
        new LevelStats { damage = 40, cooldown = 35f, stunSeconds = 2.5f, pullsWen = true, healthShare = 0.7f },
    };

    [Tooltip("seconds after it's picked before it's first ready")]
    public float firstShotDelay = 2f;

    [Header("Use")]
    [Tooltip("the key that sets it off once it has charged")]
    public KeyCode activationKey = KeyCode.E;
    [Tooltip("seconds nothing new spawns after the shockwave hits")]
    [Min(0f)] public float spawnPauseSeconds = 1.5f;

    [Header("Your own animation (replaces everything below)")]
    [Tooltip("a prefab animating the whole cast, made with a CastAnimation on its root. it spawns at the centre of the screen and moves with the camera, and damage lands on its Hit event. empty = the built in seals and shockwave below")]
    public CastAnimation castAnimation;

    [Header("Seals (placeholder squares in this colour until set)")]
    [Tooltip("stamped left to right, one sprite per seal; empty slots reuse the ones that are set")]
    public Sprite[] sealSprites = new Sprite[3];
    [Min(1)] public int sealCount = 3;
    [Tooltip("world size of a seal once it has landed")]
    public float sealSize = 1.6f;
    [Tooltip("distance between the centres of neighbouring seals")]
    public float sealSpacing = 1.9f;
    [Tooltip("height of the row on screen: 0 is the bottom edge, 1 the top")]
    [Range(0f, 1f)] public float sealRowHeight = 0.62f;
    [Tooltip("how many times bigger a seal starts before it shrinks into place")]
    public float stampStartScale = 2.5f;
    [Tooltip("seconds a seal takes to shrink into place")]
    public float stampSeconds = 0.18f;
    [Tooltip("seconds between one seal landing and the next one starting")]
    public float stampGap = 0.15f;
    [Tooltip("screen shake as each seal lands")]
    public float stampShake = 0.08f;
    [Tooltip("seconds the row holds before it explodes")]
    public float holdSeconds = 0.3f;
    public Color sealColor = new Color(0.85f, 0.15f, 0.12f);

    [Header("Explosion (placeholder ring in this colour until set)")]
    [Tooltip("seconds the seals take to flare up and fade as the shockwave leaves them")]
    public float explodeSeconds = 0.25f;
    public Sprite shockwaveSprite;
    [Tooltip("seconds the shockwave takes to cross the screen")]
    public float shockwaveSeconds = 0.6f;
    public Color shockwaveColor = new Color(0.62f, 0.3f, 1f);
    [Tooltip("screen shake when the seals explode")]
    public float shake = 0.3f;
    [Tooltip("optional: a particle effect or animation of your own, spawned where the seals explode")]
    public GameObject explosionFx;
    [Tooltip("seconds before that effect is removed")]
    public float explosionFxSeconds = 2f;

    [Header("Sorting")]
    public string sortingLayer = "HUD";
    public int sortingOrder = 50;

    public override int LevelCount => levels.Length;

    // an ability, not a weapon: it fills no slot, so a full row of weapons doesn't keep it out of
    // the level up
    public override UpgradeCategory Category => UpgradeCategory.Ability;
    public override string CategoryLabel => "Ability";
    public override bool TakesSlot => false;

    public LevelStats At(int level) =>
        levels == null || levels.Length == 0 ? default : levels[Mathf.Clamp(level - 1, 0, levels.Length - 1)];

    public override void Attributes(int level, StatContext st, List<Attribute> into)
    {
        var s = At(level);
        Add(into, "Key", activationKey.ToString());
        Add(into, "Damage (whole screen)", N(Hurt(st, s.damage, AttackClass)));
        Add(into, "Recharge", Sec(Every(st, s.cooldown)));
        if (s.stunSeconds > 0f) Add(into, "Stun", Sec(s.stunSeconds));
        if (s.pullsWen) Add(into, "Pulls qi", "yes");
        if (s.healthShare > 0f) Add(into, "Cuts (ordinary enemies)", $"{s.healthShare * 100f:0}% of max health");
        Add(into, "Spawns paused for", Sec(spawnPauseSeconds));
    }

    public override string Describe(int level)
    {
        var s = At(level);
        if (level <= 1) return $"Press {activationKey} to send a shockwave through every enemy on screen for {s.damage:0}. Recharges in {s.cooldown:0}s.";

        var was = At(level - 1);
        var changes = new List<string>();
        if (s.damage != was.damage) changes.Add($"The shockwave hits for {s.damage:0}.");
        if (s.cooldown != was.cooldown) changes.Add($"Recharges in {s.cooldown:0}s.");
        if (s.stunSeconds > was.stunSeconds) changes.Add($"Enemies it doesn't kill are stunned for {s.stunSeconds:0.#}s.");
        if (s.pullsWen && !was.pullsWen) changes.Add("It pulls every piece of qi on the map to you.");
        if (s.healthShare > was.healthShare) changes.Add($"It also cuts {s.healthShare * 100f:0}% of every ordinary enemy's max health.");
        return changes.Count > 0 ? string.Join(" ", changes) : "A stronger shockwave.";
    }
}
