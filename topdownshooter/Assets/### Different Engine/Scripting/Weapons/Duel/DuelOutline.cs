using UnityEngine;

// the player in an end boss's duel (BossDuel), blazing: a white-blue outline hugging their sprite a
// pixel out all round, shimmering between white and ice blue, and a deeper blue flame licking a
// pixel further out, flickering, so they stand out of the danmaku at a glance. flat copies of the
// body sprite behind it, the way an elite's outline is drawn (EliteOutline); they follow its frame,
// its flip and its hit flash going off, and go with the scene
public class DuelOutline : MonoBehaviour
{
    private static readonly Color White = new Color32(0xf4, 0xfd, 0xff, 0xff);
    private static readonly Color Ice = new Color32(0x9c, 0xe6, 0xff, 0xff);
    private static readonly Color Blue = new Color32(0x3c, 0x8c, 0xff, 0xff);
    private static readonly Color Deep = new Color32(0x24, 0x50, 0xd8, 0xff);

    // the rim, a pixel out each way (diagonals too, so no corner is left bare), and the flame, two out
    private static readonly Vector2[] Rim =
    {
        new Vector2(-1, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(0, -1),
        new Vector2(-1, 1), new Vector2(1, 1), new Vector2(-1, -1), new Vector2(1, -1),
    };
    private static readonly Vector2[] Flame =
    {
        new Vector2(-2, 0), new Vector2(2, 0), new Vector2(0, 2), new Vector2(0, -2),
        new Vector2(-1, 2), new Vector2(1, 2), new Vector2(-2, 1), new Vector2(2, 1),
    };

    private static Material silhouette;
    private SpriteRenderer body;
    private SpriteRenderer[] rim, flame;

    public static void On(GameObject player)
    {
        if (player == null || player.GetComponentInChildren<DuelOutline>() != null) return;
        var health = player.GetComponent<PlayerHealth>();
        var body = health != null && health.spriteRenderer != null ? health.spriteRenderer : player.GetComponentInChildren<SpriteRenderer>();
        if (body != null) body.gameObject.AddComponent<DuelOutline>();
    }

    private void Awake()
    {
        body = GetComponent<SpriteRenderer>();
        if (silhouette == null)
        {
            var shader = Shader.Find("Rogue/Silhouette");
            if (shader != null) silhouette = new Material(shader) { name = "Duel Outline" };
        }
        rim = Copies(Rim, 1);
        flame = Copies(Flame, 2);
    }

    private SpriteRenderer[] Copies(Vector2[] offsets, int behind)
    {
        float px = body != null && body.sprite != null ? 1f / body.sprite.pixelsPerUnit : 1f / YamaArt.WorldPpu;
        var list = new SpriteRenderer[offsets.Length];
        for (int i = 0; i < offsets.Length; i++)
        {
            var go = new GameObject("Duel Outline");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = offsets[i] * px;
            var sr = go.AddComponent<SpriteRenderer>();
            if (silhouette != null) sr.sharedMaterial = silhouette;
            if (body != null)
            {
                sr.sortingLayerID = body.sortingLayerID;
                sr.sortingOrder = body.sortingOrder - behind;
            }
            list[i] = sr;
        }
        return list;
    }

    private void LateUpdate()
    {
        if (body == null) return;
        float t = Time.time;
        // the rim shimmers white to ice; the flame flickers, a tongue or two dropping out each frame
        var rimColor = Color.Lerp(White, Ice, 0.5f + 0.5f * Mathf.Sin(t * 9f));
        int frame = (int)(t * 14f);
        for (int i = 0; i < rim.Length; i++) Follow(rim[i], rimColor, true);
        for (int i = 0; i < flame.Length; i++)
        {
            bool lit = ((frame + i * 3) % 5) != 0;
            Follow(flame[i], ((frame + i) & 1) == 0 ? Blue : Deep, lit);
        }
    }

    private void Follow(SpriteRenderer c, Color color, bool lit)
    {
        if (c == null) return;
        c.sprite = body.sprite;
        c.flipX = body.flipX;
        c.flipY = body.flipY;
        c.color = color;
        c.enabled = lit && body.enabled && body.gameObject.activeInHierarchy;
    }
}
