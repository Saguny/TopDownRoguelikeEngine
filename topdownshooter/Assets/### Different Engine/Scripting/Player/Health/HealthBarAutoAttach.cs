using UnityEngine;

// the player's health bar: a bar of pixels under the feet, solid red on solid black, drawn over
// every effect so it can always be read. it's measured in the character art's own pixels, so it
// sits on the same grid as the sprite
[DefaultExecutionOrder(10)]
public class HealthBarAutoAttach : MonoBehaviour
{
    [Header("size, in the character art's pixels")]
    [Tooltip("the art's pixels per unit. 0 = the player's own sprite's, so the bar follows the character's size")]
    [Min(0f)] public float artPixelsPerUnit = 0f;
    [Tooltip("the red part at full health: width and height")]
    public Vector2Int barPixels = new Vector2Int(20, 3);
    [Tooltip("the black edge around it")]
    [Min(0)] public int borderPixels = 1;
    [Tooltip("how far below the player's position the art ends: the bottom of the drop shadow")]
    public int artBottomPixels = 23;
    [Tooltip("space between the art and the bar")]
    [Min(0)] public int gapPixels = 1;

    [Header("look")]
    public Color fillColor = Color.red;
    public Color backColor = Color.black;
    [Tooltip("drawn on this layer, above every effect in the world")]
    public string barSortingLayer = "HUD";
    public int backOrder = 300;
    [Tooltip("hidden while at full health")]
    public bool hideWhenFull = true;

    private void Start()
    {
        var health = GetComponent<IHealth>();
        if (health == null)
        {
            Debug.LogWarning($"[HealthBarAutoAttach] {name} has no IHealth. No bar created.");
            return;
        }

        // remove stale children
        var existing = transform.Find("HealthBar");
        if (existing != null) Destroy(existing.gameObject);

        var barRoot = new GameObject("HealthBar");
        barRoot.layer = gameObject.layer;
        barRoot.transform.SetParent(transform, false);
        barRoot.transform.localPosition = Vector3.zero;

        var hb = barRoot.AddComponent<HealthBarSprite>();
        float ppu = artPixelsPerUnit > 0f ? artPixelsPerUnit
            : TryGetComponent(out SpriteRenderer sr) && sr.sprite != null ? sr.sprite.pixelsPerUnit : 33.88956f;
        float px = 1f / ppu;
        hb.size = new Vector2(barPixels.x, barPixels.y) * px;
        hb.bgPadding = 2 * borderPixels * px;
        hb.offset = new Vector3(0f, -(artBottomPixels + gapPixels + barPixels.y * 0.5f + borderPixels) * px, 0f);
        hb.pixelWidth = px;
        hb.fillColor = fillColor;
        hb.backColor = backColor;
        hb.hideWhenFull = hideWhenFull;
        hb.sortingLayer = barSortingLayer;
        hb.bgOrder = backOrder;
        hb.fgOrder = backOrder + 1;
        hb.Bind(transform, health);
    }
}
