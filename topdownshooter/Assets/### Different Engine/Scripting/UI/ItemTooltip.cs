using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// the attributes of a weapon or passive, in a box next to whatever it was asked for from: shown on
// hover (or pinned with a click) over a level up card or a loadout slot, on the level up screen and
// the pause screen (TooltipTrigger). a weapon lists its numbers as they are right now, with Might,
// Cooldown and Area applied (WeaponData.Attributes), and on a level up card what the pick changes
// them to; below that what it has done this run. with no ItemTooltip in the scene, a plain one is
// built on the spot. to design your own, put this on a panel and fill the fields in
public class ItemTooltip : MonoBehaviour
{
    [Header("Parts")]
    [SerializeField] private RectTransform box;
    [SerializeField] private TMP_Text title;
    [Tooltip("the kind, attack class and level, e.g. Weapon (Physical)  Lv 3/9")]
    [SerializeField] private TMP_Text subtitle;
    [Tooltip("the numbers, one per line, and what it has done this run")]
    [SerializeField] private TMP_Text body;

    [Header("Look")]
    [SerializeField] private Color labelColor = new Color(0.72f, 0.68f, 0.8f);
    [SerializeField] private Color valueColor = Color.white;
    [SerializeField] private Color betterColor = new Color(0.42f, 1f, 0f);
    [SerializeField] private Color runColor = new Color(1f, 0.82f, 0.35f);
    [Tooltip("screen pixels between the tooltip and what it's for")]
    [SerializeField] private float gap = 12f;

    private static ItemTooltip instance;
    private Object owner;
    private RectTransform anchor;
    private Canvas canvas;
    private readonly List<WeaponData.Attribute> now = new List<WeaponData.Attribute>();
    private readonly List<WeaponData.Attribute> next = new List<WeaponData.Attribute>();
    private static readonly Vector3[] corners = new Vector3[4];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    // show an item's attributes next to a rect. compare: also show what the next pick makes them.
    // who: whoever asked, so only they can hide it again
    public static void Show(UpgradeData item, RectTransform near, bool compare, Object who)
    {
        if (item == null) return;
        var tip = Get();
        if (tip == null) return;
        tip.owner = who;
        tip.anchor = near;
        tip.Fill(item, compare);
        tip.gameObject.SetActive(true);
        tip.box.gameObject.SetActive(true);
        tip.Place();
    }

    public static void Hide(Object who)
    {
        if (instance == null || (who != null && instance.owner != who)) return;
        instance.owner = null;
        instance.anchor = null;
        if (instance.box != null) instance.box.gameObject.SetActive(false);
    }

    public static bool IsShowingFor(Object who) => instance != null && instance.owner == who && instance.box != null && instance.box.gameObject.activeSelf;

    private static ItemTooltip Get()
    {
        if (instance != null) return instance;
        instance = FindAnyObjectByType<ItemTooltip>(FindObjectsInactive.Include);
        if (instance == null) instance = BuildDefault();
        return instance;
    }

    private void Awake()
    {
        if (instance == null) instance = this;
        canvas = GetComponentInParent<Canvas>();
        if (box == null) box = transform as RectTransform;
        // it must never catch the pointer itself, or it would flicker as it covers what it's for
        foreach (var g in GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void LateUpdate()
    {
        if (box == null || !box.gameObject.activeSelf) return;
        // what it was for went away (the menu closed): so does the tooltip
        if (anchor == null || !anchor.gameObject.activeInHierarchy) { Hide(null); return; }
        Place();
    }

    // ---------------------------------------------------------------- what it says

    private void Fill(UpgradeData item, bool compare)
    {
        var stats = FindAnyObjectByType<StatContext>();
        bool weapon = item is WeaponData;
        int level = item.Level;
        bool evolving = item is WeaponData w && w.NextPickEvolves;

        string name = item is WeaponData wd && wd.EvolutionLevel > 0 && level >= wd.EvolutionLevel ? wd.GetEvolvedTitle() : item.GetBaseTitle();
        if (title != null) title.text = name;
        if (subtitle != null)
        {
            string lv = level <= 0 ? "not held" : item.IsAtCap ? $"Lv {level} (max)" : $"Lv {level}/{item.MaxLevel}";
            subtitle.text = $"{item.CategoryLabel}   {lv}";
        }

        var sb = new StringBuilder();
        if (item is WeaponData data)
        {
            now.Clear();
            next.Clear();
            if (level > 0) data.Attributes(level, stats, now);
            bool showNext = compare && !item.IsAtCap;
            if (showNext) data.Attributes(level + 1, stats, next);

            if (level <= 0 && showNext)
            {
                // a new pick: what it will be
                foreach (var a in next) Row(sb, a.label, a.value, null);
            }
            else if (evolving && showNext)
            {
                foreach (var a in now) Row(sb, a.label, a.value, null);
                sb.Append($"\n<color=#{Hex(betterColor)}>Evolves: {data.GetEvolvedTitle()}</color>\n");
                foreach (var a in next) Row(sb, a.label, a.value, null);
            }
            else
            {
                // the numbers now, and where this pick takes each one that changes
                foreach (var a in now)
                {
                    string after = null;
                    if (showNext) foreach (var b in next) if (b.label == a.label && b.value != a.value) after = b.value;
                    Row(sb, a.label, a.value, after);
                }
                if (showNext) foreach (var b in next) if (!now.Exists(a => a.label == b.label)) Row(sb, b.label, "-", b.value);
            }
        }
        else
        {
            string text = compare && !item.IsAtCap ? item.GetDisplayDescription() : item.description;
            if (!string.IsNullOrWhiteSpace(text)) sb.Append(text).Append('\n');
        }

        if (weapon) RunLines(sb, item);
        if (body != null) body.text = sb.ToString().TrimEnd('\n');
    }

    private void Row(StringBuilder sb, string label, string value, string after)
    {
        sb.Append($"<color=#{Hex(labelColor)}>{label}</color><pos=62%><color=#{Hex(valueColor)}>{value}</color>");
        if (after != null) sb.Append($" <color=#{Hex(betterColor)}>> {after}</color>");
        sb.Append('\n');
    }

    // what the weapon has done this run
    private void RunLines(StringBuilder sb, UpgradeData item)
    {
        WeaponRecord found = null;
        foreach (var r in RunStats.Weapons) if (r != null && r.Asset == item && r.Held) found = r;
        if (found == null || found.Damage <= 0.0) return;
        sb.Append('\n');
        string c = Hex(runColor);
        sb.Append($"<color=#{c}>This run</color>\n");
        Row(sb, "Damage dealt", ((float)found.Damage).ToString("0"), null);
        Row(sb, "DPS", found.Dps.ToString("0.#"), null);
        Row(sb, "Kills", found.Kills.ToString(), null);
    }

    private static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

    // ---------------------------------------------------------------- where it goes

    // beside what it's for: to the right if there's room, else to the left, kept on screen
    private void Place()
    {
        if (anchor == null || box == null) return;
        LayoutRebuilder.ForceRebuildLayoutImmediate(box);

        var anchorCanvas = anchor.GetComponentInParent<Canvas>();
        var anchorCam = anchorCanvas == null || anchorCanvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : anchorCanvas.rootCanvas.worldCamera;
        anchor.GetWorldCorners(corners);
        Vector2 min = RectTransformUtility.WorldToScreenPoint(anchorCam, corners[0]);
        Vector2 max = RectTransformUtility.WorldToScreenPoint(anchorCam, corners[2]);

        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        float scale = canvas != null ? canvas.scaleFactor : 1f;
        Vector2 size = box.rect.size * scale;

        float x = max.x + gap;
        if (x + size.x > Screen.width) x = min.x - gap - size.x;
        x = Mathf.Clamp(x, 0f, Mathf.Max(0f, Screen.width - size.x));
        float y = Mathf.Clamp(max.y, size.y, Screen.height);

        // top left corner at (x, y) on screen
        box.pivot = new Vector2(0f, 1f);
        var root = canvas != null ? canvas.rootCanvas : null;
        if (root == null || root.renderMode == RenderMode.ScreenSpaceOverlay) box.position = new Vector3(x, y, 0f);
        else if (RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)root.transform, new Vector2(x, y), root.worldCamera, out var world)) box.position = world;
    }

    // ---------------------------------------------------------------- the plain one

    private static ItemTooltip BuildDefault()
    {
        var canvasGo = new GameObject("Item Tooltip", typeof(Canvas), typeof(CanvasScaler));
        var c = canvasGo.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 500;                                   // over the level up and pause screens
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;
        DontDestroyOnLoad(canvasGo);

        var boxGo = new GameObject("Box", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        var box = (RectTransform)boxGo.transform;
        box.SetParent(canvasGo.transform, false);
        box.pivot = new Vector2(0f, 1f);
        var bg = boxGo.GetComponent<Image>();
        bg.color = new Color(0.08f, 0.04f, 0.12f, 0.94f);
        var layout = boxGo.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 14, 16);
        layout.spacing = 6f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        var fit = boxGo.GetComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        box.sizeDelta = new Vector2(380f, 100f);

        // a thin bright rim, the colours of the rest of the menus
        var outline = boxGo.AddComponent<Outline>();
        outline.effectColor = new Color(0.64f, 0.36f, 0.91f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);

        TMP_Text Text(string name, float size, FontStyles style, Color colour)
        {
            var t = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            t.rectTransform.SetParent(box, false);
            t.fontSize = size;
            t.fontStyle = style;
            t.color = colour;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.richText = true;
            return t;
        }

        var tip = canvasGo.AddComponent<ItemTooltip>();
        tip.box = box;
        tip.title = Text("Title", 30f, FontStyles.Bold, Color.white);
        tip.subtitle = Text("Subtitle", 19f, FontStyles.Normal, new Color(0.72f, 0.68f, 0.8f));
        tip.body = Text("Body", 21f, FontStyles.Normal, Color.white);
        tip.canvas = c;
        foreach (var g in canvasGo.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
        box.gameObject.SetActive(false);
        return tip;
    }
}
