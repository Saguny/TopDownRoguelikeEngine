using System.Collections;
using UnityEngine;

public class GameLoopController : MonoBehaviour
{
    [SerializeField] private float waveDuration = 180f;
    [SerializeField] private int baseKillsToClear = 25;
    [SerializeField] private float breakAfterWave = 2f;
    [SerializeField] private GameObject subjectiveDeathFx;

    [Header("normal mode ending")]
    [Tooltip("in normal mode, clearing this wave starts the final boss instead of the next wave")]
    [SerializeField, Min(1)] private int finalWave = 6;
    [Tooltip("in normal mode the final boss comes by this run minute at the latest, even mid wave, so it's up well before the time limit (RunTimeLimit, 30:00)")]
    [SerializeField, Min(1f)] private float finalBossByMinute = 25f;

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

            // the run clock keeps counting through the Final Rush, so it's the real time played
            // and a normal run can't outlast its 30 minutes however long the rushes take
            finalRush = true;
            waveKills = 0;

            int quota = baseKillsToClear * (int)Mathf.Pow(1.4f, waveIndex);
            GameEvents.OnFinalRushStarted?.Invoke(waveIndex + 1, quota);

            // a rush still going when the final boss is due ends there, so the boss gets its time
            while (waveKills < quota && !BossIsDue)
            {
                Tick();
                yield return null;
            }

            GameEvents.OnPurgeEnemiesWithFx?.Invoke(subjectiveDeathFx);
            GameEvents.OnCollectAllWen?.Invoke();

            GameEvents.OnFinalRushEnded?.Invoke(waveIndex + 1);
            GameEvents.OnWaveCleared?.Invoke(waveIndex + 1);

            for (float t = 0f; t < breakAfterWave; t += Time.deltaTime)
            {
                Tick();
                yield return null;
            }

            if (!GameMode.IsEndless && (waveIndex + 1 >= finalWave || BossIsDue))
            {
                GameEvents.OnFinalBossStarted?.Invoke();
                yield return BossClock();
                yield break;
            }

            waveIndex++;
        }
    }

    // no more waves once the final boss is up, but the run clock keeps counting so the end
    // screen reports the real time the run took, and the time limit still comes
    private IEnumerator BossClock()
    {
        while (true)
        {
            Tick();
            yield return null;
        }
    }

    private void Tick()
    {
        totalRun += Time.deltaTime;
        GameEvents.OnRunTimeChanged?.Invoke(totalRun);
    }

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
