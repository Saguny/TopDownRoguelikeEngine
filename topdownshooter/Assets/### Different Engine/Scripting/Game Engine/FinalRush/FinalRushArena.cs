using System.Collections.Generic;
using UnityEngine;

// the Final Rush's arena: a ring of spirit seals, paper talismans on stakes, planted round the
// player when the wave's time runs out. they rise one after another round the circle and the
// player can't walk through them (FinalRushPlayerClamp); the horde comes and goes as it likes.
// the seals are drawn at exactly Radius, so the wall is where it looks like it is
public class FinalRushArena : MonoBehaviour
{
    [Header("Radius")]
    [SerializeField] private float radius = 15.15f;
    [Tooltip("how far inside the seals the player is stopped, so they stand against them rather than on them")]
    [SerializeField, Min(0f)] private float playerInset = 0.45f;

    [Header("Spirit seals")]
    [Tooltip("one seal standing, looping: the paper stirring and the glyph glowing. empty draws a plain talisman")]
    public Sprite[] sealFrames = new Sprite[0];
    [Min(0.01f)] public float sealFps = 8f;
    [Tooltip("played once as each seal rises out of the ground, before its loop")]
    public Sprite[] riseFrames = new Sprite[0];
    [Min(0.01f)] public float riseFps = 20f;
    [Tooltip("world distance between neighbouring seals round the ring")]
    [Min(0.2f)] public float spacing = 1.05f;
    [Tooltip("seconds for the seals to rise all the way round the circle")]
    [Min(0f)] public float riseSweepSeconds = 0.8f;
    public string sortingLayer = "Player";
    public int sortingOrder = 2;

    [Tooltip("the old hand-drawn boundary sprite in this prefab is hidden in favour of the seals")]
    [SerializeField] private bool hideHandDrawnBoundary = true;

    public float Radius => radius;
    // where the player is held: just inside the seals
    public float WalkRadius => Mathf.Max(0.5f, radius - playerInset);

    private sealed class Seal
    {
        public SpriteRenderer sr;
        public float born, phase;
        public bool looping;
    }

    private readonly List<Seal> seals = new List<Seal>();
    private static Sprite plain;

    public void Initialize(Vector3 center)
    {
        transform.position = center;
        // before the seals go in, so only the prefab's own art is hidden
        if (hideHandDrawnBoundary)
            foreach (var old in GetComponentsInChildren<SpriteRenderer>(true)) old.enabled = false;
        Plant();
    }

    // one seal every Spacing round the circle, rising in a sweep that starts at the top
    private void Plant()
    {
        int count = Mathf.Max(8, Mathf.RoundToInt(2f * Mathf.PI * radius / spacing));
        var holder = new GameObject("Spirit Seals").transform;
        holder.SetParent(transform, false);

        for (int i = 0; i < count; i++)
        {
            float k = (float)i / count;
            float a = Mathf.PI * 0.5f - k * 2f * Mathf.PI;
            var at = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius;

            var go = new GameObject("Seal");
            go.transform.SetParent(holder, false);
            go.transform.localPosition = at;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = sortingLayer;
            // the lower on screen, the further in front, like everything standing on the floor
            sr.sortingOrder = sortingOrder;
            sr.spriteSortPoint = SpriteSortPoint.Pivot;
            sr.enabled = false;

            seals.Add(new Seal { sr = sr, born = Time.time + k * riseSweepSeconds, phase = Random.value * 10f });
        }
    }

    private void Update()
    {
        bool animated = sealFrames != null && sealFrames.Length > 0 && sealFrames[0] != null;
        bool rises = riseFrames != null && riseFrames.Length > 0 && riseFrames[0] != null;
        float now = Time.time;

        foreach (var s in seals)
        {
            if (s.sr == null) continue;
            float age = now - s.born;
            if (age < 0f) continue;
            s.sr.enabled = true;

            if (!animated)
            {
                s.sr.sprite = Plain();
                // a plain seal pops up out of the ground
                float pop = Mathf.Clamp01(age / 0.15f);
                s.sr.transform.localScale = new Vector3(1f, pop, 1f);
                continue;
            }

            float riseLength = rises ? riseFrames.Length / riseFps : 0f;
            if (rises && age < riseLength)
            {
                s.sr.sprite = riseFrames[Mathf.Min(riseFrames.Length - 1, (int)(age * riseFps))];
                continue;
            }
            s.sr.sprite = sealFrames[(int)((age - riseLength + s.phase) * sealFps) % sealFrames.Length];
        }
    }

    // a talisman on a stake, drawn in code, for until the seal art is built
    private static Sprite Plain()
    {
        if (plain != null) return plain;
        const int w = 9, h = 22;
        var paper = new Color32(0xf2, 0xc2, 0x30, 0xff);
        var lit = new Color32(0xff, 0xf0, 0x7a, 0xff);
        var ink = new Color32(0xd0, 0x28, 0x38, 0xff);
        var wood = new Color32(0x7e, 0x42, 0x20, 0xff);
        var line = new Color32(0x1a, 0x0c, 0x26, 0xff);
        var clear = new Color32(0, 0, 0, 0);
        var px = new Color32[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = clear;
        void Put(int x, int y, Color32 c) { if (x >= 0 && y >= 0 && x < w && y < h) px[(h - 1 - y) * w + x] = c; }
        for (int y = 12; y < h - 1; y++) Put(4, y, wood);                       // the stake
        for (int y = 1; y <= 12; y++)
            for (int x = 1; x <= 7; x++)
                Put(x, y, x >= 6 ? lit : paper);                                // the paper
        for (int y = 3; y <= 10; y += 2) Put(4, y, ink);                        // its glyph
        Put(3, 4, ink); Put(5, 6, ink); Put(3, 8, ink);
        for (int y = 0; y < h; y++)                                             // a dark outline
            for (int x = 0; x < w; x++)
            {
                if (px[(h - 1 - y) * w + x].a != 0) continue;
                bool edge = false;
                for (int k = 0; k < 4 && !edge; k++)
                {
                    int nx = x + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    edge = nx >= 0 && ny >= 0 && nx < w && ny < h && px[(h - 1 - ny) * w + nx].a != 0 && !px[(h - 1 - ny) * w + nx].Equals(line);
                }
                if (edge) Put(x, y, line);
            }

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        tex.SetPixels32(px);
        tex.Apply();
        // the world's pixel size, the pivot at the foot of the stake
        plain = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 37f / 1.3f);
        plain.hideFlags = HideFlags.DontSave;
        return plain;
    }
}
