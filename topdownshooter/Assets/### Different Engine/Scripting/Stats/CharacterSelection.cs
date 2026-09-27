using UnityEngine;

// the character picked on the select screen, read when the run starts. a static, because the
// game scene loads after the menu is gone, the same way GameMode carries the run mode across
public static class CharacterSelection
{
    public static CharacterData Current { get; set; }

    // statics survive play sessions when domain reload is off. pressing play in the game scene
    // should start with the default character, not whatever was picked last session
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Current = null;
}
