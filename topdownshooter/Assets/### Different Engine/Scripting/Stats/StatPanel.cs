using TMPro;
using UnityEngine;

// the character stat panel on the select screen. build the rows however you like and put a
// StatRow on each; this fills in the values for the shown character and redraws when global
// upgrade ranks change. in a game scene (the level up screen) it shows the player's numbers as
// they are right now, and previews in green what the highlighted card would change
public class StatPanel : MonoBehaviour
{
    public enum Source { Auto, CharacterSheet, LiveRun }

    [Tooltip("Auto: the player's live numbers in a game scene, the character sheet in the menu")]
    [SerializeField] private Source source = Source.Auto;

    [Tooltip("optional. shows the starting weapons, e.g. Bow, Aura")]
    [SerializeField] private TMP_Text startingWeapon;
    [Tooltip("optional. shows the character's name")]
    [SerializeField] private TMP_Text characterName;

    [Header("Value colours")]
    [SerializeField] private Color better = new Color(1f, 0.84f, 0.2f);
    [SerializeField] private Color worse = new Color(1f, 0.42f, 0.36f);

    [Header("Upgrade preview (shop)")]
    [Tooltip("colour of the values a previewed upgrade would change")]
    [SerializeField] private Color preview = new Color32(0x6B, 0xFF, 0x00, 0xFF);
    [Tooltip("{0} = the value now, {1} = the value after buying, e.g. \"{0} > {1}\"")]
    [SerializeField] private string previewFormat = "{1}";

    private CharacterData shown;
    private GlobalUpgradeDef previewing;

    private StatContext liveStats;
    private UpgradeMenuUI levelUpMenu;
    private UpgradeData livePreview;
    private bool IsLive => source == Source.LiveRun || (source == Source.Auto && liveStats != null);

    public CharacterData Shown => shown;

    private void OnEnable()
    {
        MetaProgress.Changed += Refresh;
        if (source != Source.CharacterSheet && liveStats == null) liveStats = FindFirstObjectByType<StatContext>();
        if (IsLive)
        {
            if (levelUpMenu == null) levelUpMenu = GetComponentInParent<UpgradeMenuUI>(true);
            if (levelUpMenu == null) levelUpMenu = FindFirstObjectByType<UpgradeMenuUI>(FindObjectsInactive.Include);
            livePreview = null;
            Refresh();
        }
        else
        {
            ShowPicked();
        }
    }

    // live: follow the highlighted level up card
    private void Update()
    {
        if (!IsLive || levelUpMenu == null) return;
        var highlighted = levelUpMenu.Highlighted;
        if (highlighted == livePreview) return;
        livePreview = highlighted;
        Refresh();
    }

    private void OnDisable() => MetaProgress.Changed -= Refresh;

    // hook a character button's OnClick here and drag the character in: picks it for the run
    public void Pick(CharacterData character)
    {
        if (CharacterUnlocks.IsUnlocked(character)) CharacterSelection.Current = character;
        Show(character);
    }

    // shows a character without picking it, e.g. while its button is hovered
    public void Show(CharacterData character)
    {
        shown = character;
        Refresh();
    }

    // back to the picked character, e.g. when the hover ends
    public void ShowPicked()
    {
        var picked = CharacterSelection.Current;
        Show(picked != null ? picked : StatCatalog.Load().DefaultCharacter);
    }

    // shows, in the preview colour, what buying the upgrade's next rank would change
    public void ShowPreview(GlobalUpgradeDef upgrade)
    {
        previewing = upgrade;
        Refresh();
    }

    public void EndPreview()
    {
        previewing = null;
        Refresh();
    }

    public void Refresh()
    {
        StatSheet sheet, after;
        if (IsLive)
        {
            sheet = StatSheet.Live(liveStats);
            after = livePreview != null ? StatSheet.Live(liveStats, livePreview) : null;
        }
        else
        {
            sheet = StatSheet.For(shown);
            after = previewing != null ? StatSheet.For(shown, null, previewing) : null;
        }
        foreach (var row in GetComponentsInChildren<StatRow>(true))
        {
            row.Show(sheet, better, worse);
            if (after != null && Mathf.Abs(after[row.stat] - sheet[row.stat]) > 0.0001f)
                row.ShowPreview(sheet, after, preview, previewFormat);
        }

        if (startingWeapon != null) startingWeapon.text = shown != null ? shown.StartingWeaponName() : "Bow";
        if (characterName != null) characterName.text = shown != null ? shown.displayName : string.Empty;
    }

    // right click the component to see real numbers while laying the panel out
    [ContextMenu("Preview Values")]
    private void Preview() => ShowPicked();
}
