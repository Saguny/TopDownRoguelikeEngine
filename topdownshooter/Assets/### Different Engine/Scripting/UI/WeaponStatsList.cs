using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// the game over screen's weapon table: a row for every weapon held this run, an evolution on a
// row of its own, filled from RunStats when the panel opens. it's laid out in the editor as
// columns: an icon column with a Grid Layout Group, whose cell size and spacing set the height of
// a row and the gap between rows, and text columns, each showing one stat. the text columns are
// templates: every row gets its own copy, centred on its icon, so a column can't drift from the
// icons however its font spaces lines. the row with the highest DPS is picked out in red
public class WeaponStatsList : MonoBehaviour
{
    public enum Show { Name, Dps, Damage, Kills, Level, Share, TimeHeld }

    [Serializable]
    public class Column
    {
        [Tooltip("the column: its position, width, font and colour are copied for every row. its own text is only a placeholder")]
        public TMP_Text text;
        public Show show = Show.Dps;
        [Tooltip("in the Best Color on the row with the highest DPS")]
        public bool highlightBest = true;
    }

    [Tooltip("the icon column. its Grid Layout Group sets the rows: cell size is the row's height, spacing the gap. the list places the icons itself, so the grid is switched off while it's shown")]
    [SerializeField] private RectTransform icons;
    [SerializeField] private Column[] columns =
    {
        new Column { show = Show.Name, highlightBest = false },
        new Column { show = Show.Dps },
    };
    [SerializeField] private Color bestColor = new Color(0.92f, 0.16f, 0.16f);
    [Tooltip("highest DPS at the top. off: the order they were taken, each evolution under its weapon")]
    [SerializeField] private bool sortByDps;

    private readonly List<GameObject> made = new List<GameObject>();

    private void OnEnable() => Fill();

    public void Fill()
    {
        foreach (var go in made) if (go != null) Destroy(go);
        made.Clear();

        var rows = new List<WeaponRecord>(RunStats.Weapons);
        if (sortByDps) rows.Sort((a, b) => b.Dps.CompareTo(a.Dps));
        WeaponRecord best = null;
        foreach (var r in rows) if (r.Dps > 0f && (best == null || r.Dps > best.Dps)) best = r;

        // the rows, from the icon column's grid
        Vector2 cell = new Vector2(30f, 30f);
        float gap = 20f, top = 0f;
        if (icons != null && icons.TryGetComponent(out GridLayoutGroup grid))
        {
            cell = grid.cellSize;
            gap = grid.spacing.y;
            top = grid.padding.top;
            grid.enabled = false;
        }
        float step = cell.y + gap;

        // rows run down from the icon column's top, or the first text column's without icons
        RectTransform from = icons;
        foreach (var c in columns) if (from == null && c.text != null) from = c.text.rectTransform;
        if (from == null) return;

        // the placeholders stay in the editor, out of the way at runtime
        foreach (var c in columns) if (c.text != null) c.text.gameObject.SetActive(false);
        if (icons != null) foreach (Transform child in icons) child.gameObject.SetActive(false);

        for (int i = 0; i < rows.Count; i++)
        {
            var record = rows[i];
            float offset = top + cell.y * 0.5f + i * step;   // down from the icon column's top to the row's middle
            if (icons != null) MakeIcon(record, cell, offset);

            float rowY = from.TransformPoint(new Vector3(0f, from.rect.yMax - offset, 0f)).y;
            foreach (var c in columns)
                if (c.text != null) MakeText(c, record, i, rowY, cell.y, record == best);
        }
    }

    private void MakeIcon(WeaponRecord record, Vector2 cell, float offset)
    {
        var image = new GameObject(record.Title + " Icon", typeof(RectTransform)).AddComponent<Image>();
        var rt = image.rectTransform;
        rt.SetParent(icons, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = cell;
        rt.anchoredPosition = new Vector2(0f, -offset);
        image.sprite = record.Icon;
        image.preserveAspect = true;
        image.raycastTarget = false;
        if (image.sprite == null) image.color = Color.clear;   // keeps the row, shows nothing
        made.Add(image.gameObject);
    }

    // a copy of the column's text for one row, its middle on the row's
    private void MakeText(Column column, WeaponRecord record, int index, float rowY, float height, bool isBest)
    {
        var text = Instantiate(column.text, column.text.transform.parent);
        text.name = $"{column.text.name} {index + 1}";
        text.gameObject.SetActive(true);

        var rt = text.rectTransform;
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);
        Vector3 middle = rt.TransformPoint(rt.rect.center);
        rt.position += new Vector3(0f, rowY - middle.y, 0f);

        text.verticalAlignment = VerticalAlignmentOptions.Middle;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.color = isBest && column.highlightBest ? bestColor : column.text.color;
        text.text = Format(column.show, record);
        made.Add(text.gameObject);
    }

    private static string Format(Show show, WeaponRecord r)
    {
        switch (show)
        {
            case Show.Name: return r.Title;
            case Show.Dps: return RunStats.Short(r.Dps);
            case Show.Damage: return RunStats.Short(r.Damage);
            case Show.Kills: return r.Kills.ToString();
            case Show.Level: return r.Level.ToString();
            case Show.Share: return $"{r.Share * 100f:0}%";
            case Show.TimeHeld: return RunStats.Clock(r.Seconds);
        }
        return string.Empty;
    }
}
