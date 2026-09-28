using System;
using System.Collections.Generic;
using UnityEngine;

// Feijian, the flying swords, and their evolution, the Sovereign Blade Array (Imperial Sword Cage).
// the feel is Terraria's Empress of Light: blades appear round the player and snap away at full
// speed with no warning, curve through the air onto the thickest part of the horde and ricochet
// off the screen's edges faster every time
[CreateAssetMenu(menuName = "Rogue/Weapons/Flying Sword", fileName = "FlyingSword")]
public class FlyingSwordData : WeaponData<FlyingSword>
{
    public override AttackClass AttackClass => AttackClass.Physical;

    [Serializable]
    public struct LevelStats
    {
        [Tooltip("damage to each enemy a blade passes through, before Might")]
        public float damage;
        [Tooltip("blades per launch")]
        [Min(1)] public int blades;
        [Tooltip("bounces off the screen's edges (and strong enemies) before it's spent")]
        [Min(0)] public int bounces;
        [Tooltip("seconds between launches, before Cooldown")]
        public float cooldown;
    }

    [Header("Levels (the first entry is the unlock; one more pick after the last is the evolution)")]
    public LevelStats[] levels =
    {
        new LevelStats { damage = 12f, blades = 1, bounces = 2, cooldown = 2f },
        new LevelStats { damage = 12f, blades = 1, bounces = 3, cooldown = 2f },
        new LevelStats { damage = 13f, blades = 2, bounces = 3, cooldown = 2f },
        new LevelStats { damage = 14f, blades = 2, bounces = 4, cooldown = 2f },
        new LevelStats { damage = 15f, blades = 3, bounces = 4, cooldown = 2f },
        new LevelStats { damage = 16f, blades = 3, bounces = 5, cooldown = 2f },
        new LevelStats { damage = 18f, blades = 4, bounces = 5, cooldown = 2f },
        new LevelStats { damage = 20f, blades = 5, bounces = 5, cooldown = 2f },
    };

    [Header("Launch")]
    [Tooltip("how far from the player the blades appear, in a ring round them")]
    public float launchRing = 0.8f;
    [Tooltip("speed the instant it's launched, before Weapon Speed: no wind up, it snaps away")]
    public float speed = 9f;
    [Tooltip("evolved: degrees each blade leaves off to the side of its target, so it swings round onto it in an arc")]
    public float launchSwing = 55f;

    [Header("Flight")]
    [Tooltip("evolved: degrees a second a blade turns onto the crowd it's after: lower is a wider arc. unevolved blades don't seek")]
    public float turnRate = 300f;
    [Tooltip("how close together enemies have to be to count as a crowd worth curving onto")]
    public float clusterRadius = 2.2f;
    [Tooltip("how far a blade looks for a crowd")]
    public float range = 14f;
    [Tooltip("how much faster it gets off every bounce: 0.22 = 22%")]
    [Min(0f)] public float bounceAcceleration = 0.15f;
    [Tooltip("the fastest a blade gets, however many times it bounces, before Weapon Speed. the master blades fly at it")]
    public float maxSpeed = 22f;
    [Tooltip("extra damage for every bounce it has done: 0.12 = +12% a bounce")]
    [Min(0f)] public float bonusPerBounce = 0.12f;
    [Tooltip("radius it hits in, before Area. it goes straight through ordinary enemies")]
    public float hitRadius = 0.4f;
    [Tooltip("elites and bosses are solid: a blade ricochets off them like off a wall, using a bounce")]
    public bool bounceOffStrong = true;
    [Tooltip("seconds before the same blade can hurt the same enemy again")]
    public float rehit = 0.3f;

    [Header("Trail (the luminous streak that shows each blade's arc)")]
    [Tooltip("seconds of path the streak shows")]
    public float trailSeconds = 0.28f;
    [Tooltip("its width at the blade, before Area")]
    public float trailWidth = 0.22f;
    public Color trailHead = new Color(1f, 1f, 1f, 1f);
    public Color trailBody = new Color(0.33f, 0.85f, 0.6f, 0.85f);
    [Tooltip("unlit, so it glows the same everywhere. empty uses the blade's own material")]
    public Material trailMaterial;
    [Tooltip("crisp copies of the blade left along the streak")]
    [Min(0)] public int trailLength = 2;
    public float trailSpacing = 0.35f;

    [Header("Evolution: the Sovereign Blade Array")]
    [Tooltip("seconds between arrays, before Cooldown")]
    public float cageCooldown = 4.5f;
    [Tooltip("pairs of master blades per array")]
    [Min(1)] public int pairs = 1;
    [Tooltip("bounces a master blade makes before it anchors in the screen's border")]
    [Min(0)] public int cageBounces = 3;
    [Tooltip("the ordinary blades keep flying once it has evolved")]
    public bool evolvedKeepsBlades = true;
    [Tooltip("seconds the tripwire between an anchored pair lasts, before both blades shatter")]
    public float laserSeconds = 3f;
    [Tooltip("damage of the tripwire to each enemy in it, before Might, every Laser Tick")]
    public float laserDamage = 8f;
    public float laserTick = 0.2f;
    [Tooltip("the tripwire's width, before Area")]
    public float laserWidth = 0.8f;
    [Tooltip("seconds an anchored blade waits for its partner before it gives up and shatters")]
    public float partnerWait = 4f;
    [Tooltip("sparks crackling along the tripwire a second")]
    public float arcsPerSecond = 14f;

    [Header("Art (NewSprites/Asesprites/VFX/Weapons/FlyingSword; Tools > VFX > Build Weapon FX sets these. placeholder shapes until then)")]
    [Tooltip("a blade in flight, pointing right, looping")]
    public Sprite[] bladeFrames = Array.Empty<Sprite>();
    [Min(0.01f)] public float bladeFps = 16f;
    [Tooltip("a blade anchored in the screen's edge, pointing right (into the edge), looping")]
    public Sprite[] embedFrames = Array.Empty<Sprite>();
    [Tooltip("art pixels from the Embed Frames' centre to the blade's tip, which sits on the edge")]
    public float embedTipPixels = 14.5f;
    [Tooltip("the tripwire: one tile repeated along it")]
    public Sprite[] laserFrames = Array.Empty<Sprite>();
    [Min(0.01f)] public float laserFps = 25f;
    [Tooltip("the telekinetic snap where a blade appears, pointing the way it leaves")]
    public GameObject launchFx;
    [Tooltip("played where a blade ricochets, and crackling along the tripwire")]
    public GameObject sparkFx;
    [Tooltip("an anchored blade shattering once its tripwire is done")]
    public GameObject shatterFx;
    public Color bladeColor = new Color(0.45f, 1f, 0.7f);
    public string sortingLayer = "Aura";
    public int sortingOrder = 6;

    public bool Animated => bladeFrames != null && bladeFrames.Length > 0 && bladeFrames[0] != null;

    // the level table plus the evolution
    public override int LevelCount => levels.Length + 1;
    public override int EvolutionLevel => levels.Length + 1;

    public bool IsEvolved(int level) => level > levels.Length;

    public LevelStats At(int level) =>
        levels == null || levels.Length == 0 ? default : levels[Mathf.Clamp(level - 1, 0, levels.Length - 1)];

    public override void Attributes(int level, StatContext st, List<Attribute> into)
    {
        var s = At(level);
        Add(into, "Blades", s.blades.ToString());
        Add(into, "Bounces", s.bounces.ToString());
        Add(into, "Damage", N(Hurt(st, s.damage, AttackClass)));
        Add(into, "Faster per bounce", $"+{bounceAcceleration * 100f:0}%");
        Add(into, "Cooldown", Sec(Every(st, s.cooldown)));
        if (IsEvolved(level))
        {
            Add(into, "Tripwire damage", $"{N(Hurt(st, laserDamage, AttackClass))} / {Sec(laserTick)}");
            Add(into, "Tripwire lasts", Sec(laserSeconds));
            Add(into, "Array every", Sec(Every(st, cageCooldown)));
        }
    }

    public override string Describe(int level)
    {
        if (IsEvolved(level))
            return $"Evolution: the blades seek out the thickest crowds. Every {cageCooldown:0.#}s master blades launch at top speed and anchor in the screen's borders; a tripwire laser burns between each pair for {laserSeconds:0}s, then they shatter.";

        var s = At(level);
        if (level <= 1)
            return $"Blades snap away from you in every direction and ricochet off the screen's edges {s.bounces} times, faster every bounce. {s.damage:0} to everything they pass through.";

        var was = At(level - 1);
        var changes = new List<string>();
        if (s.blades > was.blades) changes.Add(s.blades - was.blades == 1 ? "+1 blade." : $"+{s.blades - was.blades} blades.");
        if (s.bounces > was.bounces) changes.Add($"{s.bounces} bounces.");
        if (s.damage > was.damage) changes.Add($"Hits for {s.damage:0}.");
        if (s.cooldown < was.cooldown) changes.Add($"Every {s.cooldown:0.#}s.");
        return changes.Count > 0 ? string.Join(" ", changes) : "Keener blades.";
    }
}
