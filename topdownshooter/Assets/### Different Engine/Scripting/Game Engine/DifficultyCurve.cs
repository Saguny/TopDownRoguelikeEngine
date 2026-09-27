using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "Rogue/DifficultyCurve")]
public class DifficultyCurve : ScriptableObject
{
    [SerializeField, HideInInspector] private AnimationCurve spawnDensityVisual;
    public AnimationCurve enemyHealth = AnimationCurve.Linear(0, 1, 1800, 12);
    public AnimationCurve enemySpeed = AnimationCurve.Linear(0, 1, 1800, 2);
    public AnimationCurve enemyDamage = AnimationCurve.Linear(0, 1, 1800, 4);

    [Header("Spawn Density Parameters")]
    [SerializeField] private float spawnDensityMax = 6f;
    [SerializeField] private float spawnDensityPlateauTime = 900f;
    [SerializeField] private float spawnDensityCurveSharpness = 3f;
    [SerializeField] private int curveSamplePoints = 32;

    // the authored curves clamp after their last key, which froze every enemy stat at around
    // 42 minutes while the player kept levelling. in endless mode, past the last key, health and
    // damage keep growing from wherever the curve ended. normal mode keeps the authored clamp. speed and density stay capped on purpose: faster
    // than the player is unwinnable rather than hard, and unbounded density is a frame rate wall
    [Header("Endless (past the last key of each curve)")]
    [Tooltip("minutes for enemy health to double once the authored curve runs out")]
    [Min(0.5f)] public float endlessHealthDoublingMinutes = 4f;
    [Tooltip("minutes for enemy damage to double once the authored curve runs out")]
    [Min(0.5f)] public float endlessDamageDoublingMinutes = 6f;

    [Header("Level -> Wen Needed")]
    [FormerlySerializedAs("gearsNeeded")]
    public AnimationCurve wenNeeded = new AnimationCurve(
        new Keyframe(1, 5),
        new Keyframe(10, 25),
        new Keyframe(20, 45)
    );

#if UNITY_EDITOR
    [SerializeField]
    private AnimationCurve spawnDensityPreview = new AnimationCurve();
#endif

    // the running map's curve, or the given one when the map has none
    public static DifficultyCurve For(DifficultyCurve fallback) =>
        Playfield.Active != null && Playfield.Active.difficulty != null ? Playfield.Active.difficulty : fallback;

    public float DensityAt(float t)
    {
        if (t <= 0f) return 1f;
        if (t >= spawnDensityPlateauTime) return spawnDensityMax;

        float x = t / spawnDensityPlateauTime;
        float k = spawnDensityCurveSharpness;
        float num = 1f - Mathf.Exp(-k * x);
        float den = 1f - Mathf.Exp(-k);
        float f = num / den;
        float value = 1f + (spawnDensityMax - 1f) * f;
        return Mathf.Max(0.1f, value);
    }

    public float HealthAt(float t) => Mathf.Max(0.5f, Endless(enemyHealth, t, endlessHealthDoublingMinutes));
    public float SpeedAt(float t) => Mathf.Max(0.5f, enemySpeed.Evaluate(t));
    public float DamageAt(float t) => Mathf.Max(0.5f, Endless(enemyDamage, t, endlessDamageDoublingMinutes));

    private static float Endless(AnimationCurve curve, float t, float doublingMinutes)
    {
        if (curve == null || curve.length == 0) return 1f;

        var last = curve[curve.length - 1];
        if (t <= last.time || !GameMode.IsEndless) return curve.Evaluate(t);

        // capped at 2^100 so a run left open for a day can't overflow float into infinity or NaN
        float doublings = Mathf.Min((t - last.time) / (doublingMinutes * 60f), 100f);
        return last.value * Mathf.Pow(2f, doublings);
    }

    public int WenForLevel(int level)
    {
        if (wenNeeded == null || wenNeeded.length == 0)
            return 5;

        float x = Mathf.Max(1, level);
        float y = wenNeeded.Evaluate(x);
        return Mathf.Max(1, Mathf.RoundToInt(y));
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (spawnDensityPreview == null)
            spawnDensityPreview = new AnimationCurve();

        spawnDensityPreview.keys = new Keyframe[0];

        for (int i = 0; i <= curveSamplePoints; i++)
        {
            float t = Mathf.Lerp(0f, spawnDensityPlateauTime, i / (float)curveSamplePoints);
            float y = DensityAt(t);
            spawnDensityPreview.AddKey(t, y);
        }

        spawnDensityPreview.AddKey(spawnDensityPlateauTime * 1.2f, spawnDensityMax);
    }
#endif
}
