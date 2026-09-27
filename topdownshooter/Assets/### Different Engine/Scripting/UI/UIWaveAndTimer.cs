using UnityEngine;
using TMPro;

public class UIWaveAndTimer : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI runTimerText;
    [SerializeField] private TextMeshProUGUI waveText;
    [SerializeField] private Color normalColor = default;
    [SerializeField] private Color rushColor = default;

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

    private void HandleFinalBoss()
    {
        if (waveText) waveText.text = "final boss";
        if (runTimerText) runTimerText.color = rushColor;
    }

    // the boss is down; the night goes on until the Wuchang come (RunTimeLimit)
    private void HandleBossDown(Vector3 _)
    {
        if (waveText) waveText.text = "survive the night";
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
        if (waveText) waveText.text = $"wave {waveNumber}";
        if (runTimerText) runTimerText.color = normalColor;
    }

    private void HandleRushStart(int waveNumber, int quota)
    {
        if (runTimerText) runTimerText.color = rushColor;
        if (waveText) waveText.text = $"Wave {waveNumber}";
    }

    private void HandleRushEnd(int waveNumber)
    {
        if (runTimerText) runTimerText.color = normalColor;
        if (waveText) waveText.text = $"Wave {waveNumber} cleared";
    }
}
