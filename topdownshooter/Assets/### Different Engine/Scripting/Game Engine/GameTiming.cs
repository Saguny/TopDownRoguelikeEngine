using UnityEngine;

// the longest a single frame may advance the game. Unity's default (a third of a second) lets one
// slow frame queue up to sixteen physics steps, which makes the next frame slower still; capped
// at a tenth, a hitch slows the game down for a moment instead of snowballing
public static class GameTiming
{
    public const float MaxFrameTime = 0.1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply() => Time.maximumDeltaTime = MaxFrameTime;
}
