using System.Collections.Generic;
using UnityEngine;

// endless ground: repeats the street tile in every direction around the camera. the tile's roads
// run off all four edges, so any grid of it joins up seamlessly. the hand placed tiles are hidden
// and only used as the template (sprite, sorting, scale and spacing)
public class GroundTiler : MonoBehaviour
{
    private Sprite sprite;
    private Material material;
    private int layerId;
    private int order;
    private Color color;
    private Vector3 scale;
    private Vector3 origin;
    private Vector2 spacing;

    private Camera cam;
    private readonly List<SpriteRenderer> tiles = new List<SpriteRenderer>();

    public static void Install(Transform groundRoot)
    {
        var template = groundRoot.GetComponent<SpriteRenderer>();
        if (template == null || template.sprite == null) return;

        var copies = new List<SpriteRenderer>();
        foreach (var r in groundRoot.GetComponentsInChildren<SpriteRenderer>(true))
            if (r.sprite == template.sprite) copies.Add(r);

        var tiler = new GameObject("GroundTiler").AddComponent<GroundTiler>();
        tiler.sprite = template.sprite;
        tiler.material = template.sharedMaterial;
        tiler.layerId = template.sortingLayerID;
        tiler.order = template.sortingOrder;
        tiler.color = template.color;
        tiler.scale = template.transform.lossyScale;
        tiler.origin = template.transform.position;
        tiler.spacing = Spacing(copies, template.bounds.size);

        foreach (var r in copies) r.enabled = false;
    }

    // the hand placed tiles overlap slightly to hide seams, so spacing comes from how far apart
    // they actually sit rather than from the sprite's size
    private static Vector2 Spacing(List<SpriteRenderer> copies, Vector2 fallback)
    {
        float sx = float.MaxValue;
        float sy = float.MaxValue;

        for (int i = 0; i < copies.Count; i++)
        {
            for (int j = i + 1; j < copies.Count; j++)
            {
                Vector2 d = copies[j].transform.position - copies[i].transform.position;
                float dx = Mathf.Abs(d.x);
                float dy = Mathf.Abs(d.y);

                if (dy < 1f && dx > 1f) sx = Mathf.Min(sx, dx);
                if (dx < 1f && dy > 1f) sy = Mathf.Min(sy, dy);
            }
        }

        return new Vector2(
            sx < float.MaxValue ? sx : fallback.x,
            sy < float.MaxValue ? sy : fallback.y);
    }

    private void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null || spacing.x <= 0f || spacing.y <= 0f) return;

        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        Vector2 c = cam.transform.position;

        // one spare tile on every side so nothing pops in at the edge of the screen
        int x0 = Mathf.FloorToInt((c.x - halfW - origin.x) / spacing.x) - 1;
        int x1 = Mathf.CeilToInt((c.x + halfW - origin.x) / spacing.x) + 1;
        int y0 = Mathf.FloorToInt((c.y - halfH - origin.y) / spacing.y) - 1;
        int y1 = Mathf.CeilToInt((c.y + halfH - origin.y) / spacing.y) + 1;

        int needed = (x1 - x0 + 1) * (y1 - y0 + 1);
        while (tiles.Count < needed) tiles.Add(MakeTile());

        int k = 0;
        for (int x = x0; x <= x1; x++)
        {
            for (int y = y0; y <= y1; y++)
            {
                var t = tiles[k++];
                t.transform.position = new Vector3(origin.x + x * spacing.x, origin.y + y * spacing.y, origin.z);
                if (!t.enabled) t.enabled = true;
            }
        }

        for (; k < tiles.Count; k++)
            if (tiles[k].enabled) tiles[k].enabled = false;
    }

    private SpriteRenderer MakeTile()
    {
        var go = new GameObject("GroundTile");
        go.transform.SetParent(transform, false);
        go.transform.localScale = scale;

        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = sprite;
        r.sharedMaterial = material;
        r.sortingLayerID = layerId;
        r.sortingOrder = order;
        r.color = color;
        return r;
    }
}
