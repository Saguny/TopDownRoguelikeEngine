using UnityEngine;
using UnityEngine.EventSystems;

// a click on something locked (a map not earned yet): its own Button or Toggle is switched off, so
// it can't be picked and the menu's stars don't circle it, and this hears the click instead so
// the menu can say no (MapSelectionMenu: a buzz and a red flash)
public class LockedClick : MonoBehaviour, IPointerClickHandler
{
    public bool locked;
    public System.Action clicked;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (locked && eventData.button == PointerEventData.InputButton.Left) clicked?.Invoke();
    }
}
