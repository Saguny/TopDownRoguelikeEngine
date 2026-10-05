using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UpgradeMenuUI : MonoBehaviour
{
    [Header("UI Refs")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Button[] upgradeButtons;
    [SerializeField] private TMP_Text[] nameTexts;
    [SerializeField] private TMP_Text[] descriptionTexts;
    [SerializeField] private Image[] iconImages;
    [SerializeField] private Sprite defaultIcon;

    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text[] lvlText;
    [Tooltip("optional, one per card: says Weapon or Passive. empty puts it in front of the level text instead")]
    [SerializeField] private TMP_Text[] categoryTexts;

    [Header("Reroll, skip, banish (bought in the shop)")]
    [Tooltip("empty: the button under the panel with Reroll in its name. shown only once Reroll has been bought")]
    [SerializeField] private Button rerollButton;
    [Tooltip("empty: the button under the panel with Skip in its name. shown only once Skip has been bought")]
    [SerializeField] private Button skipButton;
    [Tooltip("empty: the button under the panel with Banish in its name. shown only once Banish has been bought: click it, then the card to banish")]
    [SerializeField] private Button banishButton;
    [SerializeField] private Color banishTint = new Color(1f, 0.45f, 0.45f);

    // what the level up can do besides picking a card, and how many of each are left
    public class Tools
    {
        public bool ownsReroll, ownsSkip, ownsBanish;
        public int rerolls, skips, banishes;
        public Func<List<UpgradeData>> reroll;
        public Action skip;
        // the card banished and the ones showing; returns the card to show in its place, or null
        public Func<UpgradeData, List<UpgradeData>, UpgradeData> banish;
    }

    [Header("Sound")]
    [Tooltip("played every time the player levels up, as the menu opens")]
    [SerializeField] private AudioClip levelUpSound;
    [SerializeField, Range(0f, 1f)] private float levelUpVolume = 1f;

    private readonly List<UpgradeData> current = new();
    private LevelUpTransition transition;
    private AudioSource voice;
    private Action<UpgradeData> onChosen;
    private int selectedIndex = 0;
    private Tools tools;
    private bool banishMode;
    private readonly Dictionary<Button, string> toolLabels = new Dictionary<Button, string>();
    private readonly Dictionary<Button, Color> cardColors = new Dictionary<Button, Color>();

    // a card's own colour, whatever banishing tints it
    private void Tint(Button b, bool banish)
    {
        if (b == null || b.targetGraphic == null) return;
        if (!cardColors.TryGetValue(b, out var own)) cardColors[b] = own = b.targetGraphic.color;
        b.targetGraphic.color = banish ? own * banishTint : own;
    }

    // the card under the mouse, or -1. it wins the preview over the keyboard's selection, so a
    // movement key still held from the game can't drag the preview off the card being looked at
    private int hoveredIndex = -1;
    // the keyboard picked the selection since the mouse last moved: only then does a card that
    // isn't under the mouse preview. otherwise, off every card, the stats show as they are now
    private bool keyboardDriven;
    private Vector2 lastMouse;
    private bool navigationWas = true;
    private bool navigationOff;

    private void Awake()
    {
        if (panel != null)
        {
            panel.SetActive(false);
            WenRain.AddTo(panel);
            if (!panel.TryGetComponent(out transition)) transition = panel.AddComponent<LevelUpTransition>();
        }

        if (panel != null)
        {
            if (rerollButton == null) rerollButton = FindTool("reroll");
            if (skipButton == null) skipButton = FindTool("skip");
            if (banishButton == null) banishButton = FindTool("banish");
            Wire(rerollButton, Reroll);
            Wire(skipButton, SkipThis);
            Wire(banishButton, ToggleBanish);
        }

        if (upgradeButtons != null)
        {
            for (int i = 0; i < upgradeButtons.Length; i++)
            {
                var b = upgradeButtons[i];
                if (b == null) continue;
                b.onClick.RemoveAllListeners();

                // hovering a card selects it, same as the arrow keys, so the loadout can preview it
                if (!b.TryGetComponent(out EventTrigger trigger)) trigger = b.gameObject.AddComponent<EventTrigger>();
                var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                int index = i;
                enter.callback.AddListener(_ => { hoveredIndex = index; FocusButton(index); });
                trigger.triggers.Add(enter);
                var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                exit.callback.AddListener(_ => { if (hoveredIndex == index) hoveredIndex = -1; });
                trigger.triggers.Add(exit);
            }
        }
    }

    private void Update()
    {
        if (panel == null || !panel.activeSelf) return;
        Vector2 mouse = Input.mousePosition;
        if ((mouse - lastMouse).sqrMagnitude > 4f) keyboardDriven = false;
        lastMouse = mouse;
        HandleKeyboard();
    }

    private Button FindTool(string name)
    {
        foreach (var b in panel.GetComponentsInChildren<Button>(true))
        {
            if (upgradeButtons != null && Array.IndexOf(upgradeButtons, b) >= 0) continue;
            if (b.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0) return b;
        }
        return null;
    }

    private static void Wire(Button b, UnityEngine.Events.UnityAction action)
    {
        if (b == null) return;
        b.onClick.AddListener(action);
    }

    public void Open(List<UpgradeData> upgrades, Action<UpgradeData> callback, int level, Tools with = null)
    {
        tools = with;
        banishMode = false;
        if (panel == null) return;
        onChosen = callback;
        PlayLevelUpSound();

        if (levelText != null)
            levelText.text = $"Current Level: {level - 1}";

        var filtered = new List<UpgradeData>();
        if (upgrades != null)
        {
            foreach (var u in upgrades)
            {
                if (u == null) continue;
                if (u.CanOffer) filtered.Add(u);
            }
        }

        if (filtered.Count == 0)
        {
            Close();
            onChosen?.Invoke(null);
            return;
        }

        panel.SetActive(true);
        hoveredIndex = -1;
        keyboardDriven = false;
        lastMouse = Input.mousePosition;
        // the menu steps through its cards itself (HandleKeyboard). the event system's own
        // navigation is off while it's open: a movement key still held from the game would
        // otherwise keep moving the selection on its own
        var es = EventSystem.current;
        if (es != null && !navigationOff)
        {
            navigationWas = es.sendNavigationEvents;
            es.sendNavigationEvents = false;
            navigationOff = true;
        }
        current.Clear();
        current.AddRange(filtered);
        Fill();
        ShowTools();

        selectedIndex = FirstActiveIndex();
        FocusButton(selectedIndex);
        if (transition != null) transition.Play();
    }

    // the cards for what's in `current`
    private void Fill()
    {
        for (int i = 0; i < upgradeButtons.Length; i++)
        {
            bool has = i < current.Count;
            var btn = upgradeButtons[i];
            if (btn == null) continue;

            btn.gameObject.SetActive(has);
            btn.onClick.RemoveAllListeners();
            if (!has) continue;

            var data = current[i];

            if (nameTexts != null && i < nameTexts.Length && nameTexts[i] != null)
                nameTexts[i].text = data.GetDisplayTitle();

            bool ownCategoryText = categoryTexts != null && i < categoryTexts.Length && categoryTexts[i] != null;
            if (ownCategoryText)
                categoryTexts[i].text = data.CategoryLabel;

            // a favoured item (a ticket spent on it before the run) says so where a new one says New,
            // and its card wears a rainbow
            bool favoured = RunCredits.IsFavoured(data);
            string progress = favoured && data.Level == 0 ? "Favored" : data.GetLevelProgress();
            if (lvlText != null && i < lvlText.Length && lvlText[i] != null)
                lvlText[i].text = ownCategoryText ? progress : $"{data.CategoryLabel}   {progress}";
            RainbowFrame.Set(btn.gameObject, favoured);

            if (descriptionTexts != null && i < descriptionTexts.Length && descriptionTexts[i] != null)
                descriptionTexts[i].text = data.GetDisplayDescription();

            if (iconImages != null && i < iconImages.Length && iconImages[i] != null)
            {
                Sprite spriteToUse = data.CardIcon != null ? data.CardIcon : defaultIcon;

                if (spriteToUse != null)
                {
                    iconImages[i].sprite = spriteToUse;
                    iconImages[i].enabled = true;

                    // weapons with an idle loop play it on the card
                    if (!iconImages[i].TryGetComponent(out IconAnimator anim)) anim = iconImages[i].gameObject.AddComponent<IconAnimator>();
                    anim.Play(data.CardIconFrames, data.iconFps);
                }
                else
                {
                    iconImages[i].enabled = false;
                }
            }

            // hovering a card shows its weapon's numbers now and after the pick
            if (!btn.TryGetComponent(out TooltipTrigger tip)) tip = btn.gameObject.AddComponent<TooltipTrigger>();
            tip.Item = data;
            tip.compare = true;

            int index = i;
            btn.onClick.AddListener(() => Choose(index));
            Tint(btn, false);
        }
    }

    // ---------------------------------------------------------------- reroll, skip, banish

    private void ShowTools()
    {
        Show(rerollButton, tools != null && tools.ownsReroll, tools != null ? tools.rerolls : 0);
        Show(skipButton, tools != null && tools.ownsSkip, tools != null ? tools.skips : 0);
        Show(banishButton, tools != null && tools.ownsBanish, tools != null ? tools.banishes : 0);
    }

    // after a reroll, skip or banish is spent
    public void SetCharges(int rerolls, int skips, int banishes)
    {
        if (tools == null) return;
        tools.rerolls = rerolls;
        tools.skips = skips;
        tools.banishes = banishes;
        ShowTools();
    }

    // the button shows how many are left: in a text with Count in its name, or after its label
    private void Show(Button b, bool owned, int left)
    {
        if (b == null) return;
        b.gameObject.SetActive(owned);
        if (!owned) return;
        b.interactable = left > 0;
        TMP_Text count = null, label = null;
        foreach (var t in b.GetComponentsInChildren<TMP_Text>(true))
        {
            if (t.name.IndexOf("count", StringComparison.OrdinalIgnoreCase) >= 0) count = t;
            else if (label == null) label = t;
        }
        if (count != null) count.text = left.ToString();
        else if (label != null)
        {
            if (!toolLabels.TryGetValue(b, out var baseText)) toolLabels[b] = baseText = label.text;
            label.text = $"{baseText} ({left})";
        }
    }

    private void Reroll()
    {
        if (!IsOpen || tools?.reroll == null || tools.rerolls <= 0) return;
        var fresh = tools.reroll();
        if (fresh == null) return;
        banishMode = false;
        current.Clear();
        foreach (var u in fresh) if (u != null && u.CanOffer) current.Add(u);
        Fill();
        ShowTools();
        selectedIndex = FirstActiveIndex();
        FocusButton(selectedIndex);
        if (transition != null) transition.Play();
        PlayLevelUpSound();
    }

    private void SkipThis()
    {
        if (!IsOpen || tools?.skip == null || tools.skips <= 0) return;
        Close();
        tools.skip();
    }

    // on: the next card clicked is banished instead of taken
    private void ToggleBanish()
    {
        if (!IsOpen || tools?.banish == null || tools.banishes <= 0) return;
        banishMode = !banishMode;
        foreach (var b in upgradeButtons) Tint(b, banishMode);
    }

    private void BanishCard(int index)
    {
        banishMode = false;
        var replacement = tools.banish(current[index], current);
        if (replacement != null) current[index] = replacement;
        else current.RemoveAt(index);
        if (current.Count == 0)
        {
            Close();
            onChosen?.Invoke(null);
            return;
        }
        Fill();
        ShowTools();
        selectedIndex = Mathf.Clamp(index, 0, current.Count - 1);
        FocusButton(selectedIndex);
    }

    // its own 2D voice rather than one on the panel (a closed panel can't play, and the menu can
    // close straight away with nothing to offer), through the mixer's SFX group so the SFX volume
    // applies
    private void PlayLevelUpSound()
    {
        if (levelUpSound == null) return;
        if (voice == null)
        {
            voice = new GameObject("Level Up Sound").AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0f;
            var groups = GameSettings.Mixer != null ? GameSettings.Mixer.FindMatchingGroups("SFX") : null;
            if (groups != null && groups.Length > 0) voice.outputAudioMixerGroup = groups[0];
        }
        voice.PlayOneShot(levelUpSound, levelUpVolume);
    }

    public void Close()
    {
        hoveredIndex = -1;
        banishMode = false;
        if (navigationOff)
        {
            navigationOff = false;
            if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = navigationWas;
        }
        if (panel != null)
            panel.SetActive(false);
    }

    private void Choose(int index)
    {
        if (index < 0 || index >= current.Count) return;
        if (banishMode && tools?.banish != null) { BanishCard(index); return; }
        Close();
        onChosen?.Invoke(current[index]);
    }

    private void HandleKeyboard()
    {
        if (transition != null && transition.Landing) return;   // not while the window flies in
        int step = 0;
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) step = -1;
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) step = +1;
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) step = -1;
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) step = +1;

        if (step != 0)
        {
            hoveredIndex = -1;      // the keyboard takes over from the mouse
            keyboardDriven = true;
            selectedIndex = NextActiveIndex(selectedIndex, step);
            FocusButton(selectedIndex);
        }

        // R rerolls, X skips, B picks a card to banish, when they've been bought
        if (Input.GetKeyDown(KeyCode.R)) Reroll();
        if (Input.GetKeyDown(KeyCode.X)) { SkipThis(); return; }
        if (Input.GetKeyDown(KeyCode.B)) ToggleBanish();

        if (Input.GetKeyDown(KeyCode.Return) ||
            Input.GetKeyDown(KeyCode.KeypadEnter) ||
            Input.GetKeyDown(KeyCode.Space))
        {
            if (IsActive(selectedIndex))
                Choose(selectedIndex);
        }
    }

    private int FirstActiveIndex()
    {
        for (int i = 0; i < upgradeButtons.Length; i++)
            if (IsActive(i)) return i;
        return -1;
    }

    private int NextActiveIndex(int start, int step)
    {
        if (upgradeButtons == null || upgradeButtons.Length == 0) return -1;
        int count = upgradeButtons.Length;
        int i = start;
        for (int k = 0; k < count; k++)
        {
            i = (i + step + count) % count;
            if (IsActive(i)) return i;
        }
        return start;
    }

    private bool IsActive(int i)
    {
        if (i < 0 || i >= upgradeButtons.Length) return false;
        var b = upgradeButtons[i];
        return b != null && b.gameObject.activeSelf && b.interactable;
    }

    private void FocusButton(int i)
    {
        if (!IsActive(i)) return;
        var b = upgradeButtons[i];
        EventSystem es = EventSystem.current;
        if (es != null)
            es.SetSelectedGameObject(b.gameObject);
        selectedIndex = i;
    }

    // expose open state so other systems (pause menu) can check it
    public bool IsOpen => panel != null && panel.activeSelf;

    // the card to preview: the one under the mouse, or the keyboard's selection while the
    // keyboard is in charge. null (the stats as they are) when the mouse is off every card
    public UpgradeData Highlighted
    {
        get
        {
            if (!IsOpen) return null;
            if (hoveredIndex >= 0 && hoveredIndex < current.Count && IsActive(hoveredIndex)) return current[hoveredIndex];
            if (!keyboardDriven) return null;
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            int index = selectedIndex;
            for (int i = 0; selected != null && i < upgradeButtons.Length; i++)
                if (upgradeButtons[i] != null && upgradeButtons[i].gameObject == selected) index = i;
            return index >= 0 && index < current.Count && IsActive(index) ? current[index] : null;
        }
    }
}
