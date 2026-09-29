using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// the map selection page. one card per map in the MapCatalog: a Toggle on the card, with
// MapImage, MapName and MapDescription children filled in from the catalog, and exactly one card
// picked at a time, its backdrop yellow. a locked map can't be picked: clicking it buzzes and
// flashes it red. the endless toggle, remembered and locked until endless is earned. and Start
// Game, which starts the run behind the loading panel. fields left empty are found by name
public class MapSelectionMenu : MonoBehaviour
{
    [Tooltip("one toggle per map, in catalog order. cards past the last map are hidden. empty: every MapBackdrop toggle under this panel")]
    [SerializeField] private List<Toggle> mapCards = new List<Toggle>();
    [Tooltip("empty: the child called Endless Mode Toggle")]
    [SerializeField] private Toggle endlessToggle;
    [Tooltip("optional. says how many more runs until endless unlocks")]
    [SerializeField] private TMP_Text endlessLockText;
    [Tooltip("empty: the child called StartGameButton")]
    [SerializeField] private Button startButton;
    [Tooltip("shown from Start Game until the run is on screen. empty: the scene's LoadingPanel")]
    [SerializeField] private GameObject loadingPanel;
    [Tooltip("empty: the scene the MainMenu component starts")]
    [SerializeField] private string gameSceneName;
    [Tooltip("the picked map's card (its backdrop, the toggle's image) turns this colour, like a picked character's")]
    [SerializeField] private Color selectedColor = new Color32(0xff, 0xd2, 0x3c, 0xff);

    [Tooltip("a locked map, clicked, flashes this colour once")]
    [SerializeField] private Color lockedFlash = new Color32(0xff, 0x3a, 0x3a, 0xff);
    [SerializeField, Min(0.05f)] private float lockedFlashSeconds = 0.35f;
    [Tooltip("played when a locked map is clicked. empty: Resources/Sfx/ui_locked")]
    [SerializeField] private AudioClip lockedSound;

    // each card's backdrop colour as it was styled, to go back to when another map is picked
    private readonly Dictionary<Graphic, Color> styled = new Dictionary<Graphic, Color>();
    private readonly Dictionary<Toggle, Coroutine> flashing = new Dictionary<Toggle, Coroutine>();

    private const string EndlessKey = "menu_endless";
    private const string FallbackScene = "Scenes/Courtyard_Map";

    private void Awake()
    {
        FindParts();

        // exactly one card on at a time: a group on the cards' parent that can't be emptied
        ToggleGroup group = null;
        if (mapCards.Count > 0 && mapCards[0] != null)
        {
            var holder = mapCards[0].transform.parent.gameObject;
            if (!holder.TryGetComponent(out group)) group = holder.AddComponent<ToggleGroup>();
            group.allowSwitchOff = false;
        }

        for (int i = 0; i < mapCards.Count; i++)
        {
            if (mapCards[i] == null) continue;
            int index = i;
            mapCards[i].group = group;
            var card = mapCards[i];
            if (card.targetGraphic != null) styled[card.targetGraphic] = card.targetGraphic.color;
            card.onValueChanged.AddListener(on =>
            {
                if (on) MapSelection.Index = index;
                Tint(card);
            });
            if (!card.TryGetComponent(out LockedClick lockedClick)) lockedClick = card.gameObject.AddComponent<LockedClick>();
            lockedClick.clicked = () => Refuse(card);
        }

        if (endlessToggle != null)
            endlessToggle.onValueChanged.AddListener(on => { PlayerPrefs.SetInt(EndlessKey, on ? 1 : 0); PlayerPrefs.Save(); });

        // the button calls StartGame from the inspector; wire it here if nothing does yet
        if (startButton != null && !CallsThis(startButton)) startButton.onClick.AddListener(StartGame);
    }

    private void OnEnable() => Refresh();

    // a flash cut short by the page closing: the cards take their proper colours again
    private void OnDisable()
    {
        flashing.Clear();
        foreach (var card in mapCards) if (card != null) Tint(card);
    }

    public void StartGame()
    {
        if (SceneLoader.Busy) return;

        bool endless = endlessToggle != null && endlessToggle.isOn && RunProgress.EndlessUnlocked;
        GameMode.Current = endless ? RunMode.Endless : RunMode.Normal;
        if (startButton != null) startButton.interactable = false;

        SceneLoader.Load(SceneToLoad(), loadingPanel);
    }

    private void Refresh()
    {
        var catalog = MapCatalog.Load();
        int count = catalog != null ? catalog.maps.Count : 0;
        int picked = MapSelection.Index;

        for (int i = 0; i < mapCards.Count; i++)
        {
            var card = mapCards[i];
            if (card == null) continue;
            bool used = i < count;
            card.gameObject.SetActive(used);
            if (!used) continue;

            // a locked map shows nothing of itself: a black picture, ??? for its name and story
            var map = catalog.maps[i];
            bool open = MapProgress.IsUnlocked(i);
            var image = Find<Image>(card.transform, "MapImage");
            if (image != null)
            {
                if (map.preview != null) image.sprite = map.preview;
                image.color = open ? Color.white : Color.black;
                ShowLock(image.rectTransform, !open);
            }
            var title = Find<TMP_Text>(card.transform, "MapName");
            if (title != null) title.text = open ? map.title : "???";
            var subtitle = Find<TMP_Text>(card.transform, "MapDescription");
            if (subtitle != null) subtitle.text = open ? map.subtitle : "???";

            card.interactable = open;
            card.SetIsOnWithoutNotify(open && i == picked);
            if (card.TryGetComponent(out LockedClick lockedClick)) lockedClick.locked = !open;
            Tint(card);
        }

        bool unlocked = RunProgress.EndlessUnlocked;
        if (endlessToggle != null)
        {
            endlessToggle.interactable = unlocked;
            endlessToggle.SetIsOnWithoutNotify(unlocked && PlayerPrefs.GetInt(EndlessKey, 0) == 1);
        }
        if (endlessLockText != null)
        {
            int left = RunProgress.RunsUntilEndless;
            endlessLockText.text = unlocked ? string.Empty : $"finish {left} more run{(left == 1 ? "" : "s")} to unlock";
        }
        if (startButton != null) startButton.interactable = !SceneLoader.Busy;
    }

    private void Tint(Toggle card)
    {
        var g = card.targetGraphic;
        if (g == null) return;
        if (flashing.TryGetValue(card, out var running) && running != null) return;   // the flash puts it back itself
        if (!styled.TryGetValue(g, out Color normal)) styled[g] = normal = g.color;
        g.color = card.isOn ? selectedColor : normal;
    }

    // a locked map clicked: a buzz, and its card flashes red once. nothing is picked
    private void Refuse(Toggle card)
    {
        if (lockedSound == null) lockedSound = Resources.Load<AudioClip>("Sfx/ui_locked");
        if (lockedSound != null)
        {
            if (UISoundManager.Instance != null && UISoundManager.Instance.uiAudioSource != null) UISoundManager.Instance.PlayClick(lockedSound);
            else
            {
                var cam = Camera.main;
                SfxPlayer.PlayAt(lockedSound, cam != null ? cam.transform.position : Vector3.zero, 0.9f);
            }
        }
        if (card.targetGraphic == null || !isActiveAndEnabled) return;
        if (flashing.TryGetValue(card, out var running) && running != null) StopCoroutine(running);
        flashing[card] = StartCoroutine(Flash(card));
    }

    private System.Collections.IEnumerator Flash(Toggle card)
    {
        var g = card.targetGraphic;
        if (!styled.TryGetValue(g, out Color normal)) styled[g] = normal = g.color;
        for (float t = 0f; t < lockedFlashSeconds; t += Time.unscaledDeltaTime)
        {
            // straight to red, easing back
            float k = t / lockedFlashSeconds;
            g.color = Color.Lerp(lockedFlash, card.isOn ? selectedColor : normal, k * k);
            yield return null;
        }
        flashing[card] = null;
        Tint(card);
    }

    // the lock drawn over a locked map's picture, made the first time it's needed
    private static void ShowLock(RectTransform picture, bool show)
    {
        var existing = picture.Find("Lock");
        if (existing == null)
        {
            var icon = VfxLibrary.Get != null ? VfxLibrary.Get.lockIcon : null;
            if (!show || icon == null) return;
            var go = new GameObject("Lock", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(picture, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            float side = Mathf.Min(picture.rect.width, picture.rect.height) * 0.42f;
            rt.sizeDelta = new Vector2(side * 14f / 16f, side);
            var image = go.AddComponent<Image>();
            image.sprite = icon;
            image.raycastTarget = false;
            existing = rt;
        }
        existing.gameObject.SetActive(show);
    }

    private string SceneToLoad()
    {
        if (!string.IsNullOrEmpty(gameSceneName)) return gameSceneName;
        var menu = FindFirstObjectByType<MainMenu>();
        return menu != null && !string.IsNullOrEmpty(menu.gameSceneName) ? menu.gameSceneName : FallbackScene;
    }

    private void FindParts()
    {
        if (mapCards.Count == 0)
            foreach (var toggle in GetComponentsInChildren<Toggle>(true))
                if (toggle.name.StartsWith("MapBackdrop")) mapCards.Add(toggle);

        if (endlessToggle == null)
            foreach (var toggle in GetComponentsInChildren<Toggle>(true))
                if (toggle.name.Contains("Endless")) endlessToggle = toggle;

        if (startButton == null)
        {
            var t = FindDeep(transform, "StartGameButton");
            if (t != null) startButton = t.GetComponent<Button>();
        }

        if (loadingPanel == null)
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                var t = FindDeep(root.transform, "LoadingPanel");
                if (t != null) { loadingPanel = t.gameObject; break; }
            }
    }

    private bool CallsThis(Button button)
    {
        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            if (button.onClick.GetPersistentTarget(i) == this && button.onClick.GetPersistentMethodName(i) == nameof(StartGame))
                return true;
        return false;
    }

    private static T Find<T>(Transform parent, string name) where T : Component
    {
        var t = FindDeep(parent, name);
        return t != null ? t.GetComponent<T>() : null;
    }

    private static Transform FindDeep(Transform parent, string name)
    {
        foreach (var t in parent.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }
}
