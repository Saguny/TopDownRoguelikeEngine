using System.Collections;
using UnityEngine;

public class GameLoopController : MonoBehaviour
{
    [SerializeField] private float waveDuration = 180f;
    [Tooltip("kills the first Final Rush asks for")]
    [SerializeField] private int baseKillsToClear = 150;
    [Tooltip("how much more each Final Rush asks for than the one before: 1.3 = 30% more")]
    [SerializeField, Min(1f)] private float quotaGrowth = 1.25f;
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

    private void OnEnable()
    {
        GameEvents.OnEnemyKilled += OnEnemyKilled;
        GameEvents.OnSecretBossSpawned += OnSecretBossSpawned;
    }

    private void OnDisable()
    {
        GameEvents.OnEnemyKilled -= OnEnemyKilled;
        GameEvents.OnSecretBossSpawned -= OnSecretBossSpawned;
    }

    private void Start()
    {
        // normal runs end at the time limit if nothing has ended them before
        if (!TryGetComponent(out RunTimeLimit _)) gameObject.AddComponent<RunTimeLimit>();
        StartCoroutine(Loop());
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
            waveKills = 0;
            finalRush = false;

            while (elapsed < waveDuration)
            {
                elapsed += Time.deltaTime;
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
            finalRush = true;
            waveKills = 0;

            // 150, 188, 234, 293, 366, 458 by default: every rush a little more than the last
            int quota = Mathf.Max(1, Mathf.RoundToInt(baseKillsToClear * Mathf.Pow(quotaGrowth, waveIndex)));
            GameEvents.OnFinalRushStarted?.Invoke(waveIndex + 1, quota);

            // a rush ends with its quota met and its bosses down (they carry fortune envelopes); one
            // still going when the final boss is due ends there, so the boss gets its time
            while ((waveKills < quota || RushBossesUp) && !BossIsDue)
                yield return null;

            // won: nothing more spawns until the next wave, the spirit seal's wave rolls out and
            // seals the horde (SealWave), and once it has passed, the wen pours in
            GameEvents.OnFinalRushEnded?.Invoke(waveIndex + 1);
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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => AwaitingEnvelope = false;

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

    private static bool RushBossesUp => SpawnDirector.Active != null && SpawnDirector.Active.HasAliveBosses();

    private bool BossIsDue => !GameMode.IsEndless && totalRun >= finalBossByMinute * 60f;

    private void OnEnemyKilled(int _) { if (finalRush) waveKills++; }

    // dev tools: jump the run clock ahead to test later parts of a stage. going past the end of
    // the wave starts its Final Rush, same as waiting would
    public void SkipAhead(float seconds)
    {
        if (finalRush || seconds <= 0f) return;
        elapsed += seconds;
        totalRun += seconds;
        GameEvents.OnRunTimeChanged?.Invoke(totalRun);
    }
}
