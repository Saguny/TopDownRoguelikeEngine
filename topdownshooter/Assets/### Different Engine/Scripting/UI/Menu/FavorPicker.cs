using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// the map selection's ticket picker (Favor Item): every weapon, passive and ability the level up
// can offer (UpgradeCatalog), in a grid that sizes its cells to fit them all in the box, whatever
// its size. clicking one picks it, its backdrop turning yellow as a picked character's does, and
// clicking it again lets it go; each pick costs a ticket (RunCredits), as many as the player
// holds. the line under the grid says what Confirm will spend, and Confirm stays grey until there
// is something to spend or give back. while the box is up the page's Back button reads Close and
// closes it, as Escape does. the box slams in like the game's other screens and drops away when
// it closes.
//
// MapSelectionMenu puts this on its page; everything is found there by name: FavorItem (the button
// that opens it, and the box itself, the one holding a Grid), FavoritemBackdrop, Label, Grid,
// Confirm, SpentTicketsConfirmText and backbutton
public class FavorPicker : MonoBehaviour
{
    private static readonly Color Picked = new Color32(0xff, 0xd2, 0x3c, 0xff);
    private static readonly Color Refused = new Color32(0xff, 0x3a, 0x3a, 0xff);
    private static readonly Color GreyText = new Color(0.55f, 0.55f, 0.55f, 1f);
    private const string Close = "Close";

    private Button opener, confirm, back;
    private RectTransform box, gridRect;
    private GridLayoutGroup grid;
    private GameObject backdrop;
    private TMP_Text label, cost, confirmText, backText;
    private string labelWas, backWas;
    private Color confirmTextColor = Color.white;
    private UnityEventCallState[] backStates;

    private sealed class Cell
    {
        public UpgradeData item;
        public string key;
        public Button button;
        public Image frame;
        public Color normal;
        public Coroutine flash;
    }

    private readonly List<Cell> cells = new List<Cell>();
    private readonly HashSet<string> picked = new HashSet<string>();
    private bool isOpen;
    private Coroutine anim;
    private Vector2 fittedFor;

    private void Awake()
    {
        FindParts();
        if (opener != null) opener.onClick.AddListener(Open);
        if (confirm != null) confirm.onClick.AddListener(ConfirmPicks);
        if (box != null) box.gameObject.SetActive(false);
        if (backdrop != null) backdrop.SetActive(false);
    }

    private void OnDisable()
    {
        // the page went away under it: shut at once, the Back button as it was
        if (isOpen) Shut(true);
    }

    private void LateUpdate()
    {
        if (isOpen && gridRect != null && gridRect.rect.size != fittedFor) Fit();
    }

    // ---------------------------------------------------------------- open and close

    public void Open()
    {
        if (isOpen || box == null) return;
        isOpen = true;
        if (cells.Count == 0) Build();

        picked.Clear();
        foreach (var kv in RunCredits.PendingFavours) picked.Add(kv.Key);

        if (backdrop != null) backdrop.SetActive(true);
        box.gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
        Fit();
        ShowState();
        SwapBack(true);
        MenuNavigator.CloseModal = CloseIfOpen;

        if (anim != null) StopCoroutine(anim);
        anim = StartCoroutine(SlamIn());
    }

    private bool CloseIfOpen()
    {
        if (!isOpen) return false;
        Shut(false);
        return true;
    }

    private void Shut(bool atOnce)
    {
        isOpen = false;
        SwapBack(false);
        if (MenuNavigator.CloseModal == CloseIfOpen) MenuNavigator.CloseModal = null;
        if (label != null) label.text = labelWas;
        if (anim != null) StopCoroutine(anim);
        anim = null;
        if (atOnce || !isActiveAndEnabled) { Hidden(); return; }
        anim = StartCoroutine(DropAway());
    }

    // the box slams in (ScaleIn, the game's menus' way) over a backdrop fading up
    private IEnumerator SlamIn()
    {
        ScaleIn.Play(box.gameObject, 1.6f, 0.3f);
        var dim = Group(backdrop);
        for (float t = 0f; t < 0.15f; t += Time.unscaledDeltaTime)
        {
            if (dim != null) dim.alpha = t / 0.15f;
            yield return null;
        }
        if (dim != null) dim.alpha = 1f;
        anim = null;
    }

    // and drops away, shrinking a little as it fades
    private IEnumerator DropAway()
    {
        var group = Group(box.gameObject);
        var dim = Group(backdrop);
        Vector3 size = Vector3.one;
        const float seconds = 0.12f;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            float k = t / seconds;
            box.localScale = size * Mathf.Lerp(1f, 0.85f, k * k);
            if (group != null) group.alpha = 1f - k;
            if (dim != null) dim.alpha = 1f - k;
            yield return null;
        }
        Hidden();
        anim = null;
    }

    private void Hidden()
    {
        if (box != null)
        {
            box.localScale = Vector3.one;
            var group = Group(box.gameObject);
            if (group != null) group.alpha = 1f;
            box.gameObject.SetActive(false);
        }
        if (backdrop != null)
        {
            var dim = Group(backdrop);
            if (dim != null) dim.alpha = 1f;
            backdrop.SetActive(false);
        }
    }

    private static CanvasGroup Group(GameObject go)
    {
        if (go == null) return null;
        if (!go.TryGetComponent(out CanvasGroup g)) g = go.AddComponent<CanvasGroup>();
        return g;
    }

    // while the box is up, the page's Back button closes it and says so
    private void SwapBack(bool closing)
    {
        if (back == null) return;
        var click = back.onClick;
        if (closing)
        {
            backStates = new UnityEventCallState[click.GetPersistentEventCount()];
            for (int i = 0; i < backStates.Length; i++)
            {
                backStates[i] = click.GetPersistentListenerState(i);
                click.SetPersistentListenerState(i, UnityEventCallState.Off);
            }
            click.AddListener(CloseFromButton);
            if (backText != null) backText.text = Close;
        }
        else
        {
            click.RemoveListener(CloseFromButton);
            if (backStates != null)
                for (int i = 0; i < backStates.Length && i < click.GetPersistentEventCount(); i++)
                    click.SetPersistentListenerState(i, backStates[i]);
            backStates = null;
            if (backText != null) backText.text = backWas;
        }
    }

    private void CloseFromButton() => CloseIfOpen();

    // ---------------------------------------------------------------- the grid

    private void Build()
    {
        var catalog = UpgradeCatalog.Load();
        if (catalog == null || grid == null) return;
        var items = new List<UpgradeData>();
        foreach (var u in catalog.upgrades) if (RunCredits.CanFavour(u)) items.Add(u);

        // the cells look like the character cards' frames
        var look = LookOfCards();
        foreach (var u in items)
        {
            var go = new GameObject("Favor: " + u.GetBaseTitle(), typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(grid.transform, false);
            var frame = go.GetComponent<Image>();
            if (look != null)
            {
                frame.sprite = look.sprite;
                frame.type = look.type;
                frame.pixelsPerUnitMultiplier = look.pixelsPerUnitMultiplier;
                frame.color = look.color;
            }
            else frame.color = new Color(0.22f, 0.2f, 0.25f, 1f);
            var button = go.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = frame;

            // the item's icon inside, a little in from the frame
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)iconGo.transform;
            rt.SetParent(go.transform, false);
            rt.anchorMin = new Vector2(0.14f, 0.14f);
            rt.anchorMax = new Vector2(0.86f, 0.86f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var icon = iconGo.GetComponent<Image>();
            icon.sprite = u.icon;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.enabled = u.icon != null;

            var cell = new Cell { item = u, key = RunCredits.Key(u), button = button, frame = frame, normal = frame.color };
            cells.Add(cell);
            button.onClick.AddListener(() => Toggle(cell));
            Hover(go, u);
        }
    }

    // the name of what's under the pointer, in the box's title
    private void Hover(GameObject go, UpgradeData u)
    {
        var trigger = go.AddComponent<EventTrigger>();
        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => { if (label != null) label.text = $"{u.GetBaseTitle()}  ({u.CategoryLabel})"; });
        trigger.triggers.Add(enter);
        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => { if (label != null) label.text = labelWas; });
        trigger.triggers.Add(exit);
    }

    private static Image LookOfCards()
    {
        var card = FindFirstObjectByType<CharacterCard>(FindObjectsInactive.Include);
        if (card == null || !card.TryGetComponent(out Button b)) return null;
        return b.targetGraphic as Image;
    }

    // the biggest square cells that still fit every item in the box: every column count is tried,
    // and the one that leaves the largest cells wins
    private void Fit()
    {
        if (grid == null || gridRect == null) return;
        fittedFor = gridRect.rect.size;
        int n = Mathf.Max(1, cells.Count);
        if (grid.spacing == Vector2.zero) grid.spacing = new Vector2(6f, 6f);
        float w = gridRect.rect.width - grid.padding.horizontal, h = gridRect.rect.height - grid.padding.vertical;
        float sx = grid.spacing.x, sy = grid.spacing.y;
        float best = 0f;
        int bestCols = 1;
        for (int cols = 1; cols <= n; cols++)
        {
            int rows = Mathf.CeilToInt(n / (float)cols);
            float c = Mathf.Min((w - sx * (cols - 1)) / cols, (h - sy * (rows - 1)) / rows);
            if (c > best) { best = c; bestCols = cols; }
        }
        float side = Mathf.Max(8f, Mathf.Floor(best));
        grid.cellSize = new Vector2(side, side);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = bestCols;
        grid.childAlignment = TextAnchor.MiddleCenter;
    }

    // ---------------------------------------------------------------- picking

    private static int Budget => RunCredits.Count + RunCredits.PendingTotal;

    // a pick costs a ticket; one already waiting costs what was put on it
    private static int CostOf(string key) =>
        RunCredits.PendingFavours.TryGetValue(key, out int s) ? Mathf.Max(1, s) : 1;

    private int Spend
    {
        get { int n = 0; foreach (var k in picked) n += CostOf(k); return n; }
    }

    private void Toggle(Cell cell)
    {
        if (picked.Remove(cell.key)) { ShowState(); return; }
        if (Spend + 1 > Budget) { Refuse(cell); return; }
        picked.Add(cell.key);
        ShowState();
    }

    private bool Unchanged
    {
        get
        {
            if (picked.Count != RunCredits.PendingFavours.Count) return false;
            foreach (var kv in RunCredits.PendingFavours) if (!picked.Contains(kv.Key)) return false;
            return true;
        }
    }

    private void ShowState()
    {
        foreach (var c in cells)
            if (c.flash == null) c.frame.color = picked.Contains(c.key) ? Picked : c.normal;

        int budget = Budget, spend = Spend;
        if (cost != null)
        {
            if (budget == 0) cost.text = "No Tickets yet";
            else if (picked.Count == 0 && RunCredits.PendingTotal > 0) cost.text = $"Take back {Tickets(RunCredits.PendingTotal)}?";
            else if (picked.Count == 0) cost.text = $"{Tickets(budget)} to spend";
            else cost.text = $"Spend {Tickets(spend)}? ({budget - spend} left)";
        }

        // grey until it would change something
        bool can = !Unchanged;
        if (confirm != null) confirm.interactable = can;
        if (confirmText != null) confirmText.color = can ? confirmTextColor : GreyText;
    }

    private static string Tickets(int n) => n == 1 ? "1 Ticket" : $"{n} Tickets";

    private void ConfirmPicks()
    {
        if (Unchanged) return;
        var byKey = new Dictionary<string, UpgradeData>();
        foreach (var c in cells) byKey[c.key] = c.item;

        // what was let go comes back first, so its tickets can pay for the new picks
        var was = new List<KeyValuePair<string, int>>(RunCredits.PendingFavours);
        foreach (var kv in was)
            if (!picked.Contains(kv.Key) && byKey.TryGetValue(kv.Key, out var gone))
                for (int i = 0; i < kv.Value; i++) RunCredits.Unfavour(gone);
        foreach (var key in picked)
            if (!RunCredits.PendingFavours.ContainsKey(key) && byKey.TryGetValue(key, out var item))
                RunCredits.TryFavour(item);

        CloseIfOpen();
    }

    // more than the tickets held: a buzz, and the cell flashes red once
    private void Refuse(Cell cell)
    {
        var clip = Resources.Load<AudioClip>("Sfx/ui_locked");
        if (clip != null)
        {
            if (UISoundManager.Instance != null && UISoundManager.Instance.uiAudioSource != null) UISoundManager.Instance.PlayClick(clip);
            else SfxPlayer.PlayAt(clip, Camera.main != null ? Camera.main.transform.position : Vector3.zero, 0.9f);
        }
        if (cell.flash != null) StopCoroutine(cell.flash);
        cell.flash = StartCoroutine(Flash(cell));
    }

    private IEnumerator Flash(Cell cell)
    {
        const float seconds = 0.35f;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            float k = t / seconds;
            cell.frame.color = Color.Lerp(Refused, cell.normal, k * k);
            yield return null;
        }
        cell.flash = null;
        ShowState();
    }

    // ---------------------------------------------------------------- finding the parts

    private void FindParts()
    {
        foreach (var t in GetComponentsInChildren<RectTransform>(true))
        {
            if (t.name == "FavorItem")
            {
                if (t.TryGetComponent(out Button b)) opener = b;
                else if (t.Find("Grid") != null) box = t;
            }
            else if (t.name.IndexOf("backdrop", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                     t.name.IndexOf("favor", System.StringComparison.OrdinalIgnoreCase) >= 0) backdrop = t.gameObject;
            else if (t.name.Equals("backbutton", System.StringComparison.OrdinalIgnoreCase)) back = t.GetComponent<Button>();
        }
        if (box == null) return;
        gridRect = box.Find("Grid") as RectTransform;
        if (gridRect != null && !gridRect.TryGetComponent(out grid)) grid = gridRect.gameObject.AddComponent<GridLayoutGroup>();
        var c = box.Find("Confirm");
        if (c != null)
        {
            confirm = c.GetComponent<Button>();
            confirmText = c.GetComponentInChildren<TMP_Text>(true);
            if (confirmText != null) confirmTextColor = confirmText.color;
        }
        foreach (var t in box.GetComponentsInChildren<TMP_Text>(true))
        {
            if (t.name.IndexOf("Spent", System.StringComparison.OrdinalIgnoreCase) >= 0 || t.name.IndexOf("Cost", System.StringComparison.OrdinalIgnoreCase) >= 0) cost = t;
            else if (t.name == "Label") label = t;
        }
        if (label != null) labelWas = label.text;
        if (back != null)
        {
            backText = back.GetComponentInChildren<TMP_Text>(true);
            if (backText != null) backWas = backText.text;
        }
    }
}
