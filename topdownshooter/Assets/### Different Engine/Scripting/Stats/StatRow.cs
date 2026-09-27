using TMPro;
using UnityEngine;
using UnityEngine.UI;

// one line on the stat panel. pick the stat and drag in the text that shows its value. the label
// and icon are optional; when assigned they're filled from the StatCatalog
public class StatRow : MonoBehaviour
{
    public StatId stat;
    [SerializeField] private TMP_Text value;
    [Tooltip("optional. gets the stat's name from the StatCatalog")]
    [SerializeField] private TMP_Text label;
    [Tooltip("optional. gets the stat's icon from the StatCatalog, when it has one")]
    [SerializeField] private Image icon;

    private Color normal;
    private bool normalCaptured;

    public void Show(StatSheet sheet, Color better, Color worse)
    {
        if (value != null)
        {
            // whatever colour the text was styled with is the colour for an untouched stat
            if (!normalCaptured)
            {
                normal = value.color;
                normalCaptured = true;
            }

            value.text = sheet.Text(stat);
            int direction = sheet.Direction(stat);
            value.color = direction > 0 ? better : direction < 0 ? worse : normal;
        }

        FillLabel(sheet);
    }

    // the value this stat would have after a purchase, in the preview colour.
    // format: {0} = the value now, {1} = the value after
    public void ShowPreview(StatSheet now, StatSheet after, Color color, string format)
    {
        if (value == null) return;
        value.text = string.Format(format, now.Text(stat), after.Text(stat));
        value.color = color;
    }

    private void FillLabel(StatSheet sheet)
    {
        var def = sheet.Catalog.Get(stat);
        if (def == null) return;

        if (label != null && !string.IsNullOrEmpty(def.label)) label.text = def.label;
        if (icon != null && def.icon != null) icon.sprite = def.icon;
    }
}
