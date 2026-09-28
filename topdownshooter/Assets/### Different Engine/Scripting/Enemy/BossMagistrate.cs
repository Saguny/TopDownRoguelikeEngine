using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// the Jiangshi Magistrate, the Final Rush boss. it hops after the player the way a jiangshi does
// (moving only while in the air) and between hops takes turns at three attacks, each shown before
// it lands:
//   leap   a seal marks the ground under the player; it crouches, leaps there, and the landing
//          hurts everything round it and throws out a ring of corpse fire
//   storm  it plants itself, raises its arms and spins out arms of corpse fire
//   raise  lightning strikes round the player and jiangshi climb out where it struck
// below half health the seal on its face tears: it hops faster and every attack is doubled.
// damage follows its contact damage, so it scales with the run like the rest of it
[RequireComponent(typeof(EnemyHealth))]
public class BossMagistrate : MonoBehaviour
{
    [Header("Art (Tools/VFX/boss)")]
    public Sprite[] hopFrames = new Sprite[0];
    public Sprite[] castFrames = new Sprite[0];
    public Sprite[] tornHopFrames = new Sprite[0];
    public Sprite[] tornCastFrames = new Sprite[0];
    public Sprite shadowSprite;
    [Tooltip("the shadow sits this far below the boss's position, world units")]
    public float shadowDrop = 1.5f;
    [Min(0.1f)] public float hopFps = 9f;

    [Header("Rhythm")]
    public float firstAttackDelay = 2.5f;
    [Tooltip("seconds of hopping between attacks, a random amount in this range")]
    public Vector2 hopBetween = new Vector2(2.5f, 4f);
    [Tooltip("its speed while in the air, times its walking speed. on the ground it barely moves")]
    public float airSpeed = 2.2f;
    [Tooltip("how much faster everything goes once the seal has torn")]
    public float tornPace = 1.35f;

    [Header("Corpse fire")]
    public GameObject bulletPrefab;
    public float bulletSpeed = 4.2f;
    [Tooltip("damage of one shot, as a share of the boss's contact damage per tick")]
    public float bulletDamageShare = 0.5f;

    [Header("Leap")]
    public GameObject markPrefab;
    [Tooltip("the radius the Mark and Slam prefabs are drawn for, world units")]
    public float artRadius = 2f;
    public GameObject slamPrefab;
    public float slamRadius = 2.2f;
    public float leapWarning = 0.8f;
    public float leapSeconds = 0.55f;
    public float leapHeight = 2.5f;
    [Tooltip("damage of the landing, as a share of the boss's contact damage per tick")]
    public float slamDamageShare = 1.5f;
    [Min(0)] public int ringShots = 18;
    public float slamShake = 0.18f;

    [Header("Storm")]
    public float stormSeconds = 2.6f;
    [Tooltip("shots a second from each arm")]
    public float stormRate = 6f;
    [Min(1)] public int stormArms = 3;
    [Tooltip("how fast the arms turn, degrees a second")]
    public float stormSpin = 70f;

    [Header("Raise the dead")]
    public GameObject minionPrefab;
    [Min(0)] public int minions = 4;
    [Tooltip("the most of its raised dead alive at once")]
    [Min(0)] public int maxMinions = 8;
    [Tooltip("a raised jiangshi's health, as a share of the boss's")]
    public float minionHealthShare = 0.03f;
    public GameObject raiseFx;
    public float raiseRadius = 3.2f;
    [Tooltip("seconds a seal glows under each spot before the dead come up there, so the player can get clear")]
    public float raiseWarning = 0.9f;
    [Tooltip("the radius of that seal, world units")]
    public float raiseMarkRadius = 0.75f;
    [Tooltip("seconds a raised jiangshi takes to climb out of the ground; it can't hurt or move until it's up")]
    public float riseSeconds = 0.5f;

    [Header("Sound (Tools/SFX/boss.py)")]
    [Tooltip("the boss's sounds are made at the game's reference loudness; this sets them in the mix, like an enemy death's 0.45")]
    [Range(0f, 1f)] public float soundVolume = 0.5f;
    [Tooltip("the leap's seal: timed to the crouch and the flight, so it's played faster once the seal has torn")]
    public AudioClip sealSound;
    public AudioClip leapSound;
    public AudioClip slamSound;
    [Tooltip("raising its arms and the arms of fire turning, as long as the storm")]
    public AudioClip stormSound;
    [Tooltip("the cast and the seals glowing round the player, timed to the warning")]
    public AudioClip raiseSound;
    [Tooltip("lightning into each seal as the dead come up, once a spot")]
    public AudioClip strikeSound;
    public AudioClip tearSound;

    [Header("Health bar")]
    public float barWidth = 2.2f;
    public float barHeight = 3.3f;

    private const float PixelSize = 1.3f / 37f;

    private enum Pose { Hop, Crouch, Cast, Air }
    private Pose pose;
    private float animTime;
    private bool torn;
    private EnemyHealth health;
    private EnemyMovement movement;
    private EnemyContactDamage contact;
    private Rigidbody2D body;
    private Collider2D bodyCollider;
    private SpriteRenderer sr;
    private Transform shadow;
    private Vector2 ground;
    private bool airborne;
    private SpriteRenderer barFill;
    private float shownSpeed = -1f;
    private int lastAttack = -1;
    private readonly List<EnemyHealth> raised = new List<EnemyHealth>();
    private static Sprite pixel;
    // its own voices, so the horde's hits and deaths (which share SfxPlayer's) can't cut a
    // three second storm off halfway
    private AudioSource[] voices;
    private int nextVoice;
    // voices of their own out where the dead come up round the player, so the strikes are heard
    // from there and not from the boss
    private AudioSource[] groundVoices;
    private int nextGroundVoice;
    private bool soundHeld;

    private float Pace => torn ? tornPace : 1f;
    private float Damage => contact != null ? contact.damagePerTick : 10f;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        movement = GetComponent<EnemyMovement>();
        contact = GetComponent<EnemyContactDamage>();
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        sr = GetComponent<SpriteRenderer>();

        if (shadowSprite != null)
        {
            var go = new GameObject("Magistrate Shadow");
            var s = go.AddComponent<SpriteRenderer>();
            s.sprite = shadowSprite;
            s.sortingLayerName = "Enemy";
            s.sortingOrder = -1;
            shadow = go.transform;
        }
        MakeBar();
        MakeVoices();
    }

    private void OnEnable()
    {
        torn = false;
        airborne = false;
        pose = Pose.Hop;
        if (shadow != null) shadow.gameObject.SetActive(true);
        StartCoroutine(Brain());
    }

    private void OnDisable()
    {
        StopVoices(voices);
        StopVoices(groundVoices);
        soundHeld = false;
        if (shadow != null) shadow.gameObject.SetActive(false);
        if (body != null) body.simulated = true;
        if (bodyCollider != null) bodyCollider.enabled = true;
    }

    private void OnDestroy()
    {
        if (shadow != null) Destroy(shadow.gameObject);
        if (groundVoices != null) foreach (var v in groundVoices) if (v != null) Destroy(v.gameObject);
    }

    // ---- every frame: the pose's frame, its hop, its shadow and its bar

    private void Update()
    {
        float dt = Time.deltaTime;
        animTime += dt * Pace;

        if (!torn && health.Current <= health.Max * 0.5f && health.Current > 0f) Tear();

        var hop = torn && tornHopFrames.Length > 0 ? tornHopFrames : hopFrames;
        var cast = torn && tornCastFrames.Length > 0 ? tornCastFrames : castFrames;
        float speed = 0f;
        switch (pose)
        {
            case Pose.Hop:
                if (hop.Length > 0)
                {
                    int f = (int)(animTime * hopFps) % hop.Length;
                    sr.sprite = hop[f];
                    // it only covers ground in the air: the middle of the hop
                    speed = f >= 2 && f <= 4 ? airSpeed * Pace : 0.1f;
                }
                break;
            case Pose.Crouch:
                if (hop.Length > 1) sr.sprite = hop[1];
                break;
            case Pose.Air:
                if (hop.Length > 3) sr.sprite = hop[3];
                break;
            case Pose.Cast:
                if (cast.Length > 0) sr.sprite = cast[(int)(animTime * 8f) % cast.Length];
                break;
        }
        if (movement != null && !Mathf.Approximately(speed, shownSpeed))
        {
            shownSpeed = speed;
            movement.SetSpeedMultiplier(speed);
        }
    }

    private void LateUpdate()
    {
        LateUpdateSound();
        if (!airborne) ground = transform.position;
        if (shadow != null) shadow.position = ground + Vector2.down * shadowDrop;
        if (barFill != null)
        {
            float k = health.Max > 0f ? Mathf.Clamp01(health.Current / health.Max) : 0f;
            barFill.transform.localScale = new Vector3(barWidth * k, 3f * PixelSize, 1f);
            barFill.transform.localPosition = new Vector3(-barWidth * (1f - k) * 0.5f, barHeight, 0f);
        }
    }

    // ---- the fight

    private IEnumerator Brain()
    {
        // several bosses at once shouldn't all attack on the same beat
        yield return new WaitForSeconds(firstAttackDelay + Random.Range(0f, 2.5f));
        while (true)
        {
            pose = Pose.Hop;
            yield return new WaitForSeconds(Random.Range(hopBetween.x, hopBetween.y) / Pace);

            raised.RemoveAll(e => e == null || !e.isActiveAndEnabled);
            int attack;
            do attack = Random.Range(0, 3);
            while (attack == lastAttack || (attack == 2 && (minionPrefab == null || raised.Count >= maxMinions)));
            lastAttack = attack;

            if (attack == 0) yield return Leap();
            else if (attack == 1) yield return Storm();
            else yield return Raise();
        }
    }

    private IEnumerator Leap()
    {
        var player = PlayerAwareness.Player;
        if (player == null) yield break;
        Vector2 target = player.position;

        // the seal closes in over the crouch and the flight: it lands when the seal completes
        float warning = leapWarning / Pace, flight = leapSeconds / Pace;
        var mark = FxOneShot.Play(markPrefab, target, 0f, slamRadius / artRadius);
        if (mark != null)
        {
            if (mark.TryGetComponent(out Flipbook seal)) seal.PlayOver(warning + flight);
            if (mark.TryGetComponent(out FxOneShot shot)) shot.lifetime = warning + flight + 0.3f;
        }

        Play(sealSound, Pace);
        pose = Pose.Crouch;
        yield return new WaitForSeconds(warning);

        Play(leapSound);
        pose = Pose.Air;
        airborne = true;
        body.simulated = false;
        bodyCollider.enabled = false;
        Vector2 from = transform.position;
        for (float t = 0f; t < flight; t += Time.deltaTime)
        {
            float k = t / flight, e = k * k * (3f - 2f * k);
            ground = Vector2.Lerp(from, target, e);
            transform.position = ground + Vector2.up * (Mathf.Sin(k * Mathf.PI) * leapHeight);
            yield return null;
        }
        transform.position = target;
        ground = target;
        body.position = target;
        body.simulated = true;
        bodyCollider.enabled = true;
        airborne = false;
        if (mark != null && mark.activeInHierarchy) ObjectPool.Recycle(mark);

        FxOneShot.Play(slamPrefab, target, 0f, slamRadius / artRadius);
        Play(slamSound, Random.Range(0.96f, 1.04f));
        Juice.Shake(slamShake);
        if (player != null && ((Vector2)player.position - target).sqrMagnitude <= slamRadius * slamRadius &&
            player.TryGetComponent(out PlayerHealth hp))
            hp.TakeDamage(Damage * slamDamageShare);

        pose = Pose.Crouch;
        Ring(target, ringShots, 0f);
        if (torn)
        {
            yield return new WaitForSeconds(0.25f);
            Ring(target, ringShots, 180f / Mathf.Max(1, ringShots));
        }
        yield return new WaitForSeconds(0.3f);
    }

    private IEnumerator Storm()
    {
        pose = Pose.Cast;
        Play(stormSound);
        yield return new WaitForSeconds(0.45f / Pace);

        var player = PlayerAwareness.Player;
        float angle = player != null ? Angle((Vector2)(player.position - transform.position)) : 0f;
        int arms = stormArms + (torn ? 2 : 0);
        float every = 1f / Mathf.Max(0.1f, stormRate), timer = 0f;
        for (float t = 0f; t < stormSeconds; t += Time.deltaTime)
        {
            angle += stormSpin * Pace * Time.deltaTime;
            timer += Time.deltaTime;
            while (timer >= every)
            {
                timer -= every;
                for (int a = 0; a < arms; a++)
                {
                    Shoot(Chest, angle + a * 360f / arms);
                    if (torn) Shoot(Chest, -angle + (a + 0.5f) * 360f / arms);   // a second set turning back
                }
            }
            yield return null;
        }
    }

    // it casts, a seal lights up under every spot round the player where the dead will come up
    // and fills over the warning, then lightning strikes each and a jiangshi claws its way out of
    // the ground there, harmless until it's up. the spots are fixed when the seals appear, so the
    // player can read them and step out of the ring
    private IEnumerator Raise()
    {
        pose = Pose.Cast;
        var player = PlayerAwareness.Player;
        if (player == null) yield break;
        Play(raiseSound, Pace);
        yield return new WaitForSeconds(0.4f / Pace);
        if (player == null) yield break;

        int n = Mathf.Min(minions + (torn ? 2 : 0), maxMinions - raised.Count);
        float start = Random.value * 360f;
        var spots = new List<Vector2>(n);
        for (int i = 0; i < n; i++)
        {
            float a = (start + i * 360f / Mathf.Max(1, n)) * Mathf.Deg2Rad;
            spots.Add((Vector2)player.position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * raiseRadius);
        }

        float warning = raiseWarning / Pace;
        foreach (var at in spots)
        {
            var mark = FxOneShot.Play(markPrefab, at, 0f, raiseMarkRadius / Mathf.Max(0.01f, artRadius));
            if (mark == null) continue;
            if (mark.TryGetComponent(out Flipbook seal)) seal.PlayOver(warning);
            if (mark.TryGetComponent(out FxOneShot shot)) shot.lifetime = warning + 0.15f;
        }
        Juice.Shake(slamShake * 0.3f);
        yield return new WaitForSeconds(warning);

        foreach (var at in spots)
        {
            FxOneShot.Play(raiseFx, at);
            PlayAt(strikeSound, at, Random.Range(0.9f, 1.1f), 0.7f);
            var go = ObjectPool.For(minionPrefab).Get(at, Quaternion.identity);
            if (go.TryGetComponent(out EnemyHealth e))
            {
                e.SetScaled(Mathf.Max(1f, health.Max * minionHealthShare));
                raised.Add(e);
            }
            StartCoroutine(ClimbOut(go));
            yield return new WaitForSeconds(0.07f);
        }
        yield return new WaitForSeconds(0.2f);
    }

    // a raised jiangshi comes up out of the ground: it grows from a sliver at its feet, overshoots
    // and settles, and only once it's up can it move or hurt anyone
    private IEnumerator ClimbOut(GameObject go)
    {
        if (go == null) yield break;
        var t = go.transform;
        Vector3 rest = t.localScale;
        go.TryGetComponent(out EnemyContactDamage bite);
        go.TryGetComponent(out EnemyMovement move);
        if (bite != null) bite.enabled = false;
        float seconds = Mathf.Max(0.05f, riseSeconds);
        if (move != null) move.ApplySlow(0f, seconds);

        for (float k = 0f; k < 1f && go.activeInHierarchy; k += Time.deltaTime / seconds)
        {
            float up = k < 0.7f ? Mathf.Lerp(0.05f, 1.15f, k / 0.7f) : Mathf.Lerp(1.15f, 1f, (k - 0.7f) / 0.3f);
            float wide = k < 0.7f ? Mathf.Lerp(1.3f, 0.9f, k / 0.7f) : Mathf.Lerp(0.9f, 1f, (k - 0.7f) / 0.3f);
            t.localScale = new Vector3(rest.x * wide, rest.y * up, rest.z);
            yield return null;
        }
        // back to normal even if it was killed on the way up, so the pooled one isn't left harmless
        if (go == null) yield break;
        t.localScale = rest;
        if (bite != null) bite.enabled = true;
    }

    // the seal tears: a shudder and a burst of corpse fire, and from now on it's faster
    private void Tear()
    {
        torn = true;
        Play(tearSound);
        Juice.Shake(slamShake * 0.8f);
        Ring(transform.position, ringShots + 6, 0f);
    }

    // ---- corpse fire

    private Vector2 Chest => (Vector2)transform.position + Vector2.up * 0.3f;

    private void Ring(Vector2 at, int shots, float offset)
    {
        for (int i = 0; i < shots; i++) Shoot(at, offset + i * 360f / shots);
    }

    private void Shoot(Vector2 at, float degrees)
    {
        float r = degrees * Mathf.Deg2Rad;
        EnemyBullet.Fire(bulletPrefab, at, new Vector2(Mathf.Cos(r), Mathf.Sin(r)) * bulletSpeed, Damage * bulletDamageShare);
    }

    private static float Angle(Vector2 dir) => Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

    // ---- sound

    private void MakeVoices()
    {
        voices = new AudioSource[6];
        for (int i = 0; i < voices.Length; i++) voices[i] = Voice(gameObject.AddComponent<AudioSource>());

        groundVoices = new AudioSource[8];
        for (int i = 0; i < groundVoices.Length; i++)
            groundVoices[i] = Voice(new GameObject("Magistrate Strike Voice").AddComponent<AudioSource>());
    }

    private static AudioSource Voice(AudioSource v)
    {
        v.playOnAwake = false;
        v.spatialBlend = 1f;    // like SfxPlayer's voices, so it sits in the mix with the rest
        return v;
    }

    private static void StopVoices(AudioSource[] set)
    {
        if (set == null) return;
        foreach (var v in set) if (v != null) v.Stop();
    }

    // while the game is stopped (a level up, the pause menu, an envelope opening) its sounds are
    // held where they are and carry on when it starts again, so they don't play over the menu
    private void LateUpdateSound()
    {
        bool stopped = Time.timeScale <= 0f;
        if (stopped == soundHeld) return;
        soundHeld = stopped;
        Hold(voices, stopped);
        Hold(groundVoices, stopped);
    }

    private static void Hold(AudioSource[] set, bool hold)
    {
        if (set == null) return;
        foreach (var v in set)
        {
            if (v == null) continue;
            if (hold) v.Pause();
            else v.UnPause();
        }
    }

    private void PlayAt(AudioClip clip, Vector2 at, float pitch = 1f, float volume = 1f)
    {
        if (clip == null || groundVoices == null) return;
        var v = groundVoices[nextGroundVoice];
        nextGroundVoice = (nextGroundVoice + 1) % groundVoices.Length;
        v.transform.position = at;
        StartVoice(v, clip, pitch, volume);
    }

    // each on the next voice round, so a pitched one never bends another that's still ringing
    private void Play(AudioClip clip, float pitch = 1f, float volume = 1f)
    {
        if (clip == null || voices == null) return;
        var v = voices[nextVoice];
        nextVoice = (nextVoice + 1) % voices.Length;
        StartVoice(v, clip, pitch, volume);
    }

    private void StartVoice(AudioSource v, AudioClip clip, float pitch, float volume)
    {
        v.Stop();
        v.clip = clip;
        v.pitch = pitch;
        v.volume = Mathf.Clamp01(soundVolume * volume);
        v.Play();
    }

    // ---- a health bar over its hat, in the world's pixels

    private void MakeBar()
    {
        if (pixel == null)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.DontSave };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            pixel = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            pixel.hideFlags = HideFlags.DontSave;
        }
        Bar("Bar Frame", new Color32(0x17, 0x11, 0x1d, 0xff), barWidth + 2f * PixelSize, 5f * PixelSize, 40);
        Bar("Bar Back", new Color32(0x3e, 0x08, 0x12, 0xff), barWidth, 3f * PixelSize, 41);
        barFill = Bar("Bar Fill", new Color32(0xd0, 0x28, 0x38, 0xff), barWidth, 3f * PixelSize, 42);
    }

    private SpriteRenderer Bar(string name, Color color, float width, float height, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, barHeight, 0f);
        go.transform.localScale = new Vector3(width, height, 1f);
        var s = go.AddComponent<SpriteRenderer>();
        s.sprite = pixel;
        s.color = color;
        s.sortingLayerName = "Default";
        s.sortingOrder = order;
        return s;
    }
}
