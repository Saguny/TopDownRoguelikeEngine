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

    [Header("Sound")]
    [Tooltip("played every time the player levels up, as the menu opens")]
    [SerializeField] private AudioClip levelUpSound;
    [SerializeField, Range(0f, 1f)] private float levelUpVolume = 1f;

    private readonly List<UpgradeData> current = new();
    private LevelUpTransition transition;
    private AudioSource voice;
    private Action<UpgradeData> onChosen;
    private int selectedIndex = 0;

    private void Awake()
    {
        if (panel != null)
        {
            panel.SetActive(false);
            WenRain.AddTo(panel);
            if (!panel.TryGetComponent(out transition)) transition = panel.AddComponent<LevelUpTransition>();
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
                enter.callback.AddListener(_ => FocusButton(index));
                trigger.triggers.Add(enter);
            }
        }
    }

    private void Update()
    {
        if (panel == null || !panel.activeSelf) return;
        HandleKeyboard();
    }

    public void Open(List<UpgradeData> upgrades, Action<UpgradeData> callback, int level)
    {
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
        current.Clear();
        current.AddRange(filtered);

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

            if (lvlText != null && i < lvlText.Length && lvlText[i] != null)
                lvlText[i].text = ownCategoryText ? data.GetLevelProgress() : $"{data.CategoryLabel}   {data.GetLevelProgress()}";

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

            int index = i;
            btn.onClick.AddListener(() => Choose(index));
        }

        selectedIndex = FirstActiveIndex();
        FocusButton(selectedIndex);
        if (transition != null) transition.Play();
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
        if (panel != null)
            panel.SetActive(false);
    }

    private void Choose(int index)
    {
        if (index < 0 || index >= current.Count) return;
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
            selectedIndex = NextActiveIndex(selectedIndex, step);
            FocusButton(selectedIndex);
        }

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

    // the card that's selected or hovered right now, or null
    public UpgradeData Highlighted
    {
        get
        {
            if (!IsOpen) return null;
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            int index = selectedIndex;
            for (int i = 0; selected != null && i < upgradeButtons.Length; i++)
                if (upgradeButtons[i] != null && upgradeButtons[i].gameObject == selected) index = i;
            return index >= 0 && index < current.Count && IsActive(index) ? current[index] : null;
        }
    }
}
