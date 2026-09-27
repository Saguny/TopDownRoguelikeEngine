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
