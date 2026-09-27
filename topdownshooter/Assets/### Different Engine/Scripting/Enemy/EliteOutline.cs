using UnityEngine;

// an elite: a stronger copy of a regular enemy, a little bigger than its kind, many times as tough,
// and ringed in a white-blue outline so it's picked out of the crowd at a glance. the outline is
// four flat copies of its sprite a pixel out each way, behind it. pooled enemies come back plain:
// the component takes itself off when the enemy goes back to the pool
public class EliteOutline : MonoBehaviour
{
    public static readonly Color OutlineColor = new Color32(0xc4, 0xf2, 0xff, 0xff);
    public const float Size = 1.25f;
    public const float Health = 25f;

    private SpriteRenderer source;
    private readonly SpriteRenderer[] copies = new SpriteRenderer[4];
    private static Material silhouette;
    private static readonly Vector2[] Offsets = { Vector2.left, Vector2.right, Vector2.up, Vector2.down };

    // makes an enemy just spawned an elite: bigger, tougher, outlined, and worth more
    public static void Promote(GameObject enemy, float size = Size, float health = Health, int bonusWen = 0)
    {
        if (enemy == null || enemy.TryGetComponent(out EliteOutline _)) return;
        enemy.transform.localScale *= size;
        if (enemy.TryGetComponent(out EnemyHealth h))
        {
            h.SetScaled(h.Max * health);
            h.bonusWenDrops = bonusWen;
            h.alwaysDropHeal = true;
        }
        enemy.AddComponent<EliteOutline>();
    }

    private void OnEnable()
    {
        source = GetComponent<SpriteRenderer>();
        if (source == null) return;
        if (silhouette == null)
        {
            var shader = Shader.Find("Rogue/Silhouette");
            if (shader != null) silhouette = new Material(shader) { name = "Elite Outline" };
        }

        float px = source.sprite != null ? 1f / source.sprite.pixelsPerUnit : 1f / 28.46f;
        for (int i = 0; i < copies.Length; i++)
        {
            var go = new GameObject("Elite Outline");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Offsets[i] * px;
            var sr = go.AddComponent<SpriteRenderer>();
            if (silhouette != null) sr.sharedMaterial = silhouette;
            sr.color = OutlineColor;
            sr.sortingLayerID = source.sortingLayerID;
            sr.sortingOrder = source.sortingOrder - 1;
            copies[i] = sr;
        }
        LateUpdate();
    }

    private void LateUpdate()
    {
        if (source == null) return;
        foreach (var c in copies)
        {
            if (c == null) continue;
            c.sprite = source.sprite;
            c.flipX = source.flipX;
            c.enabled = source.enabled;
        }
    }

    private void OnDisable()
    {
        for (int i = 0; i < copies.Length; i++)
        {
            if (copies[i] != null) Destroy(copies[i].gameObject);
            copies[i] = null;
        }
        Destroy(this);
    }
}
