using System;
using UnityEngine;

public static class GameEvents
{
    public static Action<float> OnRunTimeChanged;
    public static Action<int> OnWaveStarted;
    public static Action<int> OnWaveCleared;
    public static Action<int, int> OnFinalRushStarted;
    public static Action<int> OnFinalRushEnded;
    public static Action<int> OnEnemyKilled;
    public static Action<GameObject> OnPurgeEnemiesWithFx;
    public static Action OnCollectAllWen;
    public static Action<SecretBossBehavior> OnSecretBossSpawned;

    // normal mode ending: the last wave clears, the final boss spawns, killing it opens the exit
    public static Action OnFinalBossStarted;
    public static Action<Vector3> OnFinalBossDefeated;
    public static Action OnRunWon;
}
