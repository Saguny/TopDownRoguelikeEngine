using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// a normal run won: its final boss is down. the horde's already gone (SpawnDirector); the level
// ups stop, the boss's envelope waits to be opened, the last wen is swept in, then gold glow floods
// the screen and the results come up as a run won, and the glow clears off them. the final boss
// runs on no clock, so this is the only way a normal run is won
public class RunVictory : MonoBehaviour
{
    public static bool Running { get; private set; }
    // true while it waits on the final boss's envelope (UIWaveAndTimer asks for it)
    public static bool AwaitingEnvelope { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Running = false; AwaitingEnvelope = false; }

    private Image glow;

    public static void Begin()
    {
        if (Running) return;
        Running = true;
        new GameObject("Run Victory").AddComponent<RunVictory>().StartCoroutine(nameof(Win));
    }

    private void OnDestroy()
    {
        Running = false;
        AwaitingEnvelope = false;
    }

    private IEnumerator Win()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && player.TryGetComponent(out PlayerInventory inventory)) inventory.LockLeveling();
        yield return new WaitForSeconds(1.5f);

        // its envelope first, if it dropped one
        while (EnvelopeWaiting())
        {
            AwaitingEnvelope = true;
            yield return null;
        }
        AwaitingEnvelope = false;
        PickupSystem.Sweep(1f);
        yield return new WaitForSeconds(2.2f);

        if (BGMManager.Instance != null) BGMManager.Instance.EndBossTheme(2.5f);
        var screen = YamaScreenIfAny();
        if (screen != null) screen.EndFight();

        // gold glow floods in over real time (the results stop the game), then the results
        MakeLight();
        for (float t = 0f; t < 1.3f; t += Time.unscaledDeltaTime)
        {
            float k = t / 1.3f;
            glow.color = new Color(1f, 0.93f, 0.72f, k * k);
            yield return null;
        }
        glow.color = new Color(1f, 0.95f, 0.8f, 1f);
        GameEvents.OnRunWon?.Invoke();
        var over = player != null && player.TryGetComponent(out PlayerHealth h) ? h.GameOverScreen : null;
        if (over == null) over = FindFirstObjectByType<GameOverScreen>(FindObjectsInactive.Include);
        if (over != null) over.ShowVictory();
        yield return new WaitForSecondsRealtime(0.15f);
        for (float t = 0f; t < 0.9f; t += Time.unscaledDeltaTime)
        {
            glow.color = new Color(1f, 0.95f, 0.8f, 1f - t / 0.9f);
            yield return null;
        }
        Destroy(glow.canvas.gameObject);
        Destroy(gameObject);
    }

    private static bool EnvelopeWaiting()
    {
        if (EnvelopeOpening.Busy) return true;
        var lying = FortuneEnvelope.Lying;
        for (int i = 0; i < lying.Count; i++)
            if (lying[i] != null && lying[i].Source == EnvelopeSource.FinalBoss) return true;
        return false;
    }

    private static YamaScreen YamaScreenIfAny() => FindFirstObjectByType<YamaScreen>();

    private void MakeLight()
    {
        var go = new GameObject("Victory Light", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;          // over the results as they come up, then gone
        glow = new GameObject("Light", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        glow.rectTransform.SetParent(go.transform, false);
        glow.rectTransform.anchorMin = Vector2.zero;
        glow.rectTransform.anchorMax = Vector2.one;
        glow.rectTransform.sizeDelta = Vector2.zero;
        glow.raycastTarget = false;
        glow.color = new Color(1f, 0.93f, 0.72f, 0f);
    }
}
