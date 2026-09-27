using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// the newer art (Tools/VFX/weapons2, under NewSprites/Asesprites/VFX/Weapons): points the Dragon
// Line, the Ice Cloud and the Flying Sword at their frames, effects and icons, the Final Rush arena
// at its spirit seals, and the starting weapons at the art they're worn on a character's back
// with. part of Tools > VFX > Build Weapon FX (WeaponFxBuilder), which sets the files up first;
// it also runs once on its own the first time the art is in the project
public static class NewWeaponFxBuilder
{
    private const string Root = "Assets/### Different Engine/";
    private const string Weapons = Root + "Data/Weapons/";
    private const float WorldPpu = WeaponFxBuilder.WorldPpu;

    internal static void Build(Material unlit)
    {
        var bite = WeaponFxBuilder.OneShot("FX Dragon Bite", WeaponFxBuilder.Frames("DragonLine/dl_bite"), 25f, "Aura", 9, unlit);
        var fire = WeaponFxBuilder.OneShot("FX Dragon Fire", WeaponFxBuilder.Frames("DragonLine/dl_fire"), 22f, "Aura", 10, unlit);
        var frost = WeaponFxBuilder.OneShot("FX Frost Burst", WeaponFxBuilder.Frames("IceCloud/ic_burst"), 25f, "Aura", 9, unlit);
        var spark = WeaponFxBuilder.OneShot("FX Sword Spark", WeaponFxBuilder.Frames("FlyingSword/fs_spark"), 25f, "Aura", 9, unlit);
        var snap = WeaponFxBuilder.OneShot("FX Sword Launch", WeaponFxBuilder.Frames("FlyingSword/fs_launch"), 28f, "Aura", 9, unlit);
        var shatter = WeaponFxBuilder.OneShot("FX Sword Shatter", WeaponFxBuilder.Frames("FlyingSword/fs_shatter"), 22f, "Aura", 9, unlit);

        Edit<DragonLineData>("DragonLine", d =>
        {
            d.headFrames = WeaponFxBuilder.Frames("DragonLine/dl_head");
            d.bodyFrames = WeaponFxBuilder.Frames("DragonLine/dl_body");
            d.legFrames = WeaponFxBuilder.Frames("DragonLine/dl_leg");
            d.tailFrames = WeaponFxBuilder.Frames("DragonLine/dl_tail");
            d.bodyFps = 10f;
            d.lineFrames = WeaponFxBuilder.Frames("DragonLine/dl_line");
            d.lineFps = 14f;
            d.biteFx = bite;
            d.fireFx = fire;
            d.segmentArtPixels = 14f;
            Icons(d, "DragonLine/dl_icon", "DragonLine/dl_icon_evolved");
        });

        Edit<IceCloudData>("IceCloud", d =>
        {
            d.cloudFrames = WeaponFxBuilder.Frames("IceCloud/ic_cloud");
            d.snowFrames = WeaponFxBuilder.Frames("IceCloud/ic_snow");
            d.cloudFps = 7f;
            // the snowfall is drawn for a 45.5 pixel radius
            d.snowArtRadius = 45.5f / WorldPpu;
            d.pileFrames = WeaponFxBuilder.Frames("IceCloud/ic_pile");
            d.pileRestFrame = 3;
            d.pileFps = 12.5f;
            d.iceFrames = WeaponFxBuilder.Frames("IceCloud/ic_ice");
            d.iceFps = 16.7f;
            d.tornadoFrames = WeaponFxBuilder.Frames("IceCloud/ic_tornado");
            d.tornadoFps = 14.3f;
            d.frostBurstFx = frost;
            Icons(d, "IceCloud/ic_icon", "IceCloud/ic_icon_evolved");
        });

        Edit<FlyingSwordData>("FlyingSword", d =>
        {
            d.bladeFrames = WeaponFxBuilder.Frames("FlyingSword/fs_blade");
            d.bladeFps = 16.7f;
            d.embedFrames = WeaponFxBuilder.Frames("FlyingSword/fs_embed");
            d.embedTipPixels = 14.5f;
            d.laserFrames = WeaponFxBuilder.Frames("FlyingSword/fs_laser");
            d.laserFps = 25f;
            d.sparkFx = spark;
            d.launchFx = snap;
            d.shatterFx = shatter;
            // the streak glows the same on any floor
            d.trailMaterial = unlit;
            Icons(d, "FlyingSword/fs_icon", "FlyingSword/fs_icon_evolved");
        });

        // the weapons the cast starts with, worn on their backs
        Edit<BowData>("Bow", d => Back(d, "Bow/back_bow"));
        Edit<PeachTalismansData>("PeachTalismans", d => Back(d, "PeachTalismans/back_peach"));
        Edit<SevenStarSwordsData>("SevenStarSwords", d => Back(d, "SevenStarSwords/back_jian"));

        // the Final Rush's ring of spirit seals
        WeaponFxBuilder.EditPrefab(Root + "Prefabs/FinalRushArena.prefab", go =>
        {
            var arena = go.GetComponent<FinalRushArena>();
            if (arena == null) return;
            arena.sealFrames = WeaponFxBuilder.Frames("Arena/arena_seal");
            arena.sealFps = 9f;
            arena.riseFrames = WeaponFxBuilder.Frames("Arena/arena_rise");
            arena.riseFps = 20f;
        });
        AssetDatabase.SaveAssets();
        Debug.Log("WEAPONFX: Dragon Line, Ice Cloud, Flying Sword, the back weapons and the arena's spirit seals pointed at their art");
    }

    private static void Edit<T>(string asset, Action<T> edit) where T : WeaponData
    {
        var data = AssetDatabase.LoadAssetAtPath<T>(Weapons + asset + ".asset");
        if (data == null) { Debug.LogWarning("Weapon FX: no " + Weapons + asset + ".asset"); return; }
        edit(data);
        EditorUtility.SetDirty(data);
    }

    private static void Icons(WeaponData d, string icon, string evolved)
    {
        var frames = WeaponFxBuilder.Frames(icon);
        d.icon = frames[0];
        d.iconFrames = frames;
        d.iconFps = 8f;
        var evo = WeaponFxBuilder.Frames(evolved);
        d.evolvedIcon = evo[0];
        d.evolvedIconFrames = evo;
    }

    private static void Back(WeaponData d, string file)
    {
        d.backFrames = WeaponFxBuilder.Frames(file);
        d.backFps = 6f;
    }

    // ------------------------------------------------------------------ the first time

    private static string AutoBuiltKey => "NewWeaponFxBuilder.v1." + Application.dataPath;
    private const string Marker = Root + "NewSprites/Asesprites/VFX/Weapons/DragonLine/dl_head.aseprite";

    [InitializeOnLoadMethod]
    private static void ScheduleAutoBuild()
    {
        if (EditorPrefs.GetBool(AutoBuiltKey)) return;
        EditorApplication.delayCall -= AutoBuild;
        EditorApplication.delayCall += AutoBuild;
    }

    // the new weapons' assets start with no art: build it all once, the way the menu would
    private static void AutoBuild()
    {
        if (EditorPrefs.GetBool(AutoBuiltKey) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += AutoBuild;
            return;
        }
        if (!File.Exists(Marker)) return;
        var dragon = AssetDatabase.LoadAssetAtPath<DragonLineData>(Weapons + "DragonLine.asset");
        EditorPrefs.SetBool(AutoBuiltKey, true);
        if (dragon != null && dragon.Animated) return;
        try { WeaponFxBuilder.Build(); }
        catch (Exception e) { Debug.LogError("couldn't set the new weapon art up automatically, try Tools > VFX > Build Weapon FX\n" + e); }
    }
}
