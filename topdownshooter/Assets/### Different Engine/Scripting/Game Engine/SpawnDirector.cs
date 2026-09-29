using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SpawnDirector : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;
    [SerializeField] private BoxCollider2D[] mapBounds = new BoxCollider2D[4];
    [SerializeField] private DifficultyCurve curve;
    [SerializeField] private List<EnemyArchetype> pool;
    [SerializeField] private float desiredPerScreen = 12f;
    [SerializeField] private float budgetPerSecond = 10f;
    [SerializeField] private List<EnemyArchetype> chunkyPool;
    [SerializeField] private float finalRushHealthMul = 2f;
    [SerializeField] private float spawnrateIncreasePerWave = 2f;
    [SerializeField] private float offscreenBand = 1.0f;
    [SerializeField] private float boundsInset = 0.25f;
    [SerializeField] private float pauseAfterClear = 2f;
    [SerializeField] private float purgeStepDelay = 0.02f;
    [SerializeField] private float startingBudget = 3f;

    [Header("Fast Phase")]
    [SerializeField] private EnemyArchetype fastPhaseArchetype;
    [SerializeField] private float fastPhaseDuration = 10f;
    [SerializeField] private float fastPhaseCheckInterval = 10f;
    [SerializeField, Range(0f, 1f)] private float fastPhaseChance = 0.01f;
    [SerializeField] private float fastPhaseSpawnrateMul = 3f;
    [SerializeField] private float fastPhaseDensityMul = 3f;
    [SerializeField] private float fastPhaseMinRunTime = 120f;
    [SerializeField] private float fastPhasePostClearDelay = 1f;

    [Header("Boss")]
    [SerializeField] private EnemyArchetype bossArchetype;

    [Header("Final Boss (normal mode)")]
    [Tooltip("defaults to the regular boss archetype until it gets its own art")]
    [SerializeField] private EnemyArchetype finalBossArchetype;
    [SerializeField, Min(1f)] private float finalBossHealthMul = 12f;
    [SerializeField, Min(0.1f)] private float finalBossScale = 1.8f;

    [Header("Secret Boss Easter Egg")]
    [SerializeField] private EnemyArchetype secretBossArchetype;
    [SerializeField] private float secretBossMinRunTime = 90f;
    [SerializeField, Range(0f, 1f)] private float secretBossChancePerCheck = 0.0005f;
    [SerializeField] private float secretBossCheckInterval = 30f;

    // NEW: scene reference to the hallucination ui (drag in inspector)
    [SerializeField] private SecretBossHallucinationUI secretBossUi;

    [Header("Final Rush UI")]
    [SerializeField] private Slider finalRushSlider;

    [Header("Spawn Caps Scaling")]
    [SerializeField] private int baseMaxSpawnsPerFrame = 3;
    [SerializeField] private int extraSpawnsPerFrameAtMax = 5;
    [SerializeField] private float spawnCapRampDuration = 600f; // seconds until fully ramped

    [SerializeField] private float minSpawnCooldownEarly = 0.05f;
    [SerializeField] private float minSpawnCooldownLate = 0.01f;
    [SerializeField] private float maxSpawnCooldownEarly = 0.15f;
    [SerializeField] private float maxSpawnCooldownLate = 0.05f;

    [Header("Timeline (Vampire Survivors style)")]
    [Tooltip("who comes when on a map that has no timeline of its own (every map's Playfield brings its own, and it wins). " +
             "with neither, the budget spawner above runs as before. the Final Rush, bosses and the secret boss work the same either way")]
    [SerializeField] private SpawnTimeline timeline;
    [Tooltip("share of spawns placed on the side the player is walking toward, so running away runs into the horde")]
    [SerializeField, Range(0f, 1f)] private float aheadBias = 0.25f;

    // the horde comes on a front, not from everywhere at once: most of it arrives from one side,
    // the front drifting round slowly and every so often swinging to another side of the screen,
    // so there's a direction to face and room to move into. the rest still comes from anywhere
    [Header("Fronts (where the horde comes from)")]
    [Tooltip("share of the horde that comes on the front (the rest from anywhere, or ahead of the player)")]
    [SerializeField, Range(0f, 1f)] private float frontShare = 0.75f;
    [Tooltip("degrees either side of the front it comes from")]
    [SerializeField, Range(10f, 180f)] private float frontSpread = 55f;
    [Tooltip("degrees a second the front drifts round")]
    [SerializeField] private float frontDrift = 6f;
    [Tooltip("seconds between the front swinging to another side, a random time in this range")]
    [SerializeField] private Vector2 frontSwingEvery = new Vector2(25f, 40f);
    [Tooltip("seconds a wave takes to build up to its full crowd after the break, so a cleared screen doesn't refill all at once")]
    [SerializeField, Min(0f)] private float waveBuildUp = 35f;
    [Tooltip("seconds before a Final Rush in which the wave's horde stops coming, so the rush doesn't start in an arena already full")]
    [SerializeField, Min(0f)] private float quietBeforeRush = 5f;
    private float frontAngle, nextSwing, waveStartedAt = -999f;
    [Tooltip("enemies left further behind than this many screen half-diagonals are brought round in front again. 0 turns it off")]
    [SerializeField, Min(0f)] private float recycleDistance = 1.6f;
    [Tooltip("the least time between two timeline events, so ones that come due together don't land at once")]
    [SerializeField, Min(0f)] private float secondsBetweenEvents = 2f;

    [Header("Elites")]
    [Tooltip("how much bigger an elite is than its kind")]
    [SerializeField, Min(1f)] private float eliteSize = EliteOutline.Size;
    [Tooltip("how many times its kind's health an elite has")]
    [SerializeField, Min(1f)] private float eliteHealth = EliteOutline.Health;
    [Tooltip("wen an elite always bursts into, and it always drops a heal")]
    [SerializeField, Min(0)] private int eliteWenDrops = 12;
    [Tooltip("armour an elite has on top of its kind's")]
    [SerializeField, Range(0f, 0.5f)] private float eliteArmour = 0.15f;

    [Header("Packs (small enemies come in groups)")]
    [Tooltip("enemies costing this much or less (wisps) arrive in a pack instead of one at a time")]
    [SerializeField, Min(0)] private int packMaxCost = 1;
    [Tooltip("how many a pack holds, at the start of the run and once the spawn ramp is done")]
    [SerializeField] private Vector2Int packSizeEarly = new Vector2Int(3, 5);
    [SerializeField] private Vector2Int packSizeLate = new Vector2Int(16, 26);
    [Tooltip("how far apart a pack's members are: its radius is this x sqrt(size) / 2")]
    [SerializeField, Min(0f)] private float packSpread = 0.55f;
    [Tooltip("a swarm of this many or more splits into two flocks crossing from different sides; 0 never splits")]
    [SerializeField, Min(0)] private int swarmSplitAt = 100;
    [Tooltip("how long after the first flock the second sets off")]
    [SerializeField, Min(0f)] private float swarmSplitDelay = 1.2f;

    // an evolution is the payoff, as in Vampire Survivors: a spike of power the run is meant to
    // enjoy. the horde only leans on it a little (Vampire Survivors doesn't at all), and never past
    // a handful of evolutions' worth, so a full build of them mows the screen down as it should.
    // the health curve (fitted to measured weapon damage, Tools/Balance) is the run's real pressure
    [Header("Evolutions (the horde leans on an evolved weapon, a little)")]
    [Tooltip("each evolved weapon the player holds adds this share to the crowd: more alive at once and more arriving")]
    [SerializeField, Min(0f)] private float evoCrowd = 0.06f;
    [Tooltip("each evolved weapon adds this share to every enemy's health, added up, not compounding: 0.1 is x1.1 with one, x1.4 with four")]
    [SerializeField, Min(0f)] private float evoHealth = 0.1f;
    [Tooltip("each evolved weapon raises the timeline's hard cap by this many, so the extra crowd has room")]
    [SerializeField, Min(0)] private int evoExtraCap = 15;
    [Tooltip("each evolved weapon cuts the wen an enemy is worth (and with it the coins) by this share, compounding: 0.1 leaves 90% with one, 66% with four")]
    [SerializeField, Range(0f, 0.9f)] private float evoWenCut = 0.1f;
    [Tooltip("the most evolutions the horde answers: past this many, the rest are all the player's")]
    [SerializeField, Min(0f)] private float evoPressureMax = 4f;
    [Tooltip("seconds the horde takes to grow into an evolution, so it swells rather than jumps")]
    [SerializeField, Min(0.1f)] private float evoRampSeconds = 45f;

    [Header("Opening")]
    [Tooltip("ordinary enemies that spawn while the player is at or below this level die to any hit. 0 turns it off")]
    [SerializeField, Min(0)] private int oneShotThroughLevel = 5;

    private SpawnTimeline activeTimeline;
    private float[] eventDue;               // run minute each event is next due; infinity once done
    private float nextEventAllowed;
    private float trickleTimer;
    private int trickleOwed;
    private float surgeUntil = -1f;
    private float surgeCrowd = 1f;
    private float nextRecycle;
    private Transform playerTransform;
    private Vector2 playerLastPos;
    private Vector2 heading;                // where the player has been walking, smoothed, in units per second
    private bool bossFight;
    private readonly List<GameObject> stragglers = new List<GameObject>(64);

    private float fastPhaseUntil = -1f;
    private float nextFastPhaseCheck;
    private float timeElapsed;
    private float budget;
    private float spawnCooldown;
    private bool finalRush;
    private float spawnPausedUntil;
    // from a cleared Final Rush until the next wave starts (its boss envelope opened first), the
    // field stays empty
    private bool betweenWaves;
    private float runTime;
    private bool preparingFastPhase;
    private bool fastPhaseSequenceRunning;

    private int currentWave;
    private int bossesSpawnedThisRush;
    private List<GameObject> activeBosses = new List<GameObject>();

    private int finalRushQuota;
    private int finalRushKills;

    private bool spawningStopped;
    private bool finalBossDown;
    private bool timeUp;
    private bool devSkipped;

    private static bool secretBossSpawnedThisRun;
    private float nextSecretBossCheck;

    private void OnEnable()
    {
        Active = this;
        GameEvents.OnFinalRushStarted += HandleFinalRushStart;
        GameEvents.OnFinalRushEnded += HandleFinalRushEnd;
        GameEvents.OnWaveCleared += HandleWaveCleared;
        GameEvents.OnPurgeEnemiesWithFx += HandlePurge;
        GameEvents.OnRunTimeChanged += HandleRunTimeChanged;
        GameEvents.OnEnemyKilled += HandleEnemyKilled;
        GameEvents.OnWaveStarted += HandleWaveStarted;
        GameEvents.OnFinalBossStarted += HandleFinalBossStarted;
    }

    private void OnDisable()
    {
        if (Active == this) Active = null;
        GameEvents.OnFinalRushStarted -= HandleFinalRushStart;
        GameEvents.OnFinalRushEnded -= HandleFinalRushEnd;
        GameEvents.OnWaveCleared -= HandleWaveCleared;
        GameEvents.OnPurgeEnemiesWithFx -= HandlePurge;
        GameEvents.OnRunTimeChanged -= HandleRunTimeChanged;
        GameEvents.OnEnemyKilled -= HandleEnemyKilled;
        GameEvents.OnWaveStarted -= HandleWaveStarted;
        GameEvents.OnFinalBossStarted -= HandleFinalBossStarted;
    }

    private void Start()
    {
        // a playfield in the scene brings its own walls; they replace whatever Map Bounds holds
        if (Playfield.Active != null && Playfield.Active.HasBounds) mapBounds = Playfield.Active.Bounds;

        // and its own schedule: the map's who-comes-when is its own (Huangquan Road's dead aren't the
        // courtyard's); this spawner's is only for a map without one
        activeTimeline = Playfield.Active != null && Playfield.Active.spawnTimeline != null ? Playfield.Active.spawnTimeline : timeline;
        ResetEvents();

        PrewarmPools();

        budget = startingBudget;
        nextFastPhaseCheck = Time.time + fastPhaseCheckInterval;
        nextSecretBossCheck = Time.time + secretBossCheckInterval;
        UpdateFinalRushProgressBar();
    }

    private void HandleRunTimeChanged(float value)
    {
        runTime = value;
        hasRunClock = true;
    }

    // how far into the run the difficulty curve is read: the run clock on screen, so a curve's
    // minutes are the minutes the player sees. the clock stops for the Final Rushes, so they don't
    // toughen the horde. without a GameLoopController, time played
    private bool hasRunClock;
    private float DifficultyTime => hasRunClock ? runTime : timeElapsed;

    private void HandleWaveStarted(int wave)
    {
        betweenWaves = false;
        // the first wave has its own gentle opening; the ones after a rush build up from a clear screen
        if (wave > 1) waveStartedAt = Time.time;

        // treat wave 1 as "new run" for the easter egg
        if (wave == 1)
        {
            secretBossSpawnedThisRun = false;
            nextSecretBossCheck = Time.time + secretBossCheckInterval;
        }
    }

    private void StartPreparingFastPhase()
    {
        preparingFastPhase = true;
        spawnCooldown = 0f;
    }

    private IEnumerator BeginFastPhaseAfterDelay()
    {
        fastPhaseSequenceRunning = true;
        yield return new WaitForSeconds(fastPhasePostClearDelay);
        preparingFastPhase = false;
        StartFastPhase(fastPhaseDuration);
        fastPhaseSequenceRunning = false;
    }

    public BoxCollider2D[] MapBounds => mapBounds;

    // nothing new spawns for a moment, e.g. after the Command Token wipes the screen, so the
    // clear actually reads as a clear. a longer pause already running is kept
    public void PauseSpawning(float seconds)
    {
        if (seconds <= 0f) return;
        spawnPausedUntil = Mathf.Max(spawnPausedUntil, Time.time + seconds);
    }

    public static SpawnDirector Active { get; private set; }

    // the running map's difficulty, or this spawner's own when the map has none
    private DifficultyCurve Curve => DifficultyCurve.For(curve);

    // the run's time is up (RunTimeLimit): nothing more spawns and the horde goes, inside out
    public void EndOfTime()
    {
        if (timeUp) return;
        timeUp = true;
        spawningStopped = true;
        // everything goes, bosses too; nothing it sweeps away counts as the player's kill
        StartCoroutine(PurgeInsideOut(null));
    }
    public Rect PlayRect => GetPlayRectFromBorders();

    // how thin the horde runs, e.g. while Yama's danmaku needs the room: 1 = as usual
    [System.NonSerialized] public float BossCrowd = 1f;

    // nothing more spawns for the rest of the run (a final boss's end)
    public void StopSpawning() => spawningStopped = true;

    private void HandleFinalBossStarted()
    {
        var mapBoss = Playfield.Active != null ? Playfield.Active.finalBoss : null;
        var arch = mapBoss != null ? mapBoss : finalBossArchetype != null ? finalBossArchetype : bossArchetype;

        // a boss with a fight of its own (Yama, Meng Po): it comes in its own way and runs the fight
        if (arch != null && arch.prefab != null && arch.prefab.TryGetComponent(out IFightBoss _))
        {
            bossFight = true;
            var p = GameObject.FindGameObjectWithTag("Player");
            Vector2 at = p != null ? (Vector2)p.transform.position + Vector2.up * 4.6f : Vector2.zero;
            var go = Instantiate(arch.prefab, at, Quaternion.identity);
            activeBosses.Add(go);
            EnvelopeCarrier.Attach(go, EnvelopeSource.FinalBoss);
            go.GetComponent<IFightBoss>().Begin(this, arch);
            return;
        }

        if (arch == null || arch.prefab == null)
        {
            // nothing to fight: count it as beaten rather than hold the night up
            var p = GameObject.FindGameObjectWithTag("Player");
            FinalBossDown(p != null ? p.transform.position : Vector3.zero);
            return;
        }

        // no set pieces during the final boss; the timeline's crowd keeps coming
        bossFight = true;

        var boss = Spawn(arch, false);
        if (boss == null) return;

        boss.transform.localScale *= finalBossScale;
        activeBosses.Add(boss);
        // it always carries an envelope, and a good one
        EnvelopeCarrier.Attach(boss, EnvelopeSource.FinalBoss);

        if (boss.TryGetComponent(out EnemyHealth health))
        {
            health.SetScaled(health.Max * finalBossHealthMul);
            health.OnHealthChanged += (current, max) =>
            {
                // swept away when the time runs out isn't beaten
                if (current <= 0f && !timeUp) FinalBossDown(boss.transform.position);
            };
        }

    }


    // the boss falls and takes the horde with it, and the run is won: its envelope to open, the
    // wen swept in, and the results (RunVictory)
    public void FinalBossDown(Vector3 position)
    {
        if (finalBossDown) return;
        finalBossDown = true;
        spawningStopped = true;

        GameEvents.OnFinalBossDefeated?.Invoke(position);
        StartCoroutine(ClearAfterBoss());
    }

    // this runs from inside the boss's own TakeDamage, before its death has resolved, so the
    // purge waits a frame instead of re-entering the boss mid death
    private IEnumerator ClearAfterBoss()
    {
        yield return null;
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            SealWave.Roll(player.transform.position);
            while (SealWave.Running) yield return null;
        }
        else yield return PurgeInsideOut(null);
        GameEvents.OnCollectAllWen?.Invoke();
        if (timeUp) yield break;
        RunVictory.Begin();
    }

    private void Update()
    {
        timeElapsed += Time.deltaTime;
        if (rushBarDirty) { rushBarDirty = false; UpdateFinalRushProgressBar(); }
        TrackPlayerHeading();
        TrackFront();
        TrackEvolutions();
        if (spawningStopped) return;

        // secret boss easter egg roll runs independently of normal spawning
        TrySpawnSecretBoss();

        if (Time.time < spawnPausedUntil || betweenWaves) return;

        bool inFastPhase = Time.time < fastPhaseUntil;

        // with a timeline, it runs ordinary spawning. the Final Rush (and a fast phase already
        // under way) keep the budget spawner below, bosses and all
        if (activeTimeline != null && !finalRush && !preparingFastPhase && !inFastPhase)
        {
            UpdateTimeline();
            return;
        }

        // the Final Rush is the Magistrate's procession: its own formations, not the horde
        if (finalRush && ProcessionRuns)
        {
            UpdateProcession();
            return;
        }

        // random chance to start preparing a fast phase
        // (the old budget spawner's surprise: a timeline has its own surges, so not with one)
        if (activeTimeline == null &&
            !preparingFastPhase &&
            !inFastPhase &&
            runTime >= fastPhaseMinRunTime &&
            Time.time >= nextFastPhaseCheck)
        {
            nextFastPhaseCheck = Time.time + fastPhaseCheckInterval;
            if (Random.value < fastPhaseChance)
            {
                StartPreparingFastPhase();
            }
        }

        float densityScale = (Curve != null ? Curve.DensityAt(DifficultyTime) : 1f) * EvoCrowd * BossCrowd;
        float rushScale = finalRush ? densityScale * 1.2f : densityScale;

        int alive = CountAlive();

        if (preparingFastPhase)
        {
            // wait until wave is clear before starting fast phase
            if (alive <= 0 && !fastPhaseSequenceRunning)
            {
                StartCoroutine(BeginFastPhaseAfterDelay());
            }
            return;
        }

        float spawnrateMul = inFastPhase ? fastPhaseSpawnrateMul : 1f;
        float densityMul = inFastPhase ? fastPhaseDensityMul : 1f;
        float target = desiredPerScreen * densityMul * rushScale;

        budget += budgetPerSecond * spawnrateMul * Time.deltaTime * rushScale;

        // 0 → start of run, 1 → fully ramped (after spawnCapRampDuration seconds)
        float spawnRamp = spawnCapRampDuration > 0f
            ? Mathf.Clamp01(timeElapsed / spawnCapRampDuration)
            : 1f;

        // allow more spawns per frame as the run goes on
        int dynamicMaxSpawnsPerFrame = baseMaxSpawnsPerFrame +
                                       Mathf.RoundToInt(extraSpawnsPerFrameAtMax * spawnRamp);

        int maxSpawnsPerFrame = inFastPhase
            ? dynamicMaxSpawnsPerFrame * 2
            : dynamicMaxSpawnsPerFrame;

        int spawnsThisFrame = 0;
        spawnCooldown -= Time.deltaTime;

        // shrink cooldown window over time
        float minCd = Mathf.Lerp(minSpawnCooldownEarly, minSpawnCooldownLate, spawnRamp);
        float maxCd = Mathf.Lerp(maxSpawnCooldownEarly, maxSpawnCooldownLate, spawnRamp);

        while (spawnsThisFrame < maxSpawnsPerFrame &&
               alive < target &&
               spawnCooldown <= 0f)
        {
            EnemyArchetype arch;

            if (finalRush && bossArchetype != null && bossArchetype.prefab != null)
            {
                int maxBosses = GetMaxBossCountForWave(currentWave);
                int aliveBosses = GetAliveBossCount();

                if (aliveBosses < maxBosses && bossesSpawnedThisRush < maxBosses)
                {
                    arch = bossArchetype;
                }
                else if (inFastPhase && fastPhaseArchetype != null)
                {
                    arch = fastPhaseArchetype;
                }
                else
                {
                    arch = PickEnemy(finalRush);
                }
            }
            else
            {
                if (inFastPhase && fastPhaseArchetype != null)
                {
                    arch = fastPhaseArchetype;
                }
                else
                {
                    arch = PickEnemy(finalRush);
                }
            }

            if (arch == null || arch.prefab == null) break;
            if (budget < arch.cost) break;

            var go = Spawn(arch, finalRush);
            if (go == null) break;

            budget -= arch.cost;
            alive++;
            spawnsThisFrame++;

            if (arch == bossArchetype)
            {
                activeBosses.Add(go);
                bossesSpawnedThisRush++;
                EnvelopeCarrier.Attach(go, EnvelopeSource.Boss);
            }

            float cd = Random.Range(minCd, maxCd) / spawnrateMul;
            spawnCooldown = cd;
        }

        // the Final Rush ends when GameLoopController says so: its quota met and its bosses down
    }

    public void StartFastPhase(float duration)
    {
        fastPhaseUntil = Time.time + duration;
        spawnCooldown = 0f;
    }

    private void TrySpawnSecretBoss()
    {
        if (secretBossArchetype == null || secretBossArchetype.prefab == null) return;
        if (secretBossSpawnedThisRun) return;
        if (finalRush) return; // do not interfere with final rush
        if (runTime < secretBossMinRunTime) return;
        if (Time.time < nextSecretBossCheck) return;

        nextSecretBossCheck = Time.time + secretBossCheckInterval;

        if (Random.value > secretBossChancePerCheck) return;

        Vector2 pos = GetSpawnPositionNearOffscreenInsideBounds();
        GameObject go = Instantiate(secretBossArchetype.prefab, pos, Quaternion.identity);

        if (go != null)
        {
            secretBossSpawnedThisRun = true;

            // NEW: hook up the ui to the spawned secret boss
            var behavior = go.GetComponent<SecretBossBehavior>();
            if (behavior != null && secretBossUi != null)
            {
                behavior.Init(secretBossUi.gameObject);
            }
        }
    }

    private EnemyArchetype PickEnemy(bool rush)
    {
        var src = (!rush || chunkyPool == null || chunkyPool.Count == 0) ? pool : chunkyPool;
        if (src == null || src.Count == 0) return null;

        float sum = 0f;
        foreach (var p in src) sum += Mathf.Max(0.0001f, p.weight);

        float r = Random.value * sum;
        float acc = 0f;
        foreach (var p in src)
        {
            acc += Mathf.Max(0.0001f, p.weight);
            if (r <= acc) return p;
        }
        return src[src.Count - 1];
    }

    private GameObject Spawn(EnemyArchetype arch, bool rush) => Spawn(arch, rush, GetSpawnPositionNearOffscreenInsideBounds());

    private GameObject Spawn(EnemyArchetype arch, bool rush, Vector2 pos)
    {
        // regular enemies come from a pool and go back to it when they die; bosses carry per
        // fight wiring (health watchers, scale) and are made fresh
        var go = IsBoss(arch)
            ? Instantiate(arch.prefab, pos, Quaternion.identity)
            : ObjectPool.For(arch.prefab).Get(pos, Quaternion.identity);
        Count(arch, go);

        if (go.TryGetComponent(out EnemyHealth h))
        {
            float hpMul = (Curve != null ? Curve.HealthAt(DifficultyTime) : 1f) * EvoHealth;
            if (rush) hpMul *= finalRushHealthMul;
            // the opening: anything ordinary goes down to one hit while the build is still bare
            h.SetScaled(OneShotOpening && !rush && !IsBoss(arch) ? 1f : arch.baseHealth * hpMul);
            h.evoHealth = EvoHealth;
            h.wenShare = EvoWen;
            h.armour = arch.armour;
            h.physicalTaken = arch.physicalTaken;
            h.magicalTaken = arch.magicalTaken;
        }

        if (go.TryGetComponent(out EnemyMovement m))
        {
            float speedMul = Curve != null ? Curve.SpeedAt(DifficultyTime) : 1f;
            m.SetSpeedMultiplier(speedMul * arch.baseSpeed);
            m.knockbackResist = IsBoss(arch) ? 1f : arch.knockbackResist;
        }

        if (go.TryGetComponent(out EnemyContactDamage d))
        {
            float dmgMul = Curve != null ? Curve.DamageAt(DifficultyTime) : 1f;
            d.SetDamageMultiplier(dmgMul * arch.baseDamage);
        }

        if (go.TryGetComponent(out EnemyContactDamage contact))
        {
            contact.tickInterval = arch.contactTickInterval;
            float dps = (Curve != null ? Curve.DamageAt(DifficultyTime) : 1f) * arch.baseDamage;
            contact.damagePerTick = dps * contact.tickInterval;
        }

        return go;
    }

    private PlayerInventory inventory;

    private bool OneShotOpening
    {
        get
        {
            if (oneShotThroughLevel <= 0) return false;
            if (inventory == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) player.TryGetComponent(out inventory);
                if (inventory == null) return false;
            }
            return inventory.CurrentLevel <= oneShotThroughLevel;
        }
    }

    private int CountAlive()
    {
        return EnemyRegistry.Count;
    }

    // the Magistrates a Final Rush brings (Magistrates Per Rush), whichever spawner runs it
    private int GetMaxBossCountForWave(int wave)
    {
        if (magistratesPerRush == null || magistratesPerRush.Length == 0) return 1;
        return Mathf.Max(1, magistratesPerRush[Mathf.Clamp(wave - 1, 0, magistratesPerRush.Length - 1)]);
    }

    private int GetAliveBossCount()
    {
        activeBosses.RemoveAll(b => b == null || !b.activeInHierarchy);
        return activeBosses.Count;
    }

    public bool HasAliveBosses()
    {
        return GetAliveBossCount() > 0;
    }

    private bool IsBossAlive()
    {
        return GetAliveBossCount() > 0;
    }

    private Rect GetPlayRectFromBorders()
    {
        if (mapBounds == null || mapBounds.Length == 0)
            return new Rect(-9999, -9999, 19998, 19998);

        Vector2 centroid = Vector2.zero;
        int c = 0;
        foreach (var b in mapBounds)
        {
            if (!b) continue;
            centroid += (Vector2)b.bounds.center;
            c++;
        }

        if (c == 0)
            return new Rect(-9999, -9999, 19998, 19998);

        centroid /= c;

        float innerLeft = float.NegativeInfinity;
        float innerRight = float.PositiveInfinity;
        float innerBottom = float.NegativeInfinity;
        float innerTop = float.PositiveInfinity;

        foreach (var b in mapBounds)
        {
            if (!b) continue;
            var bo = b.bounds;
            Vector2 d = (Vector2)bo.center - centroid;
            if (Mathf.Abs(d.x) >= Mathf.Abs(d.y))
            {
                if (d.x < 0f) innerLeft = Mathf.Max(innerLeft, bo.max.x);
                else innerRight = Mathf.Min(innerRight, bo.min.x);
            }
            else
            {
                if (d.y < 0f) innerBottom = Mathf.Max(innerBottom, bo.max.y);
                else innerTop = Mathf.Min(innerTop, bo.min.y);
            }
        }

        if (!float.IsFinite(innerLeft) ||
            !float.IsFinite(innerRight) ||
            !float.IsFinite(innerBottom) ||
            !float.IsFinite(innerTop) ||
            innerRight <= innerLeft ||
            innerTop <= innerBottom)
        {
            return new Rect(-9999, -9999, 19998, 19998);
        }

        Rect r = new Rect(innerLeft, innerBottom, innerRight - innerLeft, innerTop - innerBottom);
        r.xMin += boundsInset;
        r.xMax -= boundsInset;
        r.yMin += boundsInset;
        r.yMax -= boundsInset;
        r.yMax -= boundsInset;
        return r;
    }

    private int obstacleMask = -2;

    // a point just off screen that isn't inside a wall, a pillar or anything else solid. first a
    // spot around the screen's edge, often on the side the player is heading for; near the edge
    // of the arena, where most of that ring is outside the walls, the strips of off-screen space
    // that are left
    private Vector2 GetSpawnPositionNearOffscreenInsideBounds()
    {
        if (obstacleMask == -2) obstacleMask = LayerMask.GetMask("Obstacles");

        for (int tries = 0; tries < 6; tries++)
        {
            bool ahead = heading.sqrMagnitude > 0.25f && Random.value < aheadBias;
            Vector2 dir = ahead ? Rotate(heading.normalized, Random.Range(-60f, 60f))
                : Random.value < frontShare ? FrontDirection()
                : RandomDirection();
            if (TryEdgePoint(dir, Random.Range(0.6f, 0.6f + offscreenBand), out var ring)) return ring;
        }

        Vector2 pos = PickOffscreenPoint();
        for (int tries = 0; tries < 8 && obstacleMask != 0 && Physics2D.OverlapCircle(pos, 0.5f, obstacleMask) != null; tries++)
            pos = PickOffscreenPoint();
        return pos;
    }

    private Vector2 PickOffscreenPoint()
    {
        Rect play = GetPlayRectFromBorders();

        float h = mainCamera.orthographicSize;
        float w = h * mainCamera.aspect;

        float cx = mainCamera.transform.position.x;
        float cy = mainCamera.transform.position.y;

        float camLeft = cx - w;
        float camRight = cx + w;
        float camBottom = cy - h;
        float camTop = cy + h;

        float pad = 0.01f;

        float leftX0 = Mathf.Max(play.xMin, camLeft - offscreenBand);
        float leftX1 = Mathf.Min(play.xMax, camLeft - pad);
        float leftY0 = Mathf.Max(play.yMin, camBottom);
        float leftY1 = Mathf.Min(play.yMax, camTop);

        float rightX0 = Mathf.Max(play.xMin, camRight + pad);
        float rightX1 = Mathf.Min(play.xMax, camRight + offscreenBand);
        float rightY0 = Mathf.Max(play.yMin, camBottom);
        float rightY1 = Mathf.Min(play.yMax, camTop);

        float botX0 = Mathf.Max(play.xMin, camLeft);
        float botX1 = Mathf.Min(play.xMax, camRight);
        float botY0 = Mathf.Max(play.yMin, camBottom - offscreenBand);
        float botY1 = Mathf.Min(play.yMax, camBottom - pad);

        float topX0 = Mathf.Max(play.xMin, camLeft);
        float topX1 = Mathf.Min(play.xMax, camRight);
        float topY0 = Mathf.Max(play.yMin, camTop + pad);
        float topY1 = Mathf.Min(play.yMax, camTop + offscreenBand);

        var rects = new List<(Rect r, float area)>();
        if (leftX1 > leftX0 && leftY1 > leftY0)
            rects.Add((new Rect(leftX0, leftY0, leftX1 - leftX0, leftY1 - leftY0), (leftX1 - leftX0) * (leftY1 - leftY0)));
        if (rightX1 > rightX0 && rightY1 > rightY0)
            rects.Add((new Rect(rightX0, rightY0, rightX1 - rightX0, rightY1 - rightY0), (rightX1 - rightX0) * (rightY1 - rightY0)));
        if (botX1 > botX0 && botY1 > botY0)
            rects.Add((new Rect(botX0, botY0, botX1 - botX0, botY1 - botY0), (botX1 - botX0) * (botY1 - botY0)));
        if (topX1 > topX0 && topY1 > topY0)
            rects.Add((new Rect(topX0, topY0, topX1 - topX0, topY1 - topY0), (topX1 - topX0) * (topY1 - topY0)));

        if (rects.Count > 0)
        {
            float total = 0f;
            foreach (var it in rects) total += it.area;
            float pick = Random.value * total;
            float acc = 0f;
            foreach (var it in rects)
            {
                acc += it.area;
                if (pick <= acc)
                {
                    float rx = Random.Range(it.r.xMin, it.r.xMax);
                    float ry = Random.Range(it.r.yMin, it.r.yMax);
                    return new Vector2(rx, ry);
                }
            }

            var last = rects[rects.Count - 1].r;
            return new Vector2(Random.Range(last.xMin, last.xMax), Random.Range(last.yMin, last.yMax));
        }

        float gapLeft = Mathf.Max(0f, camLeft - play.xMin);
        float gapRight = Mathf.Max(0f, play.xMax - camRight);
        float gapTop = Mathf.Max(0f, play.yMax - camTop);
        float gapBottom = Mathf.Max(0f, camBottom - play.yMin);

        int side = 0;
        float best = gapLeft;
        if (gapRight > best) { best = gapRight; side = 1; }
        if (gapTop > best) { best = gapTop; side = 2; }
        if (gapBottom > best) { best = gapBottom; side = 3; }

        float x;
        float y;
        switch (side)
        {
            case 0:
                x = Mathf.Clamp(camLeft - pad, play.xMin, play.xMax);
                y = Random.Range(Mathf.Max(play.yMin, camBottom), Mathf.Min(play.yMax, camTop));
                break;
            case 1:
                x = Mathf.Clamp(camRight + pad, play.xMin, play.xMax);
                y = Random.Range(Mathf.Max(play.yMin, camBottom), Mathf.Min(play.yMax, camTop));
                break;
            case 2:
                y = Mathf.Clamp(camTop + pad, play.yMin, play.yMax);
                x = Random.Range(Mathf.Max(play.xMin, camLeft), Mathf.Min(play.xMax, camRight));
                break;
            case 3:
            default:
                y = Mathf.Clamp(camBottom - pad, play.yMin, play.yMax);
                x = Random.Range(Mathf.Max(play.xMin, camLeft), Mathf.Min(play.xMax, camRight));
                break;
        }

        x = Mathf.Clamp(x, play.xMin, play.xMax);
        y = Mathf.Clamp(y, play.yMin, play.yMax);

        // ensure actually offscreen
        if (x > camLeft && x < camRight && y > camBottom && y < camTop)
        {
            if (side == 0) x = Mathf.Max(play.xMin, camLeft - pad);
            else if (side == 1) x = Mathf.Min(play.xMax, camRight + pad);
            else if (side == 2) y = Mathf.Min(play.yMax, camTop + pad);
            else y = Mathf.Max(play.yMin, camBottom - pad);
        }

        return new Vector2(x, y);
    }

    // ---------------------------------------------------------------- timeline

    private void ResetEvents()
    {
        int n = activeTimeline != null ? activeTimeline.events.Count : 0;
        eventDue = new float[n];
        for (int i = 0; i < n; i++)
        {
            var e = activeTimeline.events[i];
            eventDue[i] = e != null ? e.atMinute : float.PositiveInfinity;
        }
    }

    // Vampire Survivors' rule: below the beat's minimum the crowd is refilled right away, so
    // killing fast only brings the next ones sooner. above it, a few more trickle in every
    // interval up to the cap
    private void UpdateTimeline()
    {
        float minute = runTime / 60f;
        RecycleStragglers();
        FireDueEvents(minute);

        var beat = activeTimeline.BeatAt(minute);
        if (beat == null) return;

        // the last moments before a Final Rush: nothing more of the wave's horde arrives
        if (GameLoopController.SecondsToRush <= quietBeforeRush) return;

        float surge = Time.time < surgeUntil ? surgeCrowd : 1f;
        float crowd = activeTimeline.crowdScale * surge * EvoCrowd * BossCrowd;
        // after a break the wave builds up to its crowd: half the cap and none of the refill at once
        float build = WaveBuild;
        int cap = Mathf.Min(HardCap, Mathf.RoundToInt(beat.cap * crowd * Mathf.Lerp(0.5f, 1f, build)));
        int minimum = Mathf.Min(cap, Mathf.RoundToInt(beat.minimum * crowd * build));
        int alive = CountAlive() - SpawnCrossing.Live;

        trickleTimer -= Time.deltaTime * surge;
        if (trickleTimer <= 0f)
        {
            trickleTimer = beat.interval;
            trickleOwed += Mathf.RoundToInt(beat.perInterval * crowd);
        }
        trickleOwed = Mathf.Clamp(trickleOwed, 0, Mathf.Max(0, cap - alive));

        // a few per frame, so a refill streams in rather than popping
        int perFrame = SpawnsPerFrame();
        for (int spawned = 0; spawned < perFrame; spawned++)
        {
            bool refill = alive < minimum;
            if (!refill && trickleOwed <= 0) break;

            // a kind already at its most alive (a rare lantern, the few burners) gives way to another
            EnemyArchetype arch = null;
            for (int tries = 0; tries < 4 && arch == null; tries++)
            {
                arch = SpawnTimeline.PickFrom(beat.enemies);
                if (arch != null && AtMost(arch)) arch = null;
            }
            if (arch == null) break;

            int made = SpawnByPattern(arch, cap - alive, HardCap - alive);
            if (made == 0) break;

            alive += made;
            if (!refill) trickleOwed -= made;
        }
    }

    // small enemies (wisps) come as a flock, like Vampire Survivors' bats: a dense cloud off screen
    // that pours in all at once, the flocks growing as the run goes on. a flock isn't cut down to
    // the beat's last few places (only to the hard cap), or late in a beat they'd dwindle to pairs.
    // returns how many were made
    // how each kind arrives (EnemyArchetype.pattern). returns how many were made
    private int SpawnByPattern(EnemyArchetype arch, int room, int hardRoom)
    {
        switch (arch.pattern)
        {
            case SpawnPattern.Cluster:
                return SpawnPack(arch, room, hardRoom, Random.Range(arch.group.x, arch.group.y + 1));
            case SpawnPattern.OnScreen:
                return TryOnScreenPoint(out Vector2 on) && Spawn(arch, false, on) != null ? 1 : 0;
            case SpawnPattern.Burst:
                return SpawnBurst(arch, Mathf.Min(hardRoom, Random.Range(arch.group.x, arch.group.y + 1)));
            case SpawnPattern.Edge:
            case SpawnPattern.Rare:
                return Spawn(arch, false) != null ? 1 : 0;
            default:
                return arch.cost <= packMaxCost ? SpawnPack(arch, room, hardRoom) : Spawn(arch, false) != null ? 1 : 0;
        }
    }

    // the living of each kind that has a Max Alive, to hold it to that
    private readonly Dictionary<EnemyArchetype, List<GameObject>> capped = new Dictionary<EnemyArchetype, List<GameObject>>();

    private bool AtMost(EnemyArchetype arch)
    {
        if (arch.maxAlive <= 0 || !capped.TryGetValue(arch, out var list)) return false;
        list.RemoveAll(g => g == null || !g.activeInHierarchy);
        return list.Count >= arch.maxAlive;
    }

    private void Count(EnemyArchetype arch, GameObject go)
    {
        if (arch == null || arch.maxAlive <= 0 || go == null) return;
        if (!capped.TryGetValue(arch, out var list)) capped[arch] = list = new List<GameObject>();
        list.Add(go);
    }

    // somewhere on the screen, well away from the player and off the props: for what grows where it
    // stands (a spider lily)
    private bool TryOnScreenPoint(out Vector2 pos)
    {
        GetView(out Vector2 centre, out float halfW, out float halfH);
        Vector2 me = playerTransform != null ? (Vector2)playerTransform.position : centre;
        for (int tries = 0; tries < 10; tries++)
        {
            pos = centre + new Vector2(Random.Range(-halfW + 1f, halfW - 1f), Random.Range(-halfH + 1f, halfH - 1.5f));
            if ((pos - me).sqrMagnitude < 3.5f * 3.5f) continue;
            if (ValidSpawn(pos)) return true;
        }
        pos = default;
        return false;
    }

    // a few together from one point of the edge on the front, a tight knot that sets off at once
    private int SpawnBurst(EnemyArchetype arch, int size)
    {
        if (size <= 0) return 0;
        Vector2 at = GetSpawnPositionNearOffscreenInsideBounds();
        int made = 0;
        for (int i = 0; i < size; i++)
        {
            Vector2 p = at + Random.insideUnitCircle * 0.8f;
            if (!ValidSpawn(p)) p = at;
            if (Spawn(arch, false, p) != null) made++;
        }
        return made;
    }

    private int SpawnPack(EnemyArchetype arch, int room, int hardRoom, int forced = 0)
    {
        float ramp = spawnCapRampDuration > 0f ? Mathf.Clamp01(timeElapsed / spawnCapRampDuration) : 1f;
        int least = Mathf.RoundToInt(Mathf.Lerp(packSizeEarly.x, packSizeLate.x, ramp));
        int most = Mathf.RoundToInt(Mathf.Lerp(packSizeEarly.y, packSizeLate.y, ramp));
        // a cluster kind's own size, not cut down to the beat's room (only to the hard cap)
        int size = forced > 0 ? Mathf.Min(forced, hardRoom) : Mathf.Min(Random.Range(least, most + 1), Mathf.Max(room, least), hardRoom);
        if (size <= 0) return 0;

        // the cloud's radius grows with the square root of its size, so it stays as dense; its
        // middle is pushed out by that much, so none of it appears on screen
        float radius = packSpread * Mathf.Sqrt(size) * 0.5f;
        GetView(out Vector2 centre, out _, out _);
        Vector2 middle = GetSpawnPositionNearOffscreenInsideBounds();
        Vector2 outward = middle - centre;
        if (outward.sqrMagnitude > 0.0001f) middle += outward.normalized * radius;
        if (!ValidSpawn(middle)) middle = GetSpawnPositionNearOffscreenInsideBounds();

        // a sunflower fill (each one a golden angle round from the last) with a little jitter: an
        // even cloud with a soft edge, not a random clump with holes and overlaps
        float turn = Random.value * Mathf.PI * 2f;
        int made = 0;
        for (int i = 0; i < size; i++)
        {
            float r = radius * Mathf.Sqrt((i + 0.5f) / size);
            float a = turn + i * 2.39996f;
            Vector2 at = middle + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r + Random.insideUnitCircle * packSpread * 0.2f;
            if (!ValidSpawn(at)) at = middle;
            if (Spawn(arch, false, at) != null) made++;
        }
        return made;
    }

    private int SpawnsPerFrame()
    {
        float ramp = spawnCapRampDuration > 0f ? Mathf.Clamp01(timeElapsed / spawnCapRampDuration) : 1f;
        return Mathf.Max(1, baseMaxSpawnsPerFrame + Mathf.RoundToInt(extraSpawnsPerFrameAtMax * ramp));
    }

    private void FireDueEvents(float minute)
    {
        if (eventDue == null || bossFight || Time.time < nextEventAllowed) return;

        var events = activeTimeline.events;
        bool skipped = devSkipped;
        devSkipped = false;
        for (int i = 0; i < eventDue.Length && i < events.Count; i++)
        {
            if (minute < eventDue[i]) continue;

            var e = events[i];
            // the dev tools skipped the clock past it: don't fire a backlog. one that came due
            // during a Final Rush (the timeline waits through those) still comes, a little late
            bool missed = skipped && minute - eventDue[i] > 1f;
            eventDue[i] = e != null && e.repeatEveryMinutes > 0f ? eventDue[i] + e.repeatEveryMinutes : float.PositiveInfinity;
            if (e == null || missed) continue;

            RunEvent(e);
            nextEventAllowed = Time.time + secondsBetweenEvents;
            return;
        }
    }

    public void RunEvent(SpawnTimeline.Event e)
    {
        if (e == null || mainCamera == null) return;

        var beat = activeTimeline != null ? activeTimeline.BeatAt(runTime / 60f) : null;
        EnemyArchetype Pick()
        {
            if (e.archetype != null && e.archetype.prefab != null) return e.archetype;
            return beat != null ? SpawnTimeline.PickFrom(beat.enemies) : PickEnemy(false);
        }

        bool happened;
        switch (e.kind)
        {
            case SpawnTimeline.EventKind.Swarm: happened = Crossing(e, Pick, false); break;
            case SpawnTimeline.EventKind.Stampede: happened = Crossing(e, Pick, true); break;
            case SpawnTimeline.EventKind.Ring: happened = Ring(e, Pick); break;
            case SpawnTimeline.EventKind.Elite: happened = Elites(e, Pick); break;
            case SpawnTimeline.EventKind.Surge:
                surgeCrowd = Mathf.Max(1f, e.crowd);
                surgeUntil = Time.time + e.seconds;
                happened = true;
                break;
            default: happened = false; break;
        }

        // events aren't announced: the horde doing it is the announcement
    }

    private static Color KindColor(SpawnTimeline.EventKind kind)
    {
        switch (kind)
        {
            case SpawnTimeline.EventKind.Swarm: return new Color(0.55f, 0.9f, 1f);
            case SpawnTimeline.EventKind.Stampede: return new Color(1f, 0.62f, 0.3f);
            case SpawnTimeline.EventKind.Ring: return new Color(0.8f, 0.6f, 1f);
            case SpawnTimeline.EventKind.Elite: return new Color(1f, 0.84f, 0.3f);
            default: return new Color(1f, 0.38f, 0.35f);
        }
    }

    // swarm: a tight stream from off screen, aimed through the player, crossing and leaving.
    // stampede: a line as wide as the screen sweeping across it. both come from a side the
    // arena has room on
    private bool Crossing(SpawnTimeline.Event e, System.Func<EnemyArchetype> pick, bool line)
    {
        GetView(out Vector2 centre, out float halfW, out float halfH);
        Vector2 target = playerTransform != null ? (Vector2)playerTransform.position : centre;
        Rect play = GetPlayRectFromBorders();

        // the eight compass directions in a random order; the first with room to start from wins
        int first = Random.Range(0, 8);
        for (int k = 0; k < 8; k++)
        {
            Vector2 dir = Rotate(Vector2.right, ((first + k * 3) % 8) * 45f);
            Vector2 side = new Vector2(-dir.y, dir.x);
            float back = Mathf.Abs(dir.x) * halfW + Mathf.Abs(dir.y) * halfH + 1.5f;
            Vector2 start = centre - dir * back + side * Vector2.Dot(target - centre, side);
            if (!play.Contains(start)) continue;

            if (line)
            {
                float halfSpan = Mathf.Abs(side.x) * halfW + Mathf.Abs(side.y) * halfH + 1f;
                Vector2 middle = centre - dir * back;
                int placed = 0;
                for (int i = 0; i < e.count; i++)
                {
                    float t = e.count == 1 ? 0f : Mathf.Lerp(-halfSpan, halfSpan, i / (float)(e.count - 1));
                    Vector2 p = middle + side * (t + Random.Range(-0.3f, 0.3f)) - dir * Random.Range(0f, 0.6f);
                    if (SendCrosser(pick(), p, dir, e)) placed++;
                }
                return placed > 0;
            }

            StartCoroutine(Stream(e, pick, start, dir, side));
            return true;
        }
        return false;
    }

    // a swarm is a flock, like Vampire Survivors' bats: a broad, dense body that pours through in
    // a couple of seconds whatever its size, thickest down its middle and ragged at its edges. a big
    // one splits in two, the second flock crossing the first from another side a moment later
    private IEnumerator Stream(SpawnTimeline.Event e, System.Func<EnemyArchetype> pick, Vector2 start, Vector2 dir, Vector2 side, bool mayFork = true)
    {
        int count = e.count;
        if (mayFork && swarmSplitAt > 0 && count >= swarmSplitAt)
        {
            StartCoroutine(SecondFlock(e, pick, dir, count - count / 2));
            count /= 2;
        }

        // wider for more of them, and never so long it trickles
        float halfWidth = Mathf.Clamp(0.5f + Mathf.Sqrt(count) * 0.2f, 1.1f, 2.8f);
        float seconds = Mathf.Clamp(count / 40f, 0.9f, 2.4f);
        const float Tick = 1f / 20f;
        var wait = new WaitForSeconds(Tick);
        float owed = 0f;
        int sent = 0;
        while (sent < count)
        {
            if (spawningStopped || finalRush) yield break;
            owed += count * Tick / seconds;
            for (; owed >= 1f && sent < count; owed -= 1f, sent++)
            {
                // two random numbers averaged: most of them down the middle, a few at the edges
                float across = (Random.value + Random.value - 1f) * halfWidth;
                Vector2 p = start + side * across - dir * Random.Range(0f, 0.9f);
                SendCrosser(pick(), p, dir, e);
            }
            yield return wait;
        }
    }

    // the second half of a big swarm, after a beat: aimed through the player from the first free
    // compass direction at least 90° off the first flock's. if there's nowhere for it to come
    // from, it doesn't come
    private IEnumerator SecondFlock(SpawnTimeline.Event e, System.Func<EnemyArchetype> pick, Vector2 firstDir, int count)
    {
        yield return new WaitForSeconds(swarmSplitDelay);
        if (spawningStopped || finalRush || mainCamera == null) yield break;

        GetView(out Vector2 centre, out float halfW, out float halfH);
        Vector2 target = playerTransform != null ? (Vector2)playerTransform.position : centre;
        Rect play = GetPlayRectFromBorders();
        int first = Random.Range(0, 8);
        for (int k = 0; k < 8; k++)
        {
            Vector2 dir = Rotate(Vector2.right, ((first + k * 3) % 8) * 45f);
            if (Vector2.Dot(dir, firstDir) > 0.1f) continue;
            Vector2 side = new Vector2(-dir.y, dir.x);
            float back = Mathf.Abs(dir.x) * halfW + Mathf.Abs(dir.y) * halfH + 1.5f;
            Vector2 start = centre - dir * back + side * Vector2.Dot(target - centre, side);
            if (!play.Contains(start)) continue;

            var half = new SpawnTimeline.Event
            {
                label = e.label, kind = e.kind, archetype = e.archetype,
                count = count, speed = e.speed, health = e.health,
            };
            yield return Stream(half, pick, start, dir, side, false);
            yield break;
        }
    }

    private bool SendCrosser(EnemyArchetype arch, Vector2 at, Vector2 dir, SpawnTimeline.Event e)
    {
        if (arch == null || arch.prefab == null || !ValidSpawn(at)) return false;
        var go = Spawn(arch, false, at);
        if (go == null) return false;

        if (!Mathf.Approximately(e.health, 1f) && go.TryGetComponent(out EnemyHealth h)) h.SetScaled(h.Max * e.health);
        SpawnCrossing.Send(go, dir, e.speed, mainCamera, 25f);
        return true;
    }

    // a ring just outside the screen, all around, closing in. where the ring runs outside the
    // arena or through a pillar there's a gap
    private bool Ring(SpawnTimeline.Event e, System.Func<EnemyArchetype> pick)
    {
        GetView(out Vector2 centre, out float halfW, out float halfH);
        // the ellipse through the screen's corners, pushed out a little: all of it is off screen
        float a = halfW * 1.414f + 0.8f;
        float b = halfH * 1.414f + 0.8f;
        float turn = Random.value * Mathf.PI * 2f;

        int placed = 0;
        for (int i = 0; i < e.count; i++)
        {
            float angle = turn + i * Mathf.PI * 2f / e.count;
            Vector2 p = centre + new Vector2(Mathf.Cos(angle) * a, Mathf.Sin(angle) * b);
            var arch = pick();
            if (arch == null || arch.prefab == null || !ValidSpawn(p)) continue;

            var go = Spawn(arch, false, p);
            if (go == null) continue;
            if (!Mathf.Approximately(e.health, 1f) && go.TryGetComponent(out EnemyHealth h)) h.SetScaled(h.Max * e.health);
            placed++;
        }
        return placed > 0;
    }

    // Halls of Torment / Vampire Survivors elites: bigger, outlined, far tougher, and worth it
    private bool Elites(SpawnTimeline.Event e, System.Func<EnemyArchetype> pick)
    {
        int placed = 0;
        for (int i = 0; i < e.count; i++)
        {
            var arch = pick();
            if (arch == null || arch.prefab == null) continue;

            var go = Spawn(arch, false);
            if (go == null) continue;

            EliteOutline.Promote(go, eliteSize, eliteHealth, eliteWenDrops);
            EnvelopeCarrier.Attach(go, EnvelopeSource.Elite);
            if (go.TryGetComponent(out EnemyHealth eh)) eh.armour = Mathf.Min(0.6f, eh.armour + eliteArmour);
            // an elite stands its ground: most of a hit's knockback turned aside
            if (go.TryGetComponent(out EnemyMovement em)) em.knockbackResist = Mathf.Max(em.knockbackResist, 0.7f);
            placed++;
        }
        return placed > 0;
    }

    // Vampire Survivors doesn't let the horde fall behind: enemies left far off are moved round
    // to where the player is going. bosses and crossers are left alone
    private void RecycleStragglers()
    {
        if (recycleDistance <= 0f || Time.time < nextRecycle || mainCamera == null) return;
        nextRecycle = Time.time + 0.5f;

        GetView(out Vector2 centre, out float halfW, out float halfH);
        float limit = Mathf.Sqrt(halfW * halfW + halfH * halfH) * recycleDistance;
        float limitSq = limit * limit;

        stragglers.Clear();
        foreach (var go in EnemyRegistry.All)
            if (go != null && ((Vector2)go.transform.position - centre).sqrMagnitude > limitSq)
                stragglers.Add(go);

        int moved = 0;
        foreach (var go in stragglers)
        {
            if (moved >= 24) break;
            if (go == null || activeBosses.Contains(go)) continue;
            if (go.TryGetComponent(out BossMarker _) || go.TryGetComponent(out SecretBossBehavior _) || go.TryGetComponent(out SpawnCrossing _)) continue;

            Vector2 dir = heading.sqrMagnitude > 0.25f ? heading.normalized : RandomDirection();
            for (int tries = 0; tries < 4; tries++)
            {
                if (!TryEdgePoint(Rotate(dir, Random.Range(-70f, 70f)), Random.Range(0.6f, 0.6f + offscreenBand), out var p)) continue;
                if (go.TryGetComponent(out Rigidbody2D body)) body.position = p;
                go.transform.position = p;
                moved++;
                break;
            }
        }
    }

    // each evolution the player holds brings a little more of the horde, a little tougher, up to
    // evoPressureMax of them. the pressure eases in over evoRampSeconds after each
    private int evolutionsHeld;
    private float evoPressure;
    private float nextEvoCheck;
    private readonly List<Weapon> heldWeapons = new List<Weapon>();

    private float EvoCrowd => 1f + evoCrowd * evoPressure;
    private float EvoHealth => 1f + evoHealth * evoPressure;
    // more of them and tougher, but each worth less wen (and so fewer coins): an evolution
    // shouldn't also speed up the level ups and the shop
    private float EvoWen => Mathf.Pow(1f - evoWenCut, evoPressure);
    private int HardCap => activeTimeline.hardCap + Mathf.RoundToInt(evoExtraCap * evoPressure);

    private void TrackEvolutions()
    {
        if (Time.time >= nextEvoCheck && playerTransform != null)
        {
            nextEvoCheck = Time.time + 0.5f;
            playerTransform.GetComponentsInChildren(heldWeapons);
            evolutionsHeld = 0;
            foreach (var w in heldWeapons)
                if (w != null && w.Evolved) evolutionsHeld++;
        }
        float target = Mathf.Min(evolutionsHeld, evoPressureMax);
        evoPressure = Mathf.MoveTowards(evoPressure, target, Time.deltaTime / evoRampSeconds);
    }

    private Vector2 FrontDirection()
    {
        float a = (frontAngle + Random.Range(-frontSpread, frontSpread)) * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
    }

    // the front drifting, and swinging to another side now and then
    private void TrackFront()
    {
        if (nextSwing <= 0f) { frontAngle = Random.value * 360f; nextSwing = Time.time + Random.Range(frontSwingEvery.x, frontSwingEvery.y); }
        frontAngle += frontDrift * Time.deltaTime;
        if (Time.time < nextSwing) return;
        frontAngle += Random.Range(100f, 260f);
        nextSwing = Time.time + Random.Range(frontSwingEvery.x, frontSwingEvery.y);
    }

    // 0 as a wave starts to 1 once it has built up
    private float WaveBuild => waveBuildUp <= 0f ? 1f : Mathf.Clamp01((Time.time - waveStartedAt) / waveBuildUp);

    // where the player's been walking lately, from how far they moved, whatever moves them
    private void TrackPlayerHeading()
    {
        if (playerTransform == null)
        {
            if (Time.frameCount % 30 != 0) return;
            var p = FindFirstObjectByType<PlayerMovement>();
            if (p == null) return;
            playerTransform = p.transform;
            playerLastPos = playerTransform.position;
            return;
        }

        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        Vector2 pos = playerTransform.position;
        Vector2 velocity = (pos - playerLastPos) / dt;
        playerLastPos = pos;
        if (velocity.sqrMagnitude > 900f) return;   // a teleport, not a walk
        heading = Vector2.Lerp(heading, velocity, 1f - Mathf.Exp(-dt * 3f));
    }

    private void GetView(out Vector2 centre, out float halfW, out float halfH)
    {
        centre = mainCamera.transform.position;
        halfH = mainCamera.orthographicSize;
        halfW = halfH * mainCamera.aspect;
    }

    // where a line from the middle of the screen in this direction leaves it, a margin further
    private bool TryEdgePoint(Vector2 dir, float margin, out Vector2 pos)
    {
        GetView(out Vector2 centre, out float halfW, out float halfH);
        float tx = Mathf.Abs(dir.x) > 0.0001f ? halfW / Mathf.Abs(dir.x) : float.PositiveInfinity;
        float ty = Mathf.Abs(dir.y) > 0.0001f ? halfH / Mathf.Abs(dir.y) : float.PositiveInfinity;
        pos = centre + dir * (Mathf.Min(tx, ty) + margin);
        return ValidSpawn(pos);
    }

    private bool ValidSpawn(Vector2 pos)
    {
        if (obstacleMask == -2) obstacleMask = LayerMask.GetMask("Obstacles");
        if (!GetPlayRectFromBorders().Contains(pos)) return false;
        return obstacleMask == 0 || Physics2D.OverlapCircle(pos, 0.5f, obstacleMask) == null;
    }

    private static Vector2 RandomDirection()
    {
        float a = Random.value * Mathf.PI * 2f;
        return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
    }

    private static Vector2 Rotate(Vector2 v, float degrees)
    {
        float r = degrees * Mathf.Deg2Rad;
        float c = Mathf.Cos(r), s = Mathf.Sin(r);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    // ---------------------------------------------------------------- for the dev tools

    public SpawnTimeline Timeline => activeTimeline;
    public float RunMinute => runTime / 60f;
    public bool SurgeActive => Time.time < surgeUntil;

    public string BeatLabel
    {
        get
        {
            if (activeTimeline == null) return "no timeline (budget spawner)";
            var beat = activeTimeline.BeatAt(RunMinute);
            return beat == null ? "no beats" : string.IsNullOrEmpty(beat.label) ? $"from {beat.fromMinute:0.#} min" : beat.label;
        }
    }

    public string NextEventText
    {
        get
        {
            if (activeTimeline == null || eventDue == null) return "-";
            int best = -1;
            for (int i = 0; i < eventDue.Length && i < activeTimeline.events.Count; i++)
                if (activeTimeline.events[i] != null && !float.IsInfinity(eventDue[i]) && (best < 0 || eventDue[i] < eventDue[best])) best = i;
            if (best < 0) return "none left";
            var e = activeTimeline.events[best];
            float at = eventDue[best] * 60f;
            return $"{e.kind} {(string.IsNullOrEmpty(e.label) ? "" : "\"" + e.label + "\" ")}at {(int)(at / 60f)}:{(int)(at % 60f):00}";
        }
    }

    public void RunDevEvent(SpawnTimeline.EventKind kind)
    {
        RunEvent(new SpawnTimeline.Event
        {
            kind = kind,
            label = kind.ToString(),
            count = kind == SpawnTimeline.EventKind.Elite ? 1 : kind == SpawnTimeline.EventKind.Stampede ? 10 : 24,
            speed = 1.8f,
            health = kind == SpawnTimeline.EventKind.Elite ? 8f : 1f,
            crowd = 1.8f,
            seconds = 15f,
        });
    }

    // keeps enemy toughness in step when the dev tools skip the run clock ahead
    public void SkipTime(float seconds)
    {
        timeElapsed += Mathf.Max(0f, seconds);
        devSkipped = true;
    }

    private void HandleFinalRushStart(int wave, int quota)
    {
        finalRush = true;
        currentWave = wave;
        bossesSpawnedThisRush = 0;
        activeBosses.Clear();

        finalRushQuota = Mathf.Max(1, quota);
        ResetFinalRushProgress();

        if (ProcessionRuns) BeginProcession();
        else TrySpawnInitialBoss();
    }

    private void HandleFinalRushEnd(int wave)
    {
        finalRush = false;
        betweenWaves = true;
        spawnPausedUntil = Time.time + pauseAfterClear;
        activeBosses.Clear();

        finalRushQuota = 0;
        UpdateFinalRushProgressBar();
    }

    // ---------------------------------------------------------------- the Magistrate's procession
    //
    // a Final Rush is the Jiangshi Magistrate's procession: in the old stories a Taoist priest leads
    // the dead home in a line, ringing a bell, and the corpses hop after it. so the rush is his own:
    // jiangshi and his green corpse fire, arriving in formations a bell announces, each Magistrate
    // with a retinue in file behind him, the crowd sized by how many rushes the run has been
    // through rather than by the horde's density

    [Header("Final Rush: the Magistrate's procession")]
    [Tooltip("the corpses that make up the procession (the Jiangshi)")]
    [SerializeField] private EnemyArchetype processionCorpse;
    [Tooltip("the Magistrate's corpse fire, in flocks (Ghost Fire)")]
    [SerializeField] private EnemyArchetype processionFire;
    [Tooltip("how many are up at once in the first rush")]
    [SerializeField, Min(1)] private int rushCrowd = 30;
    [Tooltip("how much bigger each rush's crowd is than the last's: 1.3 = 30% more")]
    [SerializeField, Min(1f)] private float rushCrowdGrowth = 1.3f;
    [Tooltip("seconds between formations in the first rush; each rush after is 10% quicker, to 3.5s")]
    [SerializeField, Min(1f)] private float formationEvery = 7f;
    [Tooltip("the Magistrates each Final Rush brings, all at once at its start, rush 1 first (there are nine: wave ten is the final boss's). a rush past the list uses its last")]
    // (renamed from rushMagistrates, so a scene saved with the old counts can't keep them)
    [SerializeField] private int[] magistratesPerRush = { 1, 1, 1, 2, 2, 3, 3, 3, 3 };
    [Tooltip("how much tougher each rush's Magistrate is than the last's, on top of the run's health curve: 1.35 = 35% more. the first is a fight for a build of a few minutes, the ninth for a finished one")]
    [SerializeField, Min(1f)] private float rushBossGrowth = 1.35f;

    private enum Formation { Column, Pincer, Ring, Fire }
    private float nextFormation, nextTrickle;
    private int rushBosses;             // how many Magistrates this rush brings; it's won when they're all down

    // the Final Rush is won when all its Magistrates have come and fallen (the procession). without
    // one, the old way: its kill quota met and its bosses down
    public bool RushWon(int kills, int quota)
    {
        if (!ProcessionRuns) return kills >= quota && !HasAliveBosses();
        var first = RushBoss(0);
        if (first == null || first.prefab == null) return true;
        return bossesSpawnedThisRush >= rushBosses && GetAliveBossCount() == 0;
    }

    private bool ProcessionRuns => PMain != null && PMain.prefab != null;

    // the map's own procession and rush bosses (Playfield), or the scene's: the Magistrate's
    private EnemyArchetype PMain => Playfield.Active != null && Playfield.Active.processionMain != null ? Playfield.Active.processionMain : processionCorpse;
    private EnemyArchetype PFast => Playfield.Active != null && Playfield.Active.processionFast != null ? Playfield.Active.processionFast : processionFire;
    private EnemyArchetype RushBoss(int i)
    {
        var map = Playfield.Active != null ? Playfield.Active.rushBosses : null;
        if (map != null && map.Length > 0)
        {
            var a = map[Mathf.Abs(i) % map.Length];
            if (a != null && a.prefab != null) return a;
        }
        return bossArchetype;
    }
    private bool IsRushBoss(EnemyArchetype arch)
    {
        if (arch == null) return false;
        if (arch == bossArchetype) return true;
        var map = Playfield.Active != null ? Playfield.Active.rushBosses : null;
        if (map != null) foreach (var a in map) if (a == arch) return true;
        return false;
    }
    private int formationIndex;
    private static AudioClip procBell;

    private int RushTarget => Mathf.Min(HardCap, Mathf.RoundToInt(rushCrowd * Mathf.Pow(rushCrowdGrowth, Mathf.Max(0, currentWave - 1)) * EvoCrowd));
    private float FormationGap => Mathf.Max(3.5f, formationEvery * Mathf.Pow(0.9f, Mathf.Max(0, currentWave - 1)));

    private void BeginProcession()
    {
        formationIndex = 0;
        nextFormation = Time.time + 3.5f;           // the Magistrates and their retinues first, then the rest
        nextTrickle = Time.time + 1f;
        // the rush's whole set of Magistrates comes at once, from all round, each with his retinue
        rushBosses = GetMaxBossCountForWave(currentWave);
        float a0 = Random.value * 360f;
        for (int i = 0; i < rushBosses; i++) SpawnMagistrate(Rotate(Vector2.right, a0 + 360f * i / rushBosses), i);
    }

    private void UpdateProcession()
    {
        int alive = CountAlive();
        int target = RushTarget;

        // one who couldn't be placed at the start (walls all round) comes as soon as he can
        if (bossesSpawnedThisRush < rushBosses)
        {
            SpawnMagistrate(RandomDirection(), bossesSpawnedThisRush);
            rushBarDirty = true;
        }

        if (Time.time >= nextFormation && alive < target)
        {
            nextFormation = Time.time + FormationGap;
            // the formations take turns; the fire joins from the second rush
            var order = currentWave >= 2
                ? new[] { Formation.Column, Formation.Ring, Formation.Fire, Formation.Pincer }
                : new[] { Formation.Column, Formation.Ring, Formation.Pincer };
            SpawnFormation(order[formationIndex++ % order.Length], target - alive);
        }

        // stragglers keep the pressure up when the formations have been cut down
        if (Time.time >= nextTrickle && alive < target / 2)
        {
            nextTrickle = Time.time + 0.6f;
            Spawn(PMain, true);
        }
    }

    private void SpawnFormation(Formation f, int room)
    {
        int w = Mathf.Max(1, currentWave);
        Vector2 dir = heading.sqrMagnitude > 0.25f && Random.value < 0.5f ? Rotate(heading.normalized, Random.Range(-45f, 45f)) : RandomDirection();
        switch (f)
        {
            case Formation.Column:
                Column(PMain, dir, Mathf.Min(room, 6 + 2 * w), 0.85f);
                break;
            case Formation.Pincer:
                int half = Mathf.Min(room / 2, 5 + 2 * w);
                Column(PMain, dir, half, 0.85f);
                Column(PMain, -dir, half, 0.85f);
                break;
            case Formation.Ring:
                Ring(PMain, Mathf.Min(room, 12 + 4 * w));
                break;
            case Formation.Fire:
                var fire = PFast != null && PFast.prefab != null ? PFast : PMain;
                Flock(fire, dir, Mathf.Min(room, 8 + 2 * w));
                break;
        }
        RingBell(f == Formation.Ring ? 0.9f : 1f);
    }

    // a line of them hopping in single file from the screen's edge, the first nearest
    private int Column(EnemyArchetype arch, Vector2 dir, int n, float spacing)
    {
        if (n <= 0 || !TryEdgePoint(dir, 0.8f, out Vector2 head)) return 0;
        int made = 0;
        for (int i = 0; i < n; i++)
        {
            Vector2 at = head + dir * (spacing * i) + Rotate(dir, 90f) * Random.Range(-0.12f, 0.12f);
            if (!ValidSpawn(at)) continue;
            if (Spawn(arch, true, at) != null) made++;
        }
        return made;
    }

    // a ring of them round the screen, closing in all at once
    private int Ring(EnemyArchetype arch, int n)
    {
        if (n <= 0) return 0;
        GetView(out Vector2 centre, out float halfW, out float halfH);
        float r = Mathf.Sqrt(halfW * halfW + halfH * halfH) + 0.8f;
        float a0 = Random.value * Mathf.PI * 2f;
        int made = 0;
        for (int i = 0; i < n; i++)
        {
            float a = a0 + i * Mathf.PI * 2f / n;
            Vector2 at = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            if (!ValidSpawn(at)) continue;
            if (Spawn(arch, true, at) != null) made++;
        }
        return made;
    }

    // a flock of his corpse fire from one side
    private int Flock(EnemyArchetype arch, Vector2 dir, int n)
    {
        if (n <= 0 || !TryEdgePoint(dir, 1.2f, out Vector2 middle)) return 0;
        float radius = packSpread * Mathf.Sqrt(n) * 0.5f;
        int made = 0;
        for (int i = 0; i < n; i++)
        {
            Vector2 at = middle + dir * radius + Random.insideUnitCircle * radius;
            if (!ValidSpawn(at)) continue;
            if (Spawn(arch, true, at) != null) made++;
        }
        return made;
    }

    // a rush boss from `dir` (the map's, taken in turn by `index`, or the Magistrate), and his
    // retinue in file behind him. one whose statue stands near (Huangquan Road's guardians) steps
    // down from it instead
    private void SpawnMagistrate(Vector2 dir, int index = 0)
    {
        // a map with more than one takes turns by rush too: Huangquan's first rush is Ox-Head's,
        // its second Horse-Face's, its fourth both
        var arch = RushBoss(index + Mathf.Max(0, currentWave - 1));
        if (arch == null || arch.prefab == null) return;
        if (!TryEdgePoint(dir, 1.2f, out Vector2 at)) at = GetSpawnPositionNearOffscreenInsideBounds();
        if (GuardianStatue.Awaken(arch, playerTransform != null ? (Vector2)playerTransform.position : at, out Vector2 statue))
        {
            at = statue;
            Vector2 toward = playerTransform != null ? (Vector2)playerTransform.position - at : -dir;
            if (toward.sqrMagnitude > 0.01f) dir = -toward.normalized;
        }
        var go = Spawn(arch, true, at);
        if (go == null) return;
        if (go.TryGetComponent(out EnemyHealth h)) h.SetScaled(h.Max * Mathf.Pow(rushBossGrowth, Mathf.Max(0, currentWave - 1)));
        // he raises fewer of the dead in the early rushes: 2 at a time (4 up at once) in the first,
        // 3 (6) in the second, his full count from the third
        if (go.TryGetComponent(out BossMagistrate m))
        {
            int w = Mathf.Max(1, currentWave);
            m.minions = Mathf.Min(m.minions, 1 + w);
            m.maxMinions = Mathf.Min(m.maxMinions, 2 + 2 * w);
        }
        activeBosses.Add(go);
        bossesSpawnedThisRush++;
        EnvelopeCarrier.Attach(go, EnvelopeSource.Boss);
        int retinue = 4 + Mathf.Max(1, currentWave);
        for (int i = 0; i < retinue; i++)
        {
            Vector2 p = at + dir * (1.6f + 0.85f * i) + Rotate(dir, 90f) * ((i % 2 == 0 ? 1f : -1f) * 0.45f);
            if (ValidSpawn(p)) Spawn(PMain, true, p);
        }
        RingBell(0.8f);
    }

    // the priest's hand bell: a formation is coming
    private void RingBell(float pitch)
    {
        if (procBell == null) procBell = Resources.Load<AudioClip>("Sfx/rush_bell");
        if (procBell == null) return;
        var p = GameObject.FindGameObjectWithTag("Player");
        SfxPlayer.PlayAt(procBell, p != null ? p.transform.position : Vector3.zero, 0.7f, pitch * Random.Range(0.97f, 1.03f));
    }

    private void TrySpawnInitialBoss()
    {
        if (bossArchetype == null || bossArchetype.prefab == null) return;

        int maxBosses = GetMaxBossCountForWave(currentWave);
        if (GetAliveBossCount() >= maxBosses) return;

        var go = Spawn(bossArchetype, true);
        if (go != null)
        {
            activeBosses.Add(go);
            bossesSpawnedThisRush++;
            EnvelopeCarrier.Attach(go, EnvelopeSource.Boss);
            budget = Mathf.Max(0f, budget - bossArchetype.cost);
        }
    }

    private void HandleWaveCleared(int wave)
    {
        budgetPerSecond += spawnrateIncreasePerWave;
    }

    // a Final Rush won: the spirit seal's wave rolls out from the player and seals the horde
    private void HandlePurge(GameObject fx)
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) SealWave.Roll(player.transform.position);
        else StartCoroutine(PurgeInsideOut(fx));
    }

    private void HandleEnemyKilled(int id)
    {
        if (!finalRush || finalRushQuota <= 0) return;

        finalRushKills = Mathf.Min(finalRushKills + 1, finalRushQuota);
        rushBarDirty = true;    // redrawn once per frame, not per kill
    }

    private bool rushBarDirty;

    private bool IsBoss(EnemyArchetype arch) =>
        IsRushBoss(arch) || arch == finalBossArchetype || arch == secretBossArchetype;

    // made during loading, behind the loading screen, so the first waves don't pay for it
    private const int PrewarmPerEnemy = 24;

    private void PrewarmPools()
    {
        var seen = new HashSet<GameObject>();
        void Warm(EnemyArchetype a)
        {
            if (a == null || a.prefab == null || IsBoss(a) || !seen.Add(a.prefab)) return;
            ObjectPool.For(a.prefab).Prewarm(PrewarmPerEnemy);
            // and what they drop: a few heals (wen is data, see PickupSystem)
            if (a.prefab.TryGetComponent(out EnemyHealth h))
            {
                if (h.HealItemPrefab != null && seen.Add(h.HealItemPrefab)) ObjectPool.For(h.HealItemPrefab).Prewarm(8);
            }
        }
        if (activeTimeline != null)
            foreach (var beat in activeTimeline.beats)
                if (beat != null)
                    foreach (var p in beat.enemies)
                        if (p != null) Warm(p.archetype);
        if (pool != null) foreach (var a in pool) Warm(a);
        if (chunkyPool != null) foreach (var a in chunkyPool) Warm(a);
    }

    private void ResetFinalRushProgress()
    {
        finalRushKills = 0;
        UpdateFinalRushProgressBar();
    }

    private void UpdateFinalRushProgressBar()
    {
        if (!finalRushSlider) return;

        // the procession is won by its Magistrates, who are all on the field at once: no bar
        if (!finalRush || finalRushQuota <= 0 || ProcessionRuns)
        {
            finalRushSlider.gameObject.SetActive(false);
            return;
        }

        finalRushSlider.gameObject.SetActive(true);

        finalRushSlider.minValue = 0f;
        finalRushSlider.maxValue = finalRushQuota;
        finalRushSlider.value = finalRushKills;
    }

    public float GetRunTime()
    {
        return timeElapsed;
    }

    private IEnumerator PurgeInsideOut(GameObject fx)
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (!player) yield break;

        var enemies = new List<GameObject>(EnemyRegistry.All);
        enemies.Sort((a, b) =>
        {
            float da = a ? (a.transform.position - player.transform.position).sqrMagnitude : float.MaxValue;
            float db = b ? (b.transform.position - player.transform.position).sqrMagnitude : float.MaxValue;
            return da.CompareTo(db);
        });

        foreach (var e in enemies)
        {
            if (!e) continue;

            if (fx) Instantiate(fx, e.transform.position, Quaternion.identity);

            var eh = e.GetComponent<EnemyHealth>();
            if (eh != null)
            {
                float lethalDamage = eh.Current + eh.Max + 1f;
                eh.TakeDamage(lethalDamage, DamageKind.Silent);
            }
            else
            {
                Destroy(e);
            }

            yield return new WaitForSeconds(purgeStepDelay);
        }
    }
}
