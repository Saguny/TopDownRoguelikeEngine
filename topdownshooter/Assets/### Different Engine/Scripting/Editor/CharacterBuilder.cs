using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Aseprite;
using UnityEngine;

// Tools > Characters > Build Cast, Maps and Time Limit. puts together what Tools/VFX/characters
// draws: the new characters' clips, animator overrides and character assets (Zhuo Lan's redrawn
// file keeps his), each map's difficulty curve on its playfield, the Wuchang that end a normal
// run and the lock the menus draw over what isn't earned yet. safe to run again: it updates what's
// there instead of making copies
public static class CharacterBuilder
{
    private const string Root = "Assets/### Different Engine/";
    private const string CharacterArt = Root + "NewSprites/Asesprites/Characters/";
    private const string BossArt = Root + "NewSprites/Asesprites/Enemies/Boss/";
    private const string LockArt = Root + "NewSprites/Asesprites/VFX/UI/lock.aseprite";
    private const string Animations = Root + "Animations/Player/";
    private const string Characters = Root + "Data/Characters/";
    private const string Curves = Root + "Data/Curves/";
    private const string EnemyPrefabs = Root + "Prefabs/Enemies/";
    private const string Playfields = Root + "Prefabs/Playfields/";
    private const string UnlitMaterial = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

    private sealed class Cast
    {
        public string file, asset, displayName, description;
        public Type weapon;
        public float damage = 1f, slow;
    }

    private static readonly Cast[] NewCast =
    {
        new Cast
        {
            file = "yun_xi", asset = "YunXi", displayName = "Yun Xi", weapon = typeof(PeachTalismansData), damage = 2f,
            description = "A Maoshan exorcist.",
        },
        new Cast
        {
            file = "ye_tianshu", asset = "YeTianshu", displayName = "Ye Tianshu", weapon = typeof(SevenStarSwordsData), slow = 0.3f,
            description = "A sword immortal of the Dipper.",
        },
    };

    [MenuItem("Tools/Characters/Build Cast, Maps and Time Limit")]
    public static void BuildMenu() => Build();

    public static bool Build()
    {
        try
        {
            AssetDatabase.StartAssetEditing();
            WeaponFxBuilder.Configure();       // the Wuchang, with the boss's settings
            ConfigureCharacters();
        }
        finally { AssetDatabase.StopAssetEditing(); }
        AssetDatabase.Refresh();

        var touched = new List<UnityEngine.Object>();
        BuildCast(touched);
        BuildCurves(touched);
        var lib = VfxLibrary.Get != null ? VfxLibrary.Get : AssetDatabase.LoadAssetAtPath<VfxLibrary>(Root + "Resources/VfxLibrary.asset");
        if (lib != null)
        {
            lib.lockIcon = WeaponFxBuilder.FramesAt(LockArt)[0];
            lib.wuchangBai = Wuchang("Wuchang Bai", "wuchang_bai");
            lib.wuchangHei = Wuchang("Wuchang Hei", "wuchang_hei");
            // the loading screen's art, straight from the weapons
            const string art = Root + "NewSprites/Asesprites/VFX/Weapons/";
            lib.loadingSword = WeaponFxBuilder.FramesAt(art + "SevenStarSwords/sss_sword.aseprite");
            lib.loadingStar = WeaponFxBuilder.FramesAt(art + "SevenStarSwords/sss_star.aseprite");
            lib.loadingTalisman = WeaponFxBuilder.FramesAt(art + "PeachTalismans/peach_talisman.aseprite");
            lib.loadingMeteor = WeaponFxBuilder.FramesAt(art + "Meteorite/met_meteor.aseprite");
            lib.loadingArrow = WeaponFxBuilder.FramesAt(art + "Bow/bow_arrow.aseprite");
            // the aura's ring drawn for a 70 pixel radius (its fourth size, six frames)
            lib.loadingRing = WeaponFxBuilder.FramesAt(art + "Aura/aura_ring.aseprite").Skip(3 * 6).Take(6).ToArray();
            EditorUtility.SetDirty(lib);
            touched.Add(lib);
        }
        foreach (var asset in touched) AssetDatabase.SaveAssetIfDirty(asset);
        Debug.Log("CAST built: " + string.Join(", ", NewCast.Select(c => c.displayName)) + ", both maps' curves, the Wuchang and the lock");
        return true;
    }

    // ------------------------------------------------------------------ importers

    // the new characters come in like Zhuo Lan: his pixel size and pivot, crisp, uncompressed, no
    // clips of their own (they're made below). his redrawn file keeps all its settings; like theirs,
    // it's only cut fresh (see WeaponFxBuilder.ClearStaleRects). the lock is for the UI
    private static void ConfigureCharacters()
    {
        var zhuo = AssetImporter.GetAtPath(WeaponFxBuilder.ZhuoLanArt) as AsepriteImporter;
        if (zhuo == null) throw new InvalidOperationException("Zhuo Lan's art is missing: " + WeaponFxBuilder.ZhuoLanArt);
        if (WeaponFxBuilder.ClearStaleRects(zhuo, WeaponFxBuilder.ZhuoLanArt)) zhuo.SaveAndReimport();

        foreach (var c in NewCast) Configure(CharacterArt + c.file + ".aseprite", zhuo.spritePixelsPerUnit);
        Configure(LockArt, 100f);
    }

    private static void Configure(string path, float ppu)
    {
        if (!(AssetImporter.GetAtPath(path) is AsepriteImporter importer)) throw new InvalidOperationException("not imported yet: " + path);
        importer.importMode = FileImportModes.AnimatedSprite;
        importer.layerImportMode = LayerImportModes.MergeFrame;
        importer.spritePixelsPerUnit = ppu;
        importer.pivotSpace = PivotSpaces.Canvas;
        importer.pivotAlignment = SpriteAlignment.Center;
        importer.spriteMeshType = SpriteMeshType.Tight;
        importer.generateModelPrefab = false;
        importer.generateAnimationClips = false;
        importer.addSortingGroup = false;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        var platform = importer.GetImporterPlatformSettings(EditorUserBuildSettings.activeBuildTarget);
        platform.textureCompression = TextureImporterCompression.Uncompressed;
        platform.maxTextureSize = Mathf.Max(platform.maxTextureSize, 2048);
        importer.SetImporterPlatformSettings(platform);
        WeaponFxBuilder.ClearStaleRects(importer, path);
        importer.SaveAndReimport();
    }

    // ------------------------------------------------------------------ the characters

    private static void BuildCast(List<UnityEngine.Object> touched)
    {
        var qing = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(Animations + "QingWarrior.overrideController");
        if (qing == null) throw new InvalidOperationException("QingWarrior.overrideController is missing");
        var slots = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        qing.GetOverrides(slots);
        var idleSlot = slots.First(s => s.Value != null && s.Value.name.Contains("Idle")).Key;
        var walkSlot = slots.First(s => s.Value != null && s.Value.name.Contains("Walk")).Key;

        var catalog = StatCatalog.Load();
        foreach (var c in NewCast)
        {
            var frames = WeaponFxBuilder.FramesAt(CharacterArt + c.file + ".aseprite");
            var idle = Clip(Animations + c.asset + "_Idle.anim", frames.Take(4).ToArray(), 0.22f);
            var walk = Clip(Animations + c.asset + "_Walk.anim", frames.Skip(4).Take(4).ToArray(), 0.15f);

            string controllerPath = Animations + c.asset + ".overrideController";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(controllerPath);
            if (controller == null)
            {
                controller = new AnimatorOverrideController(qing.runtimeAnimatorController);
                AssetDatabase.CreateAsset(controller, controllerPath);
            }
            controller.runtimeAnimatorController = qing.runtimeAnimatorController;
            controller.ApplyOverrides(new List<KeyValuePair<AnimationClip, AnimationClip>>
            {
                new KeyValuePair<AnimationClip, AnimationClip>(idleSlot, idle),
                new KeyValuePair<AnimationClip, AnimationClip>(walkSlot, walk),
            });
            EditorUtility.SetDirty(controller);
            touched.Add(controller);

            string dataPath = Characters + c.asset + ".asset";
            var data = AssetDatabase.LoadAssetAtPath<CharacterData>(dataPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<CharacterData>();
                AssetDatabase.CreateAsset(data, dataPath);
            }
            data.displayName = c.displayName;
            data.description = c.description;
            data.portrait = frames[0];
            data.animations = controller;
            data.startingWeapon = Upgrade(c.weapon);
            data.signatureDamage = c.damage;
            data.signatureSlow = c.slow;
            data.signatureSlowSeconds = 2f;
            data.startsUnlocked = false;
            EditorUtility.SetDirty(data);
            touched.Add(data);

            if (!catalog.characters.Contains(data)) catalog.characters.Add(data);
        }

        // Zhuo Lan is everyone's first character
        var zhuoLan = AssetDatabase.LoadAssetAtPath<CharacterData>(Characters + "ZhuoLanArcher.asset");
        if (zhuoLan != null)
        {
            zhuoLan.startsUnlocked = true;
            EditorUtility.SetDirty(zhuoLan);
            touched.Add(zhuoLan);
        }
        EditorUtility.SetDirty(catalog);
        touched.Add(catalog);
    }

    // a looping sprite clip on the player's SpriteRenderer, each frame held for the same time, the
    // last one held to the end like Zhuo Lan's clips
    private static AnimationClip Clip(string path, Sprite[] frames, float secondsEach)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        bool made = clip == null;
        if (made) clip = new AnimationClip { frameRate = 60f };

        var keys = new ObjectReferenceKeyframe[frames.Length + 1];
        for (int i = 0; i < frames.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i * secondsEach, value = frames[i] };
        keys[frames.Length] = new ObjectReferenceKeyframe { time = frames.Length * secondsEach, value = frames[frames.Length - 1] };
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        if (made) AssetDatabase.CreateAsset(clip, path);
        else EditorUtility.SetDirty(clip);
        return clip;
    }

    // the weapon's upgrade asset in the level up pool, e.g. PeachTalismans for PeachTalismansData
    private static UpgradeData Upgrade(Type type)
    {
        var found = AssetDatabase.FindAssets("t:UpgradeData")
            .Select(g => AssetDatabase.LoadAssetAtPath<UpgradeData>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(u => u != null && u.GetType() == type).ToList();
        return found.FirstOrDefault(u => u.includeInPool) ?? found.FirstOrDefault();
    }

    // ------------------------------------------------------------------ difficulty by map

    // a normal run lasts 30 minutes at most, where the Wuchang come (the keys past that are for
    // endless). the numbers are the softened ones the game shipped with, so building again doesn't
    // undo them. the courtyard is the first map, so it starts
    // gentle and climbs steadily; Huangquan Road opens only once it's cleared, when the shop has
    // made the player stronger, so everything there starts tougher, climbs higher and crowds more.
    // speed stays well under 3x: enemies faster than the player make a run unwinnable, not hard.
    // levels come at the same pace on both (the scene's curve's)
    private static void BuildCurves(List<UnityEngine.Object> touched)
    {
        var scene = AssetDatabase.LoadAssetAtPath<DifficultyCurve>(Curves + "FixedCurve.asset");

        var courtyard = Curve(scene, "CourtyardDifficulty",
            health: new[] { (0f, 1f), (300f, 1.25f), (600f, 2f), (900f, 3.1f), (1200f, 4.4f), (1500f, 5.9f), (1800f, 7.6f), (2100f, 9.5f), (2400f, 11.6f), (2700f, 13.8f) },
            speed: new[] { (0f, 1f), (1200f, 1.5f), (2400f, 2f), (2700f, 2.2f) },
            damage: new[] { (0f, 0.8f), (600f, 1.3f), (1200f, 2f), (1800f, 2.8f), (2400f, 3.6f), (2700f, 4f) },
            densityMax: 9f, plateau: 2100f, sharpness: 2.4f);
        var huangquan = Curve(scene, "HuangquanDifficulty",
            health: new[] { (0f, 1.1f), (300f, 1.6f), (600f, 2.8f), (900f, 4.2f), (1200f, 6f), (1500f, 8f), (1800f, 10.3f), (2100f, 12.8f), (2400f, 15.8f), (2700f, 19f) },
            speed: new[] { (0f, 1.1f), (1200f, 1.7f), (2400f, 2.3f), (2700f, 2.4f) },
            damage: new[] { (0f, 1f), (600f, 1.6f), (1200f, 2.6f), (1800f, 3.6f), (2400f, 4.6f), (2700f, 5.2f) },
            densityMax: 11f, plateau: 1800f, sharpness: 2.8f);
        touched.Add(courtyard);
        touched.Add(huangquan);

        SetDifficulty(Playfields + "Courtyard.prefab", courtyard);
        SetDifficulty(Playfields + "Huangquan Road.prefab", huangquan);
    }

    private static DifficultyCurve Curve(DifficultyCurve template, string name, (float t, float v)[] health, (float t, float v)[] speed,
        (float t, float v)[] damage, float densityMax, float plateau, float sharpness)
    {
        string path = Curves + name + ".asset";
        var curve = AssetDatabase.LoadAssetAtPath<DifficultyCurve>(path);
        if (curve == null)
        {
            curve = template != null ? UnityEngine.Object.Instantiate(template) : ScriptableObject.CreateInstance<DifficultyCurve>();
            AssetDatabase.CreateAsset(curve, path);
        }
        curve.enemyHealth = Smooth(health);
        curve.enemySpeed = Smooth(speed);
        curve.enemyDamage = Smooth(damage);
        if (template != null) curve.wenNeeded = new AnimationCurve(template.wenNeeded.keys);

        var so = new SerializedObject(curve);
        so.FindProperty("spawnDensityMax").floatValue = densityMax;
        so.FindProperty("spawnDensityPlateauTime").floatValue = plateau;
        so.FindProperty("spawnDensityCurveSharpness").floatValue = sharpness;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(curve);
        return curve;
    }

    private static AnimationCurve Smooth((float t, float v)[] keys)
    {
        var curve = new AnimationCurve(keys.Select(k => new Keyframe(k.t, k.v)).ToArray());
        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
        }
        return curve;
    }

    private static void SetDifficulty(string prefabPath, DifficultyCurve curve)
    {
        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var field = root.GetComponentInChildren<Playfield>(true);
            if (field == null) throw new InvalidOperationException("no Playfield on " + prefabPath);
            field.difficulty = curve;
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // ------------------------------------------------------------------ the Wuchang

    private static GameObject Wuchang(string name, string art)
    {
        var frames = WeaponFxBuilder.FramesAt(BossArt + art + ".aseprite");
        var go = new GameObject(name);
        try
        {
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = frames[0];
            var unlit = AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterial);
            if (unlit != null) sr.sharedMaterial = unlit;
            sr.sortingLayerName = "Player";
            sr.sortingOrder = 20;
            var fb = go.AddComponent<Flipbook>();
            fb.frames = frames;
            fb.fps = 1000f / 140f;
            fb.loop = true;
            go.AddComponent<Wuchang>();
            return PrefabUtility.SaveAsPrefabAsset(go, EnemyPrefabs + name + ".prefab");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }
}
