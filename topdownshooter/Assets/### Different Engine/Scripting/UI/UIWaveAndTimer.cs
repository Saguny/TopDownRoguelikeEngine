using UnityEngine;
using TMPro;

public class UIWaveAndTimer : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI runTimerText;
    [SerializeField] private TextMeshProUGUI waveText;
    [SerializeField] private Color normalColor = default;
    [SerializeField] private Color rushColor = default;
    [Tooltip("the run clock while it's stopped, from a Final Rush won until the next wave starts")]
    [SerializeField] private Color frozenColor = new Color(0.6f, 0.85f, 1f, 1f);

    private Color waveColor;

    private void Awake()
    {
        if (!runTimerText || !waveText)
        {
            var tmps = GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in tmps)
            {
                var n = t.name.ToLowerInvariant();
                if (!runTimerText && (n.Contains("timer") || n.Contains("time"))) runTimerText = t;
                else if (!waveText && n.Contains("wave")) waveText = t;
            }
            if (!runTimerText && tmps.Length > 0) runTimerText = tmps[0];
            if (!waveText && tmps.Length > 1) waveText = tmps[1];
        }

        if (normalColor.a == 0f) normalColor = Color.white;
        if (rushColor.a == 0f) rushColor = new Color(1f, 0.25f, 0.25f, 1f);
        if (frozenColor.a == 0f) frozenColor = new Color(0.6f, 0.85f, 1f, 1f);
        if (waveText) waveColor = waveText.color;
    }

    private void Start()
    {
        if (runTimerText)
        {
            runTimerText.text = "00:00";
            runTimerText.color = normalColor;
            runTimerText.enabled = true;
            runTimerText.gameObject.SetActive(true);
        }
        if (waveText)
        {
            waveText.text = "";
            waveText.enabled = true;
            waveText.gameObject.SetActive(true);
        }
    }

    private void OnEnable()
    {
        GameEvents.OnRunTimeChanged += HandleRunTimeChanged;
        GameEvents.OnWaveStarted += HandleWaveStarted;
        GameEvents.OnFinalRushStarted += HandleRushStart;
        GameEvents.OnFinalRushEnded += HandleRushEnd;
        GameEvents.OnFinalBossStarted += HandleFinalBoss;
        GameEvents.OnFinalBossDefeated += HandleBossDown;
    }

    private void OnDisable()
    {
        GameEvents.OnRunTimeChanged -= HandleRunTimeChanged;
        GameEvents.OnWaveStarted -= HandleWaveStarted;
        GameEvents.OnFinalRushStarted -= HandleRushStart;
        GameEvents.OnFinalRushEnded -= HandleRushEnd;
        GameEvents.OnFinalBossStarted -= HandleFinalBoss;
        GameEvents.OnFinalBossDefeated -= HandleBossDown;
    }

    // no clock for the final boss: it's kill or be killed
    private void HandleFinalBoss()
    {
        if (waveText) waveText.text = "final boss";
        if (runTimerText) runTimerText.color = frozenColor;
    }

    // the boss is down: the run is won (RunVictory)
    private void HandleBossDown(Vector3 _)
    {
        if (waveText) waveText.text = "victory";
        if (runTimerText) runTimerText.color = normalColor;
    }

    private void HandleRunTimeChanged(float seconds)
    {
        if (!runTimerText) return;
        int s = Mathf.FloorToInt(seconds);
        runTimerText.text = $"{s / 60:00}:{s % 60:00}";
    }

    private void HandleWaveStarted(int waveNumber)
    {
        if (waveText) { waveText.text = $"wave {waveNumber}"; waveText.color = waveColor; }
        if (runTimerText) runTimerText.color = normalColor;
    }

    private void HandleRushStart(int waveNumber, int quota)
    {
        if (runTimerText) runTimerText.color = rushColor;
        if (waveText) waveText.text = $"Wave {waveNumber}";
    }

    // the clock stays stopped until the next wave starts (GameLoopController)
    private void HandleRushEnd(int waveNumber)
    {
        if (runTimerText) runTimerText.color = frozenColor;
        if (waveText) waveText.text = $"Wave {waveNumber} cleared";
    }
}
