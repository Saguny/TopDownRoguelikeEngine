using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// the pause menu's Favored Items: what tickets were spent on for this run (RunCredits), their
// icons in the panel's Icon images, centred in a row, hover one for its numbers (click to pin
// it), and a word for each on how close its pity is. with none, the row is simply empty. it fills
// itself each time it's shown (PanelFlip turns the pause menu to it)
public class FavoredItemsPanel : MonoBehaviour
{
    [Tooltip("the icon slots. empty: every child Image whose name starts with Icon, in order")]
    [SerializeField] private List<Image> icons = new List<Image>();
    [Tooltip("the title. empty: the child text that starts with Favored")]
    [SerializeField] private TMP_Text title;
    [Tooltip("the gap between the icons, in canvas units")]
    [SerializeField] private float gap = 25f;

    private readonly List<float> slotY = new List<float>();

    private void Awake()
    {
        if (icons.Count == 0)
            foreach (Transform t in transform)
                if (t.name.StartsWith("Icon") && t.TryGetComponent(out Image img)) icons.Add(img);
        if (title == null)
            foreach (var t in GetComponentsInChildren<TMP_Text>(true))
                if (t.text != null && t.text.TrimStart().StartsWith("Favor", System.StringComparison.OrdinalIgnoreCase)) { title = t; break; }
        foreach (var i in icons) slotY.Add(i.rectTransform.anchoredPosition.y);
    }

    private void OnEnable() => Fill();

    public void Fill()
    {
        var inventory = FindFirstObjectByType<PlayerInventory>();
        var favoured = new List<UpgradeData>();
        if (inventory != null)
            foreach (var u in inventory.RunUpgrades)
                if (u != null && RunCredits.IsFavoured(u)) favoured.Add(u);


        // the ones in use, centred as a row where the slots sit
        int shown = Mathf.Min(favoured.Count, icons.Count);
        float width = 0f;
        for (int i = 0; i < shown; i++) width += icons[i].rectTransform.rect.width + (i > 0 ? gap : 0f);
        float x = -width * 0.5f;
        for (int i = 0; i < icons.Count; i++)
        {
            var img = icons[i];
            bool on = i < shown;
            img.gameObject.SetActive(on);
            if (!on) continue;
            var u = favoured[i];
            img.sprite = u.HeldIcon != null ? u.HeldIcon : u.icon;
            img.preserveAspect = true;
            img.raycastTarget = true;
            // a taken item full colour; one still to come a little faded until it's offered
            img.color = u.Level > 0 ? Color.white : new Color(1f, 1f, 1f, 0.6f);
            float wdt = img.rectTransform.rect.width;
            var rt = img.rectTransform;
            rt.anchoredPosition = new Vector2(x + wdt * 0.5f, i < slotY.Count ? slotY[i] : rt.anchoredPosition.y);
            x += wdt + gap;

            if (!img.TryGetComponent(out TooltipTrigger tip)) tip = img.gameObject.AddComponent<TooltipTrigger>();
            tip.Item = u;
            Pity(img, inventory, u);
        }
    }

    // under each icon: Lv. 3 once it's taken, or how many level ups until it's sure to be offered
    private void Pity(Image img, PlayerInventory inventory, UpgradeData u)
    {
        var t = img.transform.Find("Pity");
        TMP_Text text;
        if (t == null)
        {
            var go = new GameObject("Pity", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(img.transform, false);
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -2f);
            rt.sizeDelta = new Vector2(60f, 18f);
            text = go.AddComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.Top;
            text.fontSize = 14f;
            text.raycastTarget = false;
            if (title != null) text.font = title.font;
        }
        else text = t.GetComponent<TMP_Text>();
        if (text == null) return;

        if (u.Level > 0) { text.text = u.IsAtCap ? "MAX" : $"Lv. {u.Level}"; text.color = new Color32(0x6B, 0xFF, 0x00, 0xFF); return; }
        int left = inventory != null ? inventory.PityLeft(u) : -1;
        text.text = left < 0 ? "" : left <= 0 ? "next!" : $"sure in {left}";
        text.color = new Color32(0xff, 0xd2, 0x3c, 0xff);
    }
}
