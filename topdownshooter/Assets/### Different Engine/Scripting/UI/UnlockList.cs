using System.Collections.Generic;
using TMPro;
using UnityEngine;

// the game over screen's list of what this run unlocked (the next map, Endless Mode; see
// RunStats.Unlocked), a line each in this object's layout group. it fills when the screen opens,
// won or lost, and again if something unlocks while it's up: a finished run only counts towards
// Endless once the screen is already showing
public class UnlockList : MonoBehaviour
{
    [Tooltip("copied for every line: its font, size, colour and alignment. a child of this object is a placeholder and hidden; anything else is left as it is")]
    [SerializeField] private TMP_Text template;
    [Tooltip("the line when nothing was unlocked. empty shows no line")]
    [SerializeField] private string noneText = "Nothing new";
    [Tooltip("optional. hidden when nothing was unlocked, e.g. the Unlocks: title")]
    [SerializeField] private GameObject hideWhenNone;

    private readonly List<GameObject> made = new List<GameObject>();

    private void OnEnable()
    {
        RunStats.UnlocksChanged += Fill;
        Fill();
    }

    private void OnDisable() => RunStats.UnlocksChanged -= Fill;

    public void Fill()
    {
        // off first, so the layout never counts the old lines on their way out
        foreach (var go in made)
            if (go != null)
            {
                go.SetActive(false);
                Destroy(go);
            }
        made.Clear();
        if (template != null && template.transform.parent == transform) template.gameObject.SetActive(false);

        var unlocks = RunStats.Unlocks;
        bool none = unlocks.Count == 0;
        if (hideWhenNone != null && !transform.IsChildOf(hideWhenNone.transform)) hideWhenNone.SetActive(!none);

        if (none)
        {
            if (!string.IsNullOrEmpty(noneText)) Line(noneText);
            return;
        }
        foreach (var unlock in unlocks) Line(unlock);
    }

    private void Line(string text)
    {
        TMP_Text line;
        if (template != null)
        {
            line = Instantiate(template, transform);
            line.gameObject.SetActive(true);
        }
        else
        {
            // no template: TextMesh Pro's default font at a readable size
            line = new GameObject("Unlock", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            line.rectTransform.SetParent(transform, false);
            line.fontSize = 20f;
            line.alignment = TextAlignmentOptions.MidlineLeft;
        }

        line.name = "Unlock: " + text;
        line.textWrappingMode = TextWrappingModes.NoWrap;
        line.raycastTarget = false;
        line.text = text;
        made.Add(line.gameObject);
    }
}
