using TMPro;
using UnityEngine;

// shows the tickets (RunCredits) not yet put on an item, out of the most that can be held, e.g.
// 2/3 on the menu's top bar, and keeps it current as the picker spends them
[RequireComponent(typeof(TMP_Text))]
public class TicketsText : MonoBehaviour
{
    [Tooltip("{0} is the tickets left to spend, {1} the most that can be held")]
    [SerializeField] private string format = "{0}/{1}";

    private TMP_Text text;

    private void Awake() => text = GetComponent<TMP_Text>();

    private void OnEnable()
    {
        RunCredits.Changed += Show;
        Show();
    }

    private void OnDisable() => RunCredits.Changed -= Show;

    private void Show()
    {
        if (text == null) text = GetComponent<TMP_Text>();
        text.text = string.Format(format, RunCredits.Count, RunCredits.Max);
    }
}
