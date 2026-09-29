using UnityEngine;

// the end boss's duel (BossDuel): every weapon is put away and the character's starting weapon
// comes back as a form only this fight has, drawn clean so his danmaku stays readable through it.
// it goes for the boss while he can be hurt, else the nearest enemy in reach. its pace is its own
// (the Cooldown stat doesn't touch it); Might, its attack class and crits do. each is set for
// about 1600 damage a second on him before the player's stats: what six evolved weapons did to
// him, so his health stands as it was (Tools/Balance/boss.py)
public abstract class DuelWeapon : Weapon
{
    public const string Layer = "Aura";
    // over the boss (40-43) and his effects, under his bullets (200)
    public const int Order = 60;
    protected const float Reach = 14f;

    // its name in the run's stats and on the screen as it comes, and the line under it
    public abstract string Title { get; }
    public abstract string Line { get; }
    // its colour: the pillar it comes down in and its name
    public abstract Color Tint { get; }
    public abstract string Pillar { get; }

    private EnemyHealth colFor;
    private Collider2D col;

    // who it goes for: the boss when he can be hurt, else the nearest enemy within reach, or nobody
    protected EnemyHealth Target()
    {
        var boss = BossDuel.Boss;
        if (Hittable(boss)) return boss;
        Vector2 me = transform.position;
        EnemyHealth best = null;
        float bestD = Reach * Reach;
        foreach (var go in EnemyRegistry.All)
        {
            if (go == null || !go.TryGetComponent(out EnemyHealth e) || e == boss || !IsAlive(e)) continue;
            float d = ((Vector2)go.transform.position - me).sqrMagnitude;
            if (d < bestD) { bestD = d; best = e; }
        }
        return best;
    }

    protected bool Hittable(EnemyHealth e) => IsAlive(e) && ColliderOf(e) is Collider2D c && c.enabled;

    // the middle of what can be hit of it
    protected Vector2 AimAt(EnemyHealth e)
    {
        var c = ColliderOf(e);
        return c != null && c.enabled ? (Vector2)c.bounds.center : (Vector2)e.transform.position;
    }

    // is a point within `radius` of what can be hit of it
    protected bool Touches(EnemyHealth e, Vector2 p, float radius)
    {
        var c = ColliderOf(e);
        if (c == null || !c.enabled) return false;
        return ((Vector2)c.ClosestPoint(p) - p).sqrMagnitude <= radius * radius;
    }

    private Collider2D ColliderOf(EnemyHealth e)
    {
        if (e == null) return null;
        if (e != colFor) { colFor = e; col = e.GetComponent<Collider2D>(); }
        return col;
    }

    // a hit with Might on it (Hit adds the attack class and the crit). true when it killed
    protected bool Strike(EnemyHealth e, float damage, bool ignoreArmor = false) => e != null && Hit(e, damage * Might, ignoreArmor);

    // its art, Resources/Duel (Tools/VFX/huangquan/duel.js), at the world's pixel size
    protected static Sprite[] Art(string name) => YamaArt.Strip("Duel/" + name);
    protected static Sprite Loop(Sprite[] frames, float fps, float t) =>
        frames == null || frames.Length == 0 ? null : frames[Mathf.Abs((int)(t * fps)) % frames.Length];
    protected static Sprite Once(Sprite[] frames, float k) =>
        frames == null || frames.Length == 0 ? null : frames[Mathf.Clamp((int)(k * frames.Length), 0, frames.Length - 1)];
    protected static void Sound(string name, Vector3 at, float volume, float pitch = 1f) => YamaArt.Play(name, at, volume, pitch);

    protected SpriteRenderer Make(string name, Sprite[] frames, int order)
    {
        var sr = WeaponFx.Make(Fx, name, frames != null && frames.Length > 0 ? frames[0] : null, WeaponFx.Disc, Tint, Layer, order);
        sr.gameObject.SetActive(false);
        return sr;
    }

    protected static void Face(Transform t, Vector2 dir, float artAngle = 0f) =>
        t.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - artAngle);

    // the duel's pace doesn't bend to the level: one level, the duel's
    protected override void OnLevelChanged() { }
}
