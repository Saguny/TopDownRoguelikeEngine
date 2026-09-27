using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

// the screen at the end of a run, won or lost. it switches the panel on and slams it in
// (ScaleIn), counts the run towards Endless and stops the game behind it. what's on
// the panel fills itself as it opens: the numbers (RunStatText), the weapons (WeaponStatsList)
// and what the run unlocked (UnlockList). the player's death opens it once the body has lain
// there a moment (PlayerHealth); the Wuchang taking them at the time limit opens it as a win (SoulTaking)
public class GameOverScreen : MonoBehaviour
{
    [Tooltip("the whole screen, switched on when the run ends")]
    [SerializeField, FormerlySerializedAs("gameOverUI")] private GameObject panel;
    [Tooltip("optional: a screen of its own for a win. empty = the one above for both")]
    [SerializeField, FormerlySerializedAs("victoryUI")] private GameObject victoryPanel;

    [Header("Endless")]
    [Tooltip("optional: NEW BEST! or the time to beat")]
    [SerializeField] private TMP_Text bestText;

    [Header("Slam in")]
    [Tooltip("how many times its size the screen starts at")]
    [SerializeField, Min(1f)] private float startScale = 3.2f;
    [SerializeField, Min(0.05f)] private float slamSeconds = 0.45f;

    private const string BestKey = "endless_best_seconds";

    // death and victory can't both land, and a run is only counted once
    public bool Shown { get; private set; }

    public void ShowDefeat() => Show(false);
    public void ShowVictory() => Show(true);

    private void Show(bool won)
    {
        if (Shown) return;
        Shown = true;
        RunStats.EndRun();

        // a finished run counts towards Endless (it may be the one that unlocks it; the unlock
        // list hears about it). in Endless the score is how long you lasted
        if (GameMode.IsEndless) ShowBest();
        else RunProgress.RecordNormalRun();

        var screen = won && victoryPanel != null ? victoryPanel : panel;
        if (screen != null)
        {
            screen.SetActive(true);
            ScaleIn.Play(screen, startScale, slamSeconds);
        }

        Juice.Yield();
        Time.timeScale = 0f;
    }

    // the same run again, same map and character, through the loading screen
    public void RestartButton() => SceneLoader.Load(SceneManager.GetActiveScene().name);

    // back to the menu through the loading screen
    public void ExitButton() => SceneLoader.Load("MainMenu");

    private void ShowBest()
    {
        var director = FindFirstObjectByType<SpawnDirector>();
        float seconds = director != null ? director.GetRunTime() : RunStats.RunClock;
        float best = PlayerPrefs.GetFloat(BestKey, 0f);
        bool beaten = seconds > best;
        if (beaten)
        {
            PlayerPrefs.SetFloat(BestKey, seconds);
            PlayerPrefs.Save();
        }
        if (bestText != null) bestText.text = beaten ? "NEW BEST!" : $"Best {RunStats.Clock(best)}";
    }
}
