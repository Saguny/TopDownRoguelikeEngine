using UnityEngine;
using UnityEngine.SceneManagement;

// pushes the floor back so what moves on it stands out: a multiply over everything on the
// Background sorting layer, drawn just above it and following the camera. the player, enemies,
// pickups, projectiles and effects all sort above it and keep their full brightness. made by
// itself in any scene with a SpawnDirector; add one by hand to tune it
public class FloorDimmer : MonoBehaviour
{
    [Tooltip("what the floor is multiplied by: darker and a little cooler pushes it further back")]
    public Color tint = new Color(0.74f, 0.71f, 0.82f, 1f);

    private SpriteRenderer sr;
    private Camera cam;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (FindFirstObjectByType<SpawnDirector>() == null || FindFirstObjectByType<FloorDimmer>() != null) return;
        new GameObject("Floor Dimmer").AddComponent<FloorDimmer>();
    }

    private void Awake()
    {
        var shader = Shader.Find("Rogue/Multiply");
        if (shader == null)
        {
            enabled = false;
            return;
        }
        var tex = Texture2D.whiteTexture;
        sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
        sr.sharedMaterial = new Material(shader) { name = "Floor Dimmer" };
        sr.sortingLayerName = "Background";
        sr.sortingOrder = short.MaxValue;
    }

    private void LateUpdate()
    {
        if (sr == null) return;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        sr.color = tint;
        float h = cam.orthographicSize * 2f + 2f, w = h * cam.aspect + 2f;
        var p = cam.transform.position;
        transform.SetPositionAndRotation(new Vector3(p.x, p.y, 0f), Quaternion.identity);
        transform.localScale = new Vector3(w, h, 1f);
    }
}
