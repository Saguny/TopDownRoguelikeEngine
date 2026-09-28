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
    [Tooltip("where the numbers start in a row, as a share of the body's width")]
    [Range(0.3f, 0.9f)]
    [SerializeField] private float valueColumn = 0.62f;
    [Tooltip("with no layout group on the box: the body grows or shrinks to its text and the box with it, keeping the gap below the body as designed")]
    [SerializeField] private bool fitToText = true;

    private static ItemTooltip instance;
    private Object owner;
    private RectTransform anchor;
    private Canvas canvas;
    private bool adopted;
    private float boxDesignHeight, bodyDesignHeight;
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
        tip.gameObject.SetActive(true);
        tip.box.gameObject.SetActive(true);
        tip.Fill(item, compare);
        tip.Fit();
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
        else instance.Adopt();
        return instance;
    }

    private void Awake()
    {
        if (instance == null) instance = this;
        Adopt();
    }

    // a tooltip designed in the scene can sit anywhere, even inside a screen that's hidden while
    // another one is up (the level up panel while paused): it's moved onto a canvas of its own over
    // every menu, scaled like the canvas it was designed on, so one panel serves every screen
    private void Adopt()
    {
        if (adopted) return;
        adopted = true;
        if (box == null) box = transform as RectTransform;

        var parents = GetComponentsInParent<Canvas>(true);
        var designed = parents.Length > 0 ? parents[parents.Length - 1] : null;
        if (designed != null && transform is RectTransform rt)
        {
            var go = new GameObject("Item Tooltip", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 500;                               // over the level up and pause screens
            var scaler = go.GetComponent<CanvasScaler>();
            if (designed.TryGetComponent(out CanvasScaler from))
            {
                scaler.uiScaleMode = from.uiScaleMode;
                scaler.referenceResolution = from.referenceResolution;
                scaler.screenMatchMode = from.screenMatchMode;
                scaler.matchWidthOrHeight = from.matchWidthOrHeight;
                scaler.scaleFactor = from.scaleFactor;
                scaler.referencePixelsPerUnit = from.referencePixelsPerUnit;
            }
            go.transform.SetParent(designed.transform.parent, false);
            rt.SetParent(go.transform, false);
            canvas = c;
        }
        else canvas = GetComponentInParent<Canvas>(true);

        // the design's own size is the starting point for fitting it to its text; its parts are
        // pinned to the box's top so the box can grow downward under them
        boxDesignHeight = box.rect.height;
        if (body != null) bodyDesignHeight = body.rectTransform.rect.height;
        if (box.GetComponent<LayoutGroup>() == null)
        {
            foreach (Transform t in box)
            {
                if (!(t is RectTransform child) || child.anchorMin.y != child.anchorMax.y) continue;
                float y = child.localPosition.y - box.rect.yMax;
                child.anchorMin = new Vector2(child.anchorMin.x, 1f);
                child.anchorMax = new Vector2(child.anchorMax.x, 1f);
                child.anchoredPosition = new Vector2(child.anchoredPosition.x, y);
            }
            if (body != null)
            {
                var b = body.rectTransform;
                b.anchoredPosition += new Vector2(0f, (1f - b.pivot.y) * b.rect.height);
                b.pivot = new Vector2(b.pivot.x, 1f);
            }
        }

        // it must never catch the pointer itself, or it would flicker as it covers what it's for
        foreach (var g in GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
        if (owner == null) box.gameObject.SetActive(false);   // not the placeholder text until it's asked for
    }

    // the body as tall as its text, and the box as much taller or shorter as that makes it
    private void Fit()
    {
        if (!fitToText || body == null || box.GetComponent<LayoutGroup>() != null) return;
        var b = body.rectTransform;
        float lineHeight = body.fontSize * 1.2f;
        float h = Mathf.Max(lineHeight, body.GetPreferredValues(body.text, b.rect.width, 0f).y);
        if (b.anchorMin.y == b.anchorMax.y) b.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h);
        box.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(lineHeight, boxDesignHeight + h - bodyDesignHeight));
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
            // a passive: the stats it moves as they stand now, with this pick's change on a level
            // up, the way the weapons show theirs (and the panel on the left)
            if (stats != null && (level > 0 || compare)) PassiveRows(sb, item, stats, compare && !item.IsAtCap);
            string text = compare && !item.IsAtCap ? item.GetDisplayDescription() : item.description;
            if (!string.IsNullOrWhiteSpace(text)) sb.Append(sb.Length > 0 ? "\n" : "").Append(text).Append('\n');
        }

        if (weapon) RunLines(sb, item);
        if (body != null) body.text = sb.ToString().TrimEnd('\n');
    }

    // the stat a passive raises, and any other the pick would move
    private void PassiveRows(StringBuilder sb, UpgradeData item, StatContext stats, bool showNext)
    {
        var now = StatSheet.Live(stats);
        var next = showNext ? StatSheet.Live(stats, item) : null;
        var catalog = StatCatalog.Load();
        var own = StatOf(item.type);
        for (int i = 0; i < System.Enum.GetValues(typeof(StatId)).Length; i++)
        {
            var id = (StatId)i;
            bool moves = next != null && Mathf.Abs(next[id] - now[id]) > 0.0001f;
            if (id != own && !moves) continue;
            var def = catalog != null ? catalog.Get(id) : null;
            if (def == null || (!def.showInPanel && !moves)) continue;
            Row(sb, def.label, now.Text(id), moves ? next.Text(id) : null);
        }
    }

    private static StatId? StatOf(UpgradeType type)
    {
        switch (type)
        {
            case UpgradeType.MaxHealth: return StatId.MaxHealth;
            case UpgradeType.HealthRegen: return StatId.Recovery;
            case UpgradeType.MoveSpeed: return StatId.MoveSpeed;
            case UpgradeType.Might: return StatId.Might;
            case UpgradeType.Cooldown: return StatId.Cooldown;
            case UpgradeType.Area: return StatId.Area;
            case UpgradeType.WeaponSpeed: return StatId.WeaponSpeed;
            case UpgradeType.ArrowCount: return StatId.ArrowCount;
            case UpgradeType.Pierce: return StatId.Pierce;
            case UpgradeType.CritChance: return StatId.CritChance;
            case UpgradeType.CritDamage: return StatId.CritDamage;
            case UpgradeType.PickupRadius: return StatId.Magnet;
            case UpgradeType.ArmourPierce: return StatId.ArmourPierce;
            default: return null;
        }
    }

    private void Row(StringBuilder sb, string label, string value, string after)
    {
        sb.Append($"<color=#{Hex(labelColor)}>{label}</color><pos={valueColumn * 100f:0}%><color=#{Hex(valueColor)}>{value}</color>");
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
        canvasGo.SetActive(false);                              // built whole before its Awake
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
        tip.adopted = true;
        foreach (var g in canvasGo.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
        box.gameObject.SetActive(false);
        canvasGo.SetActive(true);
        return tip;
    }
}
