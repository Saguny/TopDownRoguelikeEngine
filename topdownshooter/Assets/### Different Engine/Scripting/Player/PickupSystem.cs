using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// every wen on the ground, as data: a position and whether the magnet has it, per kind of pickup.
// one loop a frame pulls them in and collects the ones that reach the player, and each kind is
// drawn as a single mesh, so thousands of coins are one draw call and no objects at all. what's
// collected in a frame is paid out in one go: one AddWen, one counter update, one sound.
// made on demand by the first drop, so no scene needs one
public class PickupSystem : MonoBehaviour
{
    private sealed class Kind
    {
        public int wen;
        public AudioClip sound;
        public float volume;
        public float reach;
        public Vector2 half, offset;
        public Vector2 uvMin, uvMax;
        public Color32 color;

        public int count;
        public Vector2[] pos = new Vector2[256];
        public bool[] pulled = new bool[256];
        public int[] worth = new int[256];
        public float[] launch = new float[256];     // in a sweep, when it takes off (-1: not yet given)
        public Vector2[] vel = new Vector2[256];    // in a sweep, its flight
        public int merge = -1;          // the envelope everything goes into once too much is lying about

        public Mesh mesh;
        public Vector3[] verts = new Vector3[0];
        public Vector2[] uvs = new Vector2[0];
        public Color32[] colors = new Color32[0];
        public int[] tris = new int[0];
        public bool dirty;
    }

    private static PickupSystem instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        // "collect all wen" (a cleared Final Rush, the boss, the Command Token) is a quick sweep
        GameEvents.OnCollectAllWen -= CollectAll;
        GameEvents.OnCollectAllWen += CollectAll;
    }

    private const float CollectAllSeconds = 0.6f;
    private static void CollectAll() { if (instance != null) Sweep(CollectAllSeconds); }

    private readonly Dictionary<GameObject, Kind> byPrefab = new Dictionary<GameObject, Kind>();
    private readonly Dictionary<(Sprite, int), Kind> byLook = new Dictionary<(Sprite, int), Kind>();
    private readonly List<Kind> kinds = new List<Kind>();
    private readonly List<Pickup> adoptions = new List<Pickup>();
    private Material material;

    private PlayerInventory inventory;
    private MagnetArea magnet;
    private float playerRadius = 0.4f;
    private float sweepUntil = -1f;
    private float sweepStart = -1f;
    private int sweepPieces, sweepCollected, sweepWen;
    private float nextSweepSound;
    private static AudioClip totalSound;

    // how fast a pulled pickup closes in, per second (the old magnet's pull)
    private const float PullRate = 8f;
    // a sweep: the nearest take off first and the furthest this much later, each hopping out and
    // swirling round before it's drawn in faster and faster; any still out after SweepMax are collected
    private const float SweepStagger = 0.45f;
    private const float SweepMax = 3f;
    private const float SweepHop = 4.5f, SweepSwirl = 5f;
    private const float SweepPull = 30f, SweepPullGrowth = 220f, SweepDrag = 3f, SweepTopSpeed = 45f;
    private static readonly Bounds Everywhere = new Bounds(Vector3.zero, new Vector3(100000f, 100000f, 1000f));

    private static PickupSystem Get()
    {
        if (instance == null && Application.isPlaying) instance = new GameObject("PickupSystem").AddComponent<PickupSystem>();
        return instance;
    }

    public static void Ensure() => Get();

    // ---- dropping, and the helpers dev tools and the stress test use

    public static void Drop(GameObject prefab, Vector3 at)
    {
        var sys = Get();
        if (sys == null || prefab == null) return;
        var kind = sys.KindFor(prefab);
        if (kind == null)
        {
            ObjectPool.For(prefab).Get(at, Quaternion.identity);    // not a plain pickup: the old way
            return;
        }
        sys.Add(kind, at, kind.wen);
    }

    // wen worth this much, as one piece whose kind shows its worth: bronze, jade or a red envelope,
    // like Vampire Survivors' blue, green and red gems. once too much is lying about, new wen goes
    // into one red envelope instead, which keeps growing until it's picked up. without the library's
    // tiers it's dropped as the given prefab
    public static void DropWen(Vector3 at, int worth, GameObject fallback = null)
    {
        var sys = Get();
        if (sys == null || worth <= 0) return;
        var lib = VfxLibrary.Get;
        if (lib == null || lib.wenBronze == null)
        {
            if (fallback != null) Drop(fallback, at);
            return;
        }

        if (lib.wenEnvelope != null && Count >= lib.maxOnGround)
        {
            var env = sys.KindFor(lib.wenEnvelope);
            if (env != null)
            {
                if (env.merge >= 0 && env.merge < env.count) env.worth[env.merge] += worth;
                else
                {
                    sys.Add(env, at, worth);
                    env.merge = env.count - 1;
                }
                return;
            }
        }

        var prefab = worth <= lib.bronzeUpTo ? lib.wenBronze : worth <= lib.jadeUpTo && lib.wenJade != null ? lib.wenJade : lib.wenEnvelope != null ? lib.wenEnvelope : lib.wenBronze;
        var kind = sys.KindFor(prefab);
        if (kind != null) sys.Add(kind, at, worth);
    }

    // a pickup that exists as an object (placed by hand) becomes data next frame
    internal static void Adopt(Pickup p)
    {
        var sys = Get();
        if (sys != null && !sys.adoptions.Contains(p)) sys.adoptions.Add(p);
    }

    public static int Count
    {
        get
        {
            if (instance == null) return 0;
            int n = 0;
            foreach (var k in instance.kinds) n += k.count;
            return n;
        }
    }

    // everything on the ground, gone without being collected
    public static void ClearAll()
    {
        if (instance == null) return;
        foreach (var k in instance.kinds) { k.count = 0; k.merge = -1; k.dirty = true; }
    }

    // everything on the ground moved onto one spot, e.g. the player, to be collected at once
    public static void GatherAllTo(Vector3 at)
    {
        if (instance == null) return;
        foreach (var k in instance.kinds)
        {
            for (int i = 0; i < k.count; i++) k.pos[i] = at;
            k.dirty = true;
        }
    }

    // the end of a wave: everything flies to the player, nearest first, in a swirl, and whatever
    // hasn't arrived after a few seconds (at least `seconds`) is collected where it is. a sweep
    // already going carries on
    public static void Sweep(float seconds)
    {
        var sys = Get();
        if (sys == null) return;
        if (sys.sweepStart >= 0f)
        {
            sys.sweepUntil = Mathf.Max(sys.sweepUntil, Time.time + Mathf.Max(0.05f, seconds));
            return;
        }
        sys.sweepStart = Time.time;
        sys.sweepUntil = Time.time + Mathf.Max(SweepMax, seconds);
        sys.sweepPieces = 0;
        sys.sweepCollected = 0;
        sys.sweepWen = 0;
        foreach (var k in sys.kinds)
        {
            for (int i = 0; i < k.count; i++) k.launch[i] = -1f;
            sys.sweepPieces += k.count;
        }
    }

    // ---- kinds

    private Kind KindFor(GameObject prefab)
    {
        if (byPrefab.TryGetValue(prefab, out var kind)) return kind;
        kind = Make(prefab);
        byPrefab[prefab] = kind;
        return kind;
    }

    private Kind Make(GameObject source)
    {
        var pickup = source.GetComponent<Pickup>();
        var sr = source.GetComponent<SpriteRenderer>();
        if (pickup == null || sr == null || sr.sprite == null) return null;

        var sprite = sr.sprite;
        if (byLook.TryGetValue((sprite, pickup.Wen), out var same)) return same;

        float scale = Mathf.Abs(source.transform.localScale.x);
        var kind = new Kind
        {
            wen = pickup.Wen,
            sound = pickup.Sound,
            volume = pickup.Volume,
            reach = source.TryGetComponent(out CircleCollider2D circle) ? circle.radius * scale : 0.5f * scale,
            half = sprite.rect.size / sprite.pixelsPerUnit * scale * 0.5f,
            offset = (sprite.rect.size * 0.5f - sprite.pivot) / sprite.pixelsPerUnit * scale,
            color = sr.color,
        };
        var rect = sprite.textureRect;
        var tex = sprite.texture;
        kind.uvMin = new Vector2(rect.xMin / tex.width, rect.yMin / tex.height);
        kind.uvMax = new Vector2(rect.xMax / tex.width, rect.yMax / tex.height);

        // its renderer: one mesh for every pickup of this kind, sorted where the prefab would be
        var go = new GameObject(source.name + " (all of them)");
        go.transform.SetParent(transform, false);
        kind.mesh = new Mesh { name = source.name + " batch", indexFormat = IndexFormat.UInt32 };
        kind.mesh.MarkDynamic();
        go.AddComponent<MeshFilter>().sharedMesh = kind.mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = BatchMaterial(sr);
        var block = new MaterialPropertyBlock();
        block.SetTexture("_MainTex", tex);
        mr.SetPropertyBlock(block);
        mr.sortingLayerID = sr.sortingLayerID;
        mr.sortingOrder = sr.sortingOrder;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;

        kinds.Add(kind);
        byLook[(sprite, pickup.Wen)] = kind;
        return kind;
    }

    private Material BatchMaterial(SpriteRenderer fallback)
    {
        if (material != null) return material;
        var shader = Shader.Find("Rogue/Sprite Batch");
        material = shader != null ? new Material(shader) { name = "Pickup batch" } : fallback.sharedMaterial;
        return material;
    }

    private void Add(Kind k, Vector3 at, int worth)
    {
        if (k.count == k.pos.Length)
        {
            System.Array.Resize(ref k.pos, k.count * 2);
            System.Array.Resize(ref k.pulled, k.count * 2);
            System.Array.Resize(ref k.worth, k.count * 2);
            System.Array.Resize(ref k.launch, k.count * 2);
            System.Array.Resize(ref k.vel, k.count * 2);
        }
        k.pos[k.count] = at;
        k.pulled[k.count] = false;
        k.worth[k.count] = worth;
        k.launch[k.count] = -1f;
        k.vel[k.count] = Vector2.zero;
        if (sweepStart >= 0f) sweepPieces++;
        k.count++;
        k.dirty = true;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        foreach (var k in kinds) if (k.mesh != null) Destroy(k.mesh);
        if (material != null && material.shader != null && material.name == "Pickup batch") Destroy(material);
    }

    // ---- the loop

    private void Update()
    {
        AdoptStrays();

        int total = 0;
        foreach (var k in kinds) total += k.count;
        if (total == 0) { if (sweepStart >= 0f) EndSweep(); return; }

        if (inventory == null)
        {
            inventory = FindFirstObjectByType<PlayerInventory>();
            if (inventory == null) return;
            magnet = inventory.GetComponentInChildren<MagnetArea>(true);
            if (inventory.TryGetComponent(out CircleCollider2D body))
                playerRadius = body.radius * Mathf.Abs(inventory.transform.lossyScale.x);
        }

        Vector2 player = inventory.transform.position;
        float magnetRadius = magnet != null ? magnet.Radius : 1.5f;
        float magnetSq = magnetRadius * magnetRadius;
        bool sweeping = sweepStart >= 0f;
        bool sweepOver = sweeping && Time.time >= sweepUntil;
        float dt = Time.deltaTime;
        float step = 1f - Mathf.Exp(-PullRate * dt);
        float now = Time.time;

        // a fresh sweep: when each takes off, by how far it is
        float furthest = 0f;
        if (sweeping)
            foreach (var k in kinds)
                for (int i = 0; i < k.count; i++)
                    if (k.launch[i] < 0f) furthest = Mathf.Max(furthest, (k.pos[i] - player).sqrMagnitude);
        furthest = Mathf.Sqrt(furthest);

        int collected = 0, biggest = 0;
        AudioClip sound = null;
        float volume = 0f;

        foreach (var k in kinds)
        {
            float reach = k.reach + playerRadius;
            float reachSq = reach * reach;
            var pos = k.pos;
            var pulled = k.pulled;
            for (int i = k.count - 1; i >= 0; i--)
            {
                Vector2 p = pos[i];
                float dx = player.x - p.x, dy = player.y - p.y;
                float sq = dx * dx + dy * dy;

                if (sq <= reachSq || sweepOver)
                {
                    collected += k.worth[i];
                    biggest = Mathf.Max(biggest, k.worth[i]);
                    if (k.sound != null) { sound = k.sound; volume = k.volume; }
                    int last = --k.count;
                    pos[i] = pos[last];
                    pulled[i] = pulled[last];
                    k.worth[i] = k.worth[last];
                    k.launch[i] = k.launch[last];
                    k.vel[i] = k.vel[last];
                    if (k.merge == i) k.merge = -1;
                    else if (k.merge == last) k.merge = i;
                    k.dirty = true;
                    continue;
                }

                if (sweeping)
                {
                    if (Fly(k, i, player, dx, dy, sq, reach, now, dt, furthest)) i++;   // arrived: collected next time round
                    continue;
                }

                if (!pulled[i] && sq <= magnetSq) pulled[i] = true;
                if (pulled[i] && step > 0f)
                {
                    pos[i] = new Vector2(p.x + dx * step, p.y + dy * step);
                    k.dirty = true;
                }
            }
        }
        if (collected > 0)
        {
            inventory.AddWen(collected);
            RunStats.PickedUpWen(collected);
            if (sweeping)
            {
                sweepWen += collected;
                // the pickups ring faster and higher as the sweep comes in
                if (sound != null && now >= nextSweepSound)
                {
                    nextSweepSound = now + 0.03f;
                    float done = sweepPieces > 0 ? Mathf.Clamp01((float)sweepCollected / sweepPieces) : 1f;
                    SfxPlayer.PlayAt(sound, player, volume, 0.9f + 0.7f * done);
                }
            }
            else if (sound != null) SfxPlayer.PlayAt(sound, player, volume);
            // a jade wen or an envelope says what it was worth
            var lib = VfxLibrary.Get;
            if (lib != null && biggest > lib.bronzeUpTo)
                PixelNumbers.Show(player + Vector2.up * 0.9f, biggest, false, biggest > lib.jadeUpTo ? new Color(1f, 0.45f, 0.4f) : new Color(0.55f, 0.95f, 0.8f), 1);
        }
        if (sweepOver) EndSweep();
    }

    // one piece's flight in a sweep: waiting its turn, then a hop out and round, then drawn in
    // harder and harder. true when it reached the player this frame (moved onto it, to be
    // collected on the next pass of the loop)
    private bool Fly(Kind k, int i, Vector2 player, float dx, float dy, float sq, float reach, float now, float dt, float furthest)
    {
        float d = Mathf.Sqrt(sq);
        Vector2 toward = d > 0.0001f ? new Vector2(dx / d, dy / d) : Vector2.up;
        if (k.launch[i] < 0f)
        {
            float share = furthest > 0.01f ? Mathf.Pow(d / furthest, 0.7f) : 0f;
            k.launch[i] = now + share * SweepStagger + Random.Range(0f, 0.06f);
            // out, away from the player, and round, all the same way so it reads as one whirl
            var round = new Vector2(-toward.y, toward.x);
            k.vel[i] = -toward * SweepHop * Random.Range(0.7f, 1.2f) + round * SweepSwirl * Random.Range(0.7f, 1.3f);
        }
        float flying = now - k.launch[i];
        if (flying < 0f) return false;

        Vector2 v = k.vel[i];
        v += toward * (SweepPull + SweepPullGrowth * flying) * dt;
        v *= Mathf.Exp(-SweepDrag * dt);
        if (v.sqrMagnitude > SweepTopSpeed * SweepTopSpeed) v = v.normalized * SweepTopSpeed;
        k.vel[i] = v;
        Vector2 move = v * dt;
        k.dirty = true;

        // about to pass through the player: it's arrived
        if (Vector2.Dot(move, toward) >= d - reach * 0.5f && flying > 0.1f)
        {
            k.pos[i] = player;
            sweepCollected++;
            return true;
        }
        k.pos[i] += move;
        return false;
    }

    // the sweep's last piece in: a cascade and a bell, and what it all came to
    private void EndSweep()
    {
        bool worthIt = sweepPieces >= 12 && sweepWen > 0;
        sweepStart = -1f;
        sweepUntil = -1f;
        if (!worthIt || inventory == null) return;
        if (totalSound == null) totalSound = Resources.Load<AudioClip>("Sfx/coins_total");
        Vector2 at = inventory.transform.position;
        if (totalSound != null) SfxPlayer.PlayAt(totalSound, at, 0.9f);
        PixelNumbers.Show(at + Vector2.up * 1.3f, sweepWen, true, new Color(1f, 0.85f, 0.3f), 2);
        sweepWen = 0;
    }

    private void AdoptStrays()
    {
        if (adoptions.Count == 0) return;
        foreach (var p in adoptions)
        {
            if (p == null || !p.gameObject.activeInHierarchy) continue;
            var kind = Make(p.gameObject);
            if (kind == null) continue;
            Add(kind, p.transform.position, kind.wen);
            ObjectPool.Recycle(p.gameObject);
        }
        adoptions.Clear();
    }

    private void LateUpdate()
    {
        foreach (var k in kinds)
            if (k.dirty) Rebuild(k);
    }

    private static void Rebuild(Kind k)
    {
        k.dirty = false;
        int n = k.count;
        EnsureCapacity(k, n);

        Vector2 h = k.half;
        var v = k.verts;
        for (int i = 0; i < n; i++)
        {
            Vector2 c = k.pos[i] + k.offset;
            int o = i * 4;
            v[o] = new Vector3(c.x - h.x, c.y - h.y, 0f);
            v[o + 1] = new Vector3(c.x - h.x, c.y + h.y, 0f);
            v[o + 2] = new Vector3(c.x + h.x, c.y + h.y, 0f);
            v[o + 3] = new Vector3(c.x + h.x, c.y - h.y, 0f);
        }

        const MeshUpdateFlags flags = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices;
        var mesh = k.mesh;
        mesh.Clear(true);
        if (n > 0)
        {
            mesh.SetVertices(v, 0, n * 4, flags);
            mesh.SetUVs(0, k.uvs, 0, n * 4, flags);
            mesh.SetColors(k.colors, 0, n * 4, flags);
            mesh.SetIndices(k.tris, 0, n * 6, MeshTopology.Triangles, 0, false);
        }
        mesh.bounds = Everywhere;
    }

    // the parts that are the same for every quad are written once, as room is made
    private static void EnsureCapacity(Kind k, int n)
    {
        if (k.verts.Length >= n * 4) return;
        int quads = Mathf.Max(256, Mathf.NextPowerOfTwo(n));
        k.verts = new Vector3[quads * 4];
        k.uvs = new Vector2[quads * 4];
        k.colors = new Color32[quads * 4];
        k.tris = new int[quads * 6];
        for (int i = 0; i < quads; i++)
        {
            int o = i * 4;
            k.uvs[o] = new Vector2(k.uvMin.x, k.uvMin.y);
            k.uvs[o + 1] = new Vector2(k.uvMin.x, k.uvMax.y);
            k.uvs[o + 2] = new Vector2(k.uvMax.x, k.uvMax.y);
            k.uvs[o + 3] = new Vector2(k.uvMax.x, k.uvMin.y);
            k.colors[o] = k.colors[o + 1] = k.colors[o + 2] = k.colors[o + 3] = k.color;
            int t = i * 6;
            k.tris[t] = o; k.tris[t + 1] = o + 1; k.tris[t + 2] = o + 2;
            k.tris[t + 3] = o; k.tris[t + 4] = o + 2; k.tris[t + 5] = o + 3;
        }
    }
}
