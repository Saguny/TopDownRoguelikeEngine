using UnityEngine;

// the normal mode finish line. killing the final boss opens it at the front of the arena, in the
// direction the road travels; walking into it wins the run. the visual is a generated placeholder
// until the real tunnel art exists
[RequireComponent(typeof(BoxCollider2D))]
public class ExitTunnel : MonoBehaviour
{
    private const float FrontMargin = 4f;
    private static readonly Vector2 Size = new Vector2(3.5f, 5f);

    private static Sprite placeholder;

    private SpriteRenderer visual;
    private bool won;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Hook()
    {
        GameEvents.OnFinalBossDefeated -= Open;
        GameEvents.OnFinalBossDefeated += Open;
    }

    private static void Open(Vector3 bossPosition)
    {
        Vector2 dir = RoadZone.Instance != null ? RoadZone.Instance.Direction : Vector2.right;
        Vector3 at = bossPosition + (Vector3)(dir * 10f);

        var director = FindFirstObjectByType<SpawnDirector>();
        if (director != null)
        {
            // the play rect falls back to a huge sentinel when the walls are missing; ignore that
            Rect r = director.PlayRect;
            if (r.width < 1000f && r.height < 1000f)
            {
                Vector2 half = r.size * 0.5f - Vector2.one * FrontMargin;
                at = r.center + new Vector2(dir.x * half.x, dir.y * half.y);
            }
        }

        var go = new GameObject("ExitTunnel");
        go.transform.position = at;
        go.AddComponent<ExitTunnel>();
    }

    private void Awake()
    {
        // the transform carries the size, so the unit square sprite and unit collider match it
        transform.localScale = new Vector3(Size.x, Size.y, 1f);

        var trigger = GetComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        trigger.size = Vector2.one;

        if (placeholder == null) placeholder = MakePlaceholder();

        visual = gameObject.AddComponent<SpriteRenderer>();
        visual.sprite = placeholder;
        visual.sortingLayerName = "Background";
        visual.sortingOrder = 100;

        // reuses the boss arrow, so the exit is findable from anywhere on screen
        gameObject.AddComponent<BossMarker>();

        Juice.Text(transform.position + Vector3.up * (Size.y * 0.5f + 1f), "EXIT OPEN", new Color(0.6f, 1f, 0.6f), 2f, 2.5f);
    }

    private void Update()
    {
        float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f);
        visual.color = new Color(pulse, pulse, pulse, 1f);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (won || !other.CompareTag("Player")) return;
        won = true;

        GameEvents.OnRunWon?.Invoke();

        // the same screen the player's death opens, so a win and a loss share one menu
        GameOverScreen screen = other.TryGetComponent(out PlayerHealth health) ? health.GameOverScreen : null;
        if (screen == null) screen = FindFirstObjectByType<GameOverScreen>(FindObjectsInactive.Include);
        if (screen != null) screen.ShowVictory();
    }

    // a dark doorway with a light frame, one unit square, scaled to Size by the transform
    private static Sprite MakePlaceholder()
    {
        const int size = 64;
        const int frame = 5;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Point
        };

        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool edge = x < frame || y < frame || x >= size - frame || y >= size - frame;
                px[y * size + x] = edge ? new Color32(200, 255, 200, 255) : new Color32(10, 10, 14, 255);
            }
        }

        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
