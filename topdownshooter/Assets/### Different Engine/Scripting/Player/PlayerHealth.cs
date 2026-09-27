using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[DisallowMultipleComponent]
public class PlayerHealth : MonoBehaviour, IHealth
{
    [Header("Health")]
    [Tooltip("replaced at spawn by Max Health from the StatCatalog, plus character and global upgrade bonuses")]
    [Min(1f)] public float maxHealth = 100f;
    [SerializeField] private float currentHealth;

    [Tooltip("Seconds of invulnerability after being hit")]
    [Min(0f)] public float invulnTimeOnHit = 0.15f;

    // Recovery, Armor and Revival from the character's stat sheet, set by StatContext at spawn
    [NonSerialized] public float regenPerSecond;
    [NonSerialized] public float armor;
    [NonSerialized] public int revivals;

    [Header("Revival")]
    [Tooltip("share of max health a revival brings you back with")]
    [Range(0.05f, 1f)] public float reviveHealthFraction = 0.5f;
    [Tooltip("seconds of invulnerability after getting back up")]
    [Min(0f)] public float reviveInvulnTime = 2f;

    [Header("Death")]
    [Tooltip("seconds the player lies there, white and bleeding, before the game over screen")]
    [Min(0f)] public float deathSeconds = 3f;

    [Header("UI (optional)")]
    [Tooltip("Fill image for a HUD bar; left->right fill")]
    public Image healthBarFill;

    [Tooltip("Color over health percent (0=red, 0.5=yellow, 1=green)")]
    public Gradient hudGradient;

    [Header("Visual Feedback")]
    public SpriteRenderer spriteRenderer;
    public Color hitColor = Color.red;
    public Color healColor = Color.green;
    [Min(0f)] public float hitFlashDuration = 0.1f;
    [Min(0f)] public float healFlashDuration = 0.15f;

    [Header("Audio (optional)")]
    public AudioClip hitSound;
    [Range(0f, 1f)] public float hitSoundVolume = 0.7f;
    public AudioClip healSound;
    [Range(0f, 1f)] public float healSoundVolume = 0.8f;

    // ----- IHealth -----
    public float Max => maxHealth;
    public float Current => currentHealth;
    public event Action<float, float> OnHealthChanged;

    // Events
    public static Action OnPlayerDied;
    public static Action<float, float> OnPlayerHealed;   // (amount, newHealth)
    public static Action<float, float> OnPlayerDamaged;  // (amount, newHealth)
    public static Action OnPlayerRevived;

    // internals
    private AudioSource _audio;
    private Color _originalColor = Color.white;
    // the flash: a hit draws the sprite flat white (the enemies' hit flash, all the way), a heal
    // tints it. timed on real time, so a flash never hangs while a menu has the game stopped
    private Material _restMaterial;
    private static Material _whiteMaterial;
    private float _flashUntil = -1f;
    private bool _dead;

    // down for good: no more hits, heals or regeneration
    public bool IsDead => _dead;

    // where blood comes from: the feet are this many of the character art's pixels under the
    // player's position, however big the art is drawn
    private const int FeetPixels = 22;
    private float ArtPixel => spriteRenderer != null && spriteRenderer.sprite != null ? 1f / spriteRenderer.sprite.pixelsPerUnit : 1f / 33.88956f;
    private Vector2 FeetOffset => new Vector2(0f, -FeetPixels * ArtPixel * Mathf.Abs(transform.lossyScale.y));
    private Vector2 Feet => (Vector2)transform.position + FeetOffset;
    private float _invulnTimer;

    private void Reset()
    {
        hudGradient = new Gradient
        {
            colorKeys = new[]
            {
                new GradientColorKey(new Color(0.90f, 0.15f, 0.15f), 0f), // red
                new GradientColorKey(new Color(0.95f, 0.85f, 0.20f), 0.5f), // yellow
                new GradientColorKey(new Color(0.18f, 0.78f, 0.20f), 1f), // green
            },
            alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
        };
    }

    private void Awake()
    {
        currentHealth = Mathf.Max(1f, maxHealth);

        if (spriteRenderer != null)
        {
            _originalColor = spriteRenderer.color;
            _restMaterial = spriteRenderer.sharedMaterial;
        }

        _audio = GetComponent<AudioSource>();
        if (_audio == null) _audio = gameObject.AddComponent<AudioSource>();

        PushHealthChanged();
        UpdateHudBar();
    }

    private void OnEnable()
    {
        PushHealthChanged();
        UpdateHudBar();
        _invulnTimer = 0f;
    }

    private void Update()
    {
        if (_flashUntil >= 0f && Time.unscaledTime >= _flashUntil && !_dead) EndFlash();
        if (_dead) return;
        if (_invulnTimer > 0f) _invulnTimer -= Time.deltaTime;
        Blink();
        Regenerate(Time.deltaTime);
    }

    // a grace period, e.g. after the level up menu closes: nothing hurts for a moment, and the
    // player blinks so it's clear why. a longer one already running is kept
    public void GrantInvulnerability(float seconds)
    {
        if (_dead || seconds <= 0f) return;
        _invulnTimer = Mathf.Max(_invulnTimer, seconds);
        _blinking = true;
    }

    public bool IsInvulnerable => _invulnTimer > 0f;

    private bool _blinking;
    private const float BlinkHz = 10f;

    private void Blink()
    {
        if (!_blinking || spriteRenderer == null) return;

        var c = spriteRenderer.color;
        if (_invulnTimer <= 0f)
        {
            _blinking = false;
            c.a = _originalColor.a;
        }
        else c.a = (int)(Time.time * BlinkHz) % 2 == 0 ? _originalColor.a : _originalColor.a * 0.35f;
        spriteRenderer.color = c;
    }

    // Recovery stays quiet on purpose: Heal flashes and plays a sound, which every frame would be noise
    private void Regenerate(float dt)
    {
        if (regenPerSecond <= 0f || currentHealth <= 0f || currentHealth >= maxHealth) return;

        float before = currentHealth;
        currentHealth = Mathf.Min(maxHealth, currentHealth + regenPerSecond * dt);
        RunStats.Regen(currentHealth - before);
        PushHealthChanged();
        UpdateHudBar();
    }


    public void Heal(float amount) => Heal(amount, true);

    // counts: whether it goes in the run's Healed stat. raising max health tops health up by what
    // was added, which isn't healing
    public void Heal(float amount, bool counts)
    {
        if (amount <= 0f || _dead) return;
        float prev = currentHealth;

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);

        if (!Mathf.Approximately(prev, currentHealth))
        {
            OnPlayerHealed?.Invoke(currentHealth - prev, currentHealth);
            if (counts) RunStats.WasHealed(currentHealth - prev);

            // 🔊 Sound beim Heilen
            if (healSound != null)
                _audio.PlayOneShot(healSound, healSoundVolume);

            // 💚 Visuelles Feedback
            Flash(healColor, healFlashDuration, false);

            PushHealthChanged();
            UpdateHudBar();
        }
    }


    public void StartHealFlash()
    {
        if (_dead) return;
        Flash(healColor, healFlashDuration, false);
    }


    public void SetMax(float newMax, bool fillToMax = false)
    {
        maxHealth = Mathf.Max(1f, newMax);
        if (fillToMax) currentHealth = maxHealth;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        PushHealthChanged();
        UpdateHudBar();
    }


    // set by the dev tools: nothing hurts
    [NonSerialized] public bool godMode;

    public bool TakeDamage(float amount)
    {
        if (amount <= 0f || godMode || _dead) return false;
        if (_invulnTimer > 0f) return false;

        // armor never turns a hit into nothing: whatever gets through is at least 1
        float incoming = amount;
        if (armor > 0f) amount = Mathf.Max(Mathf.Min(amount, 1f), amount - armor);

        float prev = currentHealth;
        currentHealth -= amount;
        _invulnTimer = invulnTimeOnHit;


        Flash(Color.white, hitFlashDuration, true);

        // blood out of the hit, spurting on for a moment; every hit after keeps it going
        BloodSpray.Burst(Feet, 10);
        BloodSpray.Bleed(transform, FeetOffset, 0.6f);

        // Sound
        if (hitSound != null)
            _audio.PlayOneShot(hitSound, hitSoundVolume);

        OnPlayerDamaged?.Invoke(prev - Mathf.Max(currentHealth, 0f), Mathf.Max(currentHealth, 0f));
        RunStats.Hurt(prev - Mathf.Max(currentHealth, 0f), incoming - amount, Mathf.Max(currentHealth, 0f) / Mathf.Max(1f, maxHealth));
        PushHealthChanged();
        UpdateHudBar();

        if (currentHealth <= 0f)
        {
            if (revivals > 0)
            {
                Revive();
                return false;
            }

            currentHealth = 0f;
            OnPlayerDied?.Invoke();

            StartCoroutine(Die());
            return true;
        }

        return false;
    }

    // a Revival from the stat sheet: back up at part health with a moment to get clear
    private void Revive()
    {
        revivals--;
        RunStats.Revived();
        currentHealth = Mathf.Max(1f, maxHealth * reviveHealthFraction);
        _invulnTimer = reviveInvulnTime;

        StartHealFlash();
        Juice.Text(transform.position + Vector3.up, "REVIVED", healColor);
        OnPlayerRevived?.Invoke();

        PushHealthChanged();
        UpdateHudBar();
    }

    // down: the body goes white and squashes flat into the ground, bleeding, nothing moves or
    // fires any more, and the game over screen comes a few seconds later while the world goes on
    private IEnumerator Die()
    {
        _dead = true;
        RunStats.EndRun();
        Freeze();

        Vector2 feet = Feet;
        BloodSpray.Burst(feet, 36, 1.3f);
        BloodSpray.Bleed(feet, deathSeconds, 1.4f);
        Flash(Color.white, float.MaxValue, true);

        var body = transform;
        Vector3 rest = body.localScale, at = body.position;
        float feetDown = FeetPixels * ArtPixel * Mathf.Abs(rest.y);
        var tall = new Vector2(0.82f, 1.22f);
        var flat = new Vector2(1.55f, 0.28f);
        const float pop = 0.07f, squash = 0.22f;
        for (float t = 0f; t < pop + squash; t += Time.deltaTime)
        {
            Vector2 s = t < pop
                ? Vector2.Lerp(Vector2.one, tall, t / pop)
                : Vector2.Lerp(tall, flat, 1f - Mathf.Pow(1f - (t - pop) / squash, 3f));
            Squash(body, rest, at, feetDown, s);
            yield return null;
        }
        Squash(body, rest, at, feetDown, flat);

        yield return new WaitForSeconds(Mathf.Max(0f, deathSeconds - pop - squash));
        GameOver();
    }

    // the feet stay on the ground: the middle comes down as the body flattens
    private static void Squash(Transform body, Vector3 rest, Vector3 at, float feetDown, Vector2 s)
    {
        body.localScale = new Vector3(rest.x * s.x, rest.y * s.y, rest.z);
        body.position = at - new Vector3(0f, feetDown * (1f - s.y), 0f);
    }

    // nothing moves, fires or picks anything up any more
    private void Freeze()
    {
        if (TryGetComponent(out PlayerMovement move)) move.enabled = false;
        if (TryGetComponent(out Rigidbody2D rb))
        {
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;
        }
        if (TryGetComponent(out Animator animator)) animator.enabled = false;
        if (TryGetComponent(out AutoShooter shooter))
        {
            shooter.Armed = false;
            shooter.enabled = false;
        }
        foreach (var weapon in GetComponents<Weapon>()) weapon.enabled = false;
        foreach (var meteors in GetComponentsInChildren<AOEAttack>(true)) meteors.enabled = false;
        foreach (var aura in GetComponentsInChildren<Aura>(true))
            if (aura.gameObject != gameObject) aura.gameObject.SetActive(false);
        foreach (var magnet in GetComponentsInChildren<MagnetArea>(true))
            if (magnet.gameObject != gameObject) magnet.gameObject.SetActive(false);
        var bar = transform.Find("HealthBar");
        if (bar != null) bar.gameObject.SetActive(false);
    }

    private void Flash(Color color, float seconds, bool white)
    {
        if (spriteRenderer == null) return;
        var flat = white ? WhiteMaterial : null;
        if (flat != null)
        {
            spriteRenderer.sharedMaterial = flat;
            spriteRenderer.color = color;       // the colour, all the way
        }
        else
        {
            if (_restMaterial != null) spriteRenderer.sharedMaterial = _restMaterial;
            spriteRenderer.color = white ? hitColor : color;
        }
        _flashUntil = seconds >= float.MaxValue ? float.MaxValue : Time.unscaledTime + seconds;
    }

    private void EndFlash()
    {
        _flashUntil = -1f;
        if (spriteRenderer == null) return;
        if (_restMaterial != null) spriteRenderer.sharedMaterial = _restMaterial;
        spriteRenderer.color = _originalColor;
    }

    private static Material WhiteMaterial
    {
        get
        {
            if (_whiteMaterial == null)
            {
                var shader = Shader.Find("Rogue/Silhouette");
                if (shader != null) _whiteMaterial = new Material(shader) { name = "Player Hit Flash" };
            }
            return _whiteMaterial;
        }
    }

    private void UpdateHudBar()
    {
        if (healthBarFill == null) return;

        float t = Mathf.Clamp01(currentHealth / Mathf.Max(1f, maxHealth));
        healthBarFill.fillAmount = t;

        if (hudGradient.colorKeys != null && hudGradient.colorKeys.Length > 0)
            healthBarFill.color = hudGradient.Evaluate(t);
        else
            healthBarFill.color = Color.Lerp(Color.red, Color.green, t);
    }

    private void PushHealthChanged() => OnHealthChanged?.Invoke(currentHealth, maxHealth);

    public GameOverScreen GameOverScreen;


    private void GameOver()
    {
        if (GameOverScreen == null) GameOverScreen = FindFirstObjectByType<GameOverScreen>(FindObjectsInactive.Include);
        if (GameOverScreen != null) GameOverScreen.ShowDefeat();
    }
}