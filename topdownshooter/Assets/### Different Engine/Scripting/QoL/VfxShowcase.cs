#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

// dev only: every weapon but the Command Token at its top level (evolutions too) against a
// steady crowd, photographed a few times a second, so the weapons' effects can be judged
// together in the real game. Tools > VFX > Weapon Showcase runs it and stops play afterwards.
// started from any scene, it loads the game scene itself, so nothing open in the editor changes
public class VfxShowcase : MonoBehaviour
{
    public const string PendingKey = "VfxShowcase.Pending";
    private const int Shots = 32;
    private const float Every = 0.3f;

    public static string Folder => Path.Combine(Application.temporaryCachePath, "showcase");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RunIfPending()
    {
        if (!UnityEditor.SessionState.GetBool(PendingKey, false)) return;
        UnityEditor.SessionState.EraseBool(PendingKey);
        new GameObject("VfxShowcase").AddComponent<VfxShowcase>();
    }

    private IEnumerator Start()
    {
        if (FindFirstObjectByType<SpawnDirector>() == null)
        {
            DontDestroyOnLoad(gameObject);
            SceneManager.LoadScene("Game");
            for (float waited = 0f; FindFirstObjectByType<SpawnDirector>() == null && waited < 15f; waited += Time.unscaledDeltaTime)
                yield return null;
        }
        var inventory = FindFirstObjectByType<PlayerInventory>();
        var director = FindFirstObjectByType<SpawnDirector>();
        if (inventory == null || director == null || director.Timeline == null)
        {
            Done("no game scene to show the weapons in");
            yield break;
        }

        // no level up menus pausing the pictures
        if (inventory.TryGetComponent(out PlayerHealth health)) health.godMode = true;
        inventory.wenForUpgrade = int.MaxValue / 2;
        yield return new WaitForSecondsRealtime(2.5f);
        director.enabled = false;
        if (inventory.upgradeMenuUI != null && inventory.upgradeMenuUI.IsOpen) inventory.upgradeMenuUI.Close();
        // out onto open ground, clear of the map's walls and pillars
        foreach (var go in EnemyRegistry.All.ToList()) if (go != null) ObjectPool.Recycle(go);
        TestGround.MovePlayer(inventory.gameObject);
        Time.timeScale = 1f;

        foreach (var w in inventory.RunUpgrades.OfType<WeaponData>().Where(w => !(w is CommandTokenData)).ToList())
            for (int i = 0; i < 16 && w.CanOffer; i++) inventory.TakeUpgrade(w);
        Time.timeScale = 1f;
        Debug.Log("SHOWCASE weapons: " + string.Join(", ", inventory.Taken.OfType<WeaponData>().Select(w => $"{w.GetBaseTitle()} {w.Level}")));

        var prefabs = director.Timeline.beats.SelectMany(b => b.enemies)
            .Where(p => p?.archetype?.prefab != null).Select(p => p.archetype.prefab).Distinct().ToList();

        var boss = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/### Different Engine/Prefabs/BossEnemy.prefab");
        if (boss != null)
        {
            var b = Instantiate(boss, (Vector2)inventory.transform.position + new Vector2(4f, 1.5f), Quaternion.identity);
            if (b.TryGetComponent(out EnemyHealth bh)) bh.SetScaled(60000f);
            if (b.TryGetComponent(out BossMagistrate bm)) { bm.firstAttackDelay = 0.5f; bm.hopBetween = new Vector2(0.8f, 1.2f); }
        }

        Directory.CreateDirectory(Folder);
        foreach (var old in Directory.GetFiles(Folder, "*.png")) File.Delete(old);

        float clock = 0f, next = 2f;
        int shot = 0;
        // the player walks round in a circle, so weapons that follow them show in motion, the ink
        // brush paints, and its evolution closes a loop every lap
        Vector2 home = inventory.transform.position;
        var body = inventory.GetComponent<Rigidbody2D>();
        while (shot < Shots)
        {
            float lap = clock * 1.9f;
            Vector2 walk = home + new Vector2(Mathf.Cos(lap) - 1f, Mathf.Sin(lap)) * 2.4f;
            if (body != null) body.position = walk;
            inventory.transform.position = walk;

            if (EnemyRegistry.Count < 70)
            {
                Spawn(prefabs, inventory.transform.position, 20);
                // and a few elites to see
                int elites = 0;
                foreach (var go in EnemyRegistry.All)
                {
                    if (elites >= 3 || go == null || go.TryGetComponent(out BossMarker _)) continue;
                    if (!go.TryGetComponent(out EliteOutline _) && Random.value < 0.05f) { EliteOutline.Promote(go, EliteOutline.Size, 400f, 6); elites++; }
                }
            }
            clock += Time.unscaledDeltaTime;
            if (clock >= next)
            {
                ScreenCapture.CaptureScreenshot(Path.Combine(Folder, $"shot_{shot++:00}.png"));
                next += Every;
            }
            yield return null;
        }
        Census();
        yield return new WaitForSecondsRealtime(0.5f);
        Done(null);
    }

    // every sprite, particle system and batched mesh showing, counted by what it is
    private static void Census()
    {
        var sprites = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)
            .Where(s => s.enabled && s.gameObject.activeInHierarchy && s.sprite != null)
            .GroupBy(s => s.sprite.name + " [" + (s.sprite.texture != null ? s.sprite.texture.name : "?") + ", " + s.sortingLayerName + ", " + s.gameObject.name + "]")
            .OrderByDescending(g => g.Count()).Take(30).Select(g => g.Count() + "x " + g.Key);
        var particles = FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)
            .Where(p => p.particleCount > 0).Select(p => p.name + ": " + p.particleCount);
        var meshes = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
            .Where(m => m.enabled && m.TryGetComponent(out MeshFilter f) && f.sharedMesh != null && f.sharedMesh.vertexCount > 0)
            .Select(m => m.name + ": " + m.GetComponent<MeshFilter>().sharedMesh.vertexCount / 4 + " quads");
        Debug.Log("CENSUS sprites\n" + string.Join("\n", sprites) + "\nparticles\n" + string.Join("\n", particles) + "\nmeshes\n" + string.Join("\n", meshes));
    }

    // a ring of them just off the player, walking in
    private static void Spawn(List<GameObject> prefabs, Vector2 at, int n)
    {
        for (int i = 0; i < n; i++)
        {
            float a = Random.value * Mathf.PI * 2f, d = Random.Range(5f, 8f);
            ObjectPool.For(prefabs[Random.Range(0, prefabs.Count)]).Get(at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d, Quaternion.identity);
        }
    }

    private void Done(string error)
    {
        Debug.Log(error != null ? "SHOWCASE failed: " + error : $"SHOWCASE done: {Shots} shots in {Folder}");
        UnityEditor.EditorApplication.isPlaying = false;
        Destroy(gameObject);
    }
}
#endif
