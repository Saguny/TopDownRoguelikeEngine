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
    [SerializeField, Min(1)] private int finalWave = 10;

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
                totalRun += Time.deltaTime;
                GameEvents.OnRunTimeChanged?.Invoke(totalRun);
                yield return null;
            }

            finalRush = true;
            waveKills = 0;
            float snap = (waveIndex + 1) * waveDuration;
            totalRun = snap;
            GameEvents.OnRunTimeChanged?.Invoke(totalRun);

            int quota = baseKillsToClear * (int)Mathf.Pow(1.4f, waveIndex);
            GameEvents.OnFinalRushStarted?.Invoke(waveIndex + 1, quota);

            while (waveKills < quota)
                yield return null;

            GameEvents.OnPurgeEnemiesWithFx?.Invoke(subjectiveDeathFx);
            GameEvents.OnCollectAllWen?.Invoke();

            GameEvents.OnFinalRushEnded?.Invoke(waveIndex + 1);
            GameEvents.OnWaveCleared?.Invoke(waveIndex + 1);

            yield return new WaitForSeconds(breakAfterWave);

            if (!GameMode.IsEndless && waveIndex + 1 >= finalWave)
            {
                GameEvents.OnFinalBossStarted?.Invoke();
                yield return BossClock();
                yield break;
            }

            waveIndex++;
        }
    }

    // no more waves once the final boss is up, but the run clock keeps counting so the end
    // screen reports the real time the run took
    private IEnumerator BossClock()
    {
        while (true)
        {
            totalRun += Time.deltaTime;
            GameEvents.OnRunTimeChanged?.Invoke(totalRun);
            yield return null;
        }
    }

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
