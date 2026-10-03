using System.Collections.Generic;
using UnityEngine;

// the player's weapons, drawn as see-through as the Player Shot Opacity option says
// (GameSettings.PlayerShotOpacity), so a screen full of arrows, swords and talismans can be turned
// down until the enemies' shots read through it. every sprite a weapon puts out is drawn with the
// Player Shot shader, whose alpha is multiplied by one global value: changing the option fades
// them all at once, with no renderer touched.
//
// what's tagged: anything WeaponFx.Make puts under a weapon's Fx (most of every weapon), the
// pooled arrows and one-shots (Tag, FxOneShot.PlayShot), the batched one-shots (FxBatch.PlayShot)
// and the odd renderer a weapon builds itself. only plain sprites are retagged: a sprite already
// drawn with a material of its own (an outline, a flash) keeps it
public static class PlayerShots
{
    private const string FadeProperty = "_PlayerShotFade";
    private static Material sprite;
    private static readonly HashSet<Transform> roots = new HashSet<Transform>();
    private static readonly HashSet<int> tagged = new HashSet<int>();
    private static readonly List<SpriteRenderer> found = new List<SpriteRenderer>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        roots.Clear();
        tagged.Clear();
        sprite = null;
    }

    // how solid they're drawn: 1 as made, down to MinOpacity
    public const float MinOpacity = 0.2f;
    public static void Apply(float opacity) => Shader.SetGlobalFloat(FadeProperty, 1f - Mathf.Clamp(opacity, MinOpacity, 1f));

    // a weapon's Fx holder: whatever WeaponFx.Make puts under it is tagged
    public static void AddRoot(Transform root) { if (root != null) roots.Add(root); }
    public static void RemoveRoot(Transform root) { if (root != null) roots.Remove(root); }

    public static bool UnderRoot(Transform t)
    {
        for (; t != null; t = t.parent) if (roots.Contains(t)) return true;
        return false;
    }

    private static Material SpriteMaterial
    {
        get
        {
            if (sprite == null)
            {
                var shader = Shader.Find("Rogue/Player Shot");
                if (shader != null) sprite = new Material(shader) { name = "Player Shot" };
            }
            return sprite;
        }
    }

    // a batched mesh's material (FxBatch, the ink brush's stroke), faded with the sprites
    public static Material BatchMaterial(Texture texture, string name)
    {
        var shader = Shader.Find("Rogue/Player Shot Batch");
        if (shader == null) shader = Shader.Find("Rogue/Sprite Batch");
        return shader != null ? new Material(shader) { mainTexture = texture, name = name } : null;
    }

    // drawn by the default sprite shaders, so it can take the faded one without looking different
    private static bool Plain(Material m)
    {
        if (m == null || m.shader == null) return true;
        string s = m.shader.name;
        return s.Contains("Sprite-Lit-Default") || s.Contains("Sprite-Unlit-Default") || s == "Sprites/Default";
    }

    public static void Tag(SpriteRenderer sr)
    {
        if (sr == null) return;
        var m = SpriteMaterial;
        if (m != null && Plain(sr.sharedMaterial)) sr.sharedMaterial = m;
    }

    // every sprite under a spawned shot (an arrow from the pool, a one-shot effect). a pooled one
    // keeps its material between uses, so it's only looked through once
    public static void Tag(GameObject go, bool pooled = true)
    {
        if (go == null || (pooled && !tagged.Add(go.GetInstanceID()))) return;
        go.GetComponentsInChildren(true, found);
        foreach (var sr in found) Tag(sr);
        found.Clear();
    }
}
