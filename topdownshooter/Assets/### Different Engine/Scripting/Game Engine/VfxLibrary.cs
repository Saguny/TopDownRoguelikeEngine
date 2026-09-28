using UnityEngine;

// art the game's systems draw without a prefab of their own: an enemy dying (with and without
// blood) and the three kinds of wen. lives in Resources so anything can find it; Tools > VFX >
// Build Weapon FX fills it in
[CreateAssetMenu(menuName = "Rogue/VFX Library", fileName = "VfxLibrary")]
public class VfxLibrary : ScriptableObject
{
    [Header("An enemy dying")]
    public Sprite[] enemyDeath = new Sprite[0];
    public Sprite[] enemyDeathBloodless = new Sprite[0];
    [Min(1f)] public float deathFps = 28f;

    [Header("Wen by worth, like Vampire Survivors' blue, green and red gems")]
    public GameObject wenBronze;
    public GameObject wenJade;
    public GameObject wenEnvelope;
    [Tooltip("wen worth up to this drops as bronze")]
    [Min(1)] public int bronzeUpTo = 2;
    [Tooltip("wen worth up to this drops as jade; anything more is a red envelope")]
    [Min(1)] public int jadeUpTo = 9;
    [Tooltip("with this many wen on the ground, new wen goes into one red envelope that keeps growing until it's picked up")]
    [Min(1)] public int maxOnGround = 400;

    [Header("The loading screen (LoadingAnimation), the weapons' own art")]
    public Sprite[] loadingSword = new Sprite[0];
    public Sprite[] loadingStar = new Sprite[0];
    public Sprite[] loadingTalisman = new Sprite[0];
    public Sprite[] loadingMeteor = new Sprite[0];
    public Sprite[] loadingArrow = new Sprite[0];
    public Sprite[] loadingRing = new Sprite[0];

    [Header("Locked")]
    [Tooltip("drawn over whatever hasn't been earned or bought yet")]
    public Sprite lockIcon;

    [Header("The end of a normal run (RunTimeLimit)")]
    public GameObject wuchangBai;
    public GameObject wuchangHei;

    [Header("Wen turning over, for the level up's rain (one texture)")]
    public Sprite[] wenSpinBronze = new Sprite[0];
    public Sprite[] wenSpinJade = new Sprite[0];

    [Header("The fortune envelope on the ground (FortuneEnvelope, EnvelopeCarrier)")]
    public Sprite[] envelope = new Sprite[0];
    public Sprite[] envelopeGlow = new Sprite[0];
    [Tooltip("its beam of light; the sprite's foot is the bottom of its canvas")]
    public Sprite[] envelopePillar = new Sprite[0];
    public Sprite[] envelopeMarker = new Sprite[0];
    [Tooltip("over an elite or boss that carries one")]
    public Sprite[] envelopeCarry = new Sprite[0];
    public Sprite[] envelopePickup = new Sprite[0];

    [Header("Opening it (EnvelopeOpening)")]
    [Tooltip("closed, its seal charging up")]
    public Sprite[] envelopeBig = new Sprite[0];
    [Tooltip("its flap lifting and the light pouring out")]
    public Sprite[] envelopeFlap = new Sprite[0];
    [Tooltip("its front, laid over the scroll as it rises out")]
    public Sprite envelopeFront;
    [Tooltip("greyscale light, tinted by rarity")]
    public Sprite aura;
    public Sprite rays;
    public Sprite[] mote = new Sprite[0];
    public Sprite[] burstCommon = new Sprite[0];
    public Sprite[] burstRare = new Sprite[0];
    public Sprite[] burstLegendary = new Sprite[0];
    public Sprite scrollRoller;
    public Sprite scroll;
    public Sprite rewardSlot;
    public Sprite[] rewardSlotEvolution = new Sprite[0];
    [Tooltip("greyscale ribbon, tinted by rarity")]
    public Sprite banner;
    public Sprite[] coinIcon = new Sprite[0];
    public Sprite[] peachIcon = new Sprite[0];

    [Header("Opening it: sound (optional)")]
    public AudioClip envelopePickupSound;
    [Tooltip("looped while it waits to be clicked open")]
    public AudioClip envelopeIdle;
    public AudioClip envelopeClick;
    [Tooltip("the build before the burst, one per rarity, as long as that rarity's charge")]
    public AudioClip chargeCommon;
    public AudioClip chargeRare;
    public AudioClip chargeLegendary;
    public AudioClip envelopeShake;
    [Tooltip("the light turning up a rarity, pitched up for legendary")]
    public AudioClip envelopeTier;
    public AudioClip envelopeOpen;
    public AudioClip revealCommon;
    public AudioClip revealRare;
    public AudioClip revealLegendary;
    public AudioClip rewardPop;

    [Header("The Wuchang taking the player at the end of a normal run (Wuchang)")]
    [Tooltip("a chain link lying flat, then one edge on")]
    public Sprite[] chainLink = new Sprite[0];
    public Sprite chainHook;
    public Sprite[] soul = new Sprite[0];
    public Sprite[] maw = new Sprite[0];
    [Tooltip("ink flooding the screen before the results, the screen's shape")]
    public Sprite[] inkWipe = new Sprite[0];
    public AudioClip chainSound;
    public AudioClip swallowSound;

    private static VfxLibrary cached;
    private static bool looked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        cached = null;
        looked = false;
    }

    public static VfxLibrary Get
    {
        get
        {
            if (!looked)
            {
                cached = Resources.Load<VfxLibrary>("VfxLibrary");
                looked = true;
            }
            return cached;
        }
    }
}
