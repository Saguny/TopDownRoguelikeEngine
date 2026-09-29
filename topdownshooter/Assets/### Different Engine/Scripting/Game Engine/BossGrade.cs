using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// the light a boss fight is seen in: a colour grade over the whole world (URP post-processing, on
// top of the scene's own volumes) that shifts with the fight. the underworld's light as the boss
// comes (dimmer, harder, violet in the shadows and ember in the highlights, a crimson vignette,
// bloom on the fire and the bullets, a little grain); colder and more violet while a spell card is
// up; blood red and shaking in the boss's rage; drained of colour as it dies; gold as the run is
// won, fading back to the stage's own light. big moments punch through it: a flash of exposure,
// a lens bulge and colours splitting at the edges, red when the player is hit. every look is a
// volume of its own whose weight eases toward where the fight is, in real time, so hit stops and
// slow motion don't stall it. made on first use, gone with the scene
public class BossGrade : MonoBehaviour
{
    public enum Look { Normal, Fight, Spell, Rage, Dying, Triumph }

    private static BossGrade instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    // the one up now, if any
    public static BossGrade Current => instance;

    public static BossGrade Get()
    {
        if (instance == null && Application.isPlaying) instance = new GameObject("Boss Grade").AddComponent<BossGrade>();
        return instance;
    }

    private enum V { Fight, Spell, Rage, Dying, Triumph, Punch, Hurt }
    private const int Count = 7;

    private readonly Volume[] volumes = new Volume[Count];
    private readonly VolumeProfile[] profiles = new VolumeProfile[Count];
    private readonly float[] target = new float[Count];
    private float ease = 1f;

    private void Awake()
    {
        Make(V.Fight, 20, p =>
        {
            var c = p.Add<ColorAdjustments>();
            c.postExposure.Override(-0.3f);
            c.contrast.Override(18f);
            c.saturation.Override(-10f);
            c.colorFilter.Override(new Color(1f, 0.86f, 0.86f));
            var s = p.Add<SplitToning>();
            s.shadows.Override(new Color(0.35f, 0.1f, 0.5f));
            s.highlights.Override(new Color(1f, 0.62f, 0.4f));
            s.balance.Override(-15f);
            var v = p.Add<Vignette>();
            v.color.Override(new Color(0.2f, 0f, 0.05f));
            v.intensity.Override(0.4f);
            v.smoothness.Override(0.45f);
            var b = p.Add<Bloom>();
            b.intensity.Override(0.7f);
            b.threshold.Override(0.85f);
            b.scatter.Override(0.6f);
            b.tint.Override(new Color(1f, 0.55f, 0.45f));
            var g = p.Add<FilmGrain>();
            g.type.Override(FilmGrainLookup.Medium1);
            g.intensity.Override(0.15f);
            p.Add<ChromaticAberration>().intensity.Override(0.06f);
        });
        Make(V.Spell, 21, p =>
        {
            var c = p.Add<ColorAdjustments>();
            c.hueShift.Override(-8f);
            c.saturation.Override(-20f);
            c.colorFilter.Override(new Color(0.9f, 0.8f, 1f));
            var s = p.Add<SplitToning>();
            s.shadows.Override(new Color(0.25f, 0.05f, 0.55f));
            s.highlights.Override(new Color(0.9f, 0.55f, 1f));
            var v = p.Add<Vignette>();
            v.color.Override(new Color(0.12f, 0f, 0.2f));
            v.intensity.Override(0.5f);
            p.Add<Bloom>().intensity.Override(1.1f);
        });
        Make(V.Rage, 22, p =>
        {
            var c = p.Add<ColorAdjustments>();
            c.contrast.Override(35f);
            c.saturation.Override(10f);
            c.colorFilter.Override(new Color(1f, 0.7f, 0.68f));
            var s = p.Add<SplitToning>();
            s.shadows.Override(new Color(0.5f, 0f, 0.05f));
            s.highlights.Override(new Color(1f, 0.5f, 0.3f));
            s.balance.Override(-30f);
            var v = p.Add<Vignette>();
            v.color.Override(new Color(0.35f, 0f, 0.02f));
            v.intensity.Override(0.55f);
            p.Add<ChromaticAberration>().intensity.Override(0.18f);
            p.Add<FilmGrain>().intensity.Override(0.3f);
            p.Add<Bloom>().intensity.Override(1.3f);
        });
        Make(V.Dying, 23, p =>
        {
            var c = p.Add<ColorAdjustments>();
            c.saturation.Override(-85f);
            c.contrast.Override(30f);
            c.postExposure.Override(-0.2f);
            var v = p.Add<Vignette>();
            v.color.Override(Color.black);
            v.intensity.Override(0.5f);
            p.Add<ChromaticAberration>().intensity.Override(0.35f);
        });
        Make(V.Triumph, 24, p =>
        {
            var c = p.Add<ColorAdjustments>();
            c.postExposure.Override(0.35f);
            c.saturation.Override(15f);
            c.colorFilter.Override(new Color(1f, 0.93f, 0.78f));
            var s = p.Add<SplitToning>();
            s.shadows.Override(new Color(0.35f, 0.2f, 0.1f));
            s.highlights.Override(new Color(1f, 0.8f, 0.5f));
            var v = p.Add<Vignette>();
            v.color.Override(new Color(0.4f, 0.25f, 0.05f));
            v.intensity.Override(0.25f);
            var b = p.Add<Bloom>();
            b.intensity.Override(1.6f);
            b.threshold.Override(0.75f);
            b.tint.Override(new Color(1f, 0.85f, 0.6f));
        });
        // a big moment: a flash of exposure, the lens bulging, colours splitting at the edges
        Make(V.Punch, 30, p =>
        {
            p.Add<ColorAdjustments>().postExposure.Override(0.6f);
            p.Add<ChromaticAberration>().intensity.Override(1f);
            var l = p.Add<LensDistortion>();
            l.intensity.Override(-0.3f);
            l.scale.Override(1f);
        });
        // the player hit: the edges bleeding red
        Make(V.Hurt, 31, p =>
        {
            p.Add<ColorAdjustments>().colorFilter.Override(new Color(1f, 0.55f, 0.55f));
            p.Add<ChromaticAberration>().intensity.Override(0.7f);
            var v = p.Add<Vignette>();
            v.color.Override(new Color(0.6f, 0f, 0.02f));
            v.intensity.Override(0.5f);
        });
    }

    private void Make(V which, int priority, System.Action<VolumeProfile> build)
    {
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = "Boss " + which;
        build(profile);
        var go = new GameObject("Boss Grade " + which);
        go.transform.SetParent(transform, false);
        var v = go.AddComponent<Volume>();
        v.isGlobal = true;
        v.priority = priority;
        v.weight = 0f;
        v.sharedProfile = profile;
        volumes[(int)which] = v;
        profiles[(int)which] = profile;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        foreach (var p in profiles) if (p != null) Destroy(p);
    }

    // the underworld's light is the fire's by default (Yama's courts); Meng Po's is the mist's:
    // lilac in the shadows and moonlit azure in the highlights, a cold vignette, bloom tinted pale
    // blue, and her true form's rage jade and violet rather than blood
    public enum Theme { Fire, Mist }

    public void SetTheme(Theme theme)
    {
        bool mist = theme == Theme.Mist;
        void Tone(V which, Color filter, Color shadows, Color highlights, Color vignette, Color? bloom = null)
        {
            var p = profiles[(int)which];
            if (p == null) return;
            if (p.TryGet(out ColorAdjustments c)) c.colorFilter.Override(filter);
            if (p.TryGet(out SplitToning s)) { s.shadows.Override(shadows); s.highlights.Override(highlights); }
            if (p.TryGet(out Vignette v)) v.color.Override(vignette);
            if (bloom.HasValue && p.TryGet(out Bloom b)) b.tint.Override(bloom.Value);
        }
        if (mist)
        {
            Tone(V.Fight, new Color(0.86f, 0.9f, 1f), new Color(0.22f, 0.14f, 0.45f), new Color(0.7f, 0.86f, 1f), new Color(0.05f, 0.04f, 0.2f), new Color(0.6f, 0.78f, 1f));
            Tone(V.Spell, new Color(0.8f, 0.86f, 1f), new Color(0.2f, 0.08f, 0.5f), new Color(0.75f, 0.7f, 1f), new Color(0.08f, 0.02f, 0.22f));
            Tone(V.Rage, new Color(0.82f, 0.95f, 0.95f), new Color(0.28f, 0.02f, 0.42f), new Color(0.55f, 1f, 0.9f), new Color(0.12f, 0f, 0.25f));
        }
        else
        {
            Tone(V.Fight, new Color(1f, 0.86f, 0.86f), new Color(0.35f, 0.1f, 0.5f), new Color(1f, 0.62f, 0.4f), new Color(0.2f, 0f, 0.05f), new Color(1f, 0.55f, 0.45f));
            Tone(V.Spell, new Color(0.9f, 0.8f, 1f), new Color(0.25f, 0.05f, 0.55f), new Color(0.9f, 0.55f, 1f), new Color(0.12f, 0f, 0.2f));
            Tone(V.Rage, new Color(1f, 0.7f, 0.68f), new Color(0.5f, 0f, 0.05f), new Color(1f, 0.5f, 0.3f), new Color(0.35f, 0f, 0.02f));
        }
    }

    // eases into a look over `seconds`
    public void Set(Look look, float seconds = 1.5f)
    {
        ease = Mathf.Max(0.05f, seconds);
        for (int i = 0; i < (int)V.Punch; i++) target[i] = 0f;
        switch (look)
        {
            case Look.Fight: target[(int)V.Fight] = 1f; break;
            case Look.Spell: target[(int)V.Fight] = 1f; target[(int)V.Spell] = 1f; break;
            case Look.Rage: target[(int)V.Fight] = 1f; target[(int)V.Spell] = 0.5f; target[(int)V.Rage] = 1f; break;
            case Look.Dying: target[(int)V.Fight] = 1f; target[(int)V.Dying] = 1f; break;
            case Look.Triumph: target[(int)V.Triumph] = 1f; break;
        }
    }

    // a big moment punching through the grade, 0-1
    public void Punch(float strength)
    {
        var v = volumes[(int)V.Punch];
        if (v != null) v.weight = Mathf.Max(v.weight, Mathf.Clamp01(strength));
    }

    public void Hurt()
    {
        var v = volumes[(int)V.Hurt];
        if (v != null) v.weight = 1f;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        for (int i = 0; i < (int)V.Punch; i++)
        {
            var v = volumes[i];
            if (v != null) v.weight = Mathf.MoveTowards(v.weight, target[i], dt / ease);
        }
        // the punches are gone in a moment
        var punch = volumes[(int)V.Punch];
        if (punch != null) punch.weight = Mathf.MoveTowards(punch.weight, 0f, dt / 0.35f);
        var hurt = volumes[(int)V.Hurt];
        if (hurt != null) hurt.weight = Mathf.MoveTowards(hurt.weight, 0f, dt / 0.5f);
    }
}
