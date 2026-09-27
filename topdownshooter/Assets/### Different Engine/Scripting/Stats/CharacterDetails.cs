using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// the box showing one character: name, description, bonuses, starting weapon and its idle
// animation. every field is optional, so fill in only what the layout has
public class CharacterDetails : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descriptionText;
    [Tooltip("one line per stat bonus, written from the character's bonuses, e.g. +2 Arrow Count")]
    [SerializeField] private TMP_Text bonusesText;
    [Tooltip("gets the weapon's name only, e.g. Bow")]
    [SerializeField] private TMP_Text startingWeaponText;
    [Tooltip("plays the character's idle animation, or shows the portrait when it has no animations")]
    [SerializeField] private Image preview;

    // the animation clips drive a SpriteRenderer, which a UI Image can't be, so they run on a
    // hidden one and each frame is copied into the image
    private Animator previewAnimator;
    private SpriteRenderer previewSprite;

    public void Show(CharacterData character)
    {
        // one not bought yet shows as a silhouette, its weapon unknown
        bool locked = character != null && !CharacterUnlocks.IsUnlocked(character);
        if (startingWeaponText == null) startingWeaponText = WeaponLabel();
        if (nameText != null) nameText.text = character != null ? character.displayName : string.Empty;
        if (descriptionText != null) descriptionText.text = character == null ? string.Empty : locked ? "???" : character.description;
        if (bonusesText != null) bonusesText.text = character != null ? Bonuses(character) : string.Empty;
        if (startingWeaponText != null) startingWeaponText.text = character == null ? string.Empty : locked ? "???" : character.StartingWeaponName();
        ShowPreview(character);
        if (preview != null) preview.color = locked ? Color.black : Color.white;
    }

    // the weapon line when the field is empty: the child text named for the weapon, e.g. CharacterWeapon
    private TMP_Text WeaponLabel()
    {
        foreach (var text in GetComponentsInChildren<TMP_Text>(true))
            if (text.name.Contains("Weapon") && text != nameText && text != descriptionText && text != bonusesText) return text;
        return null;
    }

    private static string Bonuses(CharacterData character)
    {
        var catalog = StatCatalog.Load();
        var text = new StringBuilder();
        foreach (var bonus in character.bonuses)
        {
            if (Mathf.Abs(bonus.value) < 0.0001f) continue;

            var def = catalog.Get(bonus.stat);
            string amount = def != null ? def.FormatBonus(bonus.value) : bonus.value.ToString("+0.##;-0.##");
            string label = def != null && !string.IsNullOrEmpty(def.label) ? def.label : bonus.stat.ToString();
            if (text.Length > 0) text.Append('\n');
            text.Append(amount).Append(' ').Append(label);
        }
        foreach (var perk in character.PerkLines())
        {
            if (text.Length > 0) text.Append('\n');
            text.Append(perk);
        }
        return text.ToString();
    }

    private void ShowPreview(CharacterData character)
    {
        if (preview == null) return;

        preview.preserveAspect = true;
        var animations = character != null ? character.animations : null;

        if (animations != null)
        {
            if (previewAnimator == null) MakePreviewAnimator();
            previewAnimator.runtimeAnimatorController = animations;
            previewAnimator.Rebind();
            previewAnimator.Update(0f);
        }
        else if (previewAnimator != null)
        {
            previewAnimator.runtimeAnimatorController = null;
        }

        var sprite = animations != null && previewSprite.sprite != null
            ? previewSprite.sprite
            : character != null ? character.portrait : null;
        preview.sprite = sprite;
        preview.enabled = sprite != null;
    }

    private void LateUpdate()
    {
        if (preview == null || previewAnimator == null || previewAnimator.runtimeAnimatorController == null) return;
        if (previewSprite.sprite != null && preview.sprite != previewSprite.sprite) preview.sprite = previewSprite.sprite;
    }

    private void MakePreviewAnimator()
    {
        var go = new GameObject("Idle Preview (hidden)");
        go.transform.SetParent(transform, false);

        previewSprite = go.AddComponent<SpriteRenderer>();
        previewSprite.enabled = false;

        previewAnimator = go.AddComponent<Animator>();
        previewAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
    }
}
