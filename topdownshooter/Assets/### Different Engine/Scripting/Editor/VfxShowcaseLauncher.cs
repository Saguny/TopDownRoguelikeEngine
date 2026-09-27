using UnityEditor;

// Tools > VFX > Weapon Showcase: plays with every weapon maxed and photographs it (see
// VfxShowcase). it starts from whatever scene is open and loads the game scene in play mode, so
// the scenes open in the editor, saved or not, are left as they are
internal static class VfxShowcaseLauncher
{
    [MenuItem("Tools/VFX/Weapon Showcase")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        SessionState.SetBool(VfxShowcase.PendingKey, true);
        EditorApplication.EnterPlaymode();
    }
}
