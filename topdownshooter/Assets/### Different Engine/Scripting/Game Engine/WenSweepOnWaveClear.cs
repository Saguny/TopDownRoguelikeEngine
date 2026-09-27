using System.Collections;
using UnityEngine;

// a wave cleared: after a beat, every wen on the ground flies to the player
public class WenSweepOnWaveClear : MonoBehaviour
{
    [SerializeField] private float delay = 0.8f;
    [SerializeField] private float duration = 0.5f;

    private void OnEnable()
    {
        GameEvents.OnWaveCleared += HandleWaveCleared;
    }

    private void OnDisable()
    {
        GameEvents.OnWaveCleared -= HandleWaveCleared;
    }

    private void HandleWaveCleared(int _)
    {
        StartCoroutine(PullAfterDelay());
    }

    private IEnumerator PullAfterDelay()
    {
        yield return new WaitForSeconds(delay);
        PickupSystem.Sweep(duration);
    }
}
