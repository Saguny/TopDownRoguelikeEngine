using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// the global upgrade shop page. put it on the page (PowerupPanel). every box with a Button under
// the grid is matched to a global upgrade by its name (Max Health, Crit Chance...), shows its
// level and opens the buy panel when clicked, while the attributes panel shows in green what the
// next level would change. a maxed box turns green and opens the "already max" panel instead.
// everything below is found by name when left empty
public class UpgradeShop : MonoBehaviour
{
    [Tooltip("the parent of the upgrade boxes, found by the name UpgradePanel")]
    [SerializeField] private Transform grid;
    [Tooltip("the attributes panel that previews a pick, found among the children")]
    [SerializeField] private StatPanel stats;

    [Header("Buy panel")]
    [SerializeField] private GameObject buyPanel;
    [SerializeField] private TMP_Text buyName;
    [SerializeField] private TMP_Text buyDescription;
    [SerializeField] private TMP_Text buyCost;
    [SerializeField] private Button buyButton;

    [Header("Already max panel")]
    [SerializeField] private GameObject maxPanel;
    [SerializeField] private TMP_Text maxName;

    [Header("Look")]
    [SerializeField] private Color maxedBox = new Color32(0x6B, 0xFF, 0x00, 0xFF);
    [Tooltip("the cost turns this colour while there aren't enough coins")]
    [SerializeField] private Color tooExpensive = new Color(1f, 0.42f, 0.36f);
    [Tooltip("{0} = level now, {1} = max level")]
    [SerializeField] private string levelFormat = "Level {0}/{1}";
    [SerializeField] private string maxedLevel = "Max";
    [Tooltip("{0} = the price")]
    [SerializeField] private string costFormat = "Cost: {0}";

    private class Box
    {
        public GlobalUpgradeDef upgrade;
        public Image frame;
        public Color frameColor;
        public TMP_Text level;
    }

    private readonly List<Box> boxes = new List<Box>();
    private Box selected;
    private Color costColor = Color.white;
    private StatCatalog catalog;

    private void Awake()
    {
        catalog = StatCatalog.Load();
        if (grid == null) grid = Find(transform, "UpgradePanel");
        if (stats == null) stats = GetComponentInChildren<StatPanel>(true);

        if (buyPanel == null) buyPanel = Find(transform, "BuyPanel")?.gameObject;
        if (buyPanel != null)
        {
            if (buyName == null) buyName = Text(buyPanel.transform, "UpgradeName");
            if (buyDescription == null) buyDescription = Text(buyPanel.transform, "UpgradeDesc");
            if (buyCost == null) buyCost = Text(buyPanel.transform, "UpgradeCost");
            if (buyButton == null) buyButton = Find(buyPanel.transform, "BuyButton")?.GetComponent<Button>();
        }
        if (maxPanel == null) maxPanel = Find(transform, "AlreadyMaxPanel")?.gameObject;
        if (maxPanel != null && maxName == null) maxName = Text(maxPanel.transform, "UpgradeName");

        if (buyCost != null) costColor = buyCost.color;
        if (buyButton != null) buyButton.onClick.AddListener(Buy);
        BindBoxes();
    }

    // each box's name picks its upgrade: the upgrade's title, id or stat, ignoring spaces and case
    private void BindBoxes()
    {
        if (grid == null)
        {
            Debug.LogWarning("UpgradeShop: no UpgradePanel found for the upgrade boxes", this);
            return;
        }

        foreach (Transform child in grid)
        {
            if (!child.TryGetComponent(out Button button)) continue;

            var upgrade = UpgradeNamed(child.name);
            if (upgrade == null)
            {
                Debug.LogWarning($"UpgradeShop: no global upgrade in the StatCatalog matches the box '{child.name}'", child);
                continue;
            }

            var box = new Box { upgrade = upgrade, frame = child.GetComponent<Image>(), level = Text(child, "LevelText") };
            if (box.frame != null) box.frameColor = box.frame.color;
            button.onClick.AddListener(() => Select(box));
            boxes.Add(box);
        }
    }

    private GlobalUpgradeDef UpgradeNamed(string boxName)
    {
        string key = Key(boxName);
        foreach (var u in catalog.globalUpgrades)
            if (u != null && (Key(u.title) == key || Key(u.id) == key || Key(u.stat.ToString()) == key))
                return u;
        return null;
    }

    private static string Key(string s) => string.IsNullOrEmpty(s) ? string.Empty : s.Replace(" ", "").Replace("_", "").ToLowerInvariant();

    private void OnEnable()
    {
        MetaProgress.Changed += RefreshBoxes;
        Coins.Changed += RefreshSelected;
        RefreshBoxes();
        Select(selected ?? (boxes.Count > 0 ? boxes[0] : null));
    }

    private void OnDisable()
    {
        MetaProgress.Changed -= RefreshBoxes;
        Coins.Changed -= RefreshSelected;
        if (stats != null) stats.EndPreview();
    }

    private void RefreshBoxes()
    {
        foreach (var box in boxes)
        {
            int rank = MetaProgress.RankOf(box.upgrade);
            bool maxed = rank >= box.upgrade.maxRank;
            if (box.level != null) box.level.text = maxed ? maxedLevel : string.Format(levelFormat, rank, box.upgrade.maxRank);
            if (box.frame != null) box.frame.color = maxed ? maxedBox : box.frameColor;
        }
    }

    private void RefreshSelected() => Select(selected);

    private void Select(Box box)
    {
        selected = box;
        if (box == null) return;

        var u = box.upgrade;
        int rank = MetaProgress.RankOf(u);
        bool maxed = rank >= u.maxRank;

        if (buyPanel != null) buyPanel.SetActive(!maxed);
        if (maxPanel != null) maxPanel.SetActive(maxed);

        if (maxed)
        {
            if (maxName != null) maxName.text = u.title;
            if (stats != null) stats.EndPreview();
            return;
        }

        int cost = u.CostOfRank(rank + 1);
        bool affordable = Coins.Balance >= cost;
        if (buyName != null) buyName.text = u.title;
        if (buyDescription != null) buyDescription.text = u.Describe(catalog);
        if (buyCost != null)
        {
            buyCost.text = string.Format(costFormat, cost);
            buyCost.color = affordable ? costColor : tooExpensive;
        }
        if (buyButton != null) buyButton.interactable = affordable;
        if (stats != null) stats.ShowPreview(u);
    }

    // hooked to the buy button on its own; also callable from a button's OnClick
    public void Buy()
    {
        if (selected == null) return;

        var u = selected.upgrade;
        int rank = MetaProgress.RankOf(u);
        if (rank >= u.maxRank || !Coins.TrySpend(u.CostOfRank(rank + 1))) return;

        MetaProgress.SetRank(u, rank + 1);
        Select(selected);
    }

    private static TMP_Text Text(Transform root, string name) => Find(root, name)?.GetComponent<TMP_Text>();

    private static Transform Find(Transform root, string name)
    {
        foreach (Transform child in root)
        {
            if (child.name == name) return child;
            var deeper = Find(child, name);
            if (deeper != null) return deeper;
        }
        return null;
    }
}
