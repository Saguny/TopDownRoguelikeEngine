using System.Collections;
using UnityEngine;

public class GameLoopController : MonoBehaviour
{
    [SerializeField] private float waveDuration = 180f;
    [Tooltip("kills the first Final Rush asks for")]
    [SerializeField] private int baseKillsToClear = 150;
    [Tooltip("how much more each Final Rush asks for than the one before: 1.3 = 30% more")]
    [SerializeField, Min(1f)] private float quotaGrowth = 1.3f;
    [SerializeField] private float breakAfterWave = 2f;
    [SerializeField] private GameObject subjectiveDeathFx;

    [Header("normal mode ending")]
    [Tooltip("in normal mode, the last wave: the final boss comes as it starts, and it runs until the time limit (RunTimeLimit, 30:00). 10 waves of 3 minutes: it starts at 27:00")]
    [SerializeField, Min(1)] private int finalWave = 10;
    [Tooltip("in normal mode the final boss comes by this run minute at the latest, even mid wave. a fallback: the last wave brings it first")]
    [SerializeField, Min(1f)] private float finalBossByMinute = 28f;

    // new: scene ui reference
    [Header("secret boss ui")]
    [SerializeField] private SecretBossHallucinationUI hallucinationUi;

    private int waveIndex;
    private float elapsed;
    private int waveKills;
    private bool finalRush;
    private float totalRun;
    private Coroutine loop;
    private float jumpElapsed = -1f;     // dev tools: where in its wave a jump lands
    private bool bossStarted;

    private void OnEnable()
    {
        GameEvents.OnEnemyKilled += OnEnemyKilled;
        GameEvents.OnSecretBossSpawned += OnSecretBossSpawned;
        GameEvents.OnFinalBossStarted += OnFinalBossStarted;
    }

    private void OnDisable()
    {
        GameEvents.OnEnemyKilled -= OnEnemyKilled;
        GameEvents.OnSecretBossSpawned -= OnSecretBossSpawned;
        GameEvents.OnFinalBossStarted -= OnFinalBossStarted;
    }

    private void OnFinalBossStarted()
    {
        bossStarted = true;
        HealToFull();   // a boss that comes on the clock, with no rush before it, too
    }

    private static void HealToFull()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null || !player.TryGetComponent(out PlayerHealth health) || health.IsDead) return;
        if (health.Current < health.Max) health.Heal(health.Max - health.Current);
    }

    private void Start()
    {
        // normal runs end at the time limit if nothing has ended them before
        if (!TryGetComponent(out RunTimeLimit _)) gameObject.AddComponent<RunTimeLimit>();
        loop = StartCoroutine(Loop());
    }

    // this is where we inject the scene ui into the spawned prefab
    private void OnSecretBossSpawned(SecretBossBehavior boss)
    {
        if (boss == null || hallucinationUi == null) return;

        boss.Init(hallucinationUi.gameObject);
    }

    private IEnumerator Loop()
    {
        while (true)
        {
            // normal mode's last wave is the final boss's: it comes as the wave starts, the crowd
            // keeps coming, and the night goes on until the time runs out (RunTimeLimit)
            if (!GameMode.IsEndless && waveIndex + 1 >= finalWave)
            {
                GameEvents.OnWaveStarted?.Invoke(waveIndex + 1);
                GameEvents.OnFinalBossStarted?.Invoke();
                yield return BossClock();
                yield break;
            }

            GameEvents.OnWaveStarted?.Invoke(waveIndex + 1);
            elapsed = 0f;
            if (jumpElapsed >= 0f) { elapsed = jumpElapsed; jumpElapsed = -1f; }
            waveKills = 0;
            finalRush = false;

            while (elapsed < waveDuration)
            {
                elapsed += Time.deltaTime;
                SecondsToRush = waveDuration - elapsed;
                Tick();
                // out of time for waves: straight to the final boss
                if (BossIsDue)
                {
                    GameEvents.OnFinalBossStarted?.Invoke();
                    yield return BossClock();
                    yield break;
                }
                yield return null;
            }

            // the run clock stops for the Final Rush and stays stopped until the next wave starts:
            // the rush, the seal's wave, the wen swept in and the boss's envelope are all extra
            SecondsToRush = float.MaxValue;
            finalRush = true;
            waveKills = 0;

            // the kill quota, for a scene without the procession (150, 195, 254 ... 1224 by default)
            int quota = Mathf.Max(1, Mathf.RoundToInt(baseKillsToClear * Mathf.Pow(quotaGrowth, waveIndex)));
            GameEvents.OnFinalRushStarted?.Invoke(waveIndex + 1, quota);

            // a rush ends when its Magistrates have all come and fallen (SpawnDirector's procession);
            // one still going when the final boss is due ends there, so the boss gets its time
            while (!RushWon(quota) && !BossIsDue)
                yield return null;

            // won: nothing more spawns until the next wave, the spirit seal's wave rolls out and
            // seals the horde (SealWave), and once it has passed, the wen pours in
            GameEvents.OnFinalRushEnded?.Invoke(waveIndex + 1);
            // the last rush won: the player goes into the final boss whole
            if (!GameMode.IsEndless && (waveIndex + 2 >= finalWave || BossIsDue)) HealToFull();
            GameEvents.OnPurgeEnemiesWithFx?.Invoke(subjectiveDeathFx);
            yield return null;
            while (SealWave.Running) yield return null;
            GameEvents.OnCollectAllWen?.Invoke();
            GameEvents.OnWaveCleared?.Invoke(waveIndex + 1);

            for (float t = 0f; t < breakAfterWave; t += Time.deltaTime)
                yield return null;

            // the next wave waits for the rush boss's envelope to be picked up and opened
            while (BossEnvelopeWaiting)
            {
                AwaitingEnvelope = true;
                yield return null;
            }
            AwaitingEnvelope = false;

            if (BossIsDue)
            {
                GameEvents.OnFinalBossStarted?.Invoke();
                yield return BossClock();
                yield break;
            }

            waveIndex++;
        }
    }

    // no more waves once the final boss is up, and no clock either: the fight runs until the boss
    // or the player falls (the end screen's time played is kept by RunStats)
    private IEnumerator BossClock()
    {
        while (true) yield return null;
    }

    private void Tick()
    {
        totalRun += Time.deltaTime;
        GameEvents.OnRunTimeChanged?.Invoke(totalRun);
    }

    // true while a cleared Final Rush waits on its boss's envelope (UIWaveAndTimer says so)
    public static bool AwaitingEnvelope { get; private set; }

    // seconds until this wave's Final Rush begins; very large outside a wave's run up to one
    public static float SecondsToRush { get; private set; } = float.MaxValue;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { AwaitingEnvelope = false; SecondsToRush = float.MaxValue; }

    private void OnDestroy() => SecondsToRush = float.MaxValue;

    private static bool BossEnvelopeWaiting
    {
        get
        {
            if (EnvelopeOpening.Busy) return true;
            var lying = FortuneEnvelope.Lying;
            for (int i = 0; i < lying.Count; i++)
                if (lying[i] != null && lying[i].Source != EnvelopeSource.Elite) return true;
            return false;
        }
    }

    private bool RushWon(int quota) => SpawnDirector.Active != null ? SpawnDirector.Active.RushWon(waveKills, quota) : waveKills >= quota;

    private bool BossIsDue => !GameMode.IsEndless && totalRun >= finalBossByMinute * 60f;

    private void OnEnemyKilled(int _) { if (finalRush) waveKills++; }

    // dev tools: jump the run clock ahead to test later parts of a stage. going past the end of
    // the wave starts its Final Rush, same as waiting would
    public float RunSeconds => totalRun;
    public bool InFinalRush => finalRush;
    public bool FinalBossStarted => bossStarted;
    // the run minute the final boss comes by, for the dev tools
    public float FinalBossDueSeconds => finalBossByMinute * 60f;

    // dev tools: straight to the end of a normal run, the final boss `lead` seconds away. the clock
    // is set just short of when it's due, in the last wave before its own, far enough from that
    // wave's end that no Final Rush comes first. false when there's no final boss to jump to
    public bool JumpToFinalBoss(float lead)
    {
        if (GameMode.IsEndless || finalRush || bossStarted) return false;
        lead = Mathf.Clamp(lead, 0.5f, waveDuration - 2f);
        float at = FinalBossDueSeconds - lead;
        if (at <= totalRun) return false;
        waveIndex = Mathf.Max(0, finalWave - 2);
        jumpElapsed = waveDuration - lead - 1f;
        totalRun = at;
        if (loop != null) StopCoroutine(loop);
        loop = StartCoroutine(Loop());
        GameEvents.OnRunTimeChanged?.Invoke(totalRun);
        return true;
    }

    // dev tools: the run clock set to `seconds`, in the wave that holds it, as if played to there.
    // not during a Final Rush (its spawner and envelope are mid-way)
    public void JumpTo(float seconds)
    {
        if (finalRush || seconds <= totalRun) return;
        int last = GameMode.IsEndless ? int.MaxValue : finalWave - 2;
        waveIndex = Mathf.Clamp(Mathf.FloorToInt(seconds / waveDuration), 0, last);
        jumpElapsed = Mathf.Clamp(seconds - waveIndex * waveDuration, 0f, waveDuration);
        totalRun = seconds;
        if (loop != null) StopCoroutine(loop);
        loop = StartCoroutine(Loop());
        GameEvents.OnRunTimeChanged?.Invoke(totalRun);
    }

    public void SkipAhead(float seconds)
    {
        if (finalRush || seconds <= 0f) return;
        elapsed += seconds;
        totalRun += seconds;
        GameEvents.OnRunTimeChanged?.Invoke(totalRun);
    }
}
