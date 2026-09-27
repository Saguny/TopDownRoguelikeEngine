using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// the character select screen: put it on the panel that holds the cards, the stat panel and the
// character details. it finds all three among its children, fills empty cards from the
// StatCatalog, and keeps them showing the same character. when the catalog has more characters
// than there are cards, the first card is copied into the empty placeholder slots beside it.
// a character not bought yet can be looked at but not played: while one is picked, the Next
// button gives way to a Buy button with its price
public class CharacterSelectScreen : MonoBehaviour
{
    [Tooltip("show the hovered card's character until the mouse leaves it")]
    [SerializeField] private bool previewOnHover = true;
    [Tooltip("the button on to the map. empty: the child called NextButton")]
    [SerializeField] private Button nextButton;

    private StatPanel stats;
    private CharacterDetails details;
    private CharacterCard[] cards;
    private CharacterData picked;
    private Button buyButton;
    private TMP_Text buyLabel;

    public CharacterData Picked => picked;

    private void Awake()
    {
        stats = GetComponentInChildren<StatPanel>(true);
        details = GetComponentInChildren<CharacterDetails>(true);
        cards = GetComponentsInChildren<CharacterCard>(true);
        AddCardsForEveryone();
        FillCards();
        MakeBuyButton();
    }

    // opening the screen shows whoever is picked, or the first character in the catalog
    private void OnEnable()
    {
        CharacterUnlocks.Changed += Redraw;
        Coins.Changed += RefreshBuy;
        var current = CharacterSelection.Current;
        Pick(current != null ? current : StatCatalog.Load().DefaultCharacter);
    }

    private void OnDisable()
    {
        CharacterUnlocks.Changed -= Redraw;
        Coins.Changed -= RefreshBuy;
    }

    // one card per character: copies of the first card go into the placeholder slots next to it
    private void AddCardsForEveryone()
    {
        if (cards.Length == 0) return;
        int wanted = 0;
        foreach (var c in StatCatalog.Load().characters) if (c != null) wanted++;
        if (wanted <= cards.Length) return;

        var first = cards[0];
        var slots = first.transform.parent;
        var added = new List<CharacterCard>(cards);
        for (int i = 0; i < slots.childCount && added.Count < wanted; i++)
        {
            var slot = slots.GetChild(i) as RectTransform;
            if (slot == null || !slot.gameObject.activeSelf || slot.GetComponent<CharacterCard>() != null) continue;
            if (!slot.name.StartsWith("CharCardPlaceholder")) continue;

            var copy = Instantiate(first.gameObject, slots);
            copy.name = "CharCard " + (added.Count + 1);
            var rt = (RectTransform)copy.transform;
            rt.anchorMin = slot.anchorMin;
            rt.anchorMax = slot.anchorMax;
            rt.pivot = slot.pivot;
            rt.sizeDelta = slot.sizeDelta;
            rt.anchoredPosition = slot.anchoredPosition;
            rt.SetSiblingIndex(slot.GetSiblingIndex());
            slot.gameObject.SetActive(false);

            var card = copy.GetComponent<CharacterCard>();
            card.character = null;
            added.Add(card);
        }
        cards = added.ToArray();
    }

    // cards with a character set keep it; the empty ones get the catalog's other characters in order
    private void FillCards()
    {
        // a card copied from another still holds the other's character: it counts as empty
        var onCards = new HashSet<CharacterData>();
        foreach (var card in cards)
            if (card.character != null && !onCards.Add(card.character)) card.character = null;

        var rest = new Queue<CharacterData>();
        foreach (var c in StatCatalog.Load().characters)
            if (c != null && !onCards.Contains(c)) rest.Enqueue(c);

        foreach (var card in cards)
            card.Bind(this, card.character != null ? card.character : rest.Count > 0 ? rest.Dequeue() : null);
    }

    // picks the character for the run, or, not bought yet, shows it with its price
    public void Pick(CharacterData character)
    {
        if (character == null) return;

        picked = character;
        if (CharacterUnlocks.IsUnlocked(character)) CharacterSelection.Current = character;
        Show(character);

        foreach (var card in cards)
            card.SetSelected(card.character == character);
        RefreshBuy();
    }

    public void Preview(CharacterData character)
    {
        if (previewOnHover && character != null) Show(character);
    }

    public void EndPreview()
    {
        if (previewOnHover && picked != null) Show(picked);
    }

    private void Show(CharacterData character)
    {
        if (stats != null) stats.Show(character);
        if (details != null) details.Show(character);
    }

    private void Redraw()
    {
        foreach (var card in cards) card.ShowLocked();
        if (picked != null) Pick(picked);
    }

    // ---------------------------------------------------------------- buying

    // a copy of the Next button, in its place, that buys the picked character instead
    private void MakeBuyButton()
    {
        if (nextButton == null)
            foreach (var b in GetComponentsInChildren<Button>(true))
                if (b.name == "NextButton") { nextButton = b; break; }
        if (nextButton == null) return;

        var copy = Instantiate(nextButton.gameObject, nextButton.transform.parent);
        copy.name = "BuyButton";
        copy.transform.SetSiblingIndex(nextButton.transform.GetSiblingIndex() + 1);
        buyButton = copy.GetComponent<Button>();
        buyButton.onClick = new Button.ButtonClickedEvent();     // not the Next button's way on
        buyButton.onClick.AddListener(Buy);
        buyLabel = copy.GetComponentInChildren<TMP_Text>(true);
        copy.SetActive(false);
    }

    private void Buy()
    {
        if (picked != null) CharacterUnlocks.TryBuy(picked);
    }

    private void RefreshBuy()
    {
        if (buyButton == null || nextButton == null) return;
        bool locked = picked != null && !CharacterUnlocks.IsUnlocked(picked);
        nextButton.gameObject.SetActive(!locked);
        buyButton.gameObject.SetActive(locked);
        if (!locked) return;

        int price = CharacterUnlocks.NextPrice;
        buyButton.interactable = CharacterUnlocks.CanAfford;
        if (buyLabel != null) buyLabel.text = $"Buy {price}";
    }
}
