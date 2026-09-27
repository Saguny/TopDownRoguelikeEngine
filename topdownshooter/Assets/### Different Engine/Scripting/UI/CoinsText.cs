using TMPro;
using UnityEngine;

// shows the coin balance, e.g. on the menu's top bar, and keeps it current
[RequireComponent(typeof(TMP_Text))]
public class CoinsText : MonoBehaviour
{
    [Tooltip("{0} is the balance")]
    [SerializeField] private string format = "{0}";

    private TMP_Text text;

    private void Awake() => text = GetComponent<TMP_Text>();

    private void OnEnable()
    {
        Coins.Changed += Show;
        Show();
    }

    private void OnDisable() => Coins.Changed -= Show;

    private void Show()
    {
        if (text == null) text = GetComponent<TMP_Text>();
        text.text = string.Format(format, Coins.Balance);
    }
}
