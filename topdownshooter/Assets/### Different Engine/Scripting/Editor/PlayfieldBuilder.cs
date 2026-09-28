using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Aseprite;
using UnityEngine;

// turns a playfield layout (.playfield.json next to its .aseprite art) into a prefab: tiled
// ground, decals, walls and props with their colliders on the Obstacles layer, animated pieces
// on a SpriteFlipbook, and a Playfield component naming the four walls for the spawner.
// building again replaces the prefab, so hand edits to it are lost; edit the layout instead
public static class PlayfieldBuilder
{
    [Serializable] private class Source { public string key; public string path; public bool tile; }
    [Serializable] private class Shape { public string type; public float x; public float y; public float w; public float h; public float r; }
    [Serializable]
    private class Obj
    {
        public string name, group, sprite, layer, bound;
        public float x, y, w, h, rot, fps;
        public bool tiled, flipX, flipY;
        public int order, frames;
        public Shape collider;
    }
    [Serializable] private class Layout { public string name; public float ppu; public string prefabPath; public string timeline; public float[] start; public Source[] sources; public Obj[] objects; }

    [MenuItem("Tools/Playfield/Build From Selected Layout")]
    private static void BuildSelected() => Build(AssetDatabase.GetAssetPath(Selection.activeObject));

    [MenuItem("Tools/Playfield/Build From Selected Layout", true)]
    private static bool CanBuildSelected() => AssetDatabase.GetAssetPath(Selection.activeObject).EndsWith(".playfield.json");

    public static GameObject Build(string layoutPath)
    {
        var text = AssetDatabase.LoadAssetAtPath<TextAsset>(layoutPath);
        if (text == null) throw new FileNotFoundException("no playfield layout at " + layoutPath);
        var layout = JsonUtility.FromJson<Layout>(text.text);
        var sprites = LoadSprites(layout);
        int obstacles = LayerMask.NameToLayer("Obstacles");

        var root = new GameObject(layout.name);
        try
        {
            var playfield = root.AddComponent<Playfield>();
            if (!string.IsNullOrEmpty(layout.timeline))
            {
                playfield.spawnTimeline = AssetDatabase.LoadAssetAtPath<SpawnTimeline>(layout.timeline);
                if (playfield.spawnTimeline == null) Debug.LogWarning("PlayfieldBuilder: no spawn timeline at " + layout.timeline);
            }
            if (layout.start != null && layout.start.Length == 2) playfield.playerStart = new Vector2(layout.start[0], layout.start[1]);
            // what's set on the prefab by hand, not in the layout, survives a rebuild
            var before = AssetDatabase.LoadAssetAtPath<GameObject>(layout.prefabPath);
            if (before != null && before.TryGetComponent(out Playfield old))
            {
                playfield.difficulty = old.difficulty;
                playfield.finalBoss = old.finalBoss;
            }
            var groups = new Dictionary<string, Transform>();
            foreach (var o in layout.objects)
            {
                if (!groups.TryGetValue(o.group ?? "", out var parent))
                {
                    parent = new GameObject(string.IsNullOrEmpty(o.group) ? "Misc" : o.group).transform;
                    parent.SetParent(root.transform, false);
                    groups[o.group ?? ""] = parent;
                }
                if (!sprites.TryGetValue(o.sprite, out var frames)) throw new ArgumentException("unknown sprite " + o.sprite);

                var go = new GameObject(o.name);
                go.transform.SetParent(parent, false);
                go.transform.localPosition = new Vector3(o.x, o.y, 0f);
                if (Mathf.Abs(o.rot) > 0.01f) go.transform.localRotation = Quaternion.Euler(0f, 0f, o.rot);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = frames[0];
                sr.sortingLayerName = o.layer;
                sr.sortingOrder = o.order;
                sr.flipX = o.flipX;
                sr.flipY = o.flipY;
                if (o.tiled)
                {
                    sr.drawMode = SpriteDrawMode.Tiled;
                    sr.tileMode = SpriteTileMode.Continuous;
                    sr.size = new Vector2(o.w, o.h);
                }
                if (o.frames > 1 && frames.Length > 1) go.AddComponent<SpriteFlipbook>().Setup(frames, o.fps > 0 ? o.fps : 8f);

                if (o.collider != null && !string.IsNullOrEmpty(o.collider.type))
                {
                    if (obstacles >= 0) go.layer = obstacles;
                    if (o.collider.type == "circle")
                    {
                        var c = go.AddComponent<CircleCollider2D>();
                        c.offset = new Vector2(o.collider.x, o.collider.y);
                        c.radius = o.collider.r;
                    }
                    else
                    {
                        var b = go.AddComponent<BoxCollider2D>();
                        b.offset = new Vector2(o.collider.x, o.collider.y);
                        b.size = new Vector2(o.collider.w, o.collider.h);
                        switch (o.bound)
                        {
                            case "north": playfield.north = b; break;
                            case "south": playfield.south = b; break;
                            case "east": playfield.east = b; break;
                            case "west": playfield.west = b; break;
                        }
                    }
                }
            }

            EnsureFolder(Path.GetDirectoryName(layout.prefabPath));
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, layout.prefabPath);
            Debug.Log($"built {layout.name}: {layout.objects.Length} objects -> {layout.prefabPath}", prefab);
            return prefab;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    // every source imported as crisp single sprites (or one per frame), centred; ground and wall
    // tiles as full-rect repeating sprites so they can be tiled
    private static Dictionary<string, Sprite[]> LoadSprites(Layout layout)
    {
        var result = new Dictionary<string, Sprite[]>();
        foreach (var s in layout.sources)
        {
            if (!(AssetImporter.GetAtPath(s.path) is AsepriteImporter importer))
                throw new FileNotFoundException("no imported Aseprite file at " + s.path);

            importer.importMode = FileImportModes.AnimatedSprite;
            importer.layerImportMode = LayerImportModes.MergeFrame;
            importer.spritePixelsPerUnit = layout.ppu;
            importer.pivotSpace = PivotSpaces.Canvas;
            importer.pivotAlignment = SpriteAlignment.Center;
            importer.generateModelPrefab = false;
            importer.generateAnimationClips = false;
            importer.addSortingGroup = false;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            if (s.tile)
            {
                importer.spriteMeshType = SpriteMeshType.FullRect;
                importer.wrapMode = TextureWrapMode.Repeat;
            }
            var platform = importer.GetImporterPlatformSettings(EditorUserBuildSettings.activeBuildTarget);
            platform.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetImporterPlatformSettings(platform);
            importer.SaveAndReimport();

            var all = AssetDatabase.LoadAllAssetsAtPath(s.path).OfType<Sprite>().ToArray();
            var frames = all.Where(x => x.name.StartsWith("Frame_")).OrderBy(x => int.Parse(x.name.Substring(6))).ToArray();
            if (frames.Length == 0 && all.Length == 1) frames = all;   // a single frame is named after the file
            if (frames.Length == 0) throw new InvalidDataException("no sprites imported from " + s.path);
            result[s.key] = frames;
        }
        return result;
    }

    private static void EnsureFolder(string folder)
    {
        folder = folder.Replace('\\', '/').TrimEnd('/');
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
}
