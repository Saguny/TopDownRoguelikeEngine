using TMPro;
using UnityEngine;

// a HUD number for this run: kills, qi gathered, or coins. put it on the text and pick which.
// Coins Earned shows what the run's fortune envelopes have paid, the only place coins come from
// (the level up's String of Wen shows on the end screen, not here)
[RequireComponent(typeof(TMP_Text))]
public class RunCounterText : MonoBehaviour
{
    // only ever add at the end: scenes store these as numbers
    public enum Counter { Kills, WenPickedUp, CoinsEarned }

    [SerializeField] private Counter counter = Counter.Kills;
    [Tooltip("{0} is the count")]
    [SerializeField] private string format = "{0}";
    [Tooltip("how much the text swells for a moment when the number goes up. 0 = none")]
    [SerializeField, Min(0f)] private float punch = 0.15f;
    [Tooltip("the HUD's animated outline and gold glint (HudTextFx)")]
    [SerializeField] private bool animatedOutline = true;

    private TMP_Text text;
    private int shown = -1;
    private float punchTime;
    private Vector3 baseScale;

    private void Awake()
    {
        text = GetComponent<TMP_Text>();
        baseScale = transform.localScale;
        if (animatedOutline) HudTextFx.On(text);
    }

    private bool dirty;

    private void OnEnable()
    {
        RunStats.Changed += MarkDirty;
        Coins.Changed += MarkDirty;     // envelopes pay coins without a pickup
        Show();
    }

    private void OnDisable()
    {
        RunStats.Changed -= MarkDirty;
        Coins.Changed -= MarkDirty;
        transform.localScale = baseScale;
    }

    // a kill or a pickup only flags it; the text is rewritten at most once per frame
    private void MarkDirty() => dirty = true;

    private void LateUpdate()
    {
        if (!dirty) return;
        dirty = false;
        Show();
    }

    private void Show()
    {
        int value = counter == Counter.Kills ? RunStats.Kills
            : counter == Counter.WenPickedUp ? RunStats.WenPickedUp
            : Coins.FromEnvelopesThisRun;
        if (value == shown) return;
        if (value > shown && shown >= 0) punchTime = 0.12f;
        shown = value;
        text.text = string.Format(format, value);
    }

    private void Update()
    {
        if (punchTime <= 0f) return;
        punchTime -= Time.unscaledDeltaTime;
        float k = Mathf.Clamp01(punchTime / 0.12f);
        transform.localScale = baseScale * (1f + punch * k);
    }
}
