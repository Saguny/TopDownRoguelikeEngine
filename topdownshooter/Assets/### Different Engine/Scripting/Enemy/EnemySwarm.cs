using System.Collections.Generic;
using UnityEngine;

// moves every enemy in one loop per physics step, the way a bullet hell has to. each one heads for
// the player (or along its crossing heading), steps round the walls and props it's about to walk
// into using a map of the arena made once, and keeps a little room from its neighbours through a
// spatial grid. enemies no longer push each other in physics: at a couple of thousand, that and a
// raycast per enemy per step were nearly the whole frame. they still collide with walls, props
// and the player. made on demand by the first enemy, so no scene needs one
[DefaultExecutionOrder(-50)]
public class EnemySwarm : MonoBehaviour
{
    private static EnemySwarm instance;
    private static readonly List<EnemyMovement> members = new List<EnemyMovement>(2048);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        members.Clear();
    }

    public static int Count => members.Count;

    // every walking enemy within `radius` of `centre` (a lantern's aura, a charging bull's path),
    // added to `into`. a plain pass over the swarm: fine a few times a second
    public static void Near(Vector2 centre, float radius, List<EnemyMovement> into)
    {
        float r2 = radius * radius;
        for (int i = 0; i < members.Count; i++)
        {
            var m = members[i];
            if (m == null) continue;
            if (((Vector2)m.transform.position - centre).sqrMagnitude <= r2) into.Add(m);
        }
    }

    internal static void Join(EnemyMovement m)
    {
        if (!Application.isPlaying) return;
        if (instance == null) instance = new GameObject("EnemySwarm").AddComponent<EnemySwarm>();
        if (m.swarmIndex >= 0) return;
        m.swarmIndex = members.Count;
        members.Add(m);
    }

    internal static void Leave(EnemyMovement m)
    {
        int i = m.swarmIndex;
        if (i >= 0 && i < members.Count && members[i] == m)
        {
            int last = members.Count - 1;
            var moved = members[last];
            members[i] = moved;
            moved.swarmIndex = i;
            members.RemoveAt(last);
        }
        m.swarmIndex = -1;
    }

    // ---- spacing: a spatial hash rebuilt every step (counting sort, no allocations)
    private const float Cell = 1.2f;
    private const int Buckets = 4096;           // power of two
    private const int MaxNeighbours = 10;        // in a dense crowd, the first few are plenty
    [Tooltip("how hard crowding enemies push apart, in units per second at full overlap")]
    [SerializeField] private float spacingStrength = 3.5f;

    private readonly int[] bucketStart = new int[Buckets + 1];
    private readonly int[] bucketFill = new int[Buckets];
    private int[] order = new int[2048];
    private int[] bucketOf = new int[2048];
    private Vector2[] positions = new Vector2[2048];
    private float[] radii = new float[2048];

    private Transform player;
    private ObstacleMap obstacles;
    private bool obstaclesTried;

    // ---- shadows: a soft dark oval under every enemy, the whole crowd one mesh, so it stands on
    // the floor instead of floating over it. the boss draws its own

    private const float ShadowAlpha = 0.42f;
    private Mesh shadowMesh;
    private Vector3[] shadowVerts = new Vector3[0];
    private Vector2[] shadowUvs = new Vector2[0];
    private Color32[] shadowColors = new Color32[0];
    private int[] shadowTris = new int[0];

    private void MakeShadows()
    {
        var shader = Shader.Find("Rogue/Sprite Batch");
        if (shader == null) return;

        // an oval in hard pixels, dithered at the rim
        const int w = 24, h = 8;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float d = Mathf.Sqrt(Mathf.Pow((x + 0.5f - w * 0.5f) / (w * 0.5f), 2f) + Mathf.Pow((y + 0.5f - h * 0.5f) / (h * 0.5f), 2f));
                bool on = d < 0.7f || (d < 1f && ((x + y) & 1) == 0);
                px[y * w + x] = on ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            }
        tex.SetPixels32(px);
        tex.Apply();

        var go = new GameObject("Enemy Shadows");
        go.transform.SetParent(transform, false);
        shadowMesh = new Mesh { name = "Enemy Shadows", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        shadowMesh.MarkDynamic();
        go.AddComponent<MeshFilter>().sharedMesh = shadowMesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = new Material(shader) { mainTexture = tex, name = "Enemy Shadows" };
        mr.sortingLayerName = "Player";
        mr.sortingOrder = -3;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    private void LateUpdate()
    {
        if (shadowMesh == null) return;

        int n = 0;
        EnsureShadowRoom(members.Count);
        for (int i = 0; i < members.Count; i++)
        {
            var m = members[i];
            if (m == null) continue;
            if (!m.shadowKnown)
            {
                var sr = m.Sprite;
                if (sr == null || sr.sprite == null) continue;
                var b = sr.bounds;
                m.shadowWidth = b.size.x * 0.62f;
                m.feetOffset = b.min.y - m.transform.position.y + 0.05f;
                m.castsShadow = !m.TryGetComponent(out BossMarker _);
                m.shadowKnown = true;
            }
            if (!m.castsShadow) continue;

            Vector3 p = m.transform.position;
            float hw = m.shadowWidth * 0.5f, hh = m.shadowWidth * 0.17f, y = p.y + m.feetOffset;
            int o = n * 4;
            shadowVerts[o] = new Vector3(p.x - hw, y - hh, 0f);
            shadowVerts[o + 1] = new Vector3(p.x - hw, y + hh, 0f);
            shadowVerts[o + 2] = new Vector3(p.x + hw, y + hh, 0f);
            shadowVerts[o + 3] = new Vector3(p.x + hw, y - hh, 0f);
            n++;
        }

        const UnityEngine.Rendering.MeshUpdateFlags flags = UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds | UnityEngine.Rendering.MeshUpdateFlags.DontValidateIndices;
        shadowMesh.Clear(true);
        if (n > 0)
        {
            shadowMesh.SetVertices(shadowVerts, 0, n * 4, flags);
            shadowMesh.SetUVs(0, shadowUvs, 0, n * 4, flags);
            shadowMesh.SetColors(shadowColors, 0, n * 4, flags);
            shadowMesh.SetIndices(shadowTris, 0, n * 6, MeshTopology.Triangles, 0, false);
        }
        shadowMesh.bounds = new Bounds(Vector3.zero, new Vector3(100000f, 100000f, 1000f));
    }

    private void EnsureShadowRoom(int n)
    {
        if (shadowVerts.Length >= n * 4) return;
        int quads = Mathf.Max(256, Mathf.NextPowerOfTwo(n));
        shadowVerts = new Vector3[quads * 4];
        shadowUvs = new Vector2[quads * 4];
        shadowColors = new Color32[quads * 4];
        shadowTris = new int[quads * 6];
        var c = new Color32(0x0c, 0x08, 0x10, (byte)(ShadowAlpha * 255f));
        for (int i = 0; i < quads; i++)
        {
            int o = i * 4, t = i * 6;
            shadowUvs[o] = new Vector2(0f, 0f); shadowUvs[o + 1] = new Vector2(0f, 1f);
            shadowUvs[o + 2] = new Vector2(1f, 1f); shadowUvs[o + 3] = new Vector2(1f, 0f);
            shadowColors[o] = shadowColors[o + 1] = shadowColors[o + 2] = shadowColors[o + 3] = c;
            shadowTris[t] = o; shadowTris[t + 1] = o + 1; shadowTris[t + 2] = o + 2;
            shadowTris[t + 3] = o; shadowTris[t + 4] = o + 2; shadowTris[t + 5] = o + 3;
        }
    }

    private void Awake()
    {
        MakeShadows();
        // enemies pass through each other; the spacing below keeps them apart instead
        int enemy = LayerMask.NameToLayer("Enemy");
        if (enemy >= 0) Physics2D.IgnoreLayerCollision(enemy, enemy, true);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // steering once a frame at most: the velocity it sets carries through every physics step the
    // frame runs. a slow frame runs several steps to catch up, and steering the whole horde again
    // for each of them only made that frame slower still (the profiler at 1,500 enemies: four steps
    // a frame, the steering a fifth of it)
    private int steeredFrame = -1;

    private void FixedUpdate()
    {
        int n = members.Count;
        if (n == 0) return;
        if (Time.frameCount == steeredFrame) return;
        steeredFrame = Time.frameCount;

        if (player == null)
        {
            var p = FindFirstObjectByType<PlayerMovement>();
            if (p == null) return;
            player = p.transform;
        }
        if (!obstaclesTried)
        {
            obstaclesTried = true;
            obstacles = ObstacleMap.Build();
        }

        if (positions.Length < n)
        {
            int size = Mathf.NextPowerOfTwo(n);
            order = new int[size];
            bucketOf = new int[size];
            positions = new Vector2[size];
            radii = new float[size];
        }

        // where everyone is, bucketed by cell
        System.Array.Clear(bucketFill, 0, Buckets);
        for (int i = 0; i < n; i++)
        {
            var m = members[i];
            Vector2 p = m.Body.position;
            positions[i] = p;
            radii[i] = m.Radius;
            int b = Bucket(Mathf.FloorToInt(p.x / Cell), Mathf.FloorToInt(p.y / Cell));
            bucketOf[i] = b;
            bucketFill[b]++;
        }
        int sum = 0;
        for (int b = 0; b < Buckets; b++)
        {
            bucketStart[b] = sum;
            sum += bucketFill[b];
            bucketFill[b] = 0;
        }
        bucketStart[Buckets] = sum;
        for (int i = 0; i < n; i++)
        {
            int b = bucketOf[i];
            order[bucketStart[b] + bucketFill[b]++] = i;
        }

        Vector2 target = player.position;
        float now = Time.time;
        for (int i = 0; i < n; i++)
            members[i].Step(positions[i], target, Spacing(i), obstacles, now);
    }

    private static int Bucket(int cx, int cy) => ((cx * 73856093) ^ (cy * 19349663)) & (Buckets - 1);

    // a push away from the neighbours overlapping this one, strongest when right on top
    private Vector2 Spacing(int i)
    {
        Vector2 p = positions[i];
        float ri = radii[i];
        int cx = Mathf.FloorToInt(p.x / Cell), cy = Mathf.FloorToInt(p.y / Cell);
        Vector2 push = Vector2.zero;
        int seen = 0;
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                int b = Bucket(cx + dx, cy + dy);
                for (int k = bucketStart[b], end = bucketStart[b + 1]; k < end; k++)
                {
                    int j = order[k];
                    if (j == i) continue;
                    Vector2 d = p - positions[j];
                    float reach = (ri + radii[j]) * 0.9f;
                    float sq = d.sqrMagnitude;
                    if (sq >= reach * reach) continue;
                    if (sq < 0.000001f)
                    {
                        // exactly on top of each other: split them by index
                        d = new Vector2((i & 1) == 0 ? 1f : -1f, (i & 2) == 0 ? 0.5f : -0.5f);
                        sq = d.sqrMagnitude;
                    }
                    float dist = Mathf.Sqrt(sq);
                    push += d / dist * ((reach - dist) / reach);
                    if (++seen >= MaxNeighbours) return push * spacingStrength;
                }
            }
        }
        return push * spacingStrength;
    }

    // ---- obstacles: which cells of the arena are solid, found once. steering checks the cell
    // an enemy is about to walk into instead of casting against physics every step
    public sealed class ObstacleMap
    {
        private const float CellSize = 0.5f;
        private Rect area;
        private int width, height;
        private bool[] solid;

        private static readonly Vector2[] Turns = BuildTurns();

        private static Vector2[] BuildTurns()
        {
            // cos/sin of 35, 70, 105 and 140 degrees, tried on the preferred side first
            var t = new Vector2[4];
            for (int k = 0; k < 4; k++)
            {
                float a = (35f + k * 35f) * Mathf.Deg2Rad;
                t[k] = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            }
            return t;
        }

        public static ObstacleMap Build()
        {
            int mask = LayerMask.GetMask("Obstacles");
            if (mask == 0) return null;

            var director = FindFirstObjectByType<SpawnDirector>();
            Rect area = director != null ? director.PlayRect : new Rect(-9999, -9999, 19998, 19998);
            if (area.width > 1000f || area.height > 1000f) return null;     // no arena to map

            // the walls themselves sit just outside the play rect; take a margin so they're in it
            area.xMin -= 2f; area.yMin -= 2f; area.xMax += 2f; area.yMax += 2f;
            var map = new ObstacleMap
            {
                area = area,
                width = Mathf.CeilToInt(area.width / CellSize),
                height = Mathf.CeilToInt(area.height / CellSize),
            };
            map.solid = new bool[map.width * map.height];
            var box = new Vector2(CellSize * 0.9f, CellSize * 0.9f);
            for (int y = 0; y < map.height; y++)
                for (int x = 0; x < map.width; x++)
                {
                    var centre = new Vector2(area.xMin + (x + 0.5f) * CellSize, area.yMin + (y + 0.5f) * CellSize);
                    map.solid[y * map.width + x] = Physics2D.OverlapBox(centre, box, 0f, mask) != null;
                }
            return map;
        }

        public bool Solid(Vector2 p)
        {
            int x = Mathf.FloorToInt((p.x - area.xMin) / CellSize);
            int y = Mathf.FloorToInt((p.y - area.yMin) / CellSize);
            if (x < 0 || y < 0 || x >= width || y >= height) return true;
            return solid[y * width + x];
        }

        // the direction as it is if the way ahead is clear, otherwise turned toward open ground,
        // keeping to the side it chose last time so it doesn't dither in front of a wall
        public Vector2 Steer(Vector2 from, Vector2 dir, float lookAhead, ref float side)
        {
            if (dir.sqrMagnitude < 0.0001f) { side = 0f; return dir; }
            float look = Mathf.Max(0.6f, lookAhead * 0.6f);
            if (!Solid(from + dir * look) && !Solid(from + dir * (look * 0.5f)))
            {
                side = 0f;
                return dir;
            }

            for (int k = 0; k < Turns.Length; k++)
            {
                float first = side != 0f ? side : 1f;
                for (int s = 0; s < 2; s++)
                {
                    float sign = s == 0 ? first : -first;
                    var t = Turns[k];
                    var turned = new Vector2(dir.x * t.x - dir.y * t.y * sign, dir.x * t.y * sign + dir.y * t.x);
                    if (!Solid(from + turned * look))
                    {
                        side = sign;
                        return turned;
                    }
                }
            }
            return dir;     // boxed in: push on and let physics sort it out
        }
    }
}
