using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// the settings' Account tab: the player's name, and Reset User Data. resetting asks first: the
// Are You Sure panel grows in from small, Confirm wipes the player's progress (SaveData) and
// reloads the menu through the loading screen, opening on the main panel; Cancel shrinks it away
// and nothing happens. anything left empty is found under this object by its name in the scene
public class AccountSettings : MonoBehaviour
{
    [Tooltip("empty: the child named Account Name. shows the player's name")]
    [SerializeField] private TMP_Text accountName;
    [Tooltip("empty: the child named ResetUserData")]
    [SerializeField] private Button resetButton;
    [Tooltip("empty: the child named AreYouSure")]
    [SerializeField] private GameObject areYouSure;
    [Tooltip("empty: the child named ResetUserDataConfirm")]
    [SerializeField] private Button confirmButton;
    [Tooltip("empty: the child named ResetUserDataCancel")]
    [SerializeField] private Button cancelButton;

    [Header("Transition")]
    [Tooltip("how small the panel starts, as a share of its size")]
    [SerializeField, Range(0.05f, 1f)] private float startScale = 0.3f;
    [SerializeField, Min(0.05f)] private float openSeconds = 0.3f;
    [SerializeField, Min(0.05f)] private float closeSeconds = 0.15f;

    private Vector3 panelSize = Vector3.one;
    private bool resetting;

    private void Awake()
    {
        if (accountName == null) accountName = Find<TMP_Text>("Account Name");
        if (resetButton == null) resetButton = Find<Button>("ResetUserData");
        if (areYouSure == null) { var t = FindChild("AreYouSure"); if (t != null) areYouSure = t.gameObject; }
        if (confirmButton == null) confirmButton = Find<Button>("ResetUserDataConfirm");
        if (cancelButton == null) cancelButton = Find<Button>("ResetUserDataCancel");

        if (accountName != null) accountName.text = PlayerIdentity.Name;
        if (resetButton != null) resetButton.onClick.AddListener(AskFirst);
        if (confirmButton != null) confirmButton.onClick.AddListener(Confirm);
        if (cancelButton != null) cancelButton.onClick.AddListener(Cancel);
        if (areYouSure != null)
        {
            panelSize = areYouSure.transform.localScale;
            areYouSure.SetActive(false);
        }
        else Debug.LogWarning("AccountSettings: no AreYouSure panel under the Account tab, so Reset User Data can't ask first", this);
    }

    private void OnDisable()
    {
        // leaving the tab closes the question
        if (areYouSure != null && !resetting)
        {
            areYouSure.SetActive(false);
            areYouSure.transform.localScale = panelSize;
        }
    }

    public void AskFirst()
    {
        if (areYouSure == null || resetting) return;
        StopAllCoroutines();
        areYouSure.transform.localScale = panelSize;
        areYouSure.SetActive(true);
        var grow = ScaleIn.Play(areYouSure, startScale, openSeconds);
        if (grow != null) grow.overshoot = 1.2f;
    }

    public void Cancel()
    {
        if (areYouSure == null || !areYouSure.activeSelf || resetting) return;
        StopAllCoroutines();
        StartCoroutine(Shrink());
    }

    public void Confirm()
    {
        if (resetting) return;
        resetting = true;
        if (confirmButton != null) confirmButton.interactable = false;
        if (cancelButton != null) cancelButton.interactable = false;
        SaveData.ResetProgress();
        // through the loading screen, back to the menu's main panel
        SceneLoader.Load(SceneManager.GetActiveScene().name);
    }

    private IEnumerator Shrink()
    {
        var t = areYouSure.transform;
        var group = areYouSure.GetComponent<CanvasGroup>();
        if (group != null) group.interactable = group.blocksRaycasts = false;
        for (float k = 0f; k < 1f; k += Time.unscaledDeltaTime / closeSeconds)
        {
            float e = k * k;
            t.localScale = panelSize * Mathf.Lerp(1f, startScale, e);
            if (group != null) group.alpha = 1f - e;
            yield return null;
        }
        areYouSure.SetActive(false);
        t.localScale = panelSize;
        if (group != null) group.alpha = 1f;
    }

    private T Find<T>(string name) where T : Component
    {
        var t = FindChild(name);
        return t != null ? t.GetComponent<T>() : null;
    }

    private Transform FindChild(string name)
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }
}
