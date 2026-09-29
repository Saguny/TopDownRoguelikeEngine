using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public enum BulletType { Orb, Rice, Talisman, Coin, Flame, BigOrb }
public enum BulletColor { Red, Gold, Violet, Azure, Jade, Bone }

// one bullet's whole life, set up before it's fired: how it flies, and the one turn its life takes
// (a change of speed and heading, or bursting into a ring). built with Shot.Of and the helpers
// below, e.g. Shot.Of(BulletType.Rice, BulletColor.Gold, 4f).Accel(-2f, 1f).Then(1.2f, 3f, aim: true)
public struct Shot
{
    public BulletType type;
    public BulletColor color;
    public float speed, accel, minSpeed, maxSpeed, turn;   // units/s, units/s², degrees/s
    public float delay;             // seconds it hangs where it appears, flashing, harmless, before it flies
    public float life;
    public float damage;            // a share of the player's max health: bullets never scale with the run
    public float home, homeFor;     // degrees/s it turns toward the player, for its first `homeFor` seconds
    public bool silent;             // no firing sound: the enemy that fired it has its own

    public bool hasChange;
    public float changeAt, changeSpeed, changeAccel, changeTurn, changeAngle;
    public bool changeAim;

    public int burstCount;          // at changeAt it bursts into a ring of these instead
    public BulletType burstType;
    public BulletColor burstColor;
    public float burstSpeed;

    public static Shot Of(BulletType type, BulletColor color, float speed)
    {
        return new Shot
        {
            type = type, color = color, speed = speed, minSpeed = 0f, maxSpeed = 30f, life = 14f,
            damage = type == BulletType.BigOrb ? 0.12f : type == BulletType.Flame ? 0.1f : 0.08f,
        };
    }

    public Shot Accel(float a, float min = 0f, float max = 30f) { accel = a; minSpeed = min; maxSpeed = max; return this; }
    public Shot Turn(float degreesPerSecond) { turn = degreesPerSecond; return this; }
    public Shot Delay(float seconds) { delay = seconds; return this; }
    public Shot Life(float seconds) { life = seconds; return this; }
    public Shot Hurts(float share) { damage = share; return this; }
    public Shot Silent() { silent = true; return this; }

    // steers toward the player, at most `degreesPerSecond`, for its first `seconds` of flight, then
    // flies on straight: a wisp that follows, but can be shaken off
    public Shot Home(float degreesPerSecond, float seconds = 999f) { home = degreesPerSecond; homeFor = seconds; return this; }

    // `at` seconds into its flight: new speed, acceleration and turn, and either aimed at the
    // player (plus `angle`) or turned by `angle`
    public Shot Then(float at, float speed, float accel = 0f, float turn = 0f, bool aim = false, float angle = 0f)
    {
        hasChange = true; changeAt = at; changeSpeed = speed; changeAccel = accel; changeTurn = turn; changeAim = aim; changeAngle = angle;
        return this;
    }

    // `at` seconds into its flight it bursts into a ring of `count`
    public Shot Burst(float at, int count, BulletType t, BulletColor c, float speed)
    {
        hasChange = true; changeAt = at; burstCount = count; burstType = t; burstColor = c; burstSpeed = speed;
        return this;
    }
}

// the boss's bullets, Touhou's way: hundreds of them, each a sprite in one shared mesh (one draw),
// moved and checked against the player in one loop. the player's hitbox is small and shown while
// bullets are about; passing close to a bullet grazes it (a spark, a tick). a hit takes a fixed
// share of the player's max health, whatever the run's difficulty, then a moment's invulnerability,
// so nobody is ever killed in one hit (or in a burst of them). a phase's
// end cancels everything into sparkles and wen. made on first use
public class Danmaku : MonoBehaviour, IEnemyShots
{
    private struct B
    {
        public Shot s;
        public Vector2 pos;
        public float angle, speed, accel, turn, age;
        public bool grazed, changed;
    }

    private const float PlayerHitRadius = 0.16f;
    private const float GrazeRadius = 0.75f;
    private const float MercySeconds = 1.3f;
    private const float Cell = 32f;
    private const float Forget = 30f;               // this far from the player, a bullet's gone

    private static Danmaku instance;
    private static readonly float[] Radius = { 0.12f, 0.09f, 0.11f, 0.14f, 0.1f, 0.3f };

    private B[] bullets = new B[1024];
    private int count;

    private Mesh mesh;
    private Material material;
    private Vector3[] verts = new Vector3[0];
    private Vector2[] uvs = new Vector2[0];
    private Color32[] colors = new Color32[0];
    private int[] tris = new int[0];
    private static readonly Bounds Everywhere = new Bounds(Vector3.zero, new Vector3(100000f, 100000f, 1000f));

    private PlayerHealth player;
    private Transform playerT;
    private CircleCollider2D playerBody;
    private float nextShotSound, nextBigSound, nextGrazeSound;

    public static int Count => instance != null ? instance.count : 0;
    public static int Hits { get; private set; }
    public static int Grazes { get; private set; }
    // shown while a boss is up, even between volleys
    public static bool ShowHitbox;
    // Meng Po's forgetting: 0 every bullet as it is, 1 all of them faded to ghosts of themselves
    // (still there, still hurting, only just to be seen, flickering), so the player has to
    // remember where they were
    public static float Veil;
    public static event System.Action<Vector2> PlayerHit;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        Hits = Grazes = 0;
        ShowHitbox = false;
        Veil = 0f;
        PlayerHit = null;
    }

    private static Danmaku Get()
    {
        if (instance == null && Application.isPlaying) instance = new GameObject("Danmaku").AddComponent<Danmaku>();
        return instance;
    }

    private void Awake()
    {
        var atlas = YamaArt.Texture("Yama/bullets");
        var shader = Shader.Find("Rogue/Sprite Batch");
        mesh = new Mesh { name = "Danmaku", indexFormat = IndexFormat.UInt32 };
        mesh.MarkDynamic();
        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = gameObject.AddComponent<MeshRenderer>();
        if (shader != null) mr.sharedMaterial = material = new Material(shader) { mainTexture = atlas, name = "Danmaku" };
        // over everything in the world: a bullet is never hidden
        mr.sortingLayerName = "Aura";
        mr.sortingOrder = 200;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        EnemyShots.Register(this);
    }

    private void OnDestroy()
    {
        EnemyShots.Unregister(this);
        if (instance == this) instance = null;
        if (mesh != null) Destroy(mesh);
        if (material != null) Destroy(material);
    }

    // ---- firing

    // the angle, in degrees, from `from` to the player
    public static float AimAt(Vector2 from)
    {
        var d = Get();
        if (d == null || !d.FindPlayer()) return -90f;
        Vector2 to = d.PlayerCentre - from;
        return Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
    }

    public static Vector2 PlayerPosition
    {
        get
        {
            var d = Get();
            return d != null && d.FindPlayer() ? d.PlayerCentre : Vector2.zero;
        }
    }

    public static void Fire(Vector2 at, float angle, Shot s)
    {
        var d = Get();
        if (d == null) return;
        if (d.count == d.bullets.Length) System.Array.Resize(ref d.bullets, d.count * 2);
        d.bullets[d.count++] = new B { s = s, pos = at, angle = angle, speed = s.speed, accel = s.accel, turn = s.turn };
        if (!s.silent) d.Sound(s.type, at);
    }

    public static void Ring(Vector2 at, int n, float angle0, Shot s)
    {
        for (int i = 0; i < n; i++) Fire(at, angle0 + 360f * i / n, s);
    }

    // n bullets spread over `spread` degrees round `centre`
    public static void Fan(Vector2 at, float centre, int n, float spread, Shot s)
    {
        if (n <= 1) { Fire(at, centre, s); return; }
        for (int i = 0; i < n; i++) Fire(at, centre - spread / 2f + spread * i / (n - 1), s);
    }

    // every bullet gone at once, as sparkles, and some of them as wen. how many there were
    public static int Cancel(bool drops = true) => instance != null ? instance.CancelWithin(Vector2.zero, float.MaxValue, drops) : 0;

    public static int CancelNear(Vector2 centre, float radius, bool drops = false) =>
        instance != null ? instance.CancelWithin(centre, radius, drops) : 0;

    // everything gone without a trace (the fight's over, the scene's ending)
    public static void Clear()
    {
        if (instance == null) return;
        instance.count = 0;
    }

    // the Gourd of Heaven and Earth: bullets in reach are drawn into its mouth and swallowed. how
    // many it swallowed this frame
    public static int Absorb(Vector2 mouth, float reach, float pullSpeed)
    {
        if (instance == null) return 0;
        var d = instance;
        int swallowed = 0;
        float r2 = reach * reach, dt = Time.deltaTime;
        for (int i = d.count - 1; i >= 0; i--)
        {
            ref B b = ref d.bullets[i];
            Vector2 off = mouth - b.pos;
            float sq = off.sqrMagnitude;
            if (sq > r2) continue;
            if (sq < 0.35f * 0.35f) { d.RemoveAt(i); swallowed++; continue; }
            b.angle = Mathf.Atan2(off.y, off.x) * Mathf.Rad2Deg;
            b.speed = Mathf.Max(b.speed, pullSpeed);
            b.accel = 0f;
            b.turn = 0f;
            b.pos += off.normalized * Mathf.Min(Mathf.Sqrt(sq), pullSpeed * dt);
        }
        return swallowed;
    }

    // a clear of every enemy shot (EnemyShots): these go the way a cancel does
    public int ClearWithin(Vector2 centre, float radius, bool drops) => CancelWithin(centre, radius, drops);

    private int CancelWithin(Vector2 centre, float radius, bool drops)
    {
        var sparkle = YamaArt.Frames("cancel");
        float r2 = radius >= float.MaxValue ? float.MaxValue : radius * radius;
        int n = 0, shown = 0, dropped = 0;
        for (int i = count - 1; i >= 0; i--)
        {
            Vector2 p = bullets[i].pos;
            if (r2 < float.MaxValue && (p - centre).sqrMagnitude > r2) continue;
            if (sparkle != null && shown++ < 400) FxBatch.Play(sparkle, 22f, p, 1f, "Aura", 201);
            // one in three turns into wen, as Touhou's cancelled bullets turn into point items
            if (drops && n % 3 == 0 && dropped++ < 150) PickupSystem.DropWen(p, 1);
            RemoveAt(i);
            n++;
        }
        if (n > 0) YamaArt.Play("yama_cancel", centre == Vector2.zero && PlayerPosition != Vector2.zero ? (Vector3)PlayerPosition : (Vector3)centre, Mathf.Min(1f, 0.4f + n * 0.01f));
        return n;
    }

    private void RemoveAt(int i)
    {
        bullets[i] = bullets[--count];
    }

    private void Sound(BulletType t, Vector2 at)
    {
        float now = Time.time;
        if (t == BulletType.BigOrb)
        {
            if (now < nextBigSound) return;
            nextBigSound = now + 0.12f;
            YamaArt.Play("yama_shot_big", at, 0.55f, Random.Range(0.95f, 1.05f));
            return;
        }
        if (now < nextShotSound) return;
        nextShotSound = now + 0.06f;
        YamaArt.Play("yama_shot", at, 0.32f, Random.Range(0.92f, 1.08f));
    }

    // ---- the loop

    private bool FindPlayer()
    {
        if (player != null) return true;
        player = FindFirstObjectByType<PlayerHealth>();
        if (player == null) return false;
        playerT = player.transform;
        player.TryGetComponent(out playerBody);
        return true;
    }

    private Vector2 PlayerCentre => playerBody != null ? (Vector2)playerBody.bounds.center : (Vector2)playerT.position;

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f || count == 0) return;
        bool havePlayer = FindPlayer();
        Vector2 me = havePlayer ? PlayerCentre : Vector2.zero;
        bool canHit = havePlayer && !player.IsDead && !player.IsInvulnerable;
        float grazeSq = GrazeRadius * GrazeRadius;
        bool hit = false;
        Vector2 hitAt = me;

        for (int i = count - 1; i >= 0; i--)
        {
            ref B b = ref bullets[i];
            b.age += dt;
            if (b.age >= b.s.life + b.s.delay) { RemoveAt(i); continue; }
            if (b.age < b.s.delay) continue;              // still appearing
            float flying = b.age - b.s.delay;

            if (b.s.hasChange && !b.changed && flying >= b.s.changeAt)
            {
                b.changed = true;
                if (b.s.burstCount > 0)
                {
                    // read everything first: removing it moves another bullet into its place
                    var burst = Shot.Of(b.s.burstType, b.s.burstColor, b.s.burstSpeed);
                    int pieces = b.s.burstCount;
                    Vector2 at = b.pos;
                    float a0 = Random.value * 360f;
                    RemoveAt(i);
                    for (int k = 0; k < pieces; k++) Fire(at, a0 + 360f * k / pieces, burst);
                    continue;
                }
                b.speed = b.s.changeSpeed;
                b.accel = b.s.changeAccel;
                b.turn = b.s.changeTurn;
                b.angle = b.s.changeAim ? AimAt(b.pos) + b.s.changeAngle : b.angle + b.s.changeAngle;
            }

            b.speed = Mathf.Clamp(b.speed + b.accel * dt, b.s.minSpeed, b.s.maxSpeed);
            b.angle += b.turn * dt;
            if (b.s.home > 0f && havePlayer && flying < b.s.homeFor)
            {
                Vector2 to = me - b.pos;
                float want = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
                b.angle = Mathf.MoveTowardsAngle(b.angle, want, b.s.home * dt);
            }
            float rad = b.angle * Mathf.Deg2Rad;
            b.pos += new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * (b.speed * dt);

            if (!havePlayer) continue;
            Vector2 off = b.pos - me;
            float sq = off.sqrMagnitude;
            if (sq > Forget * Forget) { RemoveAt(i); continue; }
            if (flying < 0.06f) continue;                  // a moment to see where it's going

            float reach = Radius[(int)b.s.type] + PlayerHitRadius;
            if (canHit && !hit && sq <= reach * reach)
            {
                hit = true;
                hitAt = b.pos;
                float share = b.s.damage;
                RemoveAt(i);
                Hurt(share);
                continue;
            }
            if (!b.grazed && sq <= grazeSq)
            {
                b.grazed = true;
                Graze(me, b.pos);
            }
        }

        if (hit)
        {
            // a moment's grace, so a hit is never the first of several at once. the bullets stay:
            // getting out of them is still the player's job
            player.GrantInvulnerability(MercySeconds);
            PlayerHit?.Invoke(hitAt);
        }
    }

    private void Hurt(float share)
    {
        Hits++;
        player.TakeDamage(player.Max * share);
        Juice.Shake(0.25f);
        YamaArt.Play("yama_player_hit", PlayerCentre, 0.7f);
    }

    private void Graze(Vector2 me, Vector2 bullet)
    {
        Grazes++;
        var spark = YamaArt.Frames("graze");
        if (spark != null) FxBatch.Play(spark, 24f, Vector2.Lerp(me, bullet, 0.5f), 1f, "Aura", 202);
        if (Time.time < nextGrazeSound) return;
        nextGrazeSound = Time.time + 0.05f;
        YamaArt.Play("yama_graze", me, 0.35f, Random.Range(0.95f, 1.15f));
    }

    // ---- drawing

    private void LateUpdate()
    {
        bool marker = ShowHitbox && FindPlayer() && !player.IsDead;
        int quads = count + (marker ? 1 : 0);
        Ensure(quads);
        float size = Cell / YamaArt.WorldPpu, half = size * 0.5f;
        const float cellUv = 1f / 6f;

        for (int i = 0; i < count; i++)
        {
            ref B b = ref bullets[i];
            int t = (int)b.s.type, c = (int)b.s.color;
            // appearing: big and faint, closing in on its size (Touhou's spawn blur); waiting on a
            // delay, it pulses where it'll come from
            float scale = 1f, alpha = 1f;
            if (b.age < b.s.delay) { scale = 1.3f + 0.2f * Mathf.Sin(b.age * 18f); alpha = 0.55f; }
            else
            {
                float k = Mathf.Clamp01((b.age - b.s.delay) / 0.14f);
                scale = Mathf.Lerp(1.9f, 1f, k);
                alpha = Mathf.Lerp(0.35f, 1f, k);
            }
            float spin = b.s.type == BulletType.Coin ? b.age * 420f
                : b.s.type == BulletType.Orb || b.s.type == BulletType.BigOrb ? 0f : b.angle;
            // forgotten: a faint ghost of itself, each one flickering on its own beat
            if (Veil > 0f) alpha *= Mathf.Lerp(1f, 0.16f + 0.1f * Mathf.Sin(b.age * 23f + i), Veil);
            Quad(i, b.pos, spin, half * scale, c * cellUv, (5 - t) * cellUv, cellUv, (byte)(alpha * 255f));
        }
        if (marker)
        {
            // the player's hitbox: a small bright heart, so they know what has to dodge
            float pulse = 0.5f + 0.1f * Mathf.Sin(Time.unscaledTime * 8f);
            Quad(count, PlayerCentre, 0f, half * pulse, (int)BulletColor.Bone * cellUv, (5 - (int)BulletType.Orb) * cellUv, cellUv, 230);
        }

        const MeshUpdateFlags flags = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices;
        mesh.Clear(true);
        if (quads > 0)
        {
            mesh.SetVertices(verts, 0, quads * 4, flags);
            mesh.SetUVs(0, uvs, 0, quads * 4, flags);
            mesh.SetColors(colors, 0, quads * 4, flags);
            mesh.SetIndices(tris, 0, quads * 6, MeshTopology.Triangles, 0, false);
        }
        mesh.bounds = Everywhere;
    }

    private void Quad(int q, Vector2 p, float degrees, float h, float u0, float v0, float cell, byte alpha)
    {
        float r = degrees * Mathf.Deg2Rad, cs = Mathf.Cos(r) * h, sn = Mathf.Sin(r) * h;
        int o = q * 4;
        // corners (-1,-1) (-1,1) (1,1) (1,-1), rotated
        verts[o] = new Vector3(p.x - cs + sn, p.y - sn - cs, 0f);
        verts[o + 1] = new Vector3(p.x - cs - sn, p.y - sn + cs, 0f);
        verts[o + 2] = new Vector3(p.x + cs - sn, p.y + sn + cs, 0f);
        verts[o + 3] = new Vector3(p.x + cs + sn, p.y + sn - cs, 0f);
        uvs[o] = new Vector2(u0, v0);
        uvs[o + 1] = new Vector2(u0, v0 + cell);
        uvs[o + 2] = new Vector2(u0 + cell, v0 + cell);
        uvs[o + 3] = new Vector2(u0 + cell, v0);
        var col = new Color32(255, 255, 255, alpha);
        colors[o] = colors[o + 1] = colors[o + 2] = colors[o + 3] = col;
    }

    private void Ensure(int quads)
    {
        if (verts.Length >= quads * 4) return;
        int n = Mathf.Max(256, Mathf.NextPowerOfTwo(quads));
        verts = new Vector3[n * 4];
        uvs = new Vector2[n * 4];
        colors = new Color32[n * 4];
        tris = new int[n * 6];
        for (int q = 0; q < n; q++)
        {
            int o = q * 4, t = q * 6;
            tris[t] = o; tris[t + 1] = o + 1; tris[t + 2] = o + 2;
            tris[t + 3] = o; tris[t + 4] = o + 2; tris[t + 5] = o + 3;
        }
    }
}
