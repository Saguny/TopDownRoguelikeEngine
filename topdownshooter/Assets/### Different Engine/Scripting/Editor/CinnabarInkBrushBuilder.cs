using UnityEditor;
using UnityEngine;

// sets the Cinnabar Ink Brush up from its Aseprite files (NewSprites/Asesprites/VFX/Weapons/
// CinnabarInkBrush, drawn by Tools/VFX/brush/brush.js): imports them as pixel art at the world's
// pixel size, makes the weapon's asset if there isn't one yet (it evolves while any other weapon
// is held) and points it at its art and icons. building again only re-points the art; the
// asset's numbers are left as they are
public static class CinnabarInkBrushBuilder
{
    private const string Root = "Assets/### Different Engine/";
    private const string Art = Root + "NewSprites/Asesprites/VFX/Weapons/CinnabarInkBrush/";
    private const string AssetPath = Root + "Data/Weapons/CinnabarInkBrush.asset";

    [MenuItem("Tools/VFX/Build Cinnabar Ink Brush")]
    public static void BuildMenu() => Build();

    public static CinnabarInkBrushData Build()
    {
        try
        {
            AssetDatabase.StartAssetEditing();
            WeaponFxBuilder.Configure();
        }
        finally { AssetDatabase.StopAssetEditing(); }
        AssetDatabase.Refresh();

        var data = AssetDatabase.LoadAssetAtPath<CinnabarInkBrushData>(AssetPath);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<CinnabarInkBrushData>();
            data.title = "Cinnabar Ink Brush";
            data.evolvedTitle = "Calligraphic Seal Grid";
            AssetDatabase.CreateAsset(data, AssetPath);
        }

        data.dabFrames = Frames("ink_dab");
        data.brushFrames = Frames("ink_brush");
        data.flameFrames = Frames("ink_flame");
        data.sealFrames = Frames("ink_seal");
        data.blastFrames = Frames("ink_blast");
        var icon = Frames("ink_icon");
        data.icon = icon[0];
        data.iconFrames = icon;
        data.iconFps = 8f;
        var evolved = Frames("ink_icon_evolved");
        data.evolvedIcon = evolved[0];
        data.evolvedIconFrames = evolved;
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();

        Debug.Log($"BRUSH built {AssetPath}: {data.dabFrames.Length} blots, {data.brushFrames.Length} brush, {data.flameFrames.Length} flame, " +
                  $"{data.sealFrames.Length} seal, {data.blastFrames.Length} blast frames; evolves with {(data.evolutionPartner != null ? data.evolutionPartner.GetBaseTitle() : "any weapon")}");
        return data;
    }

    private static Sprite[] Frames(string name) => WeaponFxBuilder.FramesAt(Art + name + ".aseprite");
}
