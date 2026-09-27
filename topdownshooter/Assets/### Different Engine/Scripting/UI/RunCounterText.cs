using TMPro;
using UnityEngine;

// a HUD number for this run: kills, or wen picked up. put it on the text and pick which
[RequireComponent(typeof(TMP_Text))]
public class RunCounterText : MonoBehaviour
{
    public enum Counter { Kills, WenPickedUp }

    [SerializeField] private Counter counter = Counter.Kills;
    [Tooltip("{0} is the count")]
    [SerializeField] private string format = "{0}";
    [Tooltip("how much the text swells for a moment when the number goes up. 0 = none")]
    [SerializeField, Min(0f)] private float punch = 0.15f;

    private TMP_Text text;
    private int shown = -1;
    private float punchTime;
    private Vector3 baseScale;

    private void Awake()
    {
        text = GetComponent<TMP_Text>();
        baseScale = transform.localScale;
    }

    private bool dirty;

    private void OnEnable()
    {
        RunStats.Changed += MarkDirty;
        Show();
    }

    private void OnDisable()
    {
        RunStats.Changed -= MarkDirty;
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
        int value = counter == Counter.Kills ? RunStats.Kills : RunStats.WenPickedUp;
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
