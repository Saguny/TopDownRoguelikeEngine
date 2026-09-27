using UnityEngine;
using UnityEngine.EventSystems;

// makes a level up card or a loadout slot show its item's attributes (ItemTooltip): on hover, and
// pinned with a click, for anyone who'd rather click than hover. a second click, or moving to
// another item, lets it go. added by LoadoutPanel and UpgradeMenuUI; set Item as the slot changes
public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [System.NonSerialized] public UpgradeData Item;
    [Tooltip("also show what the next pick of it changes, as on a level up card")]
    public bool compare;

    private bool pinned;

    public void OnPointerEnter(PointerEventData e)
    {
        if (Item != null) ItemTooltip.Show(Item, (RectTransform)transform, compare, this);
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (!pinned) ItemTooltip.Hide(this);
    }

    public void OnPointerClick(PointerEventData e)
    {
        // a level up card's click takes the pick; only the loadout's slots pin
        if (compare || Item == null) return;
        pinned = !(pinned && ItemTooltip.IsShowingFor(this));
        if (pinned) ItemTooltip.Show(Item, (RectTransform)transform, compare, this);
        else ItemTooltip.Hide(this);
    }

    private void OnDisable()
    {
        pinned = false;
        ItemTooltip.Hide(this);
    }
}
