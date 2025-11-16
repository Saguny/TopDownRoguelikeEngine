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
    public static Action OnCollectAllGears;
    public static Action<SecretBossBehavior> OnSecretBossSpawned;
}
