using System;
using System.Collections.Generic;
using UnityEngine;

// the Treasure Gourd (Bao Hulu), the spirit gourd Taoist alchemists trap evil spirits in, and its
// evolution, the Gourd of Heaven and Earth (Qiankun Hulu). every so often it's uncorked the way
// the player is walking: it pulls the small enemies in front of it together, then sprays a cone of
// holy fire over the clump that sets it burning. evolved, it swallows enemy bullets as well, and
// what it drew in comes back out as a plasma sphere that bursts on the first thing it meets
[CreateAssetMenu(menuName = "Rogue/Weapons/Treasure Gourd", fileName = "TreasureGourd")]
public class TreasureGourdData : WeaponData<TreasureGourd>
{
    public override AttackClass AttackClass => AttackClass.Magical;

    [Serializable]
    public struct LevelStats
    {
        [Tooltip("damage of the fire to each enemy it washes over, before Might")]
        public float damage;
        [Tooltip("how far in front of the mouth it pulls, before Area")]
        public float suctionRadius;
        [Tooltip("how far the fire reaches, before Area")]
        public float flameLength;
        [Tooltip("damage a second of the holy fire an enemy is left burning with, before Might")]
        public float burnDps;
        [Tooltip("seconds an enemy burns for")]
        public float burnSeconds;
        [Tooltip("seconds between uncorkings, before Cooldown")]
        public float cooldown;
    }

    [Header("Levels (the first entry is the unlock; one more pick after the last is the evolution)")]
    public LevelStats[] levels =
    {
        new LevelStats { damage = 10f, suctionRadius = 2.6f, flameLength = 2.6f, burnDps = 6f, burnSeconds = 2f, cooldown = 5f },
        new LevelStats { damage = 10f, suctionRadius = 2.9f, flameLength = 2.6f, burnDps = 6f, burnSeconds = 2.5f, cooldown = 5f },
        new LevelStats { damage = 13f, suctionRadius = 2.9f, flameLength = 3.0f, burnDps = 6f, burnSeconds = 2.5f, cooldown = 5f },
        new LevelStats { damage = 13f, suctionRadius = 3.2f, flameLength = 3.0f, burnDps = 8f, burnSeconds = 3f, cooldown = 5f },
        new LevelStats { damage = 16f, suctionRadius = 3.2f, flameLength = 3.4f, burnDps = 8f, burnSeconds = 3f, cooldown = 4.5f },
        new LevelStats { damage = 16f, suctionRadius = 3.6f, flameLength = 3.4f, burnDps = 8f, burnSeconds = 3.5f, cooldown = 4.5f },
        new LevelStats { damage = 19f, suctionRadius = 3.6f, flameLength = 3.8f, burnDps = 10f, burnSeconds = 3.5f, cooldown = 4.5f },
        new LevelStats { damage = 22f, suctionRadius = 4.0f, flameLength = 4.2f, burnDps = 10f, burnSeconds = 4f, cooldown = 4f },
    };

    [Header("The pull")]
    [Tooltip("seconds it pulls before it sprays")]
    public float suctionSeconds = 1.5f;
    [Tooltip("degrees either side of the way it's aimed that it pulls from (the art is drawn for 36)")]
    public float suctionHalfAngle = 36f;
    [Tooltip("how fast it drags enemies in, units a second")]
    public float pullSpeed = 5f;
    [Tooltip("where in front of the mouth it gathers them, units")]
    public float gatherDistance = 1.3f;
    [Tooltip("how much slower enemies walk while it has hold of them, so their own feet don't fight it")]
    [Range(0f, 1f)] public float heldSlow = 0.25f;
    [Tooltip("degrees a second it turns to follow the way the player walks while it's open")]
    public float turnRate = 150f;

    [Header("The fire")]
    [Tooltip("seconds it sprays for")]
    public float spraySeconds = 0.75f;
    [Tooltip("degrees either side of the way it's aimed the fire covers (the art spreads about 16)")]
    public float flameHalfAngle = 17f;
    [Tooltip("seconds between a burning enemy's ticks")]
    public float burnTick = 0.5f;
    [Tooltip("the most burning enemies shown with a flame on them at once; more still burn")]
    [Min(0)] public int maxBurnVisuals = 60;

    [Header("Evolution: the Gourd of Heaven and Earth")]
    [Tooltip("seconds it pulls (and swallows bullets) before it fires the sphere")]
    public float evolvedSuctionSeconds = 2f;
    [Tooltip("how fast bullets are drawn into the mouth, units a second")]
    public float bulletPullSpeed = 9f;
    [Tooltip("the sphere's damage with nothing swallowed, before Might")]
    public float sphereDamage = 60f;
    [Tooltip("extra damage for every bullet it swallowed")]
    public float damagePerBullet = 25f;
    [Tooltip("extra damage for every enemy it had hold of while it pulled")]
    public float damagePerEnemy = 8f;
    [Tooltip("the most swallowed things counted toward the sphere")]
    [Min(1)] public int maxCharge = 40;
    [Tooltip("the burst's radius with nothing swallowed, before Area")]
    public float blastRadius = 2.2f;
    [Tooltip("how much bigger the burst is at a full charge: 0.5 = half again")]
    public float blastGrowth = 0.5f;
    [Tooltip("units a second, before Weapon Speed")]
    public float sphereSpeed = 7f;
    [Tooltip("how far it flies before it bursts on its own")]
    public float sphereRange = 9f;
    [Tooltip("the sphere's size in flight with nothing swallowed, and at a full charge")]
    public Vector2 sphereScale = new Vector2(0.9f, 1.8f);

    [Header("Art (NewSprites/Asesprites/VFX/Weapons/TreasureGourd; Tools > VFX > Build Weapon FX sets these. placeholder shapes until then)")]
    [Tooltip("hovering at the player's shoulder, corked, looping")]
    public Sprite[] gourdFrames = Array.Empty<Sprite>();
    [Tooltip("uncorked and aimed, pointing right: the first half pulling, the second spraying")]
    public Sprite[] aimFrames = Array.Empty<Sprite>();
    [Tooltip("art pixels from the Aim Frames' centre to the mouth")]
    public float aimMouthPixels = 9.5f;
    public Sprite[] popFrames = Array.Empty<Sprite>();
    [Tooltip("the pull, pointing right, looping")]
    public Sprite[] suckFrames = Array.Empty<Sprite>();
    [Tooltip("art pixels from the Suck Frames' centre back to the mouth, and how long the pull is drawn")]
    public float suckMouthPixels = 46f;
    public float suckArtLength = 92f;
    [Tooltip("the fire, pointing right: 2 catching, 6 looping, 3 dying")]
    public Sprite[] flameFrames = Array.Empty<Sprite>();
    public float flameMouthPixels = 53f;
    public float flameArtLength = 104f;
    [Tooltip("on a burning enemy, looping")]
    public Sprite[] burnFrames = Array.Empty<Sprite>();
    [Tooltip("the plasma sphere, looping; also the charge gathering in the mouth")]
    public Sprite[] orbFrames = Array.Empty<Sprite>();
    [Tooltip("the sphere bursting, drawn for Blast Art Radius")]
    public Sprite[] blastFrames = Array.Empty<Sprite>();
    public float blastArtRadius = 60f / (37f / 1.3f);
    [Tooltip("a bullet swallowed at the mouth")]
    public Sprite[] absorbFrames = Array.Empty<Sprite>();
    [Min(0.1f)] public float fps = 16.7f;
    public string sortingLayer = "Aura";
    public int sortingOrder = 6;

    [Header("Sound (Tools/SFX/gourd.py)")]
    [Tooltip("its sounds are made at the game's reference loudness; this sets them in the mix")]
    [Range(0f, 1f)] public float soundVolume = 0.4f;
    public AudioClip uncorkSound;
    [Tooltip("the pull, as long as the Suction Seconds; played on a voice of its own")]
    public AudioClip pullSound;
    public AudioClip flameSound;
    [Tooltip("evolved: the pull and the charge building, as long as the Evolved Suction Seconds")]
    public AudioClip chargeSound;
    public AudioClip absorbSound;
    public AudioClip launchSound;
    public AudioClip blastSound;

    public bool Animated => gourdFrames != null && gourdFrames.Length > 0 && gourdFrames[0] != null;

    // the level table plus the evolution
    public override int LevelCount => levels.Length + 1;
    public override int EvolutionLevel => levels.Length + 1;

    public bool IsEvolved(int level) => level > levels.Length;

    public LevelStats At(int level) =>
        levels == null || levels.Length == 0 ? default : levels[Mathf.Clamp(level - 1, 0, levels.Length - 1)];

    public override void Attributes(int level, StatContext st, List<Attribute> into)
    {
        var s = At(level);
        Add(into, "Pull reach", N(Wide(st, s.suctionRadius)));
        Add(into, "Cooldown", Sec(Every(st, s.cooldown)));
        if (IsEvolved(level))
        {
            Add(into, "Sphere damage", N(Hurt(st, sphereDamage, AttackClass)));
            Add(into, "Per bullet swallowed", "+" + N(Hurt(st, damagePerBullet, AttackClass)));
            Add(into, "Per enemy held", "+" + N(Hurt(st, damagePerEnemy, AttackClass)));
            Add(into, "Burst radius", N(Wide(st, blastRadius)));
        }
        else
        {
            Add(into, "Fire damage", N(Hurt(st, s.damage, AttackClass)));
            Add(into, "Fire reach", N(Wide(st, s.flameLength)));
        }
        Add(into, "Burn", $"{N(Hurt(st, s.burnDps, AttackClass))}/s for {Sec(s.burnSeconds)}");
    }

    public override string Describe(int level)
    {
        if (IsEvolved(level))
            return $"Evolution: it swallows enemy bullets as well. After {evolvedSuctionSeconds:0.#}s it fires what it drew in back out as a plasma sphere that bursts on the first enemy it meets, harder for every bullet and enemy it caught.";

        var s = At(level);
        if (level <= 1)
            return $"Every {s.cooldown:0.#}s it's uncorked the way you walk, pulls the small enemies in front of it together for {suctionSeconds:0.#}s, then sprays holy fire over them for {s.damage:0}, leaving them burning.";

        var was = At(level - 1);
        var changes = new List<string>();
        if (s.suctionRadius > was.suctionRadius) changes.Add("Pulls from further away.");
        if (s.flameLength > was.flameLength) changes.Add("Longer fire.");
        if (s.burnSeconds > was.burnSeconds) changes.Add($"Burns for {s.burnSeconds:0.#}s.");
        if (s.burnDps > was.burnDps) changes.Add($"Burns for {s.burnDps:0} a second.");
        if (s.damage > was.damage) changes.Add($"Fire hits for {s.damage:0}.");
        if (s.cooldown < was.cooldown) changes.Add($"Every {s.cooldown:0.#}s.");
        return changes.Count > 0 ? string.Join(" ", changes) : "A hotter fire.";
    }
}
