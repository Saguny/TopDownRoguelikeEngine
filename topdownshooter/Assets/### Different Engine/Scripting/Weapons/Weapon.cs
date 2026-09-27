using System.Collections.Generic;
using UnityEngine;

// base for every weapon added through a WeaponData upgrade. the first pick adds the component to
// the player and every pick after sets its level. Might, Area, Weapon Speed and Cooldown come
// through the helpers below, so a new weapon only has to say what it does
public abstract class Weapon : MonoBehaviour
{
    public int Level { get; private set; }

    // its settings asset (this run's copy): its name, icon and evolution
    public WeaponData Asset { get; private set; }

    // it has taken the pick that evolves it
    public bool Evolved => Asset != null && Asset.EvolutionLevel > 0 && Level >= Asset.EvolutionLevel;

    // its damage and DPS so far this run, as it is now: an evolution has its own (see RunStats)
    public WeaponRecord Record { get; internal set; }

    protected StatContext Stats { get; private set; }
    protected float Might => Stats ? Stats.MightMul : 1f;
    protected float AreaMul => Stats ? Stats.AreaMul : 1f;
    protected float SpeedMul => Stats ? Stats.WeaponSpeedMul : 1f;

    // the global Cooldown stat plus the character sheet's; these weapons have no cooldown upgrade of their own
    protected float Cooldown(float seconds) => Stats ? Stats.CooldownFor(UpgradeType.Weapon, seconds) : seconds;

    // how many more enemies a projectile goes through (the Armour Piercing passive)
    protected int Pierce => Stats ? Stats.PierceTotal : 0;

    // every hit goes through here so it can crit (the Steady Hands and Executioner passives).
    // returns true when it killed
    protected bool Hit(EnemyHealth enemy, float damage, bool ignoreArmor = false)
    {
        damage *= SignatureDamage;
        bool crit = false;
        if (Stats) damage = Stats.WithCrit(damage, out crit);
        return enemy.TakeDamage(damage, DamageKind.Weapon, crit, ignoreArmor, this);
    }

    // a world space holder for the weapon's sprites, so the player's left/right flip doesn't mirror them
    protected Transform Fx { get; private set; }

    // this weapon is the character's starting weapon: its signature perks apply (see CharacterData)
    protected virtual bool IsSignature => false;
    protected CharacterData Character => Stats != null ? Stats.Character : null;
    protected float SignatureDamage => IsSignature && Character != null ? Character.signatureDamage : 1f;

    // the character's signature slow on an enemy this weapon hit, if it has one
    protected void SignatureSlow(EnemyHealth enemy)
    {
        if (!IsSignature || Character == null || Character.signatureSlow <= 0f || enemy == null) return;
        if (enemy.TryGetComponent(out EnemyMovement movement))
            movement.ApplySlow(1f - Character.signatureSlow, Character.signatureSlowSeconds);
    }

    public virtual void Init(WeaponData data) => Asset = data;

    protected virtual void Awake()
    {
        Stats = GetComponentInParent<StatContext>();
        Fx = new GameObject(GetType().Name + " (fx)").transform;
    }

    // a weapon switched off (the player went down) takes its swords, talismans and stars with it
    protected virtual void OnEnable()
    {
        if (Fx != null) Fx.gameObject.SetActive(true);
    }

    protected virtual void OnDisable()
    {
        if (Fx != null) Fx.gameObject.SetActive(false);
    }

    protected virtual void OnDestroy()
    {
        if (Fx != null) Destroy(Fx.gameObject);
    }

    public void SetLevel(int level)
    {
        Level = Mathf.Max(1, level);
        OnLevelChanged();
        RunStats.Track(this);
    }

    protected virtual void OnLevelChanged() { }

    // ---------------------------------------------------------------- helpers

    private static readonly List<Collider2D> overlap = new List<Collider2D>(32);
    private static readonly List<EnemyHealth> candidates = new List<EnemyHealth>(128);
    private static int enemyMask = -1;

    // every live enemy whose collider touches the circle, each once
    protected static void EnemiesIn(Vector2 centre, float radius, List<EnemyHealth> results)
    {
        results.Clear();
        Physics2D.OverlapCircle(centre, radius, EnemyFilter(), overlap);
        Collect(results);
    }

    // every live enemy whose collider touches the box, e.g. a beam; angle in degrees
    protected static void EnemiesInBox(Vector2 centre, Vector2 size, float angle, List<EnemyHealth> results)
    {
        results.Clear();
        Physics2D.OverlapBox(centre, size, angle, EnemyFilter(), overlap);
        Collect(results);
    }

    private static ContactFilter2D EnemyFilter()
    {
        if (enemyMask == -1) enemyMask = LayerMask.GetMask("Enemy");
        var filter = new ContactFilter2D { useTriggers = Physics2D.queriesHitTriggers };
        if (enemyMask != 0) filter.SetLayerMask(enemyMask);
        return filter;
    }

    private static void Collect(List<EnemyHealth> results)
    {
        foreach (var c in overlap)
            if (c != null && c.TryGetComponent(out EnemyHealth e) && IsAlive(e) && !results.Contains(e))
                results.Add(e);
    }

    protected static bool IsAlive(EnemyHealth e) => e != null && e.isActiveAndEnabled && e.Current > 0f;

    // a random live enemy within range of a point, or null when there's none
    protected static EnemyHealth RandomEnemy(Vector2 from, float range)
    {
        candidates.Clear();
        float r2 = range * range;
        foreach (var go in EnemyRegistry.All)
        {
            if (go == null || !go.TryGetComponent(out EnemyHealth e) || !IsAlive(e)) continue;
            if (((Vector2)go.transform.position - from).sqrMagnitude <= r2) candidates.Add(e);
        }
        return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : null;
    }

    protected static bool OnScreen(Vector3 position, Camera cam)
    {
        var v = cam.WorldToViewportPoint(position);
        return v.x >= 0f && v.x <= 1f && v.y >= 0f && v.y <= 1f;
    }
}

// what a weapon actually inherits: Data is its own settings asset, already typed
public abstract class Weapon<TData> : Weapon where TData : WeaponData
{
    protected TData Data { get; private set; }

    protected override bool IsSignature => Character != null && Character.startingWeapon is TData;

    public override void Init(WeaponData data)
    {
        base.Init(data);
        Data = data as TData;
    }
}
