using UnityEngine;
using UnityEngine.SceneManagement;

// the anti afk half of endless. enemy health doubling forever guarantees every run ends, but a
// strong stationary build could still stand in one spot for most of an hour. telegraphed strikes
// land on the player on a clock, so standing still is never a strategy. damage is a share of max
// health, so stacking health can't out-tank it and it never needs rebalancing against the enemy
// curve. in an auto shooter, movement is the one skill input, so this is where skill lives.
// it's Heaven's thunder: a thunder seal marks the ground (the eight trigrams round a bolt, a red
// disc filling it as the countdown), the storm gathers, and a bolt comes down on it. art from
// Tools/VFX/alerts (Resources/Hazards), sounds from Tools/SFX/thunder.py (Resources/Sfx)
public class Bombardment : MonoBehaviour
{
    [Header("Schedule (survival seconds)")]
    [SerializeField] private float startAfter = 150f;
    [SerializeField] private float startInterval = 14f;
    [SerializeField] private float minInterval = 3.5f;
    [Tooltip("seconds for the interval to close ~63% of the gap down to minInterval")]
    [SerializeField] private float intervalRamp = 600f;

    [Header("Salvo")]
    [Tooltip("seconds after the first strike before each salvo gains another shell")]
    [SerializeField] private float extraShellEvery = 480f;
    [SerializeField, Min(1)] private int maxShells = 4;
    [SerializeField] private float scatter = 3.5f;

    [Header("Strike")]
    [SerializeField] private float telegraph = 1.3f;
    [SerializeField] private float radius = 2.4f;
    [SerializeField, Range(0f, 1f)] private float maxHealthFraction = 0.2f;
    [SerializeField] private Color color = new Color(1f, 0.25f, 0.2f, 1f);
    [SerializeField] private float impactShake = 0.14f;
    [SerializeField] private float flashTime = 0.12f;
    [SerializeField] private string sortingLayer = "Aura";

    private const int PoolSize = 12;

    private SpawnDirector director;
    private Transform player;
    private PlayerHealth playerHealth;
    private Rigidbody2D playerBody;

    private float nextSalvo = -1f;
    private Shell[] shells;
    private static Sprite disc;
    private static Sprite ring;
    private Sprite[] sealFrames, fillFrames, boltFrames, burstFrames;
    private AudioClip warnSound, hitSound;
    private bool art;

    // the art's seal is drawn 66 pixels out from its centre; the bolt's cell is 192 tall with its
    // foot 3 pixels up from the bottom
    private const float SealArtRadius = 66f / YamaArt.WorldPpu;
    private const float BoltCell = 192f / YamaArt.WorldPpu;
    private const float BoltFoot = 3f / YamaArt.WorldPpu;
    private const float BurstArtRadius = 64f / YamaArt.WorldPpu;

    private class Shell
    {
        public GameObject root;
        public SpriteRenderer ring;
        public SpriteRenderer fill;
        public float age;
        public bool detonated;
        public bool Busy => root.activeSelf;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // installs itself into any scene with a SpawnDirector, so nothing has to be wired. drop one
    // into the scene by hand to tune it in the inspector; this only adds one when none exists
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!GameMode.IsEndless) return;
        if (FindFirstObjectByType<SpawnDirector>() == null) return;
        if (FindFirstObjectByType<Bombardment>() != null) return;

        new GameObject("Bombardment").AddComponent<Bombardment>();
    }

    private void Awake()
    {
        sealFrames = YamaArt.Strip("Hazards/strike_seal");
        fillFrames = YamaArt.Strip("Hazards/strike_fill");
        boltFrames = YamaArt.Strip("Hazards/strike_bolt");
        burstFrames = YamaArt.Strip("Hazards/strike_burst");
        warnSound = Resources.Load<AudioClip>("Sfx/strike_warn");
        hitSound = Resources.Load<AudioClip>("Sfx/strike_hit");
        art = sealFrames != null && sealFrames.Length > 0 && fillFrames != null && fillFrames.Length > 0;
        // without the art, plain circles drawn here
        if (!art)
        {
            if (disc == null) disc = MakeCircle(true);
            if (ring == null) ring = MakeCircle(false);
        }

        shells = new Shell[PoolSize];
        for (int i = 0; i < PoolSize; i++) shells[i] = MakeShell(i);
    }

    private void Start()
    {
        director = FindFirstObjectByType<SpawnDirector>();

        var p = GameObject.FindGameObjectWithTag("Player");
        if (p == null) return;

        player = p.transform;
        playerHealth = p.GetComponent<PlayerHealth>();
        playerBody = p.GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        for (int i = 0; i < shells.Length; i++)
            if (shells[i].Busy) Tick(shells[i], dt);

        // also covers one placed in the scene by hand: it idles outside endless
        if (!GameMode.IsEndless) return;
        if (director == null || player == null || playerHealth == null) return;

        float t = director.GetRunTime();
        if (t < startAfter) return;

        if (nextSalvo < 0f) nextSalvo = t;
        if (t < nextSalvo) return;

        Fire(t);
        nextSalvo = t + Interval(t);
    }

    private float Interval(float t)
    {
        float k = Mathf.Exp(-(t - startAfter) / Mathf.Max(1f, intervalRamp));
        return minInterval + (startInterval - minInterval) * k;
    }

    private void Fire(float t)
    {
        int count = Mathf.Clamp(1 + Mathf.FloorToInt((t - startAfter) / Mathf.Max(1f, extraShellEvery)), 1, maxShells);

        Vector2 at = player.position;
        Vector2 vel = playerBody != null ? playerBody.linearVelocity : Vector2.zero;

        Strike(at);

        // the second shell leads the target, so holding one direction is no longer a free dodge
        // and the player has to actually change course. the rest just deny space
        for (int i = 1; i < count; i++)
        {
            bool lead = i == 1 && vel.sqrMagnitude > 1f;
            Strike(lead ? at + vel * telegraph : at + Random.insideUnitCircle * scatter);
        }
    }

    private void Strike(Vector2 position)
    {
        Shell s = null;
        for (int i = 0; i < shells.Length; i++)
        {
            if (shells[i].Busy) continue;
            s = shells[i];
            break;
        }

        // pool exhausted: skip the shell rather than steal a telegraph the player is reading
        if (s == null) return;

        s.root.transform.position = position;
        s.root.transform.localScale = Vector3.one * (art ? radius / SealArtRadius : radius * 2f);
        s.age = 0f;
        s.detonated = false;
        s.fill.transform.localScale = Vector3.zero;
        s.fill.color = art ? Color.white : WithAlpha(color, 0.35f);
        s.ring.color = art ? Color.white : WithAlpha(color, 0.8f);
        s.root.SetActive(true);
        if (warnSound != null) SfxPlayer.PlayAt(warnSound, position, 0.8f, Random.Range(0.97f, 1.03f));
    }

    private void Tick(Shell s, float dt)
    {
        s.age += dt;

        if (!s.detonated)
        {
            float k = Mathf.Clamp01(s.age / telegraph);

            // the inner disc filling the ring is the countdown; the ring pulses faster as it closes
            s.fill.transform.localScale = Vector3.one * k;
            float pulse = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(s.age * Mathf.Lerp(6f, 22f, k)));
            if (art)
            {
                // the seal turns over its eight frames, and blinks in the last moment before it falls
                s.ring.sprite = sealFrames[(int)(s.age * 10f) % sealFrames.Length];
                bool blink = k > 0.8f && (int)(s.age * 30f) % 2 == 0;
                s.ring.color = new Color(1f, 1f, 1f, blink ? 0.45f : Mathf.Lerp(0.85f, 1f, pulse));
                s.fill.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.55f, 0.9f, pulse * k));
            }
            else s.ring.color = WithAlpha(color, pulse);

            if (s.age >= telegraph) Detonate(s);
            return;
        }

        if (s.age >= telegraph + flashTime) s.root.SetActive(false);
    }

    private void Detonate(Shell s)
    {
        s.detonated = true;
        s.fill.transform.localScale = Vector3.one;
        s.fill.color = new Color(1f, 1f, 1f, 0.75f);
        s.ring.color = Color.white;
        Vector2 at = s.root.transform.position;
        if (art)
        {
            // the seal is spent as the bolt lands: the bolt comes down on it and bursts
            s.root.SetActive(false);
            if (boltFrames != null && boltFrames.Length > 0)
                FxBatch.Play(boltFrames, 28f, at + Vector2.up * (BoltCell * 0.5f - BoltFoot), 1f, sortingLayer, 520);
            if (burstFrames != null && burstFrames.Length > 0)
                FxBatch.Play(burstFrames, 22f, at, radius / BurstArtRadius, sortingLayer, 521);
        }
        if (hitSound != null) SfxPlayer.PlayAt(hitSound, at, 0.9f, Random.Range(0.94f, 1.04f));

        if (player == null || playerHealth == null) return;

        float dist = Vector2.Distance(player.position, s.root.transform.position);

        // near misses still rattle the screen; distant ones barely register
        Juice.Shake(impactShake * Mathf.Clamp01(1f - dist / (radius * 5f)));

        if (dist <= radius && playerHealth.Current > 0f)
            playerHealth.TakeDamage(playerHealth.Max * maxHealthFraction);
    }

    private Shell MakeShell(int i)
    {
        var root = new GameObject($"Shell {i}");
        root.transform.SetParent(transform, false);

        var ringRenderer = root.AddComponent<SpriteRenderer>();
        ringRenderer.sprite = art ? sealFrames[0] : ring;
        ringRenderer.sortingLayerName = sortingLayer;
        ringRenderer.sortingOrder = 500;

        var fillObject = new GameObject("Fill");
        fillObject.transform.SetParent(root.transform, false);

        var fillRenderer = fillObject.AddComponent<SpriteRenderer>();
        fillRenderer.sprite = art ? fillFrames[0] : disc;
        fillRenderer.sortingLayerName = sortingLayer;
        fillRenderer.sortingOrder = 499;

        root.SetActive(false);
        return new Shell { root = root, ring = ringRenderer, fill = fillRenderer };
    }

    // generated at runtime so the mechanic needs no art to exist yet
    private static Sprite MakeCircle(bool filled)
    {
        const int size = 128;
        const float edge = 1.5f;
        const float thickness = 7f;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        var px = new Color32[size * size];
        float c = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float outer = Mathf.Clamp01((c - d) / edge);
                float a = filled ? outer : outer * Mathf.Clamp01((d - (c - thickness)) / edge);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }

        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private static Color WithAlpha(Color c, float a)
    {
        c.a = a;
        return c;
    }
}
