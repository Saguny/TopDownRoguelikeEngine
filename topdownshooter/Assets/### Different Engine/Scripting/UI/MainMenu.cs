using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    [Header("scenes")]
    public string gameSceneName = "Scenes/MainTestGame";
    public string creditsSceneName = "CreditsScene";

    [Header("endless")]
    [Tooltip("optional. stays non interactable until endless is unlocked")]
    public Button endlessButton;
    [Tooltip("optional. says how many more runs until endless unlocks")]
    public TMP_Text endlessLockText;

    [Header("opening")]
    [Tooltip("empty: the scene's LoadingPanel")]
    public GameObject loadingPanel;
    [Tooltip("how long the loading screen plays when the game starts, before the menu opens")]
    [Min(0.1f)] public float openingLoadSeconds = 2f;

    bool isLoading;

    // the loading screen plays the first time the menu opens in a session; back from a run, the
    // menu opens straight out of black
    static bool opened;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOpened() => opened = false;

    void Start()
    {
        // out of black: a fade when there's a fader, the loading screen and the spiral otherwise
        if (ScreenFader.Instance != null)
            ScreenFader.Instance.FadeIn();
        else
            StartCoroutine(Opening());

        RefreshEndlessLock();
    }

    private void RefreshEndlessLock()
    {
        bool unlocked = RunProgress.EndlessUnlocked;

        if (endlessButton != null)
            endlessButton.interactable = unlocked;

        if (endlessLockText != null)
        {
            int left = RunProgress.RunsUntilEndless;
            endlessLockText.text = unlocked ? string.Empty : $"finish {left} more run{(left == 1 ? "" : "s")} to unlock";
        }
    }

    IEnumerator Opening()
    {
        var panel = loadingPanel != null ? loadingPanel : FindInScene("LoadingPanel");
        // every loading screen after this one is a copy of this panel
        SceneLoader.Remember(panel);
        bool first = !opened;
        opened = true;
        // arriving through a loading screen: it opens the menu itself
        if (SceneLoader.Busy) yield break;
        if (first && panel != null)
        {
            var animation = LoadingAnimation.On(panel);
            animation.fillSeconds = openingLoadSeconds;
            animation.opaque = true;
            panel.SetActive(true);
            while (!animation.Filled) yield return null;
            yield return animation.Finale();
            SpiralReveal.Play();
            panel.SetActive(false);
            animation.opaque = false;
        }
        else
        {
            SpiralReveal.Play();
        }
    }

    GameObject FindInScene(string name)
    {
        foreach (var root in gameObject.scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t.gameObject;
        return null;
    }

    public void PlayGame() => StartRun(RunMode.Normal);

    // hook a second menu button to this. refuses while locked, even if the button isn't wired
    // to endlessButton, so the lock can't be skipped by a button that forgot to grey out
    public void PlayEndless()
    {
        if (!RunProgress.EndlessUnlocked) return;
        StartRun(RunMode.Endless);
    }

    private void StartRun(RunMode mode)
    {
        if (isLoading) return;
        isLoading = true;

        GameMode.Current = mode;

        if (ScreenFader.Instance != null)
            ScreenFader.Instance.FadeAndLoad(gameSceneName);
        else
            SceneManager.LoadScene(gameSceneName);
    }

    public void OpenCredits()
    {
        if (isLoading) return;
        isLoading = true;

        if (ScreenFader.Instance != null)
            ScreenFader.Instance.FadeAndLoad(creditsSceneName);
        else
            SceneManager.LoadScene(creditsSceneName);
    }

    public void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
