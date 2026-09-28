using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif

// something on an enemy that decides how much of a hit lands, e.g. a boss whose phases each have
// their own bar (YamaBoss): 0 turns the hit aside entirely
public interface IDamageGate
{
    float Admit(float damage, DamageKind kind, float current);
}

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class EnemyHealth : MonoBehaviour, IHealth
{
    [Header("health")]
    [Min(1f)] public float baseMaxHealth = 10f;
    [SerializeField] private float currentHealth;

    [Tooltip("flat damage reduced from each hit")]
    [Min(0f)] public float armor = 0f;
    [Tooltip("share of every hit its armour turns aside: 0.3 = 30% less damage. the spawner sets it from the enemy's archetype; Armour Piercing cuts through it")]
    [Range(0f, 0.9f)] public float armour = 0f;

    // what the two attack classes do to it, from its archetype: the dead fear talismans, blades
    // pass through ghost fire, a fox spirit shrugs off spells
    [NonSerialized] public float physicalTaken = 1f, magicalTaken = 1f;

    // how much of an enemy's armour (both kinds) the player's hits ignore: the Armour Piercing passive
    public static float ArmourPierce;

    [Header("death & drops")]
    [SerializeField, FormerlySerializedAs("gearDropPrefab")] private GameObject wenDropPrefab;
    [SerializeField, Min(0), FormerlySerializedAs("minGear")] private int minWen = 1;
    [SerializeField, Min(0), FormerlySerializedAs("maxGear")] private int maxWen = 1;
    [SerializeField, Min(0), FormerlySerializedAs("baseGearsOnKill")] private int baseWenOnKill = 1;
    [Range(0f, 1f)][SerializeField, FormerlySerializedAs("gearDropChance")] private float wenDropChance = 0.35f;
    [SerializeField] private GameObject deathVfxPrefab;
    [Tooltip("optional. spawned instead of the death vfx while Show Blood is off, e.g. a dust puff. empty spawns nothing")]
    [SerializeField] private GameObject bloodlessDeathVfxPrefab;
    [SerializeField] private AudioClip deathSound;
    [Range(0f, 1f)][SerializeField] private float deathVolume = 1f;

    [Header("heal item drop")]
    [SerializeField] private GameObject healItemPrefab;
    [Range(0f, 1f)][SerializeField] private float healDropChance = 0.01f;

    // no more than this many peaches drop in a run, elites' included, so healing stays scarce
    public const int MaxPeachesPerRun = 5;
    public static int PeachesDropped { get; private set; }

    [Header("damage feedback")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color hitColor = Color.red;
    [SerializeField] private float flashDuration = 0.1f;
    [SerializeField] private AudioClip hitSound;
    [Range(0f, 1f)][SerializeField] private float hitVolume = 1f;
    [SerializeField] private AudioSource audioSource;

    [Header("debug")]
    public bool destroyOnDeath = true;

    public static Action OnEnemyDied;

    public float Max => baseMaxHealth;
    public GameObject WenDropPrefab => wenDropPrefab;

    // how much more its wen is worth than its kind's usual: a step for every time the run has
    // doubled its health, like Vampire Survivors' stronger enemies dropping green and red gems
    // the health an evolution added (SpawnDirector) doesn't count: that's the horde pushing back,
    // not richer enemies
    private int Worth => Mathf.Clamp(1 + Mathf.FloorToInt(Mathf.Log(Mathf.Max(1f, baseMaxHealth / evoHealth / Mathf.Max(1f, _prefabMaxHealth)), 2f)), 1, 6);

    // wen it's worth rounded up or down at random, so a share like 0.4 still averages out
    private int Scaled(int wen)
    {
        if (wenShare >= 1f) return wen;
        float f = wen * wenShare;
        int whole = Mathf.FloorToInt(f);
        return whole + (UnityEngine.Random.value < f - whole ? 1 : 0);
    }

    // what one of its wen drops is worth: the wen prefab's own worth
    private int WenEach
    {
        get
        {
            if (_wenEach < 0) _wenEach = wenDropPrefab != null && wenDropPrefab.TryGetComponent(out Pickup p) ? Mathf.Max(1, p.Wen) : 1;
            return _wenEach;
        }
    }
    public GameObject HealItemPrefab => healItemPrefab;
    public float Current => currentHealth;
    public event Action<float, float> OnHealthChanged;

    private bool _dead;
    private IDamageGate _gate;
    private Color _originalColor;
    private Coroutine _flashRoutine;
    private Vector3 _restScale;
    private Material _restMaterial;
    private static Material _flashMaterial;
    private float _nextFlash;

    // the flash: most of the way to white, for a moment, and not again straight away, so a crowd
    // under constant fire still reads as enemies and not as white shapes
    private static readonly Color FlashColor = new Color(1f, 1f, 1f, 0.85f);
    private const float FlashSeconds = 0.07f, FlashRest = 0.22f;
    private static readonly Color CritYellow = new Color(1f, 0.88f, 0.23f, 0.95f);
    private static readonly Color CritRed = new Color(1f, 0.16f, 0.12f, 0.95f);
    private int _wenEach = -1;
    private float _prefabMaxHealth = -1f;

    private static PlayerInventory _cachedInventory;

    // statics survive play sessions when domain reload is off
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _cachedInventory = null;
        ArmourPierce = 0f;
        PeachesDropped = 0;
    }

    // every run is a scene load: the peach count starts over with it
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void HookRunStart()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (mode == UnityEngine.SceneManagement.LoadSceneMode.Single) PeachesDropped = 0;
    }

    // this used to run a scene wide tag search on every single enemy death
    private static PlayerInventory GetPlayerInventory()
    {
        if (_cachedInventory != null) return _cachedInventory;

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) player.TryGetComponent(out _cachedInventory);
        return _cachedInventory;
    }

    // what a pooled enemy goes back to when it's reused
    private Color _prefabColor;
    private Vector3 _prefabScale;
    private WaitForSeconds _flashWait;

    private void Awake()
    {
        if (!CompareTag("Enemy")) gameObject.tag = "Enemy";
        if (_prefabMaxHealth < 0f) _prefabMaxHealth = Mathf.Max(1f, baseMaxHealth);
        ResetHealth();

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (spriteRenderer != null)
        {
            _originalColor = spriteRenderer.color;
            _restMaterial = spriteRenderer.sharedMaterial;
        }
        _prefabColor = _originalColor;
        _prefabScale = transform.localScale;
        _flashWait = new WaitForSeconds(Mathf.Min(flashDuration, FlashSeconds));

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        TryGetComponent(out _gate);
    }

    private void OnEnable()
    {
        // back from the pool: forget the last life (an elite's gold, size and bonus drops)
        _dead = false;
        _silentDeath = false;
        physicalTaken = magicalTaken = 1f;
        armour = 0f;
        currentHealth = Mathf.Max(1f, baseMaxHealth);
        bonusWenDrops = 0;
        alwaysDropHeal = false;
        evoHealth = wenShare = 1f;
        transform.localScale = _prefabScale;
        _originalColor = _prefabColor;
        if (spriteRenderer != null)
        {
            spriteRenderer.color = _prefabColor;
            if (_restMaterial != null) spriteRenderer.sharedMaterial = _restMaterial;
        }

        EnemyRegistry.Register(gameObject);
        OnHealthChanged?.Invoke(currentHealth, baseMaxHealth);
    }

    private void OnDisable()
    {
        EnemyRegistry.Unregister(gameObject);
        _flashRoutine = null;   // a coroutine doesn't survive the object switching off
    }

    public void ResetHealth()
    {
        _dead = false;
        currentHealth = Mathf.Max(1f, baseMaxHealth);
        OnHealthChanged?.Invoke(currentHealth, baseMaxHealth);
    }

    // set by the spawner on elites: wen that always drops, on top of the usual roll, and a heal
    [NonSerialized] public int bonusWenDrops;
    [NonSerialized] public bool alwaysDropHeal;
    // set by the spawner while the player holds evolutions: the health the horde gained from them,
    // and the share of its usual wen (and so coins) it's worth
    [NonSerialized] public float evoHealth = 1f;
    [NonSerialized] public float wenShare = 1f;

    // a lasting tint (an elite's gold). the hit flash comes back to this instead of to white
    public void SetTint(Color tint)
    {
        _originalColor = tint;
        if (spriteRenderer != null) spriteRenderer.color = tint;
    }

    public void SetScaled(float absoluteMaxHealth)
    {
        baseMaxHealth = Mathf.Max(1f, absoluteMaxHealth);
        currentHealth = baseMaxHealth;
        OnHealthChanged?.Invoke(currentHealth, baseMaxHealth);
    }

    // every source of enemy damage comes through here, so this is where numbers are drawn. it
    // shows what actually landed after armor, and any new damage source gets numbers for free.
    // ignoreArmor is for damage over time: small ticks would otherwise never get through armor.
    // source is the player's weapon it came from, for the run's damage stats (RunStats). damage
    // with none, like the dev tools' kills or a purge, isn't the player's and isn't counted
    public bool TakeDamage(float rawDamage, DamageKind kind = DamageKind.Normal, bool crit = false, bool ignoreArmor = false, Weapon source = null)
    {
        if (_dead) return false;

        // armour: a flat amount off each hit (not for damage over time) and a share of every hit,
        // both cut by Armour Piercing. a hit that armour blunted shows its number in steel
        float pierce = kind == DamageKind.Silent ? 1f : 1f - Mathf.Clamp01(ArmourPierce);
        float dmg = ignoreArmor ? rawDamage : Mathf.Max(0f, rawDamage - armor * pierce);
        dmg *= 1f - armour * pierce;
        if (source != null) dmg *= source.AttackClass == AttackClass.Magical ? magicalTaken : physicalTaken;
        if (_gate != null) dmg = _gate.Admit(dmg, kind, currentHealth);
        if (dmg <= 0f) return false;
        _silentDeath = kind == DamageKind.Silent;

        Juice.Number(transform.position, dmg, kind, crit, armour * pierce >= 0.15f);

        currentHealth -= dmg;
        if (source != null) RunStats.Dealt(source, dmg, crit, currentHealth <= 0f, this);

        // the killing blow skips the flash: the enemy is gone this frame anyway
        if (spriteRenderer != null && currentHealth > 0f)
        {
            // a crit always flashes, yellow then red, cutting in over an ordinary flash
            if (crit && _flashRoutine != null)
            {
                StopCoroutine(_flashRoutine);
                EndFlash();
            }
            if (_flashRoutine == null && (crit || Time.time >= _nextFlash))
            {
                _restScale = transform.localScale;   // not mid squash: this is its real size
                _nextFlash = Time.time + FlashRest;
                _flashRoutine = StartCoroutine(crit ? FlashCrit() : FlashRed());
            }
        }

        // the killing blow is the death sound's alone: both at once smeared into one mushy thud.
        // a little pitch to every hit and death, so a crowd doesn't sound like one sound on repeat
        if (hitSound != null && currentHealth > 0f)
            SfxPlayer.PlayAt(hitSound, transform.position, hitVolume, UnityEngine.Random.Range(0.94f, 1.06f));

        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            OnHealthChanged?.Invoke(currentHealth, baseMaxHealth);
            Die();
            return true;
        }

        OnHealthChanged?.Invoke(currentHealth, baseMaxHealth);
        return false;
    }

    // the hit flash: the enemy turns white for a moment, and squashes, wider and shorter, so a hit
    // lands. white is the sprite drawn flat (Rogue/Silhouette); without that shader it tints instead
    private IEnumerator FlashRed()
    {
        if (_flashMaterial == null)
        {
            var shader = Shader.Find("Rogue/Silhouette");
            if (shader != null) _flashMaterial = new Material(shader) { name = "Hit Flash" };
        }
        if (_flashMaterial != null)
        {
            spriteRenderer.sharedMaterial = _flashMaterial;
            spriteRenderer.color = FlashColor;
        }
        else spriteRenderer.color = hitColor;
        transform.localScale = Vector3.Scale(_restScale, new Vector3(1.16f, 0.86f, 1f));
        yield return _flashWait;
        EndFlash();
    }

    // a crit: the silhouette flashes yellow, then red, a beat each
    private IEnumerator FlashCrit()
    {
        UseFlashMaterial();
        transform.localScale = Vector3.Scale(_restScale, new Vector3(1.22f, 0.8f, 1f));
        spriteRenderer.color = CritYellow;
        yield return _flashWait;
        spriteRenderer.color = CritRed;
        yield return _flashWait;
        EndFlash();
    }

    private void UseFlashMaterial()
    {
        if (_flashMaterial == null)
        {
            var shader = Shader.Find("Rogue/Silhouette");
            if (shader != null) _flashMaterial = new Material(shader) { name = "Hit Flash" };
        }
        if (_flashMaterial != null) spriteRenderer.sharedMaterial = _flashMaterial;
    }

    private void EndFlash()
    {
        if (_restMaterial != null) spriteRenderer.sharedMaterial = _restMaterial;
        spriteRenderer.color = _originalColor;
        transform.localScale = _restScale;
        _flashRoutine = null;
    }

    // killed by a purge (a wave's end, the time running out) rather than by the player
    private bool _silentDeath;

    private void Die()
    {
        if (_dead) return;
        _dead = true;

        // a fortune envelope it carried falls out of it, if the player brought it down
        if (TryGetComponent(out EnvelopeCarrier carrier) && !_silentDeath) carrier.Drop();

        try { OnEnemyDied?.Invoke(); } catch { }
        try { GameEvents.OnEnemyKilled?.Invoke(1); } catch { }

        // the death: a batched pixel animation, one mesh for every enemy dying at once. the Show
        // Blood option swaps the blood for motes of qi. the old particle burst is the fallback
        var library = VfxLibrary.Get;
        var dying = library != null ? (GameSettings.ShowBlood ? library.enemyDeath : library.enemyDeathBloodless) : null;
        if (dying != null && dying.Length > 0)
            FxBatch.Play(dying, library.deathFps, transform.position, TryGetComponent(out EliteOutline _) ? 1.6f : TryGetComponent(out BossMarker _) ? 2.2f : 1f);
        else
        {
            var deathVfx = GameSettings.ShowBlood ? deathVfxPrefab : bloodlessDeathVfxPrefab;
            if (deathVfx != null)
                BurstFx.Play(deathVfx, transform.position);     // one shared emitter, not an object per death
        }

        if (deathSound != null)
            SfxPlayer.PlayAt(deathSound, transform.position, deathVolume, UnityEngine.Random.Range(0.9f, 1.1f));

        if (baseWenOnKill > 0)
        {
            var inv = GetPlayerInventory();
            int wen = Scaled(baseWenOnKill);
            if (inv != null && wen > 0) inv.AddWen(wen);
        }

        if (wenDropPrefab != null &&
            (minWen > 0 || maxWen > 0) &&
            UnityEngine.Random.value <= wenDropChance)
        {
            int count = Mathf.Clamp(UnityEngine.Random.Range(minWen, maxWen + 1), 0, 999);
            // one piece of wen showing what it's worth, the way Vampire Survivors drops one gem
            var p = transform.position;
            p.x += UnityEngine.Random.Range(-0.2f, 0.2f);
            p.y += UnityEngine.Random.Range(-0.2f, 0.2f);
            int worth = Scaled(count * WenEach * Worth);
            if (worth > 0) PickupSystem.DropWen(p, worth, wenDropPrefab);
        }

        // an elite bursts into a ring of jade
        if (wenDropPrefab != null && bonusWenDrops > 0)
        {
            int ring = Mathf.Max(1, bonusWenDrops / 2);
            for (int i = 0; i < ring; i++)
            {
                float a = (i + UnityEngine.Random.value * 0.5f) / ring * Mathf.PI * 2f;
                float r = UnityEngine.Random.Range(0.4f, 1.1f);
                var p = transform.position + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
                int worth = Scaled(WenEach * 2 * Worth);
                if (worth > 0) PickupSystem.DropWen(p, worth, wenDropPrefab);
            }
        }

        if (healItemPrefab != null && PeachesDropped < MaxPeachesPerRun &&
            (alwaysDropHeal || UnityEngine.Random.value <= healDropChance))
        {
            PeachesDropped++;
            Vector3 dropPos = transform.position;
            dropPos.x += UnityEngine.Random.Range(-0.2f, 0.2f);
            dropPos.y += UnityEngine.Random.Range(-0.2f, 0.2f);
            ObjectPool.For(healItemPrefab).Get(dropPos, Quaternion.identity);
        }

        // back to the spawner's pool for the next one; enemies that weren't pooled are destroyed
        if (destroyOnDeath)
            ObjectPool.Recycle(gameObject);
        else
            gameObject.SetActive(false);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (maxWen < minWen) maxWen = minWen;
        baseMaxHealth = Mathf.Max(1f, baseMaxHealth);
        baseWenOnKill = Mathf.Max(0, baseWenOnKill);
        if (!Application.isPlaying)
            currentHealth = Mathf.Clamp(currentHealth, 0f, baseMaxHealth);
    }
#endif
}
