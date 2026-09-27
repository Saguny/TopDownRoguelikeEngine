using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Aseprite;
using UnityEngine;

// builds the weapons' effects from their Aseprite files (NewSprites/Asesprites/VFX/Weapons): sets
// the files up as pixel art at the world's pixel size, makes the pooled one-shot effect prefabs
// (Prefabs/VFX/Weapons) and points the weapons, the Arrow and meteor prefabs and the player's aura
// at the new art. it builds the Final Rush boss, the Jiangshi Magistrate, from its art too, the
// enemies' deaths, the three kinds of wen and the longevity peach, and fills the VfxLibrary. building again rebuilds the effect prefabs and re-points everything; the
// Aseprite files are only read, so edit those and build again
public static class WeaponFxBuilder
{
    private const string Root = "Assets/### Different Engine/";
    private const string Art = Root + "NewSprites/Asesprites/VFX/Weapons/";
    private const string BossArt = Root + "NewSprites/Asesprites/Enemies/Boss/";
    private const string EnemyArt = Root + "NewSprites/Asesprites/Enemies/";
    private const string VfxEnemies = Root + "NewSprites/Asesprites/VFX/Enemies/";
    private const string VfxPickups = Root + "NewSprites/Asesprites/VFX/Pickups/";
    private const string CharacterArt = Root + "NewSprites/Asesprites/Characters/";
    private const string VfxUi = Root + "NewSprites/Asesprites/VFX/UI/";
    // Zhuo Lan's file sits on its own at the top of the art folder
    internal const string ZhuoLanArt = Root + "NewSprites/Asesprites/qing_warrior_vs.aseprite";
    // set up and built here; the enemies' and characters' own files are only kept from coming in cut wrong
    private static readonly string[] ArtFolders = { Art, BossArt, VfxEnemies, VfxPickups };
    private static readonly string[] FreshFolders = { Art, EnemyArt, VfxEnemies, VfxPickups, CharacterArt, VfxUi };
    private static bool Fresh(string path) => FreshFolders.Any(path.StartsWith) || path == ZhuoLanArt;
    private const string FxFolder = Root + "Prefabs/VFX/Weapons";
    private const string UnlitMaterial = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

    // the enemies' and the floor's pixel size: 1.3 units is 37 pixels
    public const float WorldPpu = 37f / 1.3f;

    // drawn sizes, in pixels (see the generator): the meteor's head sits this far right of its
    // canvas centre, the target seal and blast are drawn for a 57px (2 unit) radius, the aura ring
    // at these radii, the peach burst for a 43px radius
    private const float MeteorHeadOffset = 14f;
    private static readonly int[] AuraRadii = { 44, 52, 60, 70, 80, 92, 106, 120 };
    private const int AuraFrames = 6;
    private const float PeachBurstRadius = 43f;

    [MenuItem("Tools/VFX/Build Weapon FX")]
    public static void BuildMenu() => Build();

    public static bool Build()
    {
        try
        {
            AssetDatabase.StartAssetEditing();
            Configure();
        }
        finally { AssetDatabase.StopAssetEditing(); }
        AssetDatabase.Refresh();

        var unlit = AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterial);
        EnsureFolder(FxFolder);

        // ---- the one-shot effects
        var arrowHit = OneShot("FX Arrow Hit", Frames("Bow/bow_hit"), 25f, "Default", 2, unlit);
        var loose = OneShot("FX Bow Loose", Frames("Bow/bow_loose"), 27.7f, "Default", 1, unlit);
        var impact = OneShot("FX Meteor Impact", Frames("Meteorite/met_impact"), 23.8f, "Aura", 9, unlit);
        var crater = OneShot("FX Meteor Crater", Frames("Meteorite/met_crater"), 7.1f, "Player", -6, unlit, fadeOut: 1.6f);
        var mark = OneShot("FX Meteor Mark", Frames("Meteorite/met_mark"), 10f, "Player", -4, unlit);
        var zap = OneShot("FX Aura Zap", Frames("Aura/aura_zap"), 25f, "Aura", 7, unlit);
        var ash = OneShot("FX Talisman Ash", Frames("PeachTalismans/peach_ash"), 18f, "Aura", 7, unlit);
        var starHit = OneShot("FX Star Hit", Frames("SevenStarSwords/sss_star_hit"), 25f, "Aura", 9, unlit);
        var launch = OneShot("FX Star Launch", Frames("SevenStarSwords/sss_launch"), 25f, "Aura", 8, unlit);

        // ---- the Bow: the arrow itself and what it plays
        EditPrefab(Root + "Prefabs/Arrow.prefab", go =>
        {
            var sr = go.GetComponent<SpriteRenderer>();
            var frames = Frames("Bow/bow_arrow");
            float oldScale = Mathf.Abs(go.transform.localScale.x);
            go.transform.localScale = Vector3.one;
            sr.sprite = frames[0];
            sr.drawMode = SpriteDrawMode.Simple;
            if (unlit != null) sr.sharedMaterial = unlit;
            // keep the hit size it had in the world
            if (go.TryGetComponent(out CircleCollider2D circle) && oldScale > 1.001f) circle.radius *= oldScale;
            var fb = Ensure<Flipbook>(go);
            fb.frames = frames;
            fb.fps = 16.7f;
            fb.loop = true;
            fb.randomStart = true;
            Set(go.GetComponent<Projectile>(), "hitFx", arrowHit);
        });

        // ---- the player: the bow's loose and the aura
        EditPrefab(Root + "Prefabs/Player.prefab", go =>
        {
            var shooter = go.GetComponentInChildren<AutoShooter>(true);
            if (shooter != null) Set(shooter, "looseFx", loose);

            var aura = go.GetComponentInChildren<Aura>(true);
            if (aura == null) { Debug.LogWarning("Weapon FX: no Aura under the player"); return; }
            var visual = Ensure<AuraVisual>(aura.gameObject);
            var rings = Frames("Aura/aura_ring");
            visual.sizes = AuraRadii.Select((r, k) => new AuraVisual.RingSize
            {
                radius = r / WorldPpu,
                ring = rings.Skip(k * AuraFrames).Take(AuraFrames).ToArray(),
            }).ToArray();
            visual.ringFps = 16.7f;
            visual.zapFx = zap;
            visual.sortingLayer = "Aura";
            visual.ringOrder = 1;
            Set(aura, "visual", visual);

            // the old hand-drawn ring stays in the prefab, switched off
            if (aura.TryGetComponent(out SpriteRenderer oldRing)) oldRing.enabled = false;
            foreach (var animator in aura.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
        });

        // ---- the Meteorite: the meteor, what it leaves on the ground and its blast
        EditPrefab(Root + "Prefabs/AOEProjectile.prefab", go =>
        {
            var meteor = go.GetComponent<AOEProjectile>();
            go.transform.localScale = Vector3.one;
            var sr = go.GetComponentInChildren<SpriteRenderer>(true);
            if (sr != null)
            {
                foreach (var animator in sr.GetComponents<Animator>()) UnityEngine.Object.DestroyImmediate(animator, true);
                var frames = Frames("Meteorite/met_meteor");
                sr.sprite = frames[0];
                if (unlit != null) sr.sharedMaterial = unlit;
                sr.sortingLayerName = "Aura";
                sr.sortingOrder = 10;
                // drawn flying right with its head right of centre: the head sits on the meteor's position
                sr.transform.localPosition = new Vector3(-MeteorHeadOffset / WorldPpu, 0f, 0f);
                sr.transform.localRotation = Quaternion.identity;
                sr.transform.localScale = Vector3.one;
                var fb = Ensure<Flipbook>(sr.gameObject);
                fb.frames = frames;
                fb.fps = 20f;
                fb.loop = true;
                fb.randomStart = true;
            }
            meteor.spriteAngleOffset = 0f;
            meteor.impactEffectPrefab = impact;
            meteor.impactEffectDuration = 0.6f;
            meteor.targetMarkPrefab = mark;
            meteor.craterPrefab = crater;
            meteor.impactShake = 0.05f;
        });

        // ---- the Peach Talismans and the Seven Star Swords read their art from their weapon assets
        var peach = AssetDatabase.LoadAssetAtPath<PeachTalismansData>(Root + "Data/Weapons/PeachTalismans.asset");
        if (peach != null)
        {
            peach.flightFrames = Frames("PeachTalismans/peach_talisman");
            peach.flightFps = 14.3f;
            peach.burnFrames = Frames("PeachTalismans/peach_burn");
            peach.burnFlickerFps = 12.5f;
            peach.ashFx = ash;
            peach.burstFrames = Frames("PeachTalismans/peach_burst");
            peach.burstSeconds = peach.burstFrames.Length * 0.045f;
            peach.burstArtRadius = PeachBurstRadius / WorldPpu;
            EditorUtility.SetDirty(peach);
        }

        var swords = AssetDatabase.LoadAssetAtPath<SevenStarSwordsData>(Root + "Data/Weapons/SevenStarSwords.asset");
        if (swords != null)
        {
            swords.swordFrames = Frames("SevenStarSwords/sss_sword");
            swords.swordFps = 12.5f;
            swords.starFrames = Frames("SevenStarSwords/sss_star");
            swords.starFps = 20f;
            swords.starHitFx = starHit;
            swords.launchFx = launch;
            EditorUtility.SetDirty(swords);
        }

        BuildBoss(unlit, mark, zap);
        BuildPickupsAndDeaths(unlit);
        NewWeaponFxBuilder.Build(unlit);

        AssetDatabase.SaveAssets();
        Debug.Log("WEAPONFX built: 9 effect prefabs, Arrow, AOEProjectile, Player and the Peach Talismans and Seven Star Swords assets");
        return true;
    }

    // ------------------------------------------------------------------ the Aseprite files

    // pixel art at the world's pixel size: one sprite per frame with every layer merged, pivots on
    // the canvas centre (the laser tile on its left edge, as a full rectangle so it can tile),
    // crisp and uncompressed
    internal static void Configure()
    {
        foreach (var path in ArtFolders.Where(Directory.Exists).SelectMany(f => Directory.GetFiles(f, "*.aseprite", SearchOption.AllDirectories)).Select(p => p.Replace('\\', '/')))
        {
            if (!(AssetImporter.GetAtPath(path) is AsepriteImporter importer)) continue;
            string name = Path.GetFileNameWithoutExtension(path);
            // the tiles a renderer repeats along a line: pivot on the left edge, full rectangles
            bool beam = TiledArt.Contains(name);
            // weapons worn on a character's back come at the characters' pixel size
            float ppu = name.StartsWith("back_") ? CharacterPpu() : WorldPpu;

            bool changed = false;
            void Set<T>(T current, T wanted, Action<T> apply)
            {
                if (EqualityComparer<T>.Default.Equals(current, wanted)) return;
                apply(wanted);
                changed = true;
            }
            Set(importer.importMode, FileImportModes.AnimatedSprite, v => importer.importMode = v);
            Set(importer.layerImportMode, LayerImportModes.MergeFrame, v => importer.layerImportMode = v);
            Set(importer.spritePixelsPerUnit, ppu, v => importer.spritePixelsPerUnit = v);
            Set(importer.pivotSpace, PivotSpaces.Canvas, v => importer.pivotSpace = v);
            Set(importer.pivotAlignment, beam ? SpriteAlignment.LeftCenter : SpriteAlignment.Center, v => importer.pivotAlignment = v);
            Set(importer.spriteMeshType, beam ? SpriteMeshType.FullRect : SpriteMeshType.Tight, v => importer.spriteMeshType = v);
            Set(importer.generateModelPrefab, false, v => importer.generateModelPrefab = v);
            Set(importer.generateAnimationClips, false, v => importer.generateAnimationClips = v);
            Set(importer.addSortingGroup, false, v => importer.addSortingGroup = v);
            Set(importer.filterMode, FilterMode.Point, v => importer.filterMode = v);
            Set(importer.mipmapEnabled, false, v => importer.mipmapEnabled = v);
            if (beam) Set(importer.wrapMode, TextureWrapMode.Clamp, v => importer.wrapMode = v);
            if (ClearStaleRects(importer, path)) changed = true;

            var platform = importer.GetImporterPlatformSettings(EditorUserBuildSettings.activeBuildTarget);
            if (platform.textureCompression != TextureImporterCompression.Uncompressed || platform.maxTextureSize < 4096)
            {
                platform.textureCompression = TextureImporterCompression.Uncompressed;
                platform.maxTextureSize = 4096;
                importer.SetImporterPlatformSettings(platform);
                changed = true;
            }
            if (changed) importer.SaveAndReimport();
        }

        foreach (var path in Directory.GetFiles(EnemyArt, "*.aseprite", SearchOption.TopDirectoryOnly).Select(p => p.Replace('\\', '/')))
            if (AssetImporter.GetAtPath(path) is AsepriteImporter importer && ClearStaleRects(importer, path)) importer.SaveAndReimport();
    }

    // this version of the Aseprite importer keeps each frame's old place in the packed texture
    // when a file's frames are redrawn but its canvas and texture stay the same size, so the frames
    // come in cut from the wrong spots: shuffled, with bits of their neighbours at the edges. a file
    // whose contents changed since its last import here gets its stored sprite rects cleared, so
    // every frame is cut fresh. the sprites keep their ids (those live with the frames), so nothing
    // pointing at them breaks. true when it cleared them; the caller reimports
    internal static bool ClearStaleRects(AsepriteImporter importer, string path)
    {
        string hash = FileHash(path);
        if (importer.userData == hash) return false;
        var data = new SerializedObject(importer);
        data.FindProperty("m_AnimatedSpriteImportData").ClearArray();
        data.ApplyModifiedPropertiesWithoutUndo();
        importer.userData = hash;
        return true;
    }

    private static string FileHash(string path) => Hash128.Compute(File.ReadAllBytes(path)).ToString();

    // the same fix for a file changed outside this build, e.g. edited in Aseprite: it's reimported
    // once more straight after, cut fresh
    private class FreshRectsOnChange : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (var path in imported)
            {
                if (!Fresh(path) || !path.EndsWith(".aseprite")) continue;
                if (!(AssetImporter.GetAtPath(path) is AsepriteImporter importer) || importer.userData == FileHash(path)) continue;
                EditorApplication.delayCall += () =>
                {
                    if (AssetImporter.GetAtPath(path) is AsepriteImporter again && ClearStaleRects(again, path)) again.SaveAndReimport();
                };
            }
        }
    }

    private static readonly HashSet<string> TiledArt = new HashSet<string> { "sss_beam", "dl_line", "fs_laser" };

    // Zhuo Lan's pixel size, which every character is drawn at
    internal static float CharacterPpu() =>
        AssetImporter.GetAtPath(ZhuoLanArt) is AsepriteImporter zhuo ? zhuo.spritePixelsPerUnit : 33.88956f;

    // merged frames come in as Frame_0, Frame_1...; a file with a single frame is named after itself
    internal static Sprite[] Frames(string file) => FramesAt(Art + file + ".aseprite");

    internal static Sprite[] FramesAt(string path)
    {
        var all = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
        var frames = new SortedDictionary<int, Sprite>();
        foreach (var sprite in all)
            if (sprite.name.StartsWith("Frame_") && int.TryParse(sprite.name.Substring(6), out int index)) frames[index] = sprite;
        if (frames.Count == 0 && all.Length == 1) frames[0] = all[0];
        if (frames.Count == 0) throw new InvalidDataException("no frames imported from " + path);

        var array = new Sprite[frames.Keys.Max() + 1];
        foreach (var pair in frames) array[pair.Key] = pair.Value;
        if (array.Any(s => s == null)) throw new InvalidDataException(path + " is missing a frame; an empty frame gets dropped on import");
        return array;
    }

    // ------------------------------------------------------------------ the boss

    // the Jiangshi Magistrate replaces the old boss's art and animator on BossEnemy, and brings its
    // corpse fire and slam. its leap reuses the meteor's target seal, its raising the aura's strike
    private static void BuildBoss(Material unlit, GameObject mark, GameObject strike)
    {
        if (!Directory.Exists(BossArt)) return;
        var body = FramesAt(BossArt + "boss_magistrate.aseprite");
        var shadow = FramesAt(BossArt + "boss_shadow.aseprite")[0];
        var orbFrames = FramesAt(BossArt + "boss_orb.aseprite");
        var slam = OneShot("FX Boss Slam", FramesAt(BossArt + "boss_slam.aseprite"), 25f, "Aura", 9, unlit);

        // corpse fire: above everything but the HUD, so a shot is never hidden
        GameObject orb;
        var go = new GameObject("Boss Corpse Fire");
        try
        {
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = orbFrames[0];
            if (unlit != null) sr.sharedMaterial = unlit;
            sr.sortingLayerName = "Default";
            sr.sortingOrder = 30;
            var fb = go.AddComponent<Flipbook>();
            fb.frames = orbFrames;
            fb.fps = 12.5f;
            fb.loop = true;
            fb.randomStart = true;
            go.AddComponent<EnemyBullet>().radius = 0.16f;
            orb = PrefabUtility.SaveAsPrefabAsset(go, FxFolder + "/Boss Corpse Fire.prefab");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }

        var minion = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/Enemies/Jiangshi.prefab");
        EditPrefab(Root + "Prefabs/BossEnemy.prefab", root =>
        {
            foreach (var animator in root.GetComponentsInChildren<Animator>(true)) UnityEngine.Object.DestroyImmediate(animator, true);
            var sr = root.GetComponent<SpriteRenderer>();
            sr.sprite = body[0];
            sr.flipX = false;
            // drawn facing right
            var move = root.GetComponent<EnemyMovement>();
            if (move != null)
            {
                var so = new SerializedObject(move);
                var facing = so.FindProperty("_spriteFacesLeftByDefault");
                if (facing != null) { facing.boolValue = false; so.ApplyModifiedPropertiesWithoutUndo(); }
            }
            // the body: the robe's width, low on the sprite
            if (root.TryGetComponent(out CircleCollider2D circle))
            {
                circle.radius = 0.7f;
                circle.offset = new Vector2(0f, -0.35f);
            }

            var boss = Ensure<BossMagistrate>(root);
            boss.hopFrames = body.Take(6).ToArray();
            boss.castFrames = body.Skip(6).Take(2).ToArray();
            boss.tornHopFrames = body.Skip(8).Take(6).ToArray();
            boss.tornCastFrames = body.Skip(14).Take(2).ToArray();
            boss.shadowSprite = shadow;
            boss.shadowDrop = 43f / WorldPpu;          // its boots are 43px below the canvas centre
            boss.barHeight = 55f / WorldPpu;           // just over the finial
            boss.bulletPrefab = orb;
            boss.markPrefab = mark;
            boss.slamPrefab = slam;
            boss.artRadius = 57f / WorldPpu;
            boss.minionPrefab = minion;
            boss.raiseFx = strike;
        });
    }

    // ------------------------------------------------------------------ deaths, wen and the peach

    // the three kinds of wen as pickups (bronze, jade, a red envelope), the heal as a longevity
    // peach, and the VfxLibrary in Resources pointing at them and at the enemies' deaths
    private static void BuildPickupsAndDeaths(Material unlit)
    {
        if (!Directory.Exists(VfxPickups)) return;
        const string folder = Root + "Prefabs/Pickups";
        EnsureFolder(folder);

        // the old wen's sound goes with the new ones
        var oldWen = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/Wen.prefab");
        AudioClip sound = null;
        float volume = 1f;
        if (oldWen != null && oldWen.TryGetComponent(out Pickup old))
        {
            var so = new SerializedObject(old);
            sound = so.FindProperty("pickupSound").objectReferenceValue as AudioClip;
            volume = so.FindProperty("pickupVolume").floatValue;
        }

        GameObject Wen(string name, string art, int worth)
        {
            var go = new GameObject(name);
            try
            {
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = FramesAt(VfxPickups + art + ".aseprite")[0];
                if (unlit != null) sr.sharedMaterial = unlit;
                sr.sortingLayerName = "Player";
                sr.sortingOrder = 0;
                var reach = go.AddComponent<CircleCollider2D>();
                reach.isTrigger = true;
                reach.radius = 0.3f;
                var pickup = go.AddComponent<Pickup>();
                var so = new SerializedObject(pickup);
                so.FindProperty("wen").intValue = worth;
                so.FindProperty("pickupSound").objectReferenceValue = sound;
                so.FindProperty("pickupVolume").floatValue = volume;
                so.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(go, folder + "/" + name + ".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
        var bronze = Wen("Wen Bronze", "wen_bronze", 1);
        var jade = Wen("Wen Jade", "wen_jade", 5);
        var envelope = Wen("Red Envelope", "wen_envelope", 20);

        // the heal: a longevity peach where the old medkit was, the same size in the world
        EditPrefab(Root + "Prefabs/Heal.prefab", go =>
        {
            foreach (var animator in go.GetComponentsInChildren<Animator>(true)) UnityEngine.Object.DestroyImmediate(animator, true);
            float old = Mathf.Abs(go.transform.localScale.x);
            go.transform.localScale = Vector3.one;
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = FramesAt(VfxPickups + "longevity_peach.aseprite")[0];
            if (unlit != null) sr.sharedMaterial = unlit;
            if (go.TryGetComponent(out BoxCollider2D box)) box.size *= old;
            if (go.TryGetComponent(out CircleCollider2D circle)) circle.radius *= old;
        });

        const string libraryPath = Root + "Resources/VfxLibrary.asset";
        var library = AssetDatabase.LoadAssetAtPath<VfxLibrary>(libraryPath);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<VfxLibrary>();
            AssetDatabase.CreateAsset(library, libraryPath);
        }
        library.enemyDeath = FramesAt(VfxEnemies + "enemy_death.aseprite");
        library.enemyDeathBloodless = FramesAt(VfxEnemies + "enemy_death_bloodless.aseprite");
        library.deathFps = 1000f / 35f;
        library.wenBronze = bronze;
        library.wenJade = jade;
        library.wenEnvelope = envelope;
        // the level up's rain: bronze turning over in frames 0-7, jade in 8-15
        if (File.Exists(VfxPickups + "wen_spin.aseprite"))
        {
            var spin = FramesAt(VfxPickups + "wen_spin.aseprite");
            library.wenSpinBronze = spin.Take(8).ToArray();
            library.wenSpinJade = spin.Skip(8).ToArray();
        }
        EditorUtility.SetDirty(library);
    }

    // ------------------------------------------------------------------ prefabs

    // a pooled one-shot: the flipbook and FxOneShot on one object, its lifetime the animation's
    internal static GameObject OneShot(string name, Sprite[] frames, float fps, string layer, int order, Material material, float fadeOut = 0f)
    {
        var go = new GameObject(name);
        try
        {
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = frames[0];
            if (material != null) sr.sharedMaterial = material;
            sr.sortingLayerName = layer;
            sr.sortingOrder = order;
            var fb = go.AddComponent<Flipbook>();
            fb.frames = frames;
            fb.fps = fps;
            fb.loop = false;
            fb.fadeOut = fadeOut;
            go.AddComponent<FxOneShot>().lifetime = frames.Length / fps + fadeOut + 0.02f;
            return PrefabUtility.SaveAsPrefabAsset(go, FxFolder + "/" + name + ".prefab");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    internal static void EditPrefab(string path, Action<GameObject> edit)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            edit(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static T Ensure<T>(GameObject go) where T : Component =>
        go.TryGetComponent(out T existing) ? existing : go.AddComponent<T>();

    // a serialized field, private or not
    private static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        if (target == null) return;
        var so = new SerializedObject(target);
        var prop = so.FindProperty(field);
        if (prop == null) { Debug.LogWarning($"Weapon FX: {target.GetType().Name} has no field {field}"); return; }
        prop.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureFolder(string folder)
    {
        folder = folder.Replace('\\', '/');
        if (AssetDatabase.IsValidFolder(folder)) return;
        EnsureFolder(Path.GetDirectoryName(folder));
        AssetDatabase.CreateFolder(Path.GetDirectoryName(folder).Replace('\\', '/'), Path.GetFileName(folder));
    }
}
