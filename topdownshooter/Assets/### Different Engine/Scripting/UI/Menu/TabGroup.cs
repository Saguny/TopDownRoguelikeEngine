using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// tabs inside a page, e.g. Display, Audio and Gameplay on the settings page. each tab pairs a
// button with the content it shows; clicking the button shows that content and hides the other
// tabs'. the buttons are hooked up here, so their OnClick lists stay empty
public class TabGroup : MonoBehaviour
{
    [Serializable]
    public class Tab
    {
        public Button button;
        [Tooltip("what the tab shows, e.g. Displaytab")]
        public GameObject content;
        [Tooltip("optional. switched on while this tab is open, e.g. an underline or a glow")]
        public GameObject selectedMark;
    }

    [SerializeField] private List<Tab> tabs = new List<Tab>();
    [Tooltip("reopen the tab that was open last time instead of the first one")]
    [SerializeField] private bool rememberLast;

    [Header("Label colours (optional)")]
    [Tooltip("tints each tab button's text so the open one stands out")]
    [SerializeField] private bool tintLabels;
    [SerializeField] private Color openLabel = Color.white;
    [SerializeField] private Color closedLabel = new Color(1f, 1f, 1f, 0.5f);

    private int open = -1;

    public int OpenIndex => open;

    private void Awake()
    {
        for (int i = 0; i < tabs.Count; i++)
        {
            int index = i;
            if (tabs[i].button != null) tabs[i].button.onClick.AddListener(() => Open(index));
        }
    }

    private void OnEnable() => Show(rememberLast && open >= 0 ? open : 0);

    public void Open(int index)
    {
        if (index == open) return;
        Show(index);
    }

    // for wiring a button by hand: opens the tab showing this content
    public void Open(GameObject content) => Open(tabs.FindIndex(t => t.content == content));

    private void Show(int index)
    {
        if (index < 0 || index >= tabs.Count) return;
        open = index;

        for (int i = 0; i < tabs.Count; i++)
        {
            var tab = tabs[i];
            bool on = i == index;
            if (tab.content != null) tab.content.SetActive(on);
            if (tab.selectedMark != null) tab.selectedMark.SetActive(on);

            if (tintLabels && tab.button != null)
            {
                var label = tab.button.GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.color = on ? openLabel : closedLabel;
            }
        }
    }
}
