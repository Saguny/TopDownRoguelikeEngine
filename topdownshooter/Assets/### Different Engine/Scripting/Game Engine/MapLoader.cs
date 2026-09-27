using UnityEngine;
using UnityEngine.SceneManagement;

// puts the picked map in place as a game scene loads. the scene keeps whichever playfield it was
// built with (the courtyard); when another map is picked, that one is swapped in at the same
// spot before anything starts, and the player is moved to its start. the spawner reads the
// active playfield's walls and timeline at Start, so it follows along by itself
public static class MapLoader
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        var placed = Playfield.Active;
        if (placed == null || placed.gameObject.scene != scene) return;

        var map = MapSelection.Current;
        if (map == null || map.playfield == null || IsSame(placed, map.playfield)) return;

        Vector3 at = placed.transform.position;
        placed.gameObject.SetActive(false);
        Object.Destroy(placed.gameObject);

        var field = Object.Instantiate(map.playfield, at, Quaternion.identity);
        field.name = map.playfield.name;
        if (field.gameObject.scene != scene) SceneManager.MoveGameObjectToScene(field.gameObject, scene);

        var player = Object.FindFirstObjectByType<PlayerMovement>();
        if (player == null) return;

        Vector3 start = at + (Vector3)field.playerStart;
        start.z = player.transform.position.z;
        player.transform.position = start;
        if (player.TryGetComponent(out Rigidbody2D body)) body.position = start;

        var cam = Camera.main;
        if (cam != null) cam.transform.position = new Vector3(start.x, start.y, cam.transform.position.z);
    }

    private static bool IsSame(Playfield placed, Playfield prefab) =>
        placed.name.Replace("(Clone)", string.Empty).Trim() == prefab.name;
}
