using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// switches the main menu between its pages, one page up at a time. a button calls Open with the
// page it leads to, a back button calls Back, and Escape or a gamepad's B does the same as Back.
// a new page (e.g. an upgrades page) needs nothing here: its button calls Open(UpgradesPanel)
// and its back button calls Back
public class MenuNavigator : MonoBehaviour
{
    [Tooltip("the page the menu starts on and Back ends at, e.g. MainPanel")]
    [SerializeField] private GameObject home;
    [Tooltip("pages hidden when the menu starts, so one left switched on while editing can't cover the home page. a page missing here still opens and closes fine")]
    [SerializeField] private List<GameObject> pages = new List<GameObject>();
    [Tooltip("seconds a page takes to fade in. 0 shows it at once")]
    [SerializeField, Min(0f)] private float fadeSeconds = 0.12f;
    [Tooltip("Escape and the gamepad's B button go back a page")]
    [SerializeField] private bool backOnCancel = true;

    // the pages behind the current one, most recent on top
    private readonly List<GameObject> history = new List<GameObject>();
    private GameObject current;
    private Coroutine fade;
    private CanvasGroup fading;

    public GameObject Current => current;

    private void Awake()
    {
        foreach (var page in pages)
            if (page != null && page != home) page.SetActive(false);

        current = home;
        if (home != null) home.SetActive(true);
    }

    private void Update()
    {
        if (!backOnCancel || history.Count == 0 || !CancelPressed()) return;

        // Escape on an open dropdown only closes the dropdown
        if (current != null)
            foreach (var dropdown in current.GetComponentsInChildren<TMP_Dropdown>())
                if (dropdown.IsExpanded) return;

        Back();
    }

    private static bool CancelPressed() =>
        (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
        (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);

    // shows the page and hides the current one. going to a page that's already behind this one
    // (like Home) goes back to it instead, so the history never loops
    public void Open(GameObject page)
    {
        if (page == null || page == current) return;

        int behind = history.LastIndexOf(page);
        if (behind >= 0)
            history.RemoveRange(behind, history.Count - behind);
        else if (page == home)
            history.Clear();
        else if (current != null)
            history.Add(current);

        Switch(page);
    }

    // returns to the page before this one
    public void Back()
    {
        if (history.Count == 0)
        {
            Home();
            return;
        }

        var previous = history[history.Count - 1];
        history.RemoveAt(history.Count - 1);
        Switch(previous);
    }

    public void Home()
    {
        if (home == null) return;
        history.Clear();
        Switch(home);
    }

    private void Switch(GameObject page)
    {
        if (page == current) return;

        if (current != null) current.SetActive(false);
        current = page;
        if (page == null) return;

        page.SetActive(true);

        StopFade();
        if (fadeSeconds > 0f) fade = StartCoroutine(FadeIn(page));
    }

    private IEnumerator FadeIn(GameObject page)
    {
        if (!page.TryGetComponent(out fading)) fading = page.AddComponent<CanvasGroup>();

        // unscaled, so the menu still fades if something left time paused
        for (float t = 0f; t < fadeSeconds; t += Time.unscaledDeltaTime)
        {
            fading.alpha = t / fadeSeconds;
            yield return null;
        }
        fading.alpha = 1f;
        fading = null;
        fade = null;
    }

    // a page cut off mid fade would otherwise keep its half alpha the next time it opens
    private void StopFade()
    {
        if (fade != null) StopCoroutine(fade);
        if (fading != null) fading.alpha = 1f;
        fade = null;
        fading = null;
    }

    private void OnDisable() => StopFade();
}
