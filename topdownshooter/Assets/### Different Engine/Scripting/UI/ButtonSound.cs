using UnityEngine;
using UnityEngine.UI;

// put on every button by ButtonSounds: plays the click (or the back sound) when the button is
// pressed, by mouse, touch, keyboard or pad. it listens to the button's own click, which only goes
// off for a real press, so a button that switches itself off as it's pressed (Start Game does, so
// it can't be pressed twice) still clicks. picking an upgrade on the level up cards is silent: the
// level up has its own sound
public class ButtonSound : MonoBehaviour
{
    private Button button;
    private bool goingBack, silent;

    private void Awake()
    {
        button = GetComponent<Button>();
        if (button == null) return;
        button.onClick.AddListener(Clicked);

        string n = name.ToLowerInvariant();
        goingBack = n.Contains("back") || n.Contains("close") || n.Contains("return") || n.Contains("cancel") || n.Contains("resume");
        silent = GetComponentInParent<UpgradeMenuUI>(true) != null;
    }

    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(Clicked);
    }

    private void Clicked()
    {
        if (silent) return;
        // its own click sound wins
        if (TryGetComponent(out UIButtonSound own) && own.clickSound != null && own.audioSource != null) return;
        ButtonSounds.Play(goingBack);
    }
}
