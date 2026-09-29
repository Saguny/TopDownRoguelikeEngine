using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// one card in the character grid. clicking it picks the character, hovering previews it. leave
// Character empty and the CharacterSelectScreen fills it from the StatCatalog; a card left
// without one goes blank and can't be clicked
[RequireComponent(typeof(Button))]
public class CharacterCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("leave empty to take the next character from the StatCatalog")]
    public CharacterData character;
    [Tooltip("optional. shows the portrait; one is made inside the card if empty")]
    [SerializeField] private Image portrait;
    [Tooltip("optional. shows the character's name")]
    [SerializeField] private TMP_Text nameText;
    [Tooltip("optional. shows the icon of the character's starting weapon; hidden while that weapon has no icon")]
    [SerializeField] private Image weaponIcon;
    [Tooltip("optional. switched on while this card's character is picked, e.g. a highlight frame")]
    [SerializeField] private GameObject selectedMark;
    [Tooltip("the card's own frame (the button's image) turns this colour while its character is picked")]
    [SerializeField] private Color selectedColor = new Color32(0xff, 0xd2, 0x3c, 0xff);

    private CharacterSelectScreen screen;
    private Button button;
    private Graphic frame;
    private Color frameColor = Color.white;

    private void Awake()
    {
        button = GetComponent<Button>();
        Frame();
        button.onClick.AddListener(() =>
        {
            if (screen != null && character != null) screen.Pick(character);
        });
    }

    public void Bind(CharacterSelectScreen owner, CharacterData data)
    {
        screen = owner;
        character = data;
        if (button == null) button = GetComponent<Button>();
        if (portrait == null) portrait = MakePortrait();
        if (weaponIcon == null) weaponIcon = OtherImage();

        button.interactable = character != null;
        portrait.sprite = character != null ? character.portrait : null;
        portrait.enabled = portrait.sprite != null;
        ShowLocked();
        if (nameText != null) nameText.text = character != null ? character.displayName : string.Empty;

        if (weaponIcon != null)
        {
            weaponIcon.sprite = character != null && character.startingWeapon != null ? character.startingWeapon.icon : null;
            weaponIcon.enabled = weaponIcon.sprite != null;
            weaponIcon.preserveAspect = true;
        }

        SetSelected(false);
    }

    // a character not bought yet: its figure and weapon blacked out, a lock in the corner
    public void ShowLocked()
    {
        bool locked = character != null && !CharacterUnlocks.IsUnlocked(character);
        if (portrait != null) portrait.color = locked ? Color.black : Color.white;
        if (weaponIcon != null) weaponIcon.color = locked ? Color.black : Color.white;

        var existing = transform.Find("Lock");
        if (existing == null)
        {
            var icon = VfxLibrary.Get != null ? VfxLibrary.Get.lockIcon : null;
            if (!locked || icon == null) return;
            var go = new GameObject("Lock", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);
            float side = Mathf.Max(16f, ((RectTransform)transform).rect.height * 0.3f);
            rt.sizeDelta = new Vector2(side * 14f / 16f, side);
            rt.anchoredPosition = new Vector2(0f, side * 0.35f);
            var image = go.AddComponent<Image>();
            image.sprite = icon;
            image.raycastTarget = false;
            existing = rt;
        }
        existing.gameObject.SetActive(locked);
    }

    public void SetSelected(bool selected)
    {
        if (selectedMark != null) selectedMark.SetActive(selected);
        var f = Frame();
        if (f != null) f.color = selected ? selectedColor : frameColor;
    }

    // the frame and the colour it was styled with, found once
    private Graphic Frame()
    {
        if (frame != null) return frame;
        if (button == null) button = GetComponent<Button>();
        frame = button != null && button.targetGraphic != null ? button.targetGraphic : GetComponent<Graphic>();
        if (frame != null) frameColor = frame.color;
        return frame;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (screen != null && character != null) screen.Preview(character);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (screen != null) screen.EndPreview();
    }

    // the card's weapon picture when the field is empty: its one child image that isn't the portrait
    private Image OtherImage()
    {
        foreach (Transform child in transform)
            if (child.name != "Lock" && child.TryGetComponent(out Image image) && image != portrait) return image;
        return null;
    }

    // a portrait image filling the card, inside a small margin
    private Image MakePortrait()
    {
        var go = new GameObject("Portrait", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(transform, false);
        rt.anchorMin = new Vector2(0.12f, 0.12f);
        rt.anchorMax = new Vector2(0.88f, 0.88f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var image = go.AddComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }
}
