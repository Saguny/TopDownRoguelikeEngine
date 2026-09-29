using UnityEngine;

// an enemy ringed in a flat colour, a pixel out each way: four flat copies of its sprite behind it
// that follow its frame and flip. what an elite (EliteOutline) and the empowered horde (Empowered)
// are drawn with. pooled enemies come back plain: the component takes itself off as the enemy goes
// back to the pool
public abstract class SilhouetteOutline : MonoBehaviour
{
    protected abstract Color OutlineColor { get; }

    private SpriteRenderer source;
    private readonly SpriteRenderer[] copies = new SpriteRenderer[4];
    private static Material silhouette;
    private static readonly Vector2[] Offsets = { Vector2.left, Vector2.right, Vector2.up, Vector2.down };

    protected virtual void OnEnable()
    {
        source = GetComponent<SpriteRenderer>();
        if (source == null) return;
        if (silhouette == null)
        {
            var shader = Shader.Find("Rogue/Silhouette");
            if (shader != null) silhouette = new Material(shader) { name = "Enemy Outline" };
        }

        float px = source.sprite != null ? 1f / source.sprite.pixelsPerUnit : 1f / 28.46f;
        for (int i = 0; i < copies.Length; i++)
        {
            var go = new GameObject(GetType().Name);
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

    protected virtual void OnDisable()
    {
        for (int i = 0; i < copies.Length; i++)
        {
            if (copies[i] != null) Destroy(copies[i].gameObject);
            copies[i] = null;
        }
        Destroy(this);
    }
}
