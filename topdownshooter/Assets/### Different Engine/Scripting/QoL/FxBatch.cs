using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// one-shot sprite animations by the hundred, e.g. every enemy dying at once: each animation is one
// mesh holding every copy that's playing, so a mass kill is one draw and no objects. an
// animation's frames must share one texture (an Aseprite file's frames do)
public class FxBatch : MonoBehaviour
{
    private sealed class Kind
    {
        public Sprite[] frames;
        public float fps;
        public Vector2[] uvMin, uvMax, vMin, vMax;     // per frame
        public int count;
        public Vector2[] pos = new Vector2[128];
        public float[] start = new float[128];
        public float[] scale = new float[128];
        public Mesh mesh;
        public Vector3[] verts = new Vector3[0];
        public Vector2[] uvs = new Vector2[0];
        public Color32[] colors = new Color32[0];
        public int[] tris = new int[0];
    }

    private static FxBatch instance;
    private readonly Dictionary<Sprite[], Kind> byFrames = new Dictionary<Sprite[], Kind>();
    private readonly List<Kind> kinds = new List<Kind>();
    private static readonly Bounds Everywhere = new Bounds(Vector3.zero, new Vector3(100000f, 100000f, 1000f));

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    public static void Play(Sprite[] frames, float fps, Vector2 at, float scale = 1f, string layer = "Aura", int order = 3)
    {
        if (frames == null || frames.Length == 0 || frames[0] == null || !Application.isPlaying) return;
        if (instance == null) instance = new GameObject("FxBatch").AddComponent<FxBatch>();
        var k = instance.KindFor(frames, fps, layer, order);
        if (k == null) return;

        if (k.count == k.pos.Length)
        {
            System.Array.Resize(ref k.pos, k.count * 2);
            System.Array.Resize(ref k.start, k.count * 2);
            System.Array.Resize(ref k.scale, k.count * 2);
        }
        k.pos[k.count] = at;
        k.start[k.count] = Time.time;
        k.scale[k.count] = scale;
        k.count++;
    }

    private Kind KindFor(Sprite[] frames, float fps, string layer, int order)
    {
        if (byFrames.TryGetValue(frames, out var k)) return k;
        var shader = Shader.Find("Rogue/Sprite Batch");
        var tex = frames[0].texture;
        if (shader == null || tex == null) return null;

        int n = frames.Length;
        k = new Kind { frames = frames, fps = Mathf.Max(1f, fps), uvMin = new Vector2[n], uvMax = new Vector2[n], vMin = new Vector2[n], vMax = new Vector2[n] };
        var size = new Vector2(tex.width, tex.height);
        for (int i = 0; i < n; i++)
        {
            var s = frames[i] != null ? frames[i] : frames[0];
            var r = s.rect;
            k.uvMin[i] = r.min / size;
            k.uvMax[i] = r.max / size;
            k.vMin[i] = -s.pivot / s.pixelsPerUnit;
            k.vMax[i] = (r.size - s.pivot) / s.pixelsPerUnit;
        }

        var go = new GameObject(frames[0].name + " (batched)");
        go.transform.SetParent(transform, false);
        k.mesh = new Mesh { name = go.name, indexFormat = IndexFormat.UInt32 };
        k.mesh.MarkDynamic();
        go.AddComponent<MeshFilter>().sharedMesh = k.mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = new Material(shader) { mainTexture = tex, name = go.name };
        mr.sortingLayerName = layer;
        mr.sortingOrder = order;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;

        byFrames[frames] = k;
        kinds.Add(k);
        return k;
    }

    private void LateUpdate()
    {
        float now = Time.time;
        foreach (var k in kinds)
        {
            float length = k.frames.Length / k.fps;
            for (int i = k.count - 1; i >= 0; i--)
            {
                if (now - k.start[i] < length) continue;
                int last = --k.count;
                k.pos[i] = k.pos[last];
                k.start[i] = k.start[last];
                k.scale[i] = k.scale[last];
            }
            Rebuild(k, now);
        }
    }

    private static void Rebuild(Kind k, float now)
    {
        int n = k.count;
        if (k.verts.Length < n * 4)
        {
            int quads = Mathf.Max(64, Mathf.NextPowerOfTwo(n));
            k.verts = new Vector3[quads * 4];
            k.uvs = new Vector2[quads * 4];
            k.colors = new Color32[quads * 4];
            k.tris = new int[quads * 6];
            for (int q = 0; q < quads; q++)
            {
                int o = q * 4, t = q * 6;
                k.colors[o] = k.colors[o + 1] = k.colors[o + 2] = k.colors[o + 3] = new Color32(255, 255, 255, 255);
                k.tris[t] = o; k.tris[t + 1] = o + 1; k.tris[t + 2] = o + 2;
                k.tris[t + 3] = o; k.tris[t + 4] = o + 2; k.tris[t + 5] = o + 3;
            }
        }

        for (int i = 0; i < n; i++)
        {
            int f = Mathf.Min(k.frames.Length - 1, (int)((now - k.start[i]) * k.fps));
            Vector2 p = k.pos[i], a = k.vMin[f] * k.scale[i], b = k.vMax[f] * k.scale[i], u0 = k.uvMin[f], u1 = k.uvMax[f];
            int o = i * 4;
            k.verts[o] = new Vector3(p.x + a.x, p.y + a.y, 0f);
            k.verts[o + 1] = new Vector3(p.x + a.x, p.y + b.y, 0f);
            k.verts[o + 2] = new Vector3(p.x + b.x, p.y + b.y, 0f);
            k.verts[o + 3] = new Vector3(p.x + b.x, p.y + a.y, 0f);
            k.uvs[o] = new Vector2(u0.x, u0.y);
            k.uvs[o + 1] = new Vector2(u0.x, u1.y);
            k.uvs[o + 2] = new Vector2(u1.x, u1.y);
            k.uvs[o + 3] = new Vector2(u1.x, u0.y);
        }

        const MeshUpdateFlags flags = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices;
        k.mesh.Clear(true);
        if (n > 0)
        {
            k.mesh.SetVertices(k.verts, 0, n * 4, flags);
            k.mesh.SetUVs(0, k.uvs, 0, n * 4, flags);
            k.mesh.SetColors(k.colors, 0, n * 4, flags);
            k.mesh.SetIndices(k.tris, 0, n * 6, MeshTopology.Triangles, 0, false);
        }
        k.mesh.bounds = Everywhere;
    }
}
