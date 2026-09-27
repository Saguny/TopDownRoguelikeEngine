using UnityEngine;

// the cheap half of game feel: freeze, shake, and a number. none of this needs art,
// and it is the difference between an arrow landing and an arrow just vanishing
public class Juice : MonoBehaviour
{
    // sized for every damage source at once: an aura pulse alone can tag a dozen enemies
    private const int Numbers = 96;

    // hit stop is deliberately not used on kills or crits: in a horde they never stop coming, so
    // any freeze on them reads as the game stuttering. Freeze stays available for rare moments
    // (a boss dying, say), rate limited so even a burst of calls can't stall the game
    public static float FreezeCooldown = 0.3f;

    // global multiplier on every shake. 0 turns it off entirely
    public static float ShakeScale = 1f;

    private static Juice instance;
    private static CameraFollow cam;

    private DamageNumber[] pool;
    private int next;
    private float freezeUntil;
    private float nextFreezeAllowed;
    private bool frozen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        cam = null;
    }

    private static Juice Ensure()
    {
        if (instance != null) return instance;
        if (!Application.isPlaying) return null;

        var host = new GameObject("Juice");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<Juice>();
        return instance;
    }

    private void Awake()
    {
        if (instance == null) instance = this;

        pool = new DamageNumber[Numbers];
        for (int i = 0; i < Numbers; i++)
        {
            var go = new GameObject($"DamageNumber {i}");
            go.transform.SetParent(transform, false);
            pool[i] = go.AddComponent<DamageNumber>();
        }
    }

    public static void Number(Vector3 position, float amount, DamageKind kind, bool crit = false, bool armoured = false)
    {
        if (kind == DamageKind.Silent) return;

        var j = Ensure();
        if (j == null) return;

        // anything that landed at all shows at least 1, rather than a confusing 0
        int shown = Mathf.Max(1, Mathf.RoundToInt(amount));

        // a mass kill asks for hundreds at once; past a handful per frame nobody can read them,
        // and each one rebuilds a text mesh
        if (Time.frameCount != j.numberFrame)
        {
            j.numberFrame = Time.frameCount;
            j.numbersThisFrame = 0;
        }
        if (++j.numbersThisFrame > NumbersPerFrame) return;

        // crits get their own colour, size and a "!" so the variance is legible at a glance.
        // the numbers are a pixel font drawn in one mesh (PixelNumbers)
        PixelNumbers.Show(position, shown, crit, crit ? CritColor : armoured ? ArmourColor : ColorFor(kind), crit ? 2 : SizeFor(kind));
    }

    private const int NumbersPerFrame = 24;
    private int numberFrame = -1;
    private int numbersThisFrame;

    private static readonly Color CritColor = new Color(1f, 0.82f, 0.2f);
    // a hit its armour blunted: cold steel, so it's clear why the number's small
    private static readonly Color ArmourColor = new Color(0.68f, 0.74f, 0.86f);

    // each source gets its own colour so a glance tells you which weapon is doing the work
    private static Color ColorFor(DamageKind kind)
    {
        switch (kind)
        {
            case DamageKind.Aura: return new Color(0.55f, 0.85f, 1f);
            case DamageKind.Meteor: return new Color(1f, 0.55f, 0.2f);
            case DamageKind.Weapon: return new Color(1f, 0.82f, 0.45f);
            default: return Color.white;
        }
    }

    // how many world pixels each pixel of the font covers. aura ticks constantly, so its numbers
    // stay small; a meteor is one big hit, so it reads big
    private static int SizeFor(DamageKind kind)
    {
        switch (kind)
        {
            case DamageKind.Meteor: return 2;
            default: return 1;
        }
    }

    // a floating callout, for teaching a mechanic the first time it appears
    public static void Text(Vector3 position, string text, Color color, float scale = 1.4f, float seconds = 1.3f)
    {
        var j = Ensure();
        if (j == null) return;

        j.Emit(position, text, color, scale, seconds);
    }

    // kills and crits don't shake either, for the same reason. the numbers carry kill feedback;
    // shake is saved for rare hits that matter, like a strike landing nearby
    public static void Shake(float amplitude)
    {
        if (ShakeScale <= 0f || amplitude <= 0f) return;

        if (cam == null) cam = Object.FindFirstObjectByType<CameraFollow>();
        if (cam != null) cam.Shake(amplitude * ShakeScale);
    }

    // hard rule: never touch timeScale while something else already owns it. the upgrade
    // menu parks it at 0, and a hit stop resolving mid menu would un-pause the game
    public static void Freeze(float seconds)
    {
        var j = Ensure();
        if (j == null) return;
        if (!j.frozen && Time.timeScale < 0.99f) return;
        if (!j.frozen && Time.unscaledTime < j.nextFreezeAllowed) return;

        j.freezeUntil = Mathf.Max(j.freezeUntil, Time.unscaledTime + seconds);

        if (!j.frozen)
        {
            j.frozen = true;
            Time.timeScale = 0f;
        }
    }

    // anything that pauses the game calls this first. it drops the freeze claim without
    // touching timeScale, so when the freeze would have expired there is nothing to restore
    // and a paused menu can't be un-paused out from under the player
    public static void Yield()
    {
        if (instance == null) return;
        instance.frozen = false;
    }

    private void Update()
    {
        if (!frozen || Time.unscaledTime < freezeUntil) return;

        frozen = false;
        nextFreezeAllowed = Time.unscaledTime + FreezeCooldown;

        // only restore if nothing else took ownership of timeScale while we were frozen
        if (Time.timeScale == 0f) Time.timeScale = 1f;
    }

    private void Emit(Vector3 position, string text, Color color, float scale, float seconds = 0.55f)
    {
        // scan for a free slot, fall back to stealing the oldest
        for (int i = 0; i < pool.Length; i++)
        {
            int index = (next + i) % pool.Length;
            if (pool[index].Busy) continue;

            next = (index + 1) % pool.Length;
            pool[index].Show(position, text, color, scale, seconds);
            return;
        }

        pool[next].Show(position, text, color, scale, seconds);
        next = (next + 1) % pool.Length;
    }
}

// what dealt a hit, so the number can be coloured by source. Silent draws nothing
public enum DamageKind
{
    Normal,
    Arrow,
    Aura,
    Meteor,
    Silent,
    Weapon // everything added through WeaponData
}
