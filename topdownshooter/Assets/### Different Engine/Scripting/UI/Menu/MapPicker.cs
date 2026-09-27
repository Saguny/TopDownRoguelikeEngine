using TMPro;
using UnityEngine;
using UnityEngine.UI;

// the map choice on a menu page: previous/next arrows cycling through the MapCatalog, showing the
// picked map's name, description and picture. the pick is remembered, and the next run from
// the menu takes place there. every field is optional
public class MapPicker : MonoBehaviour
{
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text subtitle;
    [SerializeField] private Image preview;
    [SerializeField] private Button previous;
    [SerializeField] private Button next;
    [Tooltip("optional. e.g. \"1 / 2\"")]
    [SerializeField] private TMP_Text counter;

    private void OnEnable()
    {
        if (previous != null) previous.onClick.AddListener(Previous);
        if (next != null) next.onClick.AddListener(Next);
        MapSelection.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (previous != null) previous.onClick.RemoveListener(Previous);
        if (next != null) next.onClick.RemoveListener(Next);
        MapSelection.Changed -= Refresh;
    }

    // hook buttons to these directly too, if the arrows aren't wired above
    public void Next() => MapSelection.Next();
    public void Previous() => MapSelection.Previous();

    private void Refresh()
    {
        var map = MapSelection.Current;
        bool several = MapSelection.Count > 1;
        if (previous != null) previous.interactable = several;
        if (next != null) next.interactable = several;

        if (title != null) title.text = map != null ? map.title : "-";
        if (subtitle != null) subtitle.text = map != null ? map.subtitle : string.Empty;
        if (counter != null) counter.text = MapSelection.Count > 0 ? $"{MapSelection.Index + 1} / {MapSelection.Count}" : string.Empty;
        if (preview != null)
        {
            preview.sprite = map != null ? map.preview : null;
            preview.enabled = preview.sprite != null;
        }
    }
}
