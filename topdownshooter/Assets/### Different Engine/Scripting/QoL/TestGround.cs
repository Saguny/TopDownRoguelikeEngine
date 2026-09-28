using UnityEngine;

// an endless empty floor for testing weapons, far out past the map where nothing stands: no walls,
// no pillars, no road, so the player and a crowd can walk anywhere without catching on anything.
// its checkered floor follows the camera, snapped to the checker, so it never runs out. the Weapon
// DPS Benchmark and the Weapon Showcase move the player out onto it before they start
public class TestGround : MonoBehaviour
{
    // far enough out that nothing of the map is anywhere near
    public static readonly Vector2 Centre = new Vector2(2000f, 2000f);

    private const float Tile = 1f;                  // units per checker square
    private const int TexturePixels = 32;           // two squares a side, 16 pixels each
    private static readonly Color32 Light = new Color32(0x3a, 0x33, 0x44, 0xff);
    private static readonly Color32 Dark = new Color32(0x30, 0x2a, 0x3a, 0xff);
    private static readonly Color32 Line = new Color32(0x44, 0x3c, 0x50, 0xff);

    private SpriteRenderer floor;
    private Camera cam;

    // moves the player (and the camera with them) out onto the ground, making it if it isn't there
    // yet. returns where they stand
    public static Vector2 MovePlayer(GameObject player)
    {
        if (FindFirstObjectByType<TestGround>() == null) new GameObject("Test Ground").AddComponent<TestGround>();
        if (player.TryGetComponent(out Rigidbody2D body))
        {
            body.position = Centre;
            body.linearVelocity = Vector2.zero;
        }
        player.transform.position = Centre;

        var cam = Camera.main;
        if (cam != null) cam.transform.position = new Vector3(Centre.x, Centre.y, cam.transform.position.z);
        return Centre;
    }

    private void Awake()
    {
        var tex = new Texture2D(TexturePixels, TexturePixels, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat,
            hideFlags = HideFlags.DontSave,
        };
        int half = TexturePixels / 2;
        var px = new Color32[TexturePixels * TexturePixels];
        for (int y = 0; y < TexturePixels; y++)
            for (int x = 0; x < TexturePixels; x++)
            {
                bool dark = (x < half) != (y < half);
                bool line = x % half == 0 || y % half == 0;
                px[y * TexturePixels + x] = line ? Line : dark ? Dark : Light;
            }
        tex.SetPixels32(px);
        tex.Apply();

        var sprite = Sprite.Create(tex, new Rect(0, 0, TexturePixels, TexturePixels), new Vector2(0.5f, 0.5f),
            half / Tile, 0, SpriteMeshType.FullRect);
        sprite.hideFlags = HideFlags.DontSave;

        floor = gameObject.AddComponent<SpriteRenderer>();
        floor.sprite = sprite;
        floor.drawMode = SpriteDrawMode.Tiled;
        floor.size = new Vector2(160f, 100f);
        // under everything: the lowest sorting layer, as far back as it goes
        floor.sortingLayerID = SortingLayer.layers.Length > 0 ? SortingLayer.layers[0].id : 0;
        floor.sortingOrder = short.MinValue;
        transform.position = Centre;
    }

    // it follows the camera a whole checker at a time, so the pattern never slides
    private void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        float step = Tile * 2f;
        Vector3 c = cam.transform.position;
        transform.position = new Vector3(Mathf.Round(c.x / step) * step, Mathf.Round(c.y / step) * step, 0f);
        // wide enough for any zoom
        float h = cam.orthographicSize * 2f + 8f, w = h * cam.aspect + 8f;
        if (floor.size.x < w || floor.size.y < h) floor.size = new Vector2(Mathf.Ceil(w / step) * step, Mathf.Ceil(h / step) * step);
    }
}
