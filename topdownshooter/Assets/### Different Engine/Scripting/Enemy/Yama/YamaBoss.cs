using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Yama, King of Hell and Judge of the Dead: the Courtyard's final boss, fought Touhou's way.
//   his entrance: the horde falls to its knees and is gone, a bell tolls three times, the ground
//   cracks open into the gate of hell and he rises out of it in his aureole of flame, his name
//   across the screen and his music taking over
//   his fight: six phases, each with its own bar, turns of plain danmaku between four spell cards.
//   a card is declared (his face sweeping across, its name under his bar, the wheel of the six
//   realms turning behind him); breaking it cancels every bullet into sparkles and wen, and one
//   cleared without being hit pays a bonus in coins. the last card he fights in a rage
//   his end: he shudders, bursts from within, and goes up in a blast of gold; the horde goes with
//   him, and his envelope and the run's results follow (RunVictory)
// no clock runs while he's up (GameLoopController): it's him or the player. his bullets take a
// fixed share of the player's max health, never scaled with the run (Danmaku), and damage never
// carries from one phase into the next, so he can't be burst down. his health follows the map's
// health curve and the horde's evolution pressure (SpawnDirector). art and sounds from Resources
// (YamaArt); his music is the Boss Music slot below
[RequireComponent(typeof(EnemyHealth))]
public class YamaBoss : MonoBehaviour, IDamageGate
{
    [Header("Music")]
    [Tooltip("the fight's own music: the run's music fades out as he comes and this fades in, looping, until he falls. empty: the run's music carries on")]
    public AudioClip bossMusic;
    [Range(0f, 1f)] public float musicVolume = 1f;
    [Min(0f)] public float musicFadeOut = 2f;
    [Min(0.1f)] public float musicFadeIn = 3f;

    [Header("Health")]
    [Tooltip("his health for each step of the map's health curve when he comes (x15 or so at 27:00 on the Courtyard), times the horde's evolution pressure")]
    [Min(1f)] public float healthPerCurve = 5600f;
    [Tooltip("each phase's share of his health, in order: Verdict, Ledger, Tribute, Naihe, Ten Courts, Mirror")]
    public float[] phaseShares = { 0.12f, 0.18f, 0.12f, 0.18f, 0.19f, 0.21f };

    [Header("The fight")]
    [Tooltip("how thin the horde runs while he's up: 1 = as usual")]
    [Range(0f, 1f)] public float crowdDuringFight = 0.3f;
    [Tooltip("how far from the player he keeps")]
    [Min(2f)] public float hoverDistance = 5f;
    [Tooltip("coins for clearing each spell card without being hit (before Greed)")]
    public int[] spellBonus = { 400, 550, 700, 1000 };

    // ---- the phases

    private struct Phase
    {
        public string card;             // null: plain danmaku
        public Func<IEnumerator> pattern;
        public bool rage;
    }

    private Phase[] phases;
    private float[] floors;             // the health each phase ends at
    private int phase = -1;
    private float floor, top;
    private bool broke, invulnerable = true, dying, allowKill;

    private EnemyHealth health;
    private SpawnDirector director;
    private SpriteRenderer body, halo, gate, wheel;
    private Collider2D hurtbox;
    private YamaScreen screen;
    private BossGrade grade;
    private Sprite[] frames, haloFrames, gateFrames;
    private readonly List<SpriteRenderer> ghosts = new List<SpriteRenderer>();
    private readonly List<float> ghostAge = new List<float>();

    private Vector2 offset = new Vector2(0f, 4.6f);    // where he keeps, from the player
    private Vector2 basePos;
    private float follow = 1.6f, glideUntil, castUntil, nextGhost, nextHitSound, age;
    private int castPose;
    private bool following, rage;
    private int cardsLeft;

    // ---- setting up

    public void Begin(SpawnDirector spawner, EnemyArchetype arch)
    {
        director = spawner;
        health = GetComponent<EnemyHealth>();
        float total = healthPerCurve * (director != null ? director.HealthMultiplier : 1f);
        health.SetScaled(total);
        if (arch != null)
        {
            health.armour = arch.armour;
            health.physicalTaken = arch.physicalTaken;
            health.magicalTaken = arch.magicalTaken;
        }

        phases = new[]
        {
            new Phase { pattern = Verdict },
            new Phase { card = "Judgement Sign \"Ledger of Life and Death\"", pattern = Ledger },
            new Phase { pattern = Tribute },
            new Phase { card = "Oblivion Sign \"Meng Po's Soup at Naihe Bridge\"", pattern = Naihe },
            new Phase { card = "Hell Sign \"Verdict of the Ten Courts\"", pattern = TenCourts },
            new Phase { card = "Karma Sign \"Mirror of Retribution\"", pattern = Mirror, rage = true },
        };
        floors = new float[phases.Length];
        float sum = 0f, left = total;
        for (int i = 0; i < phases.Length; i++) sum += Share(i);
        for (int i = 0; i < phases.Length; i++)
        {
            left -= total * Share(i) / sum;
            floors[i] = i == phases.Length - 1 ? 1f : Mathf.Max(1f, left);
        }
        foreach (var p in phases) if (p.card != null) cardsLeft++;

        basePos = transform.position;
        StartCoroutine(Fight());
    }

    private float Share(int i) => phaseShares != null && i < phaseShares.Length ? Mathf.Max(0.01f, phaseShares[i]) : 1f / 6f;

    private void Awake()
    {
        frames = YamaArt.Frames("yama");
        haloFrames = YamaArt.Frames("halo");
        gateFrames = YamaArt.Frames("gate");
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
        wheel = Loose("Wheel of the Six Realms", "Background", 1000);
        var w = YamaArt.Frames("wheel");
        if (w != null) wheel.sprite = w[0];
        wheel.transform.localScale = Vector3.one * 2.4f;
        wheel.color = new Color(0.9f, 0.5f, 1f, 0f);
        gate = Loose("Gate of Hell", "Background", 999);
        gate.transform.localScale = Vector3.one * 1.6f;
        gate.enabled = false;

        // he hovers: nothing pushes him, and he's out of reach until his entrance is over
        if (TryGetComponent(out Rigidbody2D rb)) { rb.bodyType = RigidbodyType2D.Kinematic; rb.linearVelocity = Vector2.zero; }
        if (TryGetComponent(out CircleCollider2D circle)) { circle.radius = 1.3f; circle.offset = new Vector2(0f, 0.1f); }
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

    private SpriteRenderer Loose(string name, string layer, int order)
    {
        var sr = new GameObject(name).AddComponent<SpriteRenderer>();
        sr.sortingLayerName = layer;
        sr.sortingOrder = order;
        return sr;
    }

    private void OnDestroy()
    {
        foreach (var g in ghosts) if (g != null) Destroy(g.gameObject);
        if (wheel != null) Destroy(wheel.gameObject);
        if (gate != null) Destroy(gate.gameObject);
        Danmaku.ShowHitbox = false;
        Danmaku.PlayerHit -= OnPlayerHit;
        if (director != null) director.BossCrowd = 1f;
    }

    // the player hit: the edges of the world bleed red for a moment
    private void OnPlayerHit(Vector2 at)
    {
        if (grade != null) grade.Hurt();
        screen.Flash(new Color(1f, 0.1f, 0.1f), 0.18f, 0.3f);
    }

    private BossGrade.Look Mood => rage ? BossGrade.Look.Rage : BossGrade.Look.Fight;

    // ---- the damage gate: each phase's bar is its own, and he can't be hurt between them

    public float Admit(float damage, DamageKind kind, float current)
    {
        if (allowKill) return damage;
        if (kind == DamageKind.Silent || invulnerable || dying) return 0f;
        float room = current - floor;
        if (room <= 0f) { broke = true; return 0f; }
        if (damage >= room) { broke = true; damage = room; }
        // Touhou's tick of a boss being hit, not every hit's own sound
        if (Time.time >= nextHitSound)
        {
            nextHitSound = Time.time + 0.075f;
            YamaArt.Play("yama_hit", transform.position, 0.28f, UnityEngine.Random.Range(0.95f, 1.05f));
        }
        return damage;
    }

    // ---- every frame: where he is, how he looks

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
            if (Time.time < glideUntil && Time.time >= nextGhost) { nextGhost = Time.time + 0.045f; Ghost(); }
        }
        // he floats, a pixel or two either way
        transform.position = basePos + new Vector2(0f, Mathf.Round(Mathf.Sin(age * 2.2f) * 3f) / YamaArt.WorldPpu);

        if (frames != null && frames.Length >= 14 && body.enabled)
        {
            int baseFrame = rage ? 7 : 0;
            int f = Time.time < castUntil ? (castPose == 2 ? 6 : 4 + (int)(age * 8f) % 2) : (int)(age * 6f) % 4;
            body.sprite = frames[baseFrame + f];
        }
        if (halo.enabled && haloFrames != null)
        {
            halo.sprite = YamaArt.Frame(haloFrames, age, rage ? 14f : 9f);
            halo.color = rage ? new Color(1f, 0.55f, 0.55f, 0.95f) : new Color(1f, 1f, 1f, 0.85f);
        }
        if (wheel.color.a > 0f)
        {
            wheel.transform.position = transform.position;
            wheel.transform.rotation = Quaternion.Euler(0f, 0f, age * (rage ? -22f : 12f));
        }

        for (int i = 0; i < ghosts.Count; i++)
        {
            if (!ghosts[i].enabled) continue;
            ghostAge[i] += dt;
            float k = ghostAge[i] / 0.35f;
            if (k >= 1f) { ghosts[i].enabled = false; continue; }
            ghosts[i].color = new Color(1f, 0.3f, 0.35f, 0.45f * (1f - k));
        }

        if (phase >= 0 && !dying)
        {
            float span = Mathf.Max(1f, top - floor);
            screen.SetBar((health.Current - floor) / span, cardsLeft, invulnerable);
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

    private void Cast(int pose, float seconds = 0.45f)
    {
        castPose = pose;
        castUntil = Time.time + seconds;
    }

    private Vector2 E => (Vector2)transform.position + new Vector2(0f, 0.25f);

    private static WaitForSeconds Wait(float s) => new WaitForSeconds(s);

    // a new spot round the player, on screen, off to one side of where he was
    private void Relocate(float seconds = 0.75f)
    {
        var cam = Camera.main;
        float h = cam != null && cam.orthographic ? cam.orthographicSize : 6f;
        float w = cam != null && cam.orthographic ? h * cam.aspect : 10f;
        float a = Mathf.Atan2(offset.y, offset.x) + UnityEngine.Random.Range(0.7f, 1.6f) * (UnityEngine.Random.value < 0.5f ? -1f : 1f);
        var next = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * UnityEngine.Random.Range(hoverDistance * 0.85f, hoverDistance * 1.1f);
        next.x = Mathf.Clamp(next.x, -(w - 2f), w - 2f);
        next.y = Mathf.Clamp(next.y, -(h - 2.2f), h - 2.2f);
        if (next.magnitude < hoverDistance * 0.7f) next = next.normalized * hoverDistance * 0.7f;
        offset = next;
        glideUntil = Time.time + seconds;
        YamaArt.Play("yama_dash", transform.position, 0.35f, UnityEngine.Random.Range(0.95f, 1.05f));
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
        Vector2 gateAt = basePos + new Vector2(0f, -1.4f);
        if (director != null) director.PauseSpawning(14f);
        screen.Letterbox(true);
        screen.Dim(0.35f);
        screen.Vignette(new Color(0.55f, 0.05f, 0.1f), 0.35f);
        // the world's light turns to the underworld's as he comes
        grade.Set(BossGrade.Look.Fight, 3.5f);
        if (bossMusic != null && BGMManager.Instance != null)
            BGMManager.Instance.PlayBossTheme(bossMusic, musicVolume, musicFadeOut, musicFadeIn);

        // the dead kneel to their king, and are gone
        StartCoroutine(Kneel());
        YamaArt.Play("yama_toll", basePos, 1f);
        YamaArt.Play("yama_rumble", basePos, 0.8f);
        Juice.Shake(0.12f);
        yield return Wait(1.2f);

        YamaArt.Play("yama_toll", basePos, 1f, 0.94f);
        YamaArt.Play("yama_gate", gateAt, 1f);
        Juice.Shake(0.22f);
        // the ground cracks open into the gate
        gate.transform.position = gateAt;
        gate.enabled = true;
        StartCoroutine(Gate());
        grade.Punch(0.45f);
        yield return Wait(1.2f);

        YamaArt.Play("yama_toll", basePos, 1f, 0.88f);
        Juice.Shake(0.32f);
        grade.Punch(0.3f);
        yield return Wait(0.35f);

        // he rises out of it
        body.enabled = true;
        halo.enabled = true;
        for (float t = 0f; t < 1.3f; t += Time.deltaTime)
        {
            float k = t / 1.3f, e = 1f - (1f - k) * (1f - k);
            basePos = Vector2.Lerp(gateAt + Vector2.down * 0.6f, gateAt + Vector2.up * 1.4f, e);
            body.color = Color.Lerp(new Color(0.2f, 0f, 0.05f, 0f), Color.white, Mathf.Clamp01(k * 1.4f));
            halo.transform.localScale = Vector3.one * Mathf.Lerp(0.2f, 1f, e);
            yield return null;
        }
        body.color = Color.white;

        // his name, and his roar
        Cast(1, 1.2f);
        YamaArt.Play("yama_roar", basePos, 1f);
        Juice.Freeze(0.1f);
        Juice.Shake(0.7f);
        screen.Flash(Color.white, 0.7f, 0.6f);
        grade.Punch(1f);
        screen.Title("YAMA", "King of Hell  -  Judge of the Dead", 3f);
        var death = YamaArt.Frames("death");
        if (death != null) FxBatch.Play(death, 16f, basePos, 1.5f, "Aura", 41);
        yield return Wait(1.2f);
        yield return Wait(1.8f);

        screen.Letterbox(false);
        screen.Dim(0.06f);
        screen.Vignette(new Color(0.55f, 0.05f, 0.1f), 0.15f);
        screen.ShowBar("Yama  -  King of Hell");
        YamaArt.Play("yama_bar", basePos, 0.7f);
        if (director != null) director.BossCrowd = crowdDuringFight;
        offset = basePos - Danmaku.PlayerPosition;
        following = true;
        if (hurtbox != null) hurtbox.enabled = true;
        Danmaku.ShowHitbox = true;
        yield return Wait(1f);
    }

    // everything the horde has left sinks away before him, a few at a time
    private IEnumerator Kneel()
    {
        var all = new List<GameObject>(EnemyRegistry.All);
        all.Remove(gameObject);
        for (int i = all.Count - 1; i > 0; i--) { int j = UnityEngine.Random.Range(0, i + 1); (all[i], all[j]) = (all[j], all[i]); }
        float per = all.Count > 0 ? 1.4f / all.Count : 0f, owed = 0f;
        foreach (var e in all)
        {
            if (e != null && e.activeInHierarchy && e.TryGetComponent(out EnemyHealth h)) h.TakeDamage(h.Current + h.Max + 1f, DamageKind.Silent);
            owed += per;
            if (owed >= Time.deltaTime) { owed = 0f; yield return null; }
        }
    }

    // the gate of hell: cracking open, then burning, then closing as he's risen
    private IEnumerator Gate()
    {
        if (gateFrames == null || gateFrames.Length < 12) { gate.enabled = false; yield break; }
        float t = 0f;
        while (t < 4.2f)
        {
            t += Time.deltaTime;
            gate.sprite = t < 0.6f ? gateFrames[Mathf.Min(5, (int)(t * 10f))] : gateFrames[6 + (int)(t * 8f) % 6];
            gate.color = new Color(1f, 1f, 1f, t > 3.2f ? 1f - (t - 3.2f) : 1f);
            yield return null;
        }
        gate.enabled = false;
    }

    private IEnumerator RunPhase(int i)
    {
        var p = phases[i];
        phase = i;
        top = i == 0 ? health.Max : floors[i - 1];
        floor = floors[i];
        broke = false;
        invulnerable = true;
        screen.RefillBar();

        if (p.rage && !rage)
        {
            rage = true;
            YamaArt.Play("yama_roar", transform.position, 1f, 0.85f);
            Juice.Shake(0.5f);
            screen.Flash(new Color(1f, 0.2f, 0.2f), 0.45f, 0.5f);
            screen.Vignette(new Color(0.75f, 0.05f, 0.08f), 0.4f, true);
            grade.Set(BossGrade.Look.Rage, 1.5f);
            grade.Punch(0.9f);
        }

        if (p.card != null) yield return Declare(p.card);
        else yield return Wait(0.9f);

        invulnerable = false;
        int hitsBefore = Danmaku.Hits;
        var run = StartCoroutine(p.pattern());
        while (!broke) yield return null;
        StopCoroutine(run);
        invulnerable = true;
        yield return Break(p, Danmaku.Hits == hitsBefore);
    }

    private IEnumerator Declare(string card)
    {
        Cast(1, 1.3f);
        YamaArt.Play("yama_declare", transform.position, 1f);
        var charge = YamaArt.Frames("charge");
        if (charge != null) FxBatch.Play(charge, 8f, E, 2.4f, "Aura", 42);
        screen.Declare(card, YamaArt.Frames("portrait", 100f)?[0]);
        screen.Dim(0.2f);
        grade.Set(rage ? BossGrade.Look.Rage : BossGrade.Look.Spell, 1.2f);
        grade.Punch(0.5f);
        Juice.Shake(0.18f);
        StartCoroutine(FadeWheel(0.4f, 0.6f));
        yield return Wait(1.5f);
    }

    private IEnumerator FadeWheel(float to, float seconds)
    {
        float from = wheel.color.a;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            var c = wheel.color;
            c.a = Mathf.Lerp(from, to, t / seconds);
            wheel.color = c;
            yield return null;
        }
        var e = wheel.color;
        e.a = to;
        wheel.color = e;
    }

    private IEnumerator Break(Phase p, bool clean)
    {
        // every bullet cancelled into sparkles and wen, a blast in him, the screen jolting
        Danmaku.Cancel(true);
        YamaArt.Play("yama_break", transform.position, 1f);
        var blast = YamaArt.Frames("blast");
        if (blast != null) FxBatch.Play(blast, 18f, transform.position, 2f, "Aura", 42);
        screen.Flash(Color.white, 0.4f, 0.35f);
        grade.Punch(0.8f);
        Juice.Freeze(0.08f);
        Juice.Shake(0.45f);

        if (p.card != null)
        {
            int card = Array.FindAll(phases, x => x.card != null).Length - cardsLeft;
            cardsLeft--;
            StartCoroutine(FadeWheel(0f, 0.8f));
            screen.ClearCard();
            screen.Dim(0.06f);
            grade.Set(Mood, 1.5f);
            if (clean)
            {
                int coins = Coins.WithGreed(spellBonus != null && card < spellBonus.Length ? spellBonus[card] : 500);
                Coins.Add(coins);
                screen.Bonus($"Spell Card Bonus!  +{coins} coins", new Color(1f, 0.85f, 0.35f));
                YamaArt.Play("yama_bonus", transform.position, 0.9f);
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
        if (director != null) { director.StopSpawning(); director.BossCrowd = 1f; }
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && player.TryGetComponent(out PlayerInventory inv)) inv.LockLeveling();
        if (BGMManager.Instance != null) BGMManager.Instance.EndBossTheme(3.5f);
        screen.ClearCard();
        screen.HideBar();
        screen.Letterbox(true);
        screen.Dim(0.3f);
        grade.Set(BossGrade.Look.Dying, 0.8f);
        following = false;
        StartCoroutine(FadeWheel(0f, 0.5f));
        YamaArt.Play("yama_death_cry", transform.position, 1f);

        // time slows as he comes apart (the level ups are locked, so nothing else owns the clock)
        bool slowed = Mathf.Approximately(Time.timeScale, 1f);
        if (slowed) Time.timeScale = 0.4f;

        var blast = YamaArt.Frames("blast");
        Vector2 at = basePos;
        for (int k = 0; k < 16; k++)
        {
            float gap = Mathf.Lerp(0.26f, 0.05f, k / 15f);
            Vector2 p = at + UnityEngine.Random.insideUnitCircle * 1.4f + Vector2.up * 0.3f;
            if (blast != null) FxBatch.Play(blast, 20f, p, UnityEngine.Random.Range(0.9f, 1.5f), "Aura", 42);
            YamaArt.Play("yama_blast", p, 0.55f, UnityEngine.Random.Range(0.85f, 1.15f));
            Juice.Shake(Mathf.Lerp(0.15f, 0.4f, k / 15f));
            grade.Punch(Mathf.Lerp(0.2f, 0.55f, k / 15f));
            // he shudders and flickers white
            basePos = at + UnityEngine.Random.insideUnitCircle * 0.12f;
            body.color = k % 2 == 0 ? new Color(1f, 0.6f, 0.6f) : Color.white;
            yield return new WaitForSecondsRealtime(gap);
        }
        if (slowed && Time.timeScale > 0f) Time.timeScale = 1f;
        basePos = at;

        // the end: a blast of gold, the screen white
        var death = YamaArt.Frames("death");
        if (death != null) FxBatch.Play(death, 14f, at, 2.6f, "Aura", 43);
        YamaArt.Play("yama_death", at, 1f);
        screen.Flash(Color.white, 1f, 1.4f);
        grade.Punch(1f);
        grade.Set(BossGrade.Look.Triumph, 2.5f);
        Juice.Freeze(0.15f);
        Juice.Shake(1f);
        body.enabled = false;
        halo.enabled = false;
        for (int i = 0; i < 36; i++)
        {
            float a = i / 36f * Mathf.PI * 2f;
            PickupSystem.DropWen(at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * UnityEngine.Random.Range(0.6f, 2.4f), 6);
        }
        yield return Wait(1.2f);
        screen.Title("YAMA HAS FALLEN", "The Ledger of Life and Death is closed", 3.4f);
        screen.Letterbox(false);
        screen.Dim(0f);
        screen.Vignette(new Color(1f, 0.85f, 0.4f), 0.15f);
        YamaArt.Play("yama_victory", at, 0.9f);

        // the horde goes with him, his envelope falls, and the run is won (RunVictory)
        if (director != null) director.FinalBossDown(at);
        else RunVictory.Begin();
        allowKill = true;
        if (hurtbox != null) hurtbox.enabled = false;
        health.TakeDamage(health.Current + 1f, DamageKind.Normal);
    }

    // ================================================================ his danmaku

    // plain: aimed volleys of gold rice, then two rings of red
    private IEnumerator Verdict()
    {
        while (true)
        {
            Cast(2);
            for (int v = 0; v < 3; v++)
            {
                Danmaku.Fan(E, Danmaku.AimAt(E), 5, 36f, Shot.Of(BulletType.Rice, BulletColor.Gold, 4.3f));
                yield return Wait(0.22f);
            }
            yield return Wait(0.35f);
            Cast(1);
            float a0 = UnityEngine.Random.value * 360f;
            Danmaku.Ring(E, 22, a0, Shot.Of(BulletType.Orb, BulletColor.Red, 2.5f));
            yield return Wait(0.35f);
            Danmaku.Ring(E, 22, a0 + 360f / 44f, Shot.Of(BulletType.Orb, BulletColor.Red, 2.0f));
            yield return Wait(0.8f);
            Relocate();
            yield return Wait(0.9f);
        }
    }

    // the ledger's pages: walls of talismans with a gap to slip through, and names struck out of
    // it bursting into red as they go
    private IEnumerator Ledger()
    {
        int k = 0;
        while (true)
        {
            Cast(k % 2 == 0 ? 1 : 2);
            float aim = Danmaku.AimAt(E) + UnityEngine.Random.Range(-15f, 15f);
            const int n = 36;
            const float spread = 150f;
            int gap = UnityEngine.Random.Range(8, n - 12);
            for (int i = 0; i < n; i++)
            {
                if (i >= gap && i < gap + 4) continue;
                float a = aim - spread / 2f + spread * i / (n - 1);
                var s = UnityEngine.Random.value < 0.15f
                    ? Shot.Of(BulletType.Talisman, BulletColor.Red, 1.6f).Accel(1.1f, 0f, 3.2f).Burst(1.35f, 5, BulletType.Rice, BulletColor.Red, 2.1f)
                    : Shot.Of(BulletType.Talisman, BulletColor.Bone, 1.6f).Accel(1.1f, 0f, 3.2f);
                Danmaku.Fire(E, a, s);
            }
            YamaArt.Play("yama_page", transform.position, 0.6f, UnityEngine.Random.Range(0.95f, 1.05f));
            k++;
            yield return Wait(1.75f);
            if (k % 3 == 0) { Relocate(); yield return Wait(0.8f); }
        }
    }

    // plain: a spinning flower of copper cash slowing to a drift, and jade orbs thrown at the player
    private IEnumerator Tribute()
    {
        float spin = 0f;
        int dir = 1;
        while (true)
        {
            Cast(1, 2.4f);
            for (float t = 0f; t < 2.4f; t += 0.12f)
            {
                Danmaku.Ring(E, 6, spin, Shot.Of(BulletType.Coin, BulletColor.Gold, 3.6f).Accel(-3f, 0.7f).Life(7f));
                spin += 13f * dir;
                yield return Wait(0.12f);
            }
            Cast(2);
            Danmaku.Fan(E, Danmaku.AimAt(E), 3, 30f, Shot.Of(BulletType.BigOrb, BulletColor.Jade, 2.6f));
            dir = -dir;
            yield return Wait(1f);
            Relocate();
            yield return Wait(0.8f);
        }
    }

    // Meng Po's soup: two spirals of azure curling round each other, and bowls thrown at the player
    // that break into more
    private IEnumerator Naihe()
    {
        float a = 0f, bowl = 0f;
        while (true)
        {
            Danmaku.Fire(E, a, Shot.Of(BulletType.Orb, BulletColor.Azure, 2.1f).Turn(22f).Life(9f));
            Danmaku.Fire(E, 180f - a, Shot.Of(BulletType.Orb, BulletColor.Azure, 2.1f).Turn(-22f).Life(9f));
            a += 11f;
            bowl += 0.1f;
            if (bowl >= 2.2f)
            {
                bowl = 0f;
                Cast(2);
                Danmaku.Fire(E, Danmaku.AimAt(E), Shot.Of(BulletType.BigOrb, BulletColor.Jade, 2.5f).Burst(1.4f, 12, BulletType.Rice, BulletColor.Azure, 2.2f));
            }
            yield return Wait(0.1f);
        }
    }

    // ten rings, one for each court, each quicker and fuller than the last, each with its gap
    // somewhere near the player; then a fan of hellfire, and again, faster
    private IEnumerator TenCourts()
    {
        float boost = 0f;
        while (true)
        {
            Cast(1, 5f);
            for (int k = 0; k < 10; k++)
            {
                int n = 16 + 2 * k;
                float step = 360f / n;
                float gapAt = Danmaku.AimAt(E) + UnityEngine.Random.Range(-40f, 40f);
                var s = Shot.Of(k % 2 == 0 ? BulletType.Orb : BulletType.Rice, k % 2 == 0 ? BulletColor.Red : BulletColor.Violet, 1.7f + 0.2f * k + boost);
                for (int i = 0; i < n; i++)
                {
                    float ang = gapAt + step * i + (k % 2) * step * 0.5f;
                    if (Mathf.Abs(Mathf.DeltaAngle(ang, gapAt)) < step * 1.6f) continue;
                    Danmaku.Fire(E, ang, s);
                }
                YamaArt.Play("yama_court", transform.position, 0.55f, 0.8f + k * 0.05f);
                yield return Wait(0.5f);
            }
            Cast(2);
            Danmaku.Fan(E, Danmaku.AimAt(E), 7, 50f, Shot.Of(BulletType.Flame, BulletColor.Red, 3.8f));
            boost = Mathf.Min(boost + 0.25f, 1f);
            yield return Wait(1.3f);
            Relocate();
            yield return Wait(0.8f);
        }
    }

    // the mirror shows the player their own path: marks open where they were a moment ago and burst,
    // so standing still is death; hellfire aimed at them, and bone rings as he weakens
    private IEnumerator Mirror()
    {
        var trail = new List<Vector2>(64);
        float mark = 0f, shot = 0f, ring = 0f;
        while (true)
        {
            trail.Add(Danmaku.PlayerPosition);
            if (trail.Count > 30) trail.RemoveAt(0);
            mark += 0.1f; shot += 0.1f; ring += 0.1f;
            if (mark >= 0.35f && trail.Count >= 15)
            {
                mark = 0f;
                Vector2 was = trail[trail.Count - 14] + UnityEngine.Random.insideUnitCircle * 0.3f;
                Danmaku.Fire(was, 0f, Shot.Of(BulletType.BigOrb, BulletColor.Violet, 0f).Delay(0.7f).Burst(0.02f, 8, BulletType.Rice, BulletColor.Violet, 1.7f).Life(1f));
            }
            if (shot >= 1.3f)
            {
                shot = 0f;
                Cast(2);
                Danmaku.Fan(E, Danmaku.AimAt(E), 3, 24f, Shot.Of(BulletType.Flame, BulletColor.Red, 3.6f));
            }
            float span = Mathf.Max(1f, top - floor);
            if (ring >= 2.2f && (health.Current - floor) / span < 0.5f)
            {
                ring = 0f;
                Danmaku.Ring(E, 24, UnityEngine.Random.value * 360f, Shot.Of(BulletType.Orb, BulletColor.Bone, 1.6f));
            }
            yield return Wait(0.1f);
        }
    }
}
