using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// the level up screen's list of what this run has picked: weapons in one grid, passives in the
// other, each slot showing its icon and level, in the order they were taken. the slot template is
// copied for each. while a card is highlighted, its item previews the pick: its slot pulses with
// the level it would reach, a new pick shows as a faded NEW slot, and an evolution shows its
// evolved icon. fields left empty are found by name
public class LoadoutPanel : MonoBehaviour
{
    [Tooltip("where weapon slots go. empty: the child called Grid under the Weapons label")]
    [SerializeField] private RectTransform weaponsGrid;
    [Tooltip("where passive slots go. empty: the child called Grid under the Passives label. if there is none, the weapons grid is copied under that label")]
    [SerializeField] private RectTransform passivesGrid;
    [Tooltip("one slot, with an Image child for the icon and a Level text. hidden and copied into the grids. empty: the child called Slot")]
    [SerializeField] private GameObject slotTemplate;
    [Tooltip("empty: the level up menu this panel sits in")]
    [SerializeField] private UpgradeMenuUI menu;
    [Tooltip("the Weapons title; in a normal run it shows how many are held of how many can be, e.g. Weapons (3/6). empty: found by its text")]
    [SerializeField] private TMP_Text weaponsLabel;
    [Tooltip("the same for the Passives title")]
    [SerializeField] private TMP_Text passivesLabel;

    [Header("Look")]
    [SerializeField] private Color maxedColor = new Color32(0x6B, 0xFF, 0x00, 0xFF);
    [SerializeField] private Color previewColor = new Color32(0x6B, 0xFF, 0x00, 0xFF);
    [SerializeField, Range(0f, 1f)] private float newSlotAlpha = 0.5f;
    [SerializeField, Min(0f)] private float previewPulse = 0.12f;

    private sealed class Slot
    {
        public GameObject root;
        public Image icon;
        public TMP_Text tmp;
        public Text text;
        public IconAnimator anim;
        public Vector3 scale;
    }

    private readonly List<Slot> weaponSlots = new List<Slot>();
    private readonly List<Slot> passiveSlots = new List<Slot>();
    private PlayerInventory inventory;
    private UpgradeData previewing;
    private Slot pulsing;
    private Color levelColor = Color.white;
    private bool found;
    private string weaponsTitle, passivesTitle;

    private void Awake() => FindParts();

    private void OnEnable()
    {
        previewing = menu != null ? menu.Highlighted : null;
        Refresh();
    }

    private void Update()
    {
        var highlighted = menu != null ? menu.Highlighted : null;
        if (highlighted != previewing)
        {
            previewing = highlighted;
            Refresh();
        }

        if (pulsing != null && pulsing.root != null)
            pulsing.root.transform.localScale = pulsing.scale * (1f + previewPulse * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f)));
    }

    public void Refresh()
    {
        FindParts();
        if (inventory == null) inventory = FindFirstObjectByType<PlayerInventory>();

        if (pulsing != null && pulsing.root != null) pulsing.root.transform.localScale = pulsing.scale;
        pulsing = null;

        int weapons = 0, passives = 0;
        if (inventory != null)
        {
            foreach (var u in inventory.Taken)
            {
                if (u == null) continue;
                if (u.Category == UpgradeCategory.Weapon) Show(weaponSlots, weaponsGrid, weapons++, u, false);
                else Show(passiveSlots, passivesGrid, passives++, u, false);
            }
        }

        // a pick that isn't held yet shows as a faded slot at the end of its row
        if (previewing != null && previewing.Level == 0)
        {
            if (previewing.Category == UpgradeCategory.Weapon) Show(weaponSlots, weaponsGrid, weapons++, previewing, true);
            else Show(passiveSlots, passivesGrid, passives++, previewing, true);
        }

        HideFrom(weaponSlots, weapons);
        HideFrom(passiveSlots, passives);

        if (inventory != null)
        {
            Count(weaponsLabel, weaponsTitle, inventory.WeaponsHeld, inventory.WeaponSlots);
            Count(passivesLabel, passivesTitle, inventory.PassivesHeld, inventory.PassiveSlots);
        }
    }

    // Weapons (3/6). Endless has no limit, so just the title
    private static void Count(TMP_Text label, string title, int held, int slots)
    {
        if (label == null || title == null) return;
        label.text = slots > 0 ? $"{title} ({held}/{slots})" : title;
    }

    private void Show(List<Slot> slots, RectTransform grid, int index, UpgradeData u, bool isNew)
    {
        if (grid == null || slotTemplate == null) return;
        while (slots.Count <= index) slots.Add(MakeSlot(grid));

        var s = slots[index];
        s.root.SetActive(true);
        s.root.transform.localScale = s.scale;

        bool preview = u == previewing;
        var sprite = preview ? u.CardIcon : u.icon;
        if (s.icon != null)
        {
            s.icon.sprite = sprite;
            s.icon.enabled = sprite != null;
            var c = s.icon.color;
            c.a = isNew ? newSlotAlpha : 1f;
            s.icon.color = c;
        }

        string label;
        Color colour;
        if (isNew) { label = "NEW"; colour = previewColor; }
        else if (preview) { label = PreviewLabel(u); colour = previewColor; }
        else if (u.IsAtCap) { label = "MAX"; colour = maxedColor; }
        else { label = u.Level.ToString(); colour = levelColor; }
        SetText(s, label, colour);

        // the highlighted item plays its idle loop and pulses
        if (s.anim != null) s.anim.Play(preview ? u.CardIconFrames : null, u.iconFps);
        if (preview && !isNew) pulsing = s;
    }

    private static string PreviewLabel(UpgradeData u)
    {
        if (u.IsAtCap) return "MAX+";                             // an overcharge pick in endless
        if (u is WeaponData w && w.NextPickEvolves) return "EVO";
        int next = u.Level + 1;
        return next >= u.MaxLevel ? "MAX" : next.ToString();
    }

    private static void HideFrom(List<Slot> slots, int used)
    {
        for (int i = used; i < slots.Count; i++)
            if (slots[i].root != null) slots[i].root.SetActive(false);
    }

    private Slot MakeSlot(RectTransform grid)
    {
        var go = Instantiate(slotTemplate, grid, false);
        go.name = "Slot";
        go.SetActive(true);
        var s = new Slot { root = go, scale = go.transform.localScale };

        var iconT = FindDeep(go.transform, "Image");
        s.icon = iconT != null ? iconT.GetComponent<Image>() : go.GetComponentInChildren<Image>(true);
        var levelT = FindDeep(go.transform, "Level");
        if (levelT != null)
        {
            s.tmp = levelT.GetComponent<TMP_Text>();
            s.text = levelT.GetComponent<Text>();
        }
        if (s.icon != null && !s.icon.TryGetComponent(out s.anim)) s.anim = s.icon.gameObject.AddComponent<IconAnimator>();
        return s;
    }

    private static void SetText(Slot s, string label, Color colour)
    {
        if (s.tmp != null) { s.tmp.text = label; s.tmp.color = colour; }
        if (s.text != null) { s.text.text = label; s.text.color = colour; }
    }

    // ---------------------------------------------------------------- finding the parts by name

    private void FindParts()
    {
        if (found) return;
        found = true;

        if (menu == null) menu = GetComponentInParent<UpgradeMenuUI>(true);
        if (menu == null) menu = FindFirstObjectByType<UpgradeMenuUI>(FindObjectsInactive.Include);

        if (weaponsGrid == null) weaponsGrid = GridUnder("Weapon");
        if (passivesGrid == null) passivesGrid = GridUnder("Passive");
        if (weaponsLabel == null) weaponsLabel = LabelSaying("Weapon");
        if (passivesLabel == null) passivesLabel = LabelSaying("Passive");
        weaponsTitle = Title(weaponsLabel);
        passivesTitle = Title(passivesLabel);

        if (slotTemplate == null)
        {
            var t = FindDeep(transform, "Slot");
            if (t != null) slotTemplate = t.gameObject;
        }
        if (slotTemplate != null)
        {
            var level = FindDeep(slotTemplate.transform, "Level");
            if (level != null)
            {
                if (level.TryGetComponent(out TMP_Text tmp)) levelColor = tmp.color;
                else if (level.TryGetComponent(out Text text)) levelColor = text.color;
            }
            slotTemplate.SetActive(false);
        }

        // no grid under the Passives label yet: copy the weapons grid there, empty
        if (passivesGrid == null && weaponsGrid != null)
        {
            var label = FindByNameContaining(transform, "Passive");
            if (label != null)
            {
                var copy = Instantiate(weaponsGrid.gameObject, label, false);
                copy.name = "Grid";
                for (int i = copy.transform.childCount - 1; i >= 0; i--) Destroy(copy.transform.GetChild(i).gameObject);
                passivesGrid = (RectTransform)copy.transform;
            }
        }
    }

    // the text whose words are the title, e.g. "Weapons"
    private TMP_Text LabelSaying(string part)
    {
        foreach (var t in GetComponentsInChildren<TMP_Text>(true))
            if (t.text != null && t.text.TrimStart().StartsWith(part, System.StringComparison.OrdinalIgnoreCase) && t.text.Length <= 40)
                return t;
        return null;
    }

    // the title without a count a previous run left on it
    private static string Title(TMP_Text label)
    {
        if (label == null) return null;
        string text = label.text ?? string.Empty;
        int bracket = text.IndexOf(" (", System.StringComparison.Ordinal);
        return (bracket > 0 ? text.Substring(0, bracket) : text).Trim();
    }

    private RectTransform GridUnder(string labelPart)
    {
        foreach (var t in GetComponentsInChildren<RectTransform>(true))
            if (t.name == "Grid" && t.parent != null && t.parent.name.IndexOf(labelPart, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return t;
        return null;
    }

    private static Transform FindByNameContaining(Transform root, string part)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t != root && t.name.IndexOf(part, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return t;
        return null;
    }

    private static Transform FindDeep(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t != root && t.name == name) return t;
        return null;
    }
}
