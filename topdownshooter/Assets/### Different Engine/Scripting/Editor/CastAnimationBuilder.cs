using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Aseprite;
using UnityEngine;

// turns a cast timeline (a .cast.json next to its .aseprite files) into a CastAnimation prefab:
// one sprite child per object, one clip holding every track and animation event, and an
// animator playing it. after that the prefab and clip are ordinary assets to edit by hand.
// building again replaces them, so it asks first when the prefab already exists
public static class CastAnimationBuilder
{
    private const string CommandTokenTimeline = "Assets/### Different Engine/NewSprites/Asesprites/VFX/CommandToken/CommandTokenCast.cast.json";
    private const string UnlitMaterial = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

    [Serializable] private class Source { public string key; public string path; }
    [Serializable] private class Obj { public string name; public string sprite; public float x; public float y; public float scale; public int order; public float[] color; public bool active; }
    [Serializable] private class Track { public string path; public string prop; public string mode; public float[] times; public float[] values; public string[] sprites; }
    [Serializable] private class Ev { public float time; public string fn; public float f; }
    [Serializable]
    private class Timeline
    {
        public string name; public float ppu; public float length; public float lifetime; public string sortingLayer;
        public string prefabPath; public string clipPath; public string controllerPath; public string weaponAsset;
        public Source[] sources; public Obj[] objects; public Track[] tracks; public Ev[] events;
    }

    [MenuItem("Tools/VFX/Build Command Token Cast")]
    private static void BuildCommandToken() => Build(CommandTokenTimeline, true);

    [MenuItem("Tools/VFX/Build Cast From Selected Timeline")]
    private static void BuildSelected() => Build(AssetDatabase.GetAssetPath(Selection.activeObject), true);

    [MenuItem("Tools/VFX/Build Cast From Selected Timeline", true)]
    private static bool CanBuildSelected() => AssetDatabase.GetAssetPath(Selection.activeObject).EndsWith(".cast.json");

    public static GameObject Build(string timelinePath, bool askBeforeReplacing)
    {
        var json = AssetDatabase.LoadAssetAtPath<TextAsset>(timelinePath);
        if (json == null) throw new FileNotFoundException("no cast timeline at " + timelinePath);
        var tl = JsonUtility.FromJson<Timeline>(json.text);

        if (askBeforeReplacing && File.Exists(tl.prefabPath) &&
            !EditorUtility.DisplayDialog("Rebuild " + tl.name + "?",
                "This replaces " + tl.name + "'s prefab and animation clip, including any changes made to them by hand.", "Rebuild", "Cancel"))
            return null;

        var sprites = LoadSprites(tl);
        Sprite Resolve(string reference)
        {
            var parts = reference.Split(':');
            if (!sprites.TryGetValue(parts[0], out var frames)) throw new ArgumentException("unknown sprite source " + parts[0]);
            int index = int.Parse(parts[1]);
            if (index >= frames.Length || frames[index] == null) throw new ArgumentException($"{parts[0]} has no frame {index}");
            return frames[index];
        }

        // the objects
        var root = new GameObject(tl.name);
        try
        {
            var cast = root.AddComponent<CastAnimation>();
            var castData = new SerializedObject(cast);
            castData.FindProperty("lifetime").floatValue = tl.lifetime;
            castData.ApplyModifiedPropertiesWithoutUndo();

            var unlit = AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterial);
            foreach (var o in tl.objects)
            {
                var go = new GameObject(o.name);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = new Vector3(o.x / tl.ppu, o.y / tl.ppu, 0f);
                go.transform.localScale = Vector3.one * o.scale;

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = Resolve(o.sprite);
                if (unlit != null) sr.sharedMaterial = unlit;
                sr.sortingLayerName = tl.sortingLayer;
                sr.sortingOrder = o.order;
                if (o.color != null && o.color.Length == 4) sr.color = new Color(o.color[0], o.color[1], o.color[2], o.color[3]);
                go.SetActive(o.active);
            }

            // the clip
            var clip = new AnimationClip { name = tl.name, frameRate = 60f };
            foreach (var t in tl.tracks)
            {
                switch (t.prop)
                {
                    case "sprite":
                        var keys = t.times.Select((time, i) => new ObjectReferenceKeyframe { time = time, value = Resolve(t.sprites[i]) }).ToArray();
                        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve(t.path, typeof(SpriteRenderer), "m_Sprite"), keys);
                        break;
                    case "active": SetCurve(clip, t, typeof(GameObject), "m_IsActive", true, 1f); break;
                    case "alpha": SetCurve(clip, t, typeof(SpriteRenderer), "m_Color.a", t.mode == "step", 1f); break;
                    case "x": SetCurve(clip, t, typeof(Transform), "m_LocalPosition.x", t.mode == "step", 1f / tl.ppu); break;
                    case "y": SetCurve(clip, t, typeof(Transform), "m_LocalPosition.y", t.mode == "step", 1f / tl.ppu); break;
                    case "scale":
                        foreach (var axis in new[] { "x", "y", "z" })
                            SetCurve(clip, t, typeof(Transform), "m_LocalScale." + axis, t.mode == "step", 1f);
                        break;
                    default: throw new ArgumentException("unknown track property " + t.prop);
                }
            }
            AnimationUtility.SetAnimationEvents(clip, tl.events.Select(e => new AnimationEvent { time = e.time, functionName = e.fn, floatParameter = e.f }).ToArray());
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            clip = SaveReplacing(clip, tl.clipPath);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(tl.controllerPath);
            if (controller == null)
            {
                EnsureFolder(Path.GetDirectoryName(tl.controllerPath));
                controller = AnimatorController.CreateAnimatorControllerAtPathWithClip(tl.controllerPath, clip);
            }

            var animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // the prefab, and the weapon pointed at it unless it already plays something else
            EnsureFolder(Path.GetDirectoryName(tl.prefabPath));
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, tl.prefabPath);
            if (!string.IsNullOrEmpty(tl.weaponAsset))
            {
                var weapon = AssetDatabase.LoadMainAssetAtPath(tl.weaponAsset);
                var slot = weapon != null ? new SerializedObject(weapon).FindProperty("castAnimation") : null;
                if (slot != null && slot.objectReferenceValue == null)
                {
                    slot.objectReferenceValue = prefab.GetComponent<CastAnimation>();
                    slot.serializedObject.ApplyModifiedPropertiesWithoutUndo();
                    AssetDatabase.SaveAssets();
                }
            }

            Debug.Log($"built {tl.name}: {tl.objects.Length} objects, {tl.tracks.Length} tracks, {tl.events.Length} events -> {tl.prefabPath}", prefab);
            return prefab;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void SetCurve(AnimationClip clip, Track t, Type type, string property, bool step, float factor)
    {
        var curve = new AnimationCurve(t.times.Select((time, i) => new Keyframe(time, t.values[i] * factor)).ToArray());
        var mode = step ? AnimationUtility.TangentMode.Constant : AnimationUtility.TangentMode.Linear;
        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, mode);
            AnimationUtility.SetKeyRightTangentMode(curve, i, mode);
        }
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(t.path, type, property), curve);
    }

    // the Aseprite files, set up for this: one sprite per frame with every layer merged, pivots on
    // the canvas centre, crisp pixels, and no prefab or clips of their own
    private static Dictionary<string, Sprite[]> LoadSprites(Timeline tl)
    {
        var result = new Dictionary<string, Sprite[]>();
        foreach (var source in tl.sources)
        {
            if (!(AssetImporter.GetAtPath(source.path) is AsepriteImporter importer))
                throw new FileNotFoundException("no imported Aseprite file at " + source.path);

            bool changed = false;
            void Set<T>(T current, T wanted, Action<T> apply)
            {
                if (EqualityComparer<T>.Default.Equals(current, wanted)) return;
                apply(wanted);
                changed = true;
            }
            Set(importer.importMode, FileImportModes.AnimatedSprite, v => importer.importMode = v);
            Set(importer.layerImportMode, LayerImportModes.MergeFrame, v => importer.layerImportMode = v);
            Set(importer.spritePixelsPerUnit, tl.ppu, v => importer.spritePixelsPerUnit = v);
            Set(importer.pivotSpace, PivotSpaces.Canvas, v => importer.pivotSpace = v);
            Set(importer.pivotAlignment, SpriteAlignment.Center, v => importer.pivotAlignment = v);
            Set(importer.generateModelPrefab, false, v => importer.generateModelPrefab = v);
            Set(importer.generateAnimationClips, false, v => importer.generateAnimationClips = v);
            Set(importer.addSortingGroup, false, v => importer.addSortingGroup = v);
            Set(importer.filterMode, FilterMode.Point, v => importer.filterMode = v);
            Set(importer.mipmapEnabled, false, v => importer.mipmapEnabled = v);

            var platform = importer.GetImporterPlatformSettings(EditorUserBuildSettings.activeBuildTarget);
            if (platform.textureCompression != TextureImporterCompression.Uncompressed)
            {
                platform.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SetImporterPlatformSettings(platform);
                changed = true;
            }
            if (changed) importer.SaveAndReimport();

            // merged frames come in as Frame_0, Frame_1...; a file with a single frame is named after itself
            var frames = new SortedDictionary<int, Sprite>();
            var all = AssetDatabase.LoadAllAssetsAtPath(source.path).OfType<Sprite>().ToArray();
            foreach (var sprite in all)
                if (sprite.name.StartsWith("Frame_") && int.TryParse(sprite.name.Substring(6), out int index)) frames[index] = sprite;
            if (frames.Count == 0 && all.Length == 1) frames[0] = all[0];
            if (frames.Count == 0) throw new InvalidDataException("no frames imported from " + source.path);

            var array = new Sprite[frames.Keys.Max() + 1];
            foreach (var pair in frames) array[pair.Key] = pair.Value;
            result[source.key] = array;
        }
        return result;
    }

    // overwrites an existing clip in place so everything pointing at it keeps working
    private static AnimationClip SaveReplacing(AnimationClip clip, string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing == null)
        {
            EnsureFolder(Path.GetDirectoryName(path));
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }
        EditorUtility.CopySerialized(clip, existing);
        existing.name = Path.GetFileNameWithoutExtension(path);
        EditorUtility.SetDirty(existing);
        AssetDatabase.SaveAssets();
        return existing;
    }

    private static void EnsureFolder(string folder)
    {
        folder = folder.Replace('\\', '/');
        if (AssetDatabase.IsValidFolder(folder)) return;
        EnsureFolder(Path.GetDirectoryName(folder));
        AssetDatabase.CreateFolder(Path.GetDirectoryName(folder).Replace('\\', '/'), Path.GetFileName(folder));
    }

    // the first time the Command Token timeline shows up, its cast is built on its own, once
    private static string AutoBuiltKey => "CastAnimationBuilder.CommandTokenCast.v2." + Application.dataPath;

    [InitializeOnLoadMethod]
    private static void ScheduleAutoBuild()
    {
        if (EditorPrefs.GetBool(AutoBuiltKey)) return;
        EditorApplication.delayCall -= AutoBuild;
        EditorApplication.delayCall += AutoBuild;
    }

    private static void AutoBuild()
    {
        if (EditorPrefs.GetBool(AutoBuiltKey) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += AutoBuild;
            return;
        }
        var json = AssetDatabase.LoadAssetAtPath<TextAsset>(CommandTokenTimeline);
        if (json == null) return;

        EditorPrefs.SetBool(AutoBuiltKey, true);
        if (File.Exists(JsonUtility.FromJson<Timeline>(json.text).prefabPath)) return;
        try { Build(CommandTokenTimeline, false); }
        catch (Exception e) { Debug.LogError("couldn't build the Command Token cast automatically, try Tools > VFX > Build Command Token Cast\n" + e); }
    }

    private class AutoBuildOnImport : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (imported.Contains(CommandTokenTimeline)) ScheduleAutoBuild();
        }
    }
}
