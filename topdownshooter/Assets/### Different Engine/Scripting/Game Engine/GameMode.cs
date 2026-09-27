using UnityEngine;

// which kind of run is being played. endless is an opt in mode on top of the normal game: it turns
// on unbounded enemy scaling, overcharge upgrades, bombardment and the best time record. everything
// else is shared, so both modes run in the same scene
public enum RunMode
{
    Normal,
    Endless
}

public static class GameMode
{
    public static RunMode Current { get; set; } = RunMode.Normal;
    public static bool IsEndless => Current == RunMode.Endless;

#if UNITY_EDITOR
    private const string EditorKey = "GameMode.EditorDefault";
    private const string NormalPath = "Tools/Run Mode/Normal";
    private const string EndlessPath = "Tools/Run Mode/Endless";

    // pressing play straight from a gameplay scene skips the menu, so the editor remembers which
    // mode to start in. the menu's buttons still override this when a run starts from the menu
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ApplyEditorDefault()
    {
        Current = EditorDefault;
    }

    private static RunMode EditorDefault
    {
        get => (RunMode)UnityEditor.EditorPrefs.GetInt(EditorKey, (int)RunMode.Normal);
        set => UnityEditor.EditorPrefs.SetInt(EditorKey, (int)value);
    }

    [UnityEditor.MenuItem(NormalPath)]
    private static void UseNormal() => EditorDefault = RunMode.Normal;

    [UnityEditor.MenuItem(EndlessPath)]
    private static void UseEndless() => EditorDefault = RunMode.Endless;

    [UnityEditor.MenuItem(NormalPath, true)]
    private static bool CheckNormal()
    {
        UnityEditor.Menu.SetChecked(NormalPath, EditorDefault == RunMode.Normal);
        return true;
    }

    [UnityEditor.MenuItem(EndlessPath, true)]
    private static bool CheckEndless()
    {
        UnityEditor.Menu.SetChecked(EndlessPath, EditorDefault == RunMode.Endless);
        return true;
    }
#endif
}
