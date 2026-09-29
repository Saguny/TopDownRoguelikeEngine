using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Meng Po, the Lady of Forgetting, keeper of Naihe Bridge: Huangquan Road's final boss, fought
// Touhou's way as Yama is (Danmaku, YamaScreen, BossGrade), in her own world.
//   her entrance: the horde thins to mist and is gone; a bell tolls over water; the Wangchuan,
//   the river of forgetting, floods in across the road; Naihe Bridge rises out of the fog over it;
//   and she's there on its crest, an old woman stirring a cauldron of violet soup, who ladles out
//   a bowl and offers it: drink, and forget
//   her fight: eight phases, five of them spell cards. her soup of forgetting fades the bullets
//   to ghosts of themselves for a moment (Danmaku.Veil): they're still there, and the player has
//   to remember where; the river's current carries the player downstream while walls of bone ride
//   it; the bridge's crossing is a corridor of talismans winding down at the player. before her last
//   two cards her old form cracks off her: she's young and terrible, white hair streaming as if
//   under water, enthroned on a lotus, the six paths of reincarnation wheeling out of her
//   her end: the bowl breaks in her hands, the forgetting sweeps out one last time and a lotus of
//   light opens where she was; the bridge and the river go back into the mist
// fought alone, no clock running, bullets a fixed share of the player's health, each phase's
// damage its own (as Yama). art in Resources/MengPo (Tools/VFX/huangquan), sounds Resources/Sfx
// mp_* (Tools/SFX/mengpo.py)
[RequireComponent(typeof(EnemyHealth))]
public class MengPoBoss : MonoBehaviour, IDamageGate, IFightBoss
{
    [Header("Music")]
    public AudioClip bossMusic;
    [Range(0f, 1f)] public float musicVolume = 1f;
    [Min(0f)] public float musicFadeOut = 2f;
    [Min(0.1f)] public float musicFadeIn = 3f;

    [Header("Health")]
    [Tooltip("her whole health, for a fight of about 3 minutes against six evolved weapons")]
    [Min(1f)] public float health = 250000f;
    [Tooltip("each phase's share of her health, in order: Ladle, Soup, Lanterns, Current, Crossing, Steam, Six Paths, Last Bowl")]
    public float[] phaseShares = { 0.09f, 0.14f, 0.09f, 0.14f, 0.14f, 0.1f, 0.14f, 0.16f };

    [Header("The fight")]
    [Range(0f, 1f)] public float crowdDuringFight = 0f;
    [Min(2f)] public float hoverDistance = 5f;
    [Tooltip("coins for clearing each spell card without being hit (before Greed)")]
    public int[] spellBonus = { 9000, 12000, 15000, 19000, 25000 };
    [Tooltip("how hard the river's current carries the player, units a second")]
    public float currentStrength = 2.4f;

    private struct Phase
    {
        public string card;
        public Func<IEnumerator> pattern;
        public bool trueForm;
    }

    private Phase[] phases;
    private float[] floors;
    private int phase = -1;
    private float floor, top;
    private bool broke, invulnerable = true, dying, allowKill, trueForm;

    private EnemyHealth hp;
    private SpawnDirector director;
    private SpriteRenderer body, halo, river, bridge, mandala;
    private Collider2D hurtbox;
    private YamaScreen screen;
    private BossGrade grade;
    private Sprite[] frames, steamHalo, lotusHalo, riverFrames, petals;
    private readonly List<SpriteRenderer> ghosts = new List<SpriteRenderer>();
    private readonly List<float> ghostAge = new List<float>();

    private Vector2 offset = new Vector2(0f, 4.6f);
    private Vector2 basePos, bridgeAt;
    private float follow = 1.5f, glideUntil, castUntil, nextGhost, nextHitSound, age, sceneAlpha;
    private int castPose, cardsLeft;
    private bool following;
    private Coroutine forgetting;

    // ---- setting up

    public void Begin(SpawnDirector spawner, EnemyArchetype arch)
    {
        director = spawner;
        hp = GetComponent<EnemyHealth>();
        hp.SetScaled(health);
        if (arch != null)
        {
            hp.armour = arch.armour;
            hp.physicalTaken = arch.physicalTaken;
            hp.magicalTaken = arch.magicalTaken;
        }

        phases = new[]
        {
            new Phase { pattern = Ladle },
            new Phase { card = "Oblivion Sign \"Soup of Forgetting\"", pattern = Soup },
            new Phase { pattern = Lanterns },
            new Phase { card = "River Sign \"Current of the Wangchuan\"", pattern = Current },
            new Phase { card = "Bridge Sign \"Crossing at Naihe\"", pattern = Crossing },
            new Phase { pattern = Steam },
            new Phase { card = "Rebirth Sign \"Wheel of the Six Paths\"", pattern = SixPaths, trueForm = true },
            new Phase { card = "Last Bowl \"Drink, and Forget Everything\"", pattern = LastBowl, trueForm = true },
        };
        floors = new float[phases.Length];
        float sum = 0f, left = health;
        for (int i = 0; i < phases.Length; i++) sum += Share(i);
        for (int i = 0; i < phases.Length; i++)
        {
            left -= health * Share(i) / sum;
            floors[i] = i == phases.Length - 1 ? 1f : Mathf.Max(1f, left);
        }
        foreach (var p in phases) if (p.card != null) cardsLeft++;

        basePos = transform.position;
        bridgeAt = basePos + BridgeBelow;
        StartCoroutine(Fight());
    }

    private float Share(int i) => phaseShares != null && i < phaseShares.Length ? Mathf.Max(0.01f, phaseShares[i]) : 1f / 8f;

    // where the bridge's centre sits under her (her cauldron on its crest), and the river under it
    private static readonly Vector2 BridgeBelow = new Vector2(0f, -2.8f);
    private static readonly Vector2 RiverBelow = new Vector2(0f, -1.7f);

    private static Sprite[] Art(string name) => YamaArt.Strip("MengPo/" + name);
    private static void Sound(string name, Vector2 at, float volume = 1f, float pitch = 1f) => YamaArt.Play(name, at, volume, pitch);

    private void Awake()
    {
        frames = Art("mengpo");
        steamHalo = Art("steam_halo");
        lotusHalo = Art("lotus_halo");
        riverFrames = Art("river");
        petals = Art("petal");
        screen = YamaScreen.Get();
        grade = BossGrade.Get();
        Danmaku.PlayerHit += OnPlayerHit;

        body = GetComponent<SpriteRenderer>();
        body.sortingLayerName = "Aura";
        body.sortingOrder = 40;
        if (frames != null) body.sprite = frames[0];
        body.enabled = false;
        halo = Child("Aureole", "Aura", 39);
        halo.enabled = false;

        // the river and the bridge: the world she's fought in, under everything that moves
        river = Loose("The Wangchuan", "Background", 998);
        river.drawMode = SpriteDrawMode.Tiled;
        if (riverFrames != null && riverFrames.Length > 0) river.sprite = riverFrames[0];
        river.size = new Vector2(60f, riverFrames != null && riverFrames.Length > 0 ? riverFrames[0].bounds.size.y : 2.25f);
        river.transform.localScale = new Vector3(1f, 1.8f, 1f);
        river.color = new Color(1f, 1f, 1f, 0f);
        bridge = Loose("Naihe Bridge", "Background", 999);
        var b = Art("bridge");
        if (b != null) bridge.sprite = b[0];
        bridge.color = new Color(1f, 1f, 1f, 0f);
        mandala = Loose("Lotus of Forgetting", "Background", 1000);
        var m = Art("mandala");
        if (m != null) mandala.sprite = m[0];
        mandala.transform.localScale = Vector3.one * 2.2f;
        mandala.color = new Color(1f, 1f, 1f, 0f);

        if (TryGetComponent(out Rigidbody2D rb)) { rb.bodyType = RigidbodyType2D.Kinematic; rb.linearVelocity = Vector2.zero; }
        if (TryGetComponent(out CircleCollider2D circle)) { circle.radius = 1.3f; circle.offset = new Vector2(0f, 0.2f); }
        hurtbox = GetComponent<Collider2D>();
        if (hurtbox != null) hurtbox.enabled = false;

        for (int i = 0; i < 8; i++)
        {
            var g = Child("Afterimage", "Aura", 38);
            g.transform.SetParent(null, true);
            g.enabled = false;
            ghosts.Add(g);
            ghostAge.Add(99f);
        }
    }

    private SpriteRenderer Child(string name, string layer, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = layer;
        sr.sortingOrder = order;
        return sr;
    }

    private static SpriteRenderer Loose(string name, string layer, int order)
    {
        var sr = new GameObject(name).AddComponent<SpriteRenderer>();
        sr.sortingLayerName = layer;
        sr.sortingOrder = order;
        return sr;
    }

    private void OnDestroy()
    {
        foreach (var g in ghosts) if (g != null) Destroy(g.gameObject);
        foreach (var sr in new[] { river, bridge, mandala }) if (sr != null) Destroy(sr.gameObject);
        Danmaku.ShowHitbox = false;
        Danmaku.Veil = 0f;
        PlayerMovement.Drift = Vector2.zero;
        Danmaku.PlayerHit -= OnPlayerHit;
        if (director != null) director.BossCrowd = 1f;
        if (grade != null) grade.SetTheme(BossGrade.Theme.Fire);
    }

    private void OnPlayerHit(Vector2 at)
    {
        if (grade != null) grade.Hurt();
        screen.Flash(new Color(0.75f, 0.55f, 1f), 0.18f, 0.3f);
    }

    private BossGrade.Look Mood => trueForm ? BossGrade.Look.Rage : BossGrade.Look.Fight;

    // ---- the damage gate: each phase's bar its own, none between them

    public float Admit(float damage, DamageKind kind, float current)
    {
        if (allowKill) return damage;
        if (kind == DamageKind.Silent || invulnerable || dying) return 0f;
        float room = current - floor;
        if (room <= 0f) { broke = true; return 0f; }
        if (damage >= room) { broke = true; damage = room; }
        if (Time.time >= nextHitSound)
        {
            nextHitSound = Time.time + 0.075f;
            Sound("yama_hit", transform.position, 0.28f, UnityEngine.Random.Range(1.05f, 1.15f));
        }
        return damage;
    }

    // ---- every frame: where she is, how she looks, the world round her

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        age += dt;

        if (following)
        {
            Vector2 player = Danmaku.PlayerPosition;
            float rate = Time.time < glideUntil ? 5f : follow;
            basePos = Vector2.Lerp(basePos, player + offset, 1f - Mathf.Exp(-rate * dt));
            if (Time.time < glideUntil && Time.time >= nextGhost) { nextGhost = Time.time + 0.05f; Ghost(); }
        }
        // she drifts on the air, slowly
        transform.position = basePos + new Vector2(0f, Mathf.Round(Mathf.Sin(age * 1.7f) * 3f) / YamaArt.WorldPpu);

        if (frames != null && frames.Length >= 14 && body.enabled)
        {
            int form = trueForm ? 7 : 0;
            int f = Time.time < castUntil ? 3 + castPose : (int)(age * 5f) % 4;
            body.sprite = frames[form + f];
        }
        var haloArt = trueForm ? lotusHalo : steamHalo;
        if (halo.enabled && haloArt != null)
        {
            halo.sprite = YamaArt.Frame(haloArt, age, trueForm ? 10f : 6f);
            halo.color = new Color(1f, 1f, 1f, trueForm ? 0.95f : 0.8f);
        }

        // the bridge floats under her, a beat behind; the river runs across the whole view
        bridgeAt = Vector2.Lerp(bridgeAt, basePos + BridgeBelow, 1f - Mathf.Exp(-2.2f * dt));
        bridge.transform.position = bridgeAt;
        var cam = Camera.main;
        float camX = cam != null ? cam.transform.position.x : bridgeAt.x;
        // the tile moves a whole tile at a time as the camera does, so its flow never jumps
        float tile = riverFrames != null && riverFrames.Length > 0 ? riverFrames[0].bounds.size.x : 2.25f;
        river.transform.position = new Vector2(Mathf.Round(camX / tile) * tile, bridgeAt.y + RiverBelow.y);
        // it flows the way its current carries the player (the art flows right; left, played backwards)
        if (riverFrames != null && riverFrames.Length > 0)
        {
            int n = riverFrames.Length, k = (int)(age * 10f) % n;
            river.sprite = riverFrames[PlayerMovement.Drift.x < -0.1f ? n - 1 - k : k];
        }
        var rc = river.color; rc.a = sceneAlpha; river.color = rc;
        var bc = bridge.color; bc.a = sceneAlpha; bridge.color = bc;
        if (mandala.color.a > 0f)
        {
            mandala.transform.position = transform.position;
            mandala.transform.rotation = Quaternion.Euler(0f, 0f, age * (trueForm ? -18f : 9f));
        }

        for (int i = 0; i < ghosts.Count; i++)
        {
            if (!ghosts[i].enabled) continue;
            ghostAge[i] += dt;
            float k = ghostAge[i] / 0.4f;
            if (k >= 1f) { ghosts[i].enabled = false; continue; }
            ghosts[i].color = trueForm ? new Color(0.55f, 0.85f, 1f, 0.45f * (1f - k)) : new Color(0.75f, 0.65f, 1f, 0.45f * (1f - k));
        }

        if (phase >= 0 && !dying)
        {
            float span = Mathf.Max(1f, top - floor);
            screen.SetBar((hp.Current - floor) / span, cardsLeft, invulnerable);
        }
    }

    private void Ghost()
    {
        for (int i = 0; i < ghosts.Count; i++)
        {
            if (ghosts[i].enabled) continue;
            ghosts[i].enabled = true;
            ghosts[i].sprite = body.sprite;
            ghosts[i].transform.position = transform.position;
            ghostAge[i] = 0f;
            return;
        }
    }

    // 1 the ladle dipped, 2 raised and pouring, 3 the bowl flung
    private void Cast(int pose, float seconds = 0.45f)
    {
        castPose = pose;
        castUntil = Time.time + seconds;
    }

    // where her bullets come from: the cauldron's soup, or (in her true form) the bowl at her heart
    private Vector2 E => (Vector2)transform.position + (trueForm ? new Vector2(0f, 0.5f) : new Vector2(0f, -0.3f));

    private static WaitForSeconds Wait(float s) => new WaitForSeconds(s);

    private void Relocate(float seconds = 0.8f)
    {
        var cam = Camera.main;
        float h = cam != null && cam.orthographic ? cam.orthographicSize : 6f;
        float w = cam != null && cam.orthographic ? h * cam.aspect : 10f;
        // she keeps above the player, over her river, drifting side to side
        float x = UnityEngine.Random.Range(-w * 0.55f, w * 0.55f);
        if (Mathf.Abs(x - offset.x) < 1.5f) x = offset.x + (x >= offset.x ? 2.5f : -2.5f);
        offset = new Vector2(Mathf.Clamp(x, -(w - 2f), w - 2f), Mathf.Clamp(hoverDistance * UnityEngine.Random.Range(0.85f, 1.05f), 3.2f, h - 1.6f));
        glideUntil = Time.time + seconds;
        var mist = Art("mist_puff");
        if (mist != null) FxBatch.Play(mist, 14f, transform.position, 1.4f, "Aura", 37);
        Sound("mp_glide", transform.position, 0.4f, UnityEngine.Random.Range(0.95f, 1.05f));
    }

    // the forgetting: the bullets fade to ghosts of themselves for `hold` seconds, then come back
    private IEnumerator Forget(float hold)
    {
        var wave = Art("forget_wave");
        if (wave != null) FxBatch.Play(wave, 16f, E, 2.2f, "Aura", 36);
        Sound("mp_forget", E, 0.9f);
        screen.Flash(new Color(0.85f, 0.8f, 1f), 0.22f, 0.5f);
        grade.Punch(0.35f);
        for (float t = 0f; t < 0.3f; t += Time.deltaTime) { Danmaku.Veil = t / 0.3f; yield return null; }
        Danmaku.Veil = 1f;
        yield return Wait(hold);
        for (float t = 0f; t < 0.45f; t += Time.deltaTime) { Danmaku.Veil = 1f - t / 0.45f; yield return null; }
        Danmaku.Veil = 0f;
    }

    // ================================================================ the fight

    private IEnumerator Fight()
    {
        yield return Entrance();
        for (int i = 0; i < phases.Length; i++) yield return RunPhase(i);
        yield return Death();
    }

    private IEnumerator Entrance()
    {
        if (director != null)
        {
            if (crowdDuringFight <= 0f) director.StopSpawning();
            else director.PauseSpawning(16f);
        }
        grade.SetTheme(BossGrade.Theme.Mist);
        screen.Letterbox(true);
        screen.Dim(0.3f);
        screen.Vignette(new Color(0.75f, 0.7f, 0.95f), 0.4f);
        grade.Set(BossGrade.Look.Fight, 4f);
        if (bossMusic != null && BGMManager.Instance != null)
            BGMManager.Instance.PlayBossTheme(bossMusic, musicVolume, musicFadeOut, musicFadeIn);

        // the horde thins to mist and is gone
        StartCoroutine(Fade());
        Sound("mp_bell", basePos, 1f);
        Juice.Shake(0.08f);
        yield return Wait(1.4f);

        // the river of forgetting floods in across the road
        Sound("mp_river", basePos, 0.9f);
        Sound("mp_bell", basePos, 0.9f, 0.9f);
        for (float t = 0f; t < 1.4f; t += Time.deltaTime) { sceneAlpha = Mathf.Clamp01(t / 1.4f) * 0.55f; yield return null; }
        // Naihe Bridge rising out of the fog over it
        Sound("mp_bridge", bridgeAt, 1f);
        Juice.Shake(0.25f);
        grade.Punch(0.4f);
        var mist = Art("mist_puff");
        for (int k = 0; k < 9; k++)
            if (mist != null) FxBatch.Play(mist, 12f, bridgeAt + new Vector2(-4f + k, UnityEngine.Random.Range(-0.6f, 0.6f)), 2f, "Aura", 35);
        for (float t = 0f; t < 1.2f; t += Time.deltaTime) { sceneAlpha = Mathf.Lerp(0.55f, 1f, t / 1.2f); yield return null; }
        sceneAlpha = 1f;
        yield return Wait(0.5f);

        // and she's on its crest, stirring her soup
        body.enabled = true;
        halo.enabled = true;
        for (float t = 0f; t < 1.2f; t += Time.deltaTime)
        {
            float k = t / 1.2f;
            body.color = new Color(1f, 1f, 1f, k);
            halo.transform.localScale = Vector3.one * Mathf.Lerp(0.3f, 1f, k);
            yield return null;
        }
        body.color = Color.white;
        Cast(1, 0.8f);
        Sound("mp_ladle", basePos, 0.9f);
        yield return Wait(0.8f);
        Cast(2, 1.2f);
        Sound("mp_pour", basePos, 0.9f);
        screen.Bonus("\"Drink, child. Drink, and forget.\"", new Color(0.85f, 0.8f, 1f));
        yield return Wait(1.4f);

        // her name
        Cast(3, 0.8f);
        Sound("mp_title", basePos, 1f);
        Juice.Freeze(0.08f);
        Juice.Shake(0.5f);
        screen.Flash(new Color(0.9f, 0.85f, 1f), 0.6f, 0.7f);
        grade.Punch(1f);
        screen.Title("MENG PO", "Lady of Forgetting  -  Keeper of Naihe Bridge", 3f);
        var wave = Art("forget_wave");
        if (wave != null) FxBatch.Play(wave, 14f, basePos, 2.6f, "Aura", 36);
        yield return Wait(3f);

        screen.Letterbox(false);
        screen.Dim(0.06f);
        screen.Vignette(new Color(0.5f, 0.45f, 0.85f), 0.15f);
        screen.ShowBar("Meng Po  -  Lady of Forgetting");
        Sound("yama_bar", basePos, 0.7f);
        if (director != null && crowdDuringFight > 0f) director.BossCrowd = crowdDuringFight;
        offset = basePos - Danmaku.PlayerPosition;
        following = true;
        if (hurtbox != null) hurtbox.enabled = true;
        Danmaku.ShowHitbox = true;
        yield return Wait(0.8f);
    }

    // the horde left on the road thins to mist, a few at a time
    private IEnumerator Fade()
    {
        var all = new List<GameObject>(EnemyRegistry.All);
        all.Remove(gameObject);
        var mist = Art("mist_puff");
        float per = all.Count > 0 ? 1.2f / all.Count : 0f, owed = 0f;
        int shown = 0;
        foreach (var e in all)
        {
            if (e != null && e.activeInHierarchy && e.TryGetComponent(out EnemyHealth h))
            {
                if (mist != null && shown++ % 4 == 0) FxBatch.Play(mist, 16f, e.transform.position, 0.8f, "Aura", 30);
                h.TakeDamage(h.Current + h.Max + 1f, DamageKind.Silent);
            }
            owed += per;
            if (owed >= Time.deltaTime) { owed = 0f; yield return null; }
        }
    }

    private IEnumerator RunPhase(int i)
    {
        var p = phases[i];
        phase = i;
        top = i == 0 ? hp.Max : floors[i - 1];
        floor = floors[i];
        broke = false;
        invulnerable = true;
        screen.RefillBar();

        if (p.trueForm && !trueForm) yield return Transform();

        if (p.card != null) yield return Declare(p.card);
        else yield return Wait(0.9f);

        invulnerable = false;
        int hitsBefore = Danmaku.Hits;
        var run = StartCoroutine(p.pattern());
        while (!broke) yield return null;
        StopCoroutine(run);
        // a forgetting under way ends with the phase, or it'd veil the next one
        if (forgetting != null) { StopCoroutine(forgetting); forgetting = null; }
        Danmaku.Veil = 0f;
        PlayerMovement.Drift = Vector2.zero;
        invulnerable = true;
        yield return Break(p, Danmaku.Hits == hitsBefore);
    }

    // her old form cracks off her
    private IEnumerator Transform()
    {
        following = false;
        Cast(2, 2f);
        Sound("mp_transform_gather", transform.position, 1f);
        screen.Letterbox(true);
        screen.Dim(0.35f);
        var charge = Art("mp_charge");
        for (int k = 0; k < 3; k++)
        {
            if (charge != null) FxBatch.Play(charge, 10f, E, 2f + k * 0.6f, "Aura", 42);
            Juice.Shake(0.1f + k * 0.08f);
            body.color = k % 2 == 0 ? new Color(0.8f, 0.9f, 1f) : Color.white;
            yield return Wait(0.45f);
        }
        var burst = Art("transform");
        if (burst != null) FxBatch.Play(burst, 16f, transform.position, 1.6f, "Aura", 43);
        Sound("mp_transform", transform.position, 1f);
        screen.Flash(Color.white, 1f, 1f);
        Juice.Freeze(0.15f);
        Juice.Shake(0.9f);
        grade.Punch(1f);
        trueForm = true;
        body.color = Color.white;
        grade.Set(BossGrade.Look.Rage, 1.2f);
        screen.Vignette(new Color(0.35f, 0.75f, 0.95f), 0.35f, true);
        yield return Wait(0.6f);
        screen.Title("MENG PO", "The Lady Unveiled  -  She Who Remembers For All", 2.6f);
        StartCoroutine(PetalRain(6f));
        yield return Wait(2.6f);
        screen.Letterbox(false);
        screen.Dim(0.06f);
        following = true;
    }

    // lotus petals drifting down over the whole view for a while
    private IEnumerator PetalRain(float seconds)
    {
        if (petals == null) yield break;
        var cam = Camera.main;
        for (float t = 0f; t < seconds; t += 0.05f)
        {
            if (cam != null)
            {
                float h = cam.orthographicSize, w = h * cam.aspect;
                Vector2 c = cam.transform.position;
                FxBatch.Play(petals, 5f, c + new Vector2(UnityEngine.Random.Range(-w, w), UnityEngine.Random.Range(-h, h)), 1f, "Aura", 34);
            }
            yield return Wait(0.05f);
        }
    }

    private IEnumerator Declare(string card)
    {
        Cast(2, 1.3f);
        Sound("mp_declare", transform.position, 1f);
        var charge = Art("mp_charge");
        if (charge != null) FxBatch.Play(charge, 8f, E, 2.4f, "Aura", 42);
        // the cut-in's face at the UI's pixel size, as Yama's is
        var face = YamaArt.Strip(trueForm ? "MengPo/portrait_true" : "MengPo/portrait", 100f);
        screen.Declare(card, face != null && face.Length > 0 ? face[0] : null);
        screen.Dim(0.2f);
        grade.Set(trueForm ? BossGrade.Look.Rage : BossGrade.Look.Spell, 1.2f);
        grade.Punch(0.5f);
        Juice.Shake(0.18f);
        StartCoroutine(FadeMandala(0.45f, 0.6f));
        yield return Wait(1.5f);
    }

    private IEnumerator FadeMandala(float to, float seconds)
    {
        float from = mandala.color.a;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            var c = mandala.color;
            c.a = Mathf.Lerp(from, to, t / seconds);
            mandala.color = c;
            yield return null;
        }
        var e = mandala.color;
        e.a = to;
        mandala.color = e;
    }

    private IEnumerator Break(Phase p, bool clean)
    {
        Danmaku.Cancel(true);
        Sound("yama_break", transform.position, 1f, 1.1f);
        Sound("mp_bowl", transform.position, 0.8f);
        var blast = Art("soup_blast");
        if (blast != null) FxBatch.Play(blast, 18f, transform.position, 1.6f, "Aura", 42);
        screen.Flash(new Color(0.9f, 0.85f, 1f), 0.4f, 0.35f);
        grade.Punch(0.8f);
        Juice.Freeze(0.08f);
        Juice.Shake(0.45f);

        if (p.card != null)
        {
            int card = Array.FindAll(phases, x => x.card != null).Length - cardsLeft;
            cardsLeft--;
            StartCoroutine(FadeMandala(0f, 0.8f));
            screen.ClearCard();
            screen.Dim(0.06f);
            grade.Set(Mood, 1.5f);
            if (clean)
            {
                // a gift, so it counts in the run's coins (the HUD's counter, the end screen) as well as the wallet
                int coins = Coins.Gift(spellBonus != null && card < spellBonus.Length ? spellBonus[card] : 500);
                screen.Bonus($"Spell Card Bonus!  +{coins} coins", new Color(0.8f, 0.9f, 1f));
                Sound("yama_bonus", transform.position, 0.9f);
            }
            else screen.Bonus("Bonus Failed", new Color(0.7f, 0.65f, 0.75f));
        }
        yield return Wait(0.5f);
        PickupSystem.Sweep(1f);
        Relocate(0.9f);
        yield return Wait(1.3f);
    }

    private IEnumerator Death()
    {
        dying = true;
        invulnerable = true;
        Danmaku.Cancel(true);
        Danmaku.ShowHitbox = false;
        Danmaku.Veil = 0f;
        PlayerMovement.Drift = Vector2.zero;
        if (director != null) { director.StopSpawning(); director.BossCrowd = 1f; }
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && player.TryGetComponent(out PlayerInventory inv)) inv.LockLeveling();
        if (BGMManager.Instance != null) BGMManager.Instance.EndBossTheme(4f);
        screen.ClearCard();
        screen.HideBar();
        screen.Letterbox(true);
        screen.Dim(0.3f);
        grade.Set(BossGrade.Look.Dying, 0.8f);
        following = false;
        StartCoroutine(FadeMandala(0f, 0.5f));

        bool slowed = Mathf.Approximately(Time.timeScale, 1f);
        if (slowed) Time.timeScale = 0.4f;

        // the bowl breaks in her hands
        Cast(2, 3f);
        Sound("mp_death_bowl", transform.position, 1f);
        var shatter = Art("bowl_shatter");
        if (shatter != null) FxBatch.Play(shatter, 14f, E, 2f, "Aura", 43);
        Juice.Shake(0.3f);
        yield return new WaitForSecondsRealtime(0.8f);

        // the forgetting sweeps out one last time, again and again
        var wave = Art("forget_wave");
        var mist = Art("mist_puff");
        Vector2 at = basePos;
        for (int k = 0; k < 10; k++)
        {
            if (wave != null && k % 3 == 0) FxBatch.Play(wave, 16f, at, 1.2f + k * 0.25f, "Aura", 36);
            if (mist != null) FxBatch.Play(mist, 14f, at + UnityEngine.Random.insideUnitCircle * 1.6f, 1.6f, "Aura", 41);
            Sound("mp_glide", at, 0.5f, 0.7f + k * 0.05f);
            Juice.Shake(Mathf.Lerp(0.1f, 0.35f, k / 9f));
            grade.Punch(Mathf.Lerp(0.2f, 0.5f, k / 9f));
            basePos = at + UnityEngine.Random.insideUnitCircle * 0.08f;
            body.color = new Color(1f, 1f, 1f, 1f - k / 12f);
            yield return new WaitForSecondsRealtime(Mathf.Lerp(0.28f, 0.1f, k / 9f));
        }
        if (slowed && Time.timeScale > 0f) Time.timeScale = 1f;
        basePos = at;

        // a lotus of light opens where she was
        var bloom = Art("death_bloom");
        if (bloom != null) FxBatch.Play(bloom, 12f, at, 2.2f, "Aura", 44);
        Sound("mp_death", at, 1f);
        screen.Flash(Color.white, 1f, 1.6f);
        grade.Punch(1f);
        grade.Set(BossGrade.Look.Triumph, 2.5f);
        Juice.Freeze(0.15f);
        Juice.Shake(1f);
        body.enabled = false;
        halo.enabled = false;
        StartCoroutine(PetalRain(5f));
        for (int i = 0; i < 40; i++)
        {
            float a = i / 40f * Mathf.PI * 2f;
            PickupSystem.DropWen(at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * UnityEngine.Random.Range(0.6f, 2.6f), 6);
        }
        // the bridge and the river go back into the mist
        for (float t = 0f; t < 1.4f; t += Time.deltaTime) { sceneAlpha = 1f - t / 1.4f; yield return null; }
        sceneAlpha = 0f;
        screen.Title("MENG PO RESTS", "You crossed Naihe Bridge, and remembered everything", 3.6f);
        screen.Letterbox(false);
        screen.Dim(0f);
        screen.Vignette(new Color(0.85f, 0.9f, 1f), 0.15f);
        Sound("yama_victory", at, 0.9f);

        if (director != null) director.FinalBossDown(at);
        else RunVictory.Begin();
        allowKill = true;
        if (hurtbox != null) hurtbox.enabled = false;
        hp.TakeDamage(hp.Current + 1f, DamageKind.Normal);
    }

    // ================================================================ her danmaku

    private void Splash(Vector2 at, float scale = 1f)
    {
        var s = Art("ladle_splash");
        if (s != null) FxBatch.Play(s, 18f, at, scale, "Aura", 41);
    }

    // plain: ladlefuls flung at the player in arcs, and slow bubbles of soup that burst into rings
    private IEnumerator Ladle()
    {
        int k = 0;
        while (true)
        {
            Cast(1, 0.4f);
            Sound("mp_ladle", transform.position, 0.6f, UnityEngine.Random.Range(0.95f, 1.05f));
            yield return Wait(0.4f);
            Cast(3);
            Splash(E);
            Sound("mp_fling", transform.position, 0.7f);
            float aim = Danmaku.AimAt(E);
            int side = k % 2 == 0 ? 1 : -1;
            for (int i = 0; i < 9; i++)
                Danmaku.Fire(E, aim - 40f * side + i * 10f * side, Shot.Of(BulletType.Rice, BulletColor.Azure, 3.8f + i * 0.12f).Turn(18f * side).Life(6f));
            yield return Wait(0.5f);
            for (int b = 0; b < 3; b++)
                Danmaku.Fire(E, aim + (b - 1) * 38f, Shot.Of(BulletType.BigOrb, BulletColor.Violet, 1.6f).Accel(-0.6f, 0.4f).Burst(1.7f, 10, BulletType.Orb, BulletColor.Azure, 1.9f));
            k++;
            yield return Wait(1.3f);
            if (k % 2 == 0) { Relocate(); yield return Wait(0.8f); }
        }
    }

    // bowls thrown at the player that burst into violet; two arms of soup spiralling off the
    // cauldron; and every few seconds the forgetting
    private IEnumerator Soup()
    {
        float spin = 0f, bowl = 0f, forget = 2.5f;
        while (true)
        {
            Danmaku.Fire(E, spin, Shot.Of(BulletType.Orb, BulletColor.Violet, 2.2f).Life(9f));
            Danmaku.Fire(E, spin + 180f, Shot.Of(BulletType.Orb, BulletColor.Violet, 2.2f).Life(9f));
            spin += 13f;
            bowl += 0.12f;
            forget -= 0.12f;
            if (bowl >= 1.5f)
            {
                bowl = 0f;
                Cast(3);
                Sound("mp_fling", transform.position, 0.6f);
                Danmaku.Fire(E, Danmaku.AimAt(E) + UnityEngine.Random.Range(-12f, 12f),
                    Shot.Of(BulletType.BigOrb, BulletColor.Jade, 2.6f).Accel(-1f, 0.6f).Burst(1.25f, 14, BulletType.Rice, BulletColor.Violet, 2.3f));
            }
            if (forget <= 0f) { forget = 5f; forgetting = StartCoroutine(Forget(1.3f)); }
            yield return Wait(0.12f);
        }
    }

    // plain: lotus lanterns set floating round the player, each opening into rings of gold
    private IEnumerator Lanterns()
    {
        while (true)
        {
            Cast(2, 1f);
            Sound("mp_ladle", transform.position, 0.5f, 1.2f);
            Vector2 c = Danmaku.PlayerPosition;
            float a0 = UnityEngine.Random.value * 360f;
            for (int i = 0; i < 5; i++)
            {
                float a = (a0 + i * 72f) * Mathf.Deg2Rad;
                Vector2 at = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 4.2f;
                // a lantern hangs there, flickering, then opens: two rings, the second turned
                Danmaku.Fire(at, 0f, Shot.Of(BulletType.BigOrb, BulletColor.Gold, 0f).Delay(0.9f).Life(0.8f)
                    .Burst(0.75f, 8, BulletType.Coin, BulletColor.Gold, 1.8f));
            }
            yield return Wait(1.9f);
            Cast(3);
            Danmaku.Fan(E, Danmaku.AimAt(E), 5, 44f, Shot.Of(BulletType.Rice, BulletColor.Jade, 3.4f));
            yield return Wait(1.1f);
            Relocate();
            yield return Wait(0.8f);
        }
    }

    // the river's current carries the player downstream, turning every few seconds; walls of bone
    // ride it across the screen, a gap in each; drops of soup fall at the player
    private IEnumerator Current()
    {
        var current = Art("current");
        float dir = UnityEngine.Random.value < 0.5f ? -1f : 1f, turn = 0f, wall = 0f, drop = 0f, streak = 0f;
        Sound("mp_current", Danmaku.PlayerPosition, 0.8f);
        while (true)
        {
            float dt = Time.deltaTime;
            // stronger in the duel, so the faster player still has to fight it
            PlayerMovement.Drift = new Vector2(dir * currentStrength * (BossDuel.Active ? BossDuel.CurrentScale : 1f), 0f);
            turn += dt; wall += dt; drop += dt; streak += dt;
            if (turn >= 6f)
            {
                turn = 0f;
                dir = -dir;
                Sound("mp_current", Danmaku.PlayerPosition, 0.8f, 0.9f);
                screen.Flash(new Color(0.5f, 0.6f, 1f), 0.12f, 0.3f);
            }
            if (streak >= 0.2f && current != null)
            {
                streak = 0f;
                FxBatch.Play(current, 12f, Danmaku.PlayerPosition + new Vector2(UnityEngine.Random.Range(-3f, 3f), UnityEngine.Random.Range(-2f, 2f)), 1f, "Player", -2);
            }
            if (wall >= 1.5f)
            {
                wall = 0f;
                // a wall of bone orbs from the upstream edge, flowing across, one gap in it
                var cam = Camera.main;
                float h = cam != null ? cam.orthographicSize : 6f, w = cam != null ? h * cam.aspect : 10f;
                Vector2 c = cam != null ? (Vector2)cam.transform.position : Danmaku.PlayerPosition;
                float gap = UnityEngine.Random.Range(-h + 1.5f, h - 1.5f);
                for (float y = -h; y <= h; y += 0.55f)
                {
                    if (Mathf.Abs(y - gap) < 1.1f) continue;
                    Danmaku.Fire(c + new Vector2(-dir * (w + 0.5f), y), dir > 0f ? 0f : 180f, Shot.Of(BulletType.Orb, BulletColor.Bone, 2.6f).Life(10f).Silent());
                }
                Sound("mp_river", c, 0.35f, 1.2f);
            }
            if (drop >= 0.7f)
            {
                drop = 0f;
                Cast(3, 0.3f);
                Danmaku.Fan(E, Danmaku.AimAt(E), 3, 20f, Shot.Of(BulletType.Rice, BulletColor.Violet, 3.2f));
            }
            yield return null;
        }
    }

    // the crossing: a corridor of talismans winding down at the player, its walls swaying, soup
    // poured down its middle now and then; stepping out of it is stepping off the bridge
    private IEnumerator Crossing()
    {
        float t = 0f, pour = 0f;
        while (true)
        {
            t += 0.08f;
            pour += 0.08f;
            float aim = Danmaku.AimAt(E) + Mathf.Sin(t * 0.9f) * 22f;
            Vector2 dir = new Vector2(Mathf.Cos(aim * Mathf.Deg2Rad), Mathf.Sin(aim * Mathf.Deg2Rad));
            Vector2 side = new Vector2(-dir.y, dir.x);
            float width = 1.9f + Mathf.Sin(t * 1.7f) * 0.4f;
            var wall = Shot.Of(BulletType.Talisman, BulletColor.Bone, 3.4f).Life(8f).Silent();
            Danmaku.Fire(E + side * width, aim, wall);
            Danmaku.Fire(E - side * width, aim, wall);
            if ((int)(t / 0.08f) % 4 == 0) Sound("yama_shot", transform.position, 0.2f, 1.3f);
            if (pour >= 1.6f)
            {
                pour = 0f;
                Cast(2, 0.6f);
                Sound("mp_pour", transform.position, 0.5f);
                Danmaku.Fire(E, aim, Shot.Of(BulletType.BigOrb, BulletColor.Violet, 2.2f).Burst(1.1f, 6, BulletType.Rice, BulletColor.Azure, 1.4f));
            }
            yield return Wait(0.08f);
        }
    }

    // plain: steam off the cauldron in turning arms, rings of bone between
    private IEnumerator Steam()
    {
        float spin = 0f, ring = 0f;
        int dir = 1;
        while (true)
        {
            Cast(1, 0.3f);
            for (int a = 0; a < 4; a++) Danmaku.Fire(E, spin + a * 90f, Shot.Of(BulletType.Flame, BulletColor.Azure, 3f).Life(7f));
            spin += 9f * dir;
            ring += 0.1f;
            if (ring >= 1.8f)
            {
                ring = 0f;
                dir = -dir;
                Danmaku.Ring(E, 20, UnityEngine.Random.value * 360f, Shot.Of(BulletType.Orb, BulletColor.Bone, 1.7f));
                Sound("mp_glide", transform.position, 0.3f, 1.3f);
            }
            yield return Wait(0.1f);
        }
    }

    // the six paths of rebirth wheeling out of her, each its own colour (gods gold, asuras red,
    // men azure, beasts jade, hungry ghosts violet, the hells bone), turning one way then the other;
    // and echoes: marks where the player was a moment ago, loosing rice at where they are now
    private IEnumerator SixPaths()
    {
        var colours = new[] { BulletColor.Gold, BulletColor.Red, BulletColor.Azure, BulletColor.Jade, BulletColor.Violet, BulletColor.Bone };
        var trail = new List<Vector2>(40);
        float spin = 0f, echo = 0f, flip = 0f;
        int dir = 1;
        while (true)
        {
            for (int p = 0; p < 6; p++)
                Danmaku.Fire(E, spin + p * 60f, Shot.Of(p % 2 == 0 ? BulletType.Orb : BulletType.Rice, colours[p], 2f).Accel(0.9f, 0f, 3.6f).Life(8f).Silent());
            spin += 7.5f * dir;
            trail.Add(Danmaku.PlayerPosition);
            if (trail.Count > 30) trail.RemoveAt(0);
            echo += 0.1f; flip += 0.1f;
            if (flip >= 2.2f) { flip = 0f; dir = -dir; Cast(1, 0.4f); Sound("mp_glide", transform.position, 0.35f, 1.4f); }
            if (echo >= 0.5f && trail.Count >= 16)
            {
                echo = 0f;
                Vector2 was = trail[trail.Count - 15];
                float at = Hq.Angle(Danmaku.PlayerPosition - was);
                Danmaku.Fan(was, at, 3, 18f, Shot.Of(BulletType.Rice, BulletColor.Violet, 2.8f).Delay(0.45f));
            }
            if ((int)(spin / 7.5f) % 3 == 0) Sound("yama_shot", transform.position, 0.22f, 1.2f);
            yield return Wait(0.1f);
        }
    }

    // her last card: bowls raining down all over the view, breaking into soup; lotus spirals
    // turning off her; the forgetting coming faster; harder as she weakens
    private IEnumerator LastBowl()
    {
        float bowl = 0f, forget = 1.8f, spin = 0f;
        var cam = Camera.main;
        while (true)
        {
            float span = Mathf.Max(1f, top - floor);
            bool late = (hp.Current - floor) / span < 0.4f;
            bowl += 0.1f; forget -= 0.1f;
            // lotus spirals
            for (int a = 0; a < 3; a++)
                Danmaku.Fire(E, spin + a * 120f, Shot.Of(BulletType.Orb, BulletColor.Jade, 1.9f).Turn(late ? 26f : 18f).Life(9f).Silent());
            spin += late ? 17f : 12f;
            if (bowl >= (late ? 0.45f : 0.7f) && cam != null)
            {
                bowl = 0f;
                float h = cam.orthographicSize, w = h * cam.aspect;
                Vector2 c = cam.transform.position;
                Vector2 at = c + new Vector2(UnityEngine.Random.Range(-w + 1f, w - 1f), h - 0.5f);
                // it hangs a moment, falls, and breaks where it lands
                Danmaku.Fire(at, -90f, Shot.Of(BulletType.BigOrb, BulletColor.Violet, 0.5f).Delay(0.5f).Accel(4f, 0f, 5f)
                    .Burst(UnityEngine.Random.Range(0.9f, 1.4f), late ? 10 : 8, BulletType.Rice, BulletColor.Azure, 2f));
                Sound("mp_fling", at, 0.35f, 1.3f);
            }
            if (forget <= 0f) { forget = late ? 3f : 3.8f; forgetting = StartCoroutine(Forget(late ? 1.1f : 0.9f)); Cast(2, 0.6f); }
            yield return Wait(0.1f);
        }
    }
}
