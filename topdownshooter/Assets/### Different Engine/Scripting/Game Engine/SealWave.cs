using System.Collections.Generic;
using UnityEngine;

// the end of a Final Rush: the spirit seal's wave. a flare at the player, then a ring of gold dashes
// and red seal stamps rolling out across the field, fast at first and easing off past the screen's
// edge, and every enemy it passes is sealed: a stamp slammed on it and cracking into gold, a seal's
// knock that climbs in pitch as the wave rolls on. what's still out past the screen goes as it ends.
// its art and sounds come from Resources (FinalRush, Sfx; made by Tools/VFX/rush and Tools/SFX/finale.py)
public class SealWave : MonoBehaviour
{
    private const float WorldPpu = 37f / 1.3f;
    private const float Seconds = 1.15f;
    private const float DashSpacing = 0.8f;         // along the front, in units
    private const int MaxDashes = 200;
    private const int SealEvery = 5;                // one seal stamp riding the front for every few dashes
    private const int BurstsPerFrame = 40;          // enemies sealed in a frame that get their own stamp
    private const float SealSoundEvery = 0.035f;

    private static Sprite[] dash, seal, burst, origin;
    private static AudioClip clearSound, sealSound;
    private static bool loaded;

    public static bool Running { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Running = false;

    private struct Target { public GameObject go; public EnemyHealth health; }

    private readonly List<Target> left = new List<Target>(512);
    private readonly List<SpriteRenderer> front = new List<SpriteRenderer>(MaxDashes);
    private Vector2 center;
    private float reach, age, nextSealSound;
    private int sealed_, total;
    private Camera cam;

    // rolls out from `at`, sealing every enemy there is
    public static void Roll(Vector3 at)
    {
        Load();
        var go = new GameObject("Seal Wave");
        go.transform.position = new Vector3(at.x, at.y, 0f);
        go.AddComponent<SealWave>().Begin(at);
    }

    private static void Load()
    {
        if (loaded) return;
        loaded = true;
        dash = Slice("FinalRush/rush_dash");
        seal = Slice("FinalRush/rush_seal");
        burst = Slice("FinalRush/rush_burst");
        origin = Slice("FinalRush/rush_origin");
        clearSound = Resources.Load<AudioClip>("Sfx/rush_clear");
        sealSound = Resources.Load<AudioClip>("Sfx/rush_seal");
    }

    // a strip of square frames side by side, cut into sprites at the world's pixel size
    private static Sprite[] Slice(string path)
    {
        var strip = Resources.Load<Texture2D>(path);
        if (strip == null) return null;
        strip.filterMode = FilterMode.Point;
        int size = strip.height, n = Mathf.Max(1, strip.width / size);
        var frames = new Sprite[n];
        for (int i = 0; i < n; i++)
            frames[i] = Sprite.Create(strip, new Rect(i * size, 0, size, size), new Vector2(0.5f, 0.5f), WorldPpu, 0, SpriteMeshType.FullRect);
        return frames;
    }

    private void Begin(Vector3 at)
    {
        Running = true;
        center = at;
        cam = Camera.main;

        // out past the corners of the screen; whatever's further than that goes at the end
        reach = 14f;
        if (cam != null && cam.orthographic)
        {
            float h = cam.orthographicSize;
            reach = Mathf.Sqrt(h * h * (1f + cam.aspect * cam.aspect)) + 1.5f;
            Vector2 off = (Vector2)cam.transform.position - center;
            reach += off.magnitude;
        }

        foreach (var e in EnemyRegistry.All)
            if (e != null) left.Add(new Target { go = e, health = e.GetComponent<EnemyHealth>() });
        total = Mathf.Max(1, left.Count);

        // the flare, the gong, the whole screen jolting
        if (origin != null) FxBatch.Play(origin, 22f, center, 2f, "Aura", 9);
        if (clearSound != null) SfxPlayer.PlayAt(clearSound, center, 1f);
        Juice.Shake(0.45f);
        Juice.Freeze(0.05f);

        for (int i = 0; i < MaxDashes; i++)
        {
            var go = new GameObject("Front");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = "Aura";
            sr.sortingOrder = 8;
            sr.enabled = false;
            front.Add(sr);
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        age += dt;
        float u = Mathf.Clamp01(age / Seconds);
        // fast off the mark, easing out as it reaches the screen's edge
        float r = reach * (1f - Mathf.Pow(1f - u, 3f));

        Seal(r, u >= 1f);
        Draw(r, u);

        if (u >= 1f) Finish();
    }

    private void Seal(float r, bool everything)
    {
        float rSq = r * r;
        int bursts = 0;
        Rect view = View();
        for (int i = left.Count - 1; i >= 0; i--)
        {
            var t = left[i];
            if (t.go == null || !t.go.activeInHierarchy) { RemoveAt(i); continue; }
            Vector2 p = t.go.transform.position;
            if (!everything && (p - center).sqrMagnitude > rSq) continue;
            RemoveAt(i);

            bool seen = view.Contains(p);
            if (seen && bursts < BurstsPerFrame && burst != null)
            {
                FxBatch.Play(burst, 24f, p, 1f, "Aura", 7);
                bursts++;
            }
            if (t.health != null) t.health.TakeDamage(t.health.Current + t.health.Max + 1f, DamageKind.Silent);
            else Destroy(t.go);
            sealed_++;

            // a seal's knock as they go, faster and higher as the wave rolls on
            if (seen && sealSound != null && Time.time >= nextSealSound)
            {
                nextSealSound = Time.time + SealSoundEvery;
                float pitch = 0.85f + 0.65f * Mathf.Clamp01((float)sealed_ / total) + Random.Range(-0.03f, 0.03f);
                SfxPlayer.PlayAt(sealSound, p, 0.5f, pitch);
            }
        }
    }

    private void RemoveAt(int i)
    {
        int last = left.Count - 1;
        left[i] = left[last];
        left.RemoveAt(last);
    }

    private Rect View()
    {
        if (cam == null || !cam.orthographic) return new Rect(center - Vector2.one * 20f, Vector2.one * 40f);
        float h = cam.orthographicSize + 1f, w = cam.orthographicSize * cam.aspect + 1f;
        Vector2 c = cam.transform.position;
        return new Rect(c.x - w, c.y - h, w * 2f, h * 2f);
    }

    // the front: dashes lying along it, bright edge outward, with seal stamps riding between them.
    // they multiply as it widens so the ring never thins out, and fade as it spends itself
    private void Draw(float r, float u)
    {
        if (dash == null) return;
        int n = Mathf.Clamp(Mathf.CeilToInt(2f * Mathf.PI * r / DashSpacing), 10, MaxDashes);
        float alpha = u < 0.65f ? 1f : 1f - (u - 0.65f) / 0.35f;
        float spin = age * 0.35f;
        int frame = (int)(age * 14f);
        for (int i = 0; i < front.Count; i++)
        {
            var sr = front[i];
            if (i >= n || alpha <= 0f) { sr.enabled = false; continue; }
            float a = spin + i * Mathf.PI * 2f / n;
            // a pixel or two of wobble, so it reads as light rather than a drawn circle
            float wob = (Mathf.PerlinNoise(i * 0.37f, age * 3f) - 0.5f) * 0.3f;
            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            sr.transform.position = center + dir * (r + wob);
            bool stamp = seal != null && i % SealEvery == 0;
            var frames = stamp ? seal : dash;
            sr.sprite = frames[(frame + i) % frames.Length];
            // dashes lie along the front with their bright edge outward; the stamps stay upright
            sr.transform.rotation = stamp ? Quaternion.identity : Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg - 90f);
            sr.color = new Color(1f, 1f, 1f, alpha);
            sr.enabled = true;
        }
    }

    private void Finish()
    {
        Running = false;
        Juice.Shake(0.15f);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (Running) Running = false;
    }
}
