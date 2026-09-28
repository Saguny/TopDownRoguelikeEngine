using UnityEngine;
using TMPro;

// the run clock and the wave. the wave text only ever reads "Wave n" and is always up; the clock
// turns red through a Final Rush and, while it's stopped (a rush won until the next wave, and the
// final boss, who runs on no clock), wears HudTextFx's blue and red outline
public class UIWaveAndTimer : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI runTimerText;
    [SerializeField] private TextMeshProUGUI waveText;
    [SerializeField] private Color normalColor = default;
    [SerializeField] private Color rushColor = default;
    [Tooltip("the run clock while it's stopped, from a Final Rush won until the next wave starts")]
    [SerializeField] private Color frozenColor = new Color(0.6f, 0.85f, 1f, 1f);

    private Color waveColor;
    private HudTextFx timerFx;
    private int wave = 1;

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
        timerFx = HudTextFx.On(runTimerText);
        HudTextFx.On(waveText);
    }

    // nothing else may hide it or say anything else in it
    private void LateUpdate()
    {
        if (!waveText) return;
        if (!waveText.gameObject.activeSelf) waveText.gameObject.SetActive(true);
        if (!waveText.enabled) waveText.enabled = true;
        string want = $"Wave {wave}";
        if (waveText.text != want) waveText.text = want;
    }

    private void Frozen(bool frozen)
    {
        if (timerFx != null) timerFx.Frozen = frozen;
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
            waveText.text = $"Wave {wave}";
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
        if (runTimerText) runTimerText.color = frozenColor;
        Frozen(true);
    }

    // the boss is down: the run is won (RunVictory)
    private void HandleBossDown(Vector3 _)
    {
        if (runTimerText) runTimerText.color = normalColor;
        Frozen(false);
    }

    private void HandleRunTimeChanged(float seconds)
    {
        if (!runTimerText) return;
        int s = Mathf.FloorToInt(seconds);
        runTimerText.text = $"{s / 60:00}:{s % 60:00}";
    }

    private void HandleWaveStarted(int waveNumber)
    {
        wave = Mathf.Max(1, waveNumber);
        if (waveText) { waveText.text = $"Wave {wave}"; waveText.color = waveColor; }
        if (runTimerText) runTimerText.color = normalColor;
        Frozen(false);
    }

    private void HandleRushStart(int waveNumber, int quota)
    {
        wave = Mathf.Max(1, waveNumber);
        if (runTimerText) runTimerText.color = rushColor;
        Frozen(false);
    }

    // the clock stays stopped until the next wave starts (GameLoopController)
    private void HandleRushEnd(int waveNumber)
    {
        if (runTimerText) runTimerText.color = frozenColor;
        Frozen(true);
    }
}
