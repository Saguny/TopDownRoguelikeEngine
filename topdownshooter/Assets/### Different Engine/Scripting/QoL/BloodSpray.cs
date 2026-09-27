using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// the player's blood, in the characters' own pixels: spurts of droplets that fly out, arc up
// and fall back to the floor, where they stay a while as stains and fade. a hit spurts for a
// moment and every hit after keeps it going; a death bleeds for seconds. droplets and stains are
// two meshes, drawn in one go each however many there are. with Show Blood off the droplets are
// motes of qi instead, and leave nothing behind
public class BloodSpray : MonoBehaviour
{
    // one pixel of the world's art (the floor, the enemies, the effects), in world units
    public const float Pixel = 1f / 28.46154f;

    private const float Gravity = 13f;
    private const float Drag = 1.6f;
    private const float StainSeconds = 4f, StainFade = 1.4f;
    private const int MaxDrops = 600, MaxStains = 500;

    private static readonly Color32[] Blood =
    {
        new Color32(0x4a, 0x06, 0x12, 0xff), new Color32(0x8e, 0x0f, 0x1e, 0xff),
        new Color32(0xc4, 0x16, 0x24, 0xff), new Color32(0xe8, 0x3a, 0x3a, 0xff),
    };
    private static readonly Color32[] Stain = { new Color32(0x3a, 0x04, 0x0e, 0xff), new Color32(0x62, 0x08, 0x16, 0xff) };
    private static readonly Color32[] Qi =
    {
        new Color32(0x5a, 0xa8, 0xe0, 0xff), new Color32(0x9f, 0xd8, 0xff, 0xff), new Color32(0xe8, 0xf6, 0xff, 0xff),
    };

    private struct Drop
    {
        public Vector2 ground, vel;     // where it is on the floor, and how fast it's crossing it
        public float height, rise;      // how high above that it is, and how fast it's climbing
        public int size;                // pixels square
        public Color32 color;
        public bool blood;
    }

    private struct Mark
    {
        public Vector2 at;
        public int w, h;
        public Color32 color;
        public float born;
    }

    // something bleeding: a follow target (with its feet below it) or a fixed spot
    private sealed class Wound
    {
        public Transform follow;
        public bool follows;
        public Vector2 feet;            // the fixed spot, or the offset from the target to its feet
        public float left, length, nextSpurt, strength;
        public Vector2 Ground => follows && follow != null ? (Vector2)follow.position + feet : feet;
    }

    private static BloodSpray instance;
    private readonly List<Drop> drops = new List<Drop>(256);
    private readonly List<Mark> stains = new List<Mark>(256);
    private readonly List<Wound> wounds = new List<Wound>();
    private Layer air, floor;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    private static BloodSpray Get()
    {
        if (instance == null && Application.isPlaying) instance = new GameObject("Blood").AddComponent<BloodSpray>();
        return instance;
    }

    // a splash of count droplets from a body whose feet are at ground, spurting from its middle
    public static void Burst(Vector2 ground, int count, float power = 1f)
    {
        var b = Get();
        if (b == null) return;
        for (int i = 0; i < count; i++)
            b.Spray(ground, Random.Range(0f, 360f), 180f, power);
    }

    // keep spurting from a body for a while. hitting the same body again tops the time up
    public static void Bleed(Transform body, Vector2 feetOffset, float seconds, float strength = 1f)
    {
        var b = Get();
        if (b == null || body == null) return;
        foreach (var w in b.wounds)
            if (w.follow == body)
            {
                w.left = Mathf.Max(w.left, seconds);
                w.length = Mathf.Max(w.left, w.length);
                w.strength = Mathf.Max(w.strength, strength);
                w.feet = feetOffset;
                return;
            }
        b.wounds.Add(new Wound { follow = body, follows = true, feet = feetOffset, left = seconds, length = seconds, strength = strength });
    }

    // the same from a spot that doesn't move, e.g. a body lying where it fell
    public static void Bleed(Vector2 ground, float seconds, float strength = 1f)
    {
        var b = Get();
        if (b == null) return;
        b.wounds.Add(new Wound { feet = ground, left = seconds, length = seconds, strength = strength });
    }

    // a droplet thrown from the body's middle at an angle, give or take spread
    private void Spray(Vector2 ground, float angle, float spread, float power)
    {
        if (drops.Count >= MaxDrops) return;
        bool blood = GameSettings.ShowBlood;
        float a = (angle + Random.Range(-spread, spread) * 0.5f) * Mathf.Deg2Rad;
        float speed = Random.Range(0.7f, 2.6f) * power;
        var palette = blood ? Blood : Qi;
        drops.Add(new Drop
        {
            ground = ground + Random.insideUnitCircle * 0.06f,
            vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.75f) * speed,     // a little flattened: the floor is seen at a slant
            height = Random.Range(0.5f, 0.95f),
            rise = Random.Range(1.6f, 3.8f) * Mathf.Sqrt(power),
            size = Random.value < 0.3f ? 2 : 1,
            color = palette[Random.Range(0, palette.Length)],
            blood = blood,
        });
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;   // paused: everything holds where it is

        // wounds spurt in beats, a jet of a few droplets one way, weaker as the wound runs out
        for (int i = wounds.Count - 1; i >= 0; i--)
        {
            var w = wounds[i];
            w.left -= dt;
            if (w.left <= 0f || (w.follows && w.follow == null))
            {
                wounds.RemoveAt(i);
                continue;
            }
            w.nextSpurt -= dt;
            if (w.nextSpurt > 0f) continue;
            float fade = Mathf.Clamp01(w.left / Mathf.Max(0.01f, w.length));
            w.nextSpurt = Random.Range(0.1f, 0.2f);
            int n = Mathf.Max(1, Mathf.RoundToInt(Random.Range(3f, 6f) * w.strength * (0.35f + 0.65f * fade)));
            float angle = Random.Range(0f, 360f);
            var ground = w.Ground;
            for (int k = 0; k < n; k++) Spray(ground, angle, 50f, 0.6f + 0.6f * fade);
        }

        // droplets fly, fall and land
        float drag = Mathf.Exp(-Drag * dt);
        float now = Time.time;
        for (int i = drops.Count - 1; i >= 0; i--)
        {
            var d = drops[i];
            d.ground += d.vel * dt;
            d.vel *= drag;
            d.rise -= Gravity * dt;
            d.height += d.rise * dt;
            if (d.height > 0f)
            {
                drops[i] = d;
                continue;
            }

            drops.RemoveAt(i);
            if (!d.blood) continue;
            if (stains.Count >= MaxStains) stains.RemoveAt(0);
            // a heavier drop leaves a splat, smeared the way it was going
            bool smear = d.vel.sqrMagnitude > 1f;
            stains.Add(new Mark
            {
                at = d.ground,
                w = d.size + (smear ? 1 : 0) + (Random.value < 0.25f ? 1 : 0),
                h = d.size,
                color = Stain[Random.Range(0, Stain.Length)],
                born = now,
            });
        }

        for (int i = stains.Count - 1; i >= 0; i--)
            if (now - stains[i].born > StainSeconds) stains.RemoveAt(i);
    }

    private void LateUpdate()
    {
        if (air == null)
        {
            air = new Layer(transform, "Blood (air)", "Default", 150);
            floor = new Layer(transform, "Blood (floor)", "Background", short.MaxValue - 1);
        }

        air.Begin(drops.Count);
        foreach (var d in drops)
            air.Quad(Snap(d.ground + new Vector2(0f, d.height)), d.size, d.size, d.color);
        air.End();

        float now = Time.time;
        floor.Begin(stains.Count);
        foreach (var s in stains)
        {
            float age = now - s.born;
            var c = s.color;
            c.a = (byte)(255f * Mathf.Clamp01((StainSeconds - age) / StainFade));
            floor.Quad(Snap(s.at), s.w, s.h, c);
        }
        floor.End();
    }

    // on the art's pixel grid, so the blood sits in the same pixels as everything else
    private static Vector2 Snap(Vector2 p) => new Vector2(Mathf.Round(p.x / Pixel) * Pixel, Mathf.Round(p.y / Pixel) * Pixel);

    // a mesh of flat coloured squares
    private sealed class Layer
    {
        private readonly Mesh mesh;
        private Vector3[] verts = new Vector3[0];
        private Color32[] colors = new Color32[0];
        private Vector2[] uvs = new Vector2[0];
        private int[] tris = new int[0];
        private int count;

        public Layer(Transform parent, string name, string sortingLayer, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var shader = Shader.Find("Rogue/Sprite Batch");
            mr.sharedMaterial = new Material(shader != null ? shader : Shader.Find("Sprites/Default")) { mainTexture = Texture2D.whiteTexture, name = name };
            mr.sortingLayerName = sortingLayer;
            mr.sortingOrder = order;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        public void Begin(int quads)
        {
            count = 0;
            if (verts.Length >= quads * 4) return;
            int n = Mathf.Max(64, Mathf.NextPowerOfTwo(quads));
            verts = new Vector3[n * 4];
            colors = new Color32[n * 4];
            uvs = new Vector2[n * 4];
            tris = new int[n * 6];
        }

        public void Quad(Vector2 at, int w, int h, Color32 c)
        {
            int o = count * 4, t = count * 6;
            float x0 = at.x, y0 = at.y, x1 = at.x + w * Pixel, y1 = at.y + h * Pixel;
            verts[o] = new Vector3(x0, y0);
            verts[o + 1] = new Vector3(x0, y1);
            verts[o + 2] = new Vector3(x1, y1);
            verts[o + 3] = new Vector3(x1, y0);
            colors[o] = colors[o + 1] = colors[o + 2] = colors[o + 3] = c;
            uvs[o] = uvs[o + 1] = uvs[o + 2] = uvs[o + 3] = new Vector2(0.5f, 0.5f);
            tris[t] = o; tris[t + 1] = o + 1; tris[t + 2] = o + 2;
            tris[t + 3] = o; tris[t + 4] = o + 2; tris[t + 5] = o + 3;
            count++;
        }

        public void End()
        {
            // the unused tail collapses to nothing
            for (int i = count * 4; i < verts.Length; i++) verts[i] = Vector3.zero;
            mesh.Clear();
            mesh.vertices = verts;
            mesh.colors32 = colors;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(100000f, 100000f, 1000f));
        }
    }
}
