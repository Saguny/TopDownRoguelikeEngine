using UnityEngine;

// the evolved Bow's arrows: a one pixel outline all the way round, shimmering through the Bow's
// outline colours. four flat copies of the arrow, one art pixel out in each direction, drawn
// just behind it, so it's a real pixel outline at any angle. lives on the pooled arrow and
// switches off again when the arrow is reused by a plain volley
[DisallowMultipleComponent]
public class ArrowShine : MonoBehaviour
{
    private static readonly Vector2[] Offsets = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
    private static Material flat;

    private SpriteRenderer body;
    private SpriteRenderer[] rim;
    private BowData from;
    private float phase;

    // on with the Bow's colours, or off with null
    public static void Set(GameObject arrow, BowData shine)
    {
        if (arrow == null) return;
        if (!arrow.TryGetComponent(out ArrowShine s))
        {
            if (shine == null) return;
            s = arrow.AddComponent<ArrowShine>();
        }
        s.from = shine;
        s.phase = Random.value;
        s.Show(shine != null);
    }

    private void Build()
    {
        body = GetComponentInChildren<SpriteRenderer>();
        if (body == null) return;

        if (flat == null)
        {
            var shader = Shader.Find("Rogue/Silhouette");
            if (shader != null) flat = new Material(shader) { name = "Arrow Shine" };
        }

        float px = body.sprite != null ? 1f / body.sprite.pixelsPerUnit : 1f / 28.46f;
        rim = new SpriteRenderer[Offsets.Length];
        for (int i = 0; i < Offsets.Length; i++)
        {
            var go = new GameObject("Shine");
            go.transform.SetParent(body.transform, false);
            go.transform.localPosition = (Vector3)(Offsets[i] * px);
            var sr = go.AddComponent<SpriteRenderer>();
            if (flat != null) sr.sharedMaterial = flat;
            sr.sortingLayerID = body.sortingLayerID;
            sr.sortingOrder = body.sortingOrder - 1;
            rim[i] = sr;
        }
    }

    private void Show(bool on)
    {
        if (on && rim == null) Build();
        if (rim == null) return;
        foreach (var sr in rim) if (sr != null) sr.enabled = on;
        enabled = on;
    }

    private void LateUpdate()
    {
        if (from == null || body == null || rim == null) return;

        var colours = from.outlineColors;
        Color c = Color.white;
        if (colours != null && colours.Length > 0)
        {
            // stepped, not blended: pixel art shimmers by swapping colours, a frame at a time
            float t = (Time.time * from.outlineShimmerSpeed * 0.25f + phase) * colours.Length;
            c = colours[(int)t % colours.Length];
        }
        c.a = 1f;   // all the way flat in the Silhouette shader

        for (int i = 0; i < rim.Length; i++)
        {
            var sr = rim[i];
            if (sr == null) continue;
            sr.sprite = body.sprite;
            sr.flipX = body.flipX;
            sr.flipY = body.flipY;
            sr.color = c;
        }
    }
}
