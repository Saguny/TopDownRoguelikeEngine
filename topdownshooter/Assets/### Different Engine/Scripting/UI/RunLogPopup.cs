using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// the game over screen's Log This Run button and the note it drops in at the top of the screen:
// the button writes the run out (RunLog) and the note slides down saying where to, stays a few
// seconds (longer while the pointer is on it) and fades. clicking the note opens the folder,
// the run's file picked out. it sits on the note itself; the note is hidden until the button is
// pressed. all on unscaled time: the game is stopped behind the screen
[RequireComponent(typeof(RectTransform))]
public class RunLogPopup : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("the note's text. empty = the first text under it")]
    [SerializeField] private TMP_Text text;
    [Tooltip("{0} is the folder the run was written to")]
    [SerializeField] private string format = "Run saved to: \"{0}\"";
    [SerializeField] private string failed = "Couldn't save the run";
    [SerializeField, Min(0.5f)] private float showSeconds = 5f;
    [SerializeField, Min(0.01f)] private float fadeSeconds = 0.3f;
    [Tooltip("how far above its place the note slides in from")]
    [SerializeField] private float slide = 40f;

    private CanvasGroup group;
    private RectTransform rt;
    private Vector2 home;
    private string file;
    private float shownAt = -1f, left;
    private bool hovered;

    private void Awake()
    {
        rt = (RectTransform)transform;
        home = rt.anchoredPosition;
        if (text == null) text = GetComponentInChildren<TMP_Text>(true);
        if (!TryGetComponent(out group)) group = gameObject.AddComponent<CanvasGroup>();
        if (TryGetComponent(out Button button)) button.onClick.AddListener(Open);
        Hide();
    }

    // the Log This Run button
    public void LogRun()
    {
        var screen = GetComponentInParent<GameOverScreen>(true);
        if (screen == null) screen = FindFirstObjectByType<GameOverScreen>();
        file = RunLog.Save(screen != null && screen.Won);
        if (text != null) text.text = file != null ? string.Format(format, System.IO.Path.GetDirectoryName(file)) : failed;
        gameObject.SetActive(true);
        shownAt = Time.unscaledTime;
        left = showSeconds;
        group.blocksRaycasts = group.interactable = file != null;
    }

    // a click on the note
    public void Open()
    {
        if (file == null) return;
        RunLog.Reveal(file);
        left = Mathf.Max(left, 1.5f);   // a moment more, so it doesn't vanish under the click
    }

    public void OnPointerEnter(PointerEventData _) => hovered = true;
    public void OnPointerExit(PointerEventData _) => hovered = false;

    private void Update()
    {
        if (shownAt < 0f) return;
        float dt = Time.unscaledDeltaTime;
        if (!hovered) left -= dt;

        // in: a quick drop with a little overshoot. out: a fade
        float t = Mathf.Clamp01((Time.unscaledTime - shownAt) / 0.22f);
        float drop = 1f - Mathf.Pow(1f - t, 3f) + Mathf.Sin(t * Mathf.PI) * 0.08f;
        rt.anchoredPosition = home + new Vector2(0f, slide * (1f - drop));
        group.alpha = Mathf.Min(t * 2f, Mathf.Clamp01(left / fadeSeconds));
        if (left <= 0f) Hide();
    }

    private void Hide()
    {
        shownAt = -1f;
        hovered = false;
        group.alpha = 0f;
        group.blocksRaycasts = group.interactable = false;
        rt.anchoredPosition = home;
    }
}
