using UnityEngine;

// one weapon's part in the run: what it has done and how long it has been held. an evolution gets
// a record of its own from the pick that evolves it, the way Vampire Survivors lists an evolved
// weapon on its own line: the weapon's record stops its clock there and keeps what it did up to
// then, and everything after, the old attacks included, is the evolution's. RunStats keeps them
public sealed class WeaponRecord
{
    // this run's copy of the weapon's asset: its name, icon and level table
    public WeaponData Asset { get; }
    public bool Evolved { get; }
    // for an evolution, the record of the weapon it grew out of
    public WeaponRecord EvolvedFrom { get; }

    public string Title => Asset == null ? "?" : Evolved ? Asset.GetEvolvedTitle() : Asset.GetBaseTitle();
    public Sprite Icon => Asset == null ? null : Evolved && Asset.evolvedIcon != null ? Asset.evolvedIcon : Asset.icon;

    // the weapon's level, the last time it was levelled as this record
    public int Level { get; internal set; }

    // what landed, after armour: the numbers the player sees
    public double Damage { get; private set; }
    public int Hits { get; private set; }
    public int Crits { get; private set; }
    public int Kills { get; private set; }
    public float BiggestHit { get; private set; }

    // run time (RunStats.Now) it was taken or evolved, and when it evolved on into its next
    // record. -1 while it's still held as this
    public float Since { get; }
    public float Until { get; private set; } = -1f;
    public bool Held => Until < 0f;
    public float Seconds => Mathf.Max(0f, (Held ? RunStats.Now : Until) - Since);

    // its damage over the time it has been held, Vampire Survivors' DPS. the first second counts
    // as a whole one, so a weapon just taken doesn't read as thousands
    public float Dps => (float)(Damage / Mathf.Max(1f, Seconds));
    // over the last few seconds; 0 once the evolution has taken over
    public float LiveDps => Held ? window.PerSecond(RunStats.Now, Seconds) : 0f;
    // its best few seconds
    public float PeakDps => window.Peak;
    // its part of all the damage the player has done this run, 0 to 1
    public float Share => RunStats.DamageDealt > 0.0 ? (float)(Damage / RunStats.DamageDealt) : 0f;

    internal int Run { get; }
    private readonly RollingDamage window = new RollingDamage();

    internal WeaponRecord(WeaponData asset, bool evolved, int level, WeaponRecord evolvedFrom, int run, float now)
    {
        Asset = asset;
        Evolved = evolved;
        Level = level;
        EvolvedFrom = evolvedFrom;
        Run = run;
        Since = now;
    }

    internal void Add(float damage, bool crit, bool killed, float now)
    {
        Damage += damage;
        Hits++;
        if (crit) Crits++;
        if (killed) Kills++;
        if (damage > BiggestHit) BiggestHit = damage;
        window.Add(damage, now);
    }

    internal void Retire(float now) => Until = now;
}

// damage over the last few seconds of play, for a live DPS: a ring of quarter second buckets.
// adding is a few operations, so it keeps up with a hit on every enemy on screen every frame
public sealed class RollingDamage
{
    public const float Bucket = 0.25f;
    public const int Buckets = 20;
    public const float Window = Bucket * Buckets;   // five seconds

    private const int Never = int.MinValue / 2;
    private readonly float[] sums = new float[Buckets];
    private readonly int[] stamps = new int[Buckets];
    private int last = Never;
    private float peak;

    public RollingDamage()
    {
        for (int i = 0; i < Buckets; i++) stamps[i] = Never;
    }

    // the most it has done over a whole window, per second. the best window always ends on a
    // bucket something landed in, so sampling each one as it closes (and the open one) finds it
    public float Peak => last == Never ? 0f : Mathf.Max(peak, SumUpTo(last) / Window);

    public void Add(float amount, float now)
    {
        int b = Mathf.FloorToInt(now / Bucket);
        if (b != last)
        {
            if (last != Never) peak = Mathf.Max(peak, SumUpTo(last) / Window);
            last = b;
        }

        int i = ((b % Buckets) + Buckets) % Buckets;
        if (stamps[i] != b)
        {
            stamps[i] = b;
            sums[i] = 0f;
        }
        sums[i] += amount;
    }

    // per second over the window, or over the time there has been when that's shorter
    public float PerSecond(float now, float span) =>
        SumUpTo(Mathf.FloorToInt(now / Bucket)) / Mathf.Clamp(span, Bucket, Window);

    private float SumUpTo(int b)
    {
        float s = 0f;
        for (int i = 0; i < Buckets; i++)
            if (stamps[i] <= b && b - stamps[i] < Buckets) s += sums[i];
        return s;
    }
}
