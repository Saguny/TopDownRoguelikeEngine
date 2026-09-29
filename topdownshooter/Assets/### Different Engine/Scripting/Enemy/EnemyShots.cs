using System.Collections.Generic;
using UnityEngine;

// everything the enemies throw at the player, whatever it's made of, so it can be wiped in one
// go: the Command Token's shockwave clears every shot on the screen (a bomb, the way Touhou's
// clears the bullets), and the Electrical Aura's field those inside it every other pulse. each kind of enemy shot keeps its own and signs up here as it comes into
// use (Register is safe to call again and again): the danmaku (Danmaku: the bosses' bullets and
// Huangquan Road's), the Magistrate's corpse fire (EnemyBullet), the burners' embers and their
// fire (EmberLobs). a new kind of enemy projectile only has to implement IEnemyShots and
// Register, and every clear reaches it
public interface IEnemyShots
{
    // everything within `radius` of `centre` gone, harmlessly, with a little flash where it was;
    // `drops` lets it leave wen behind the way cancelled danmaku can. how many went
    int ClearWithin(Vector2 centre, float radius, bool drops);
}

public static class EnemyShots
{
    private static readonly List<IEnemyShots> sources = new List<IEnemyShots>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => sources.Clear();

    public static void Register(IEnemyShots source)
    {
        if (source != null && !sources.Contains(source)) sources.Add(source);
    }

    public static void Unregister(IEnemyShots source) => sources.Remove(source);

    // every enemy shot within `radius` of `centre`. how many went
    public static int Clear(Vector2 centre, float radius, bool drops = false)
    {
        int n = 0;
        for (int i = sources.Count - 1; i >= 0; i--)
        {
            // a source that's been destroyed without signing off
            if (sources[i] == null || (sources[i] is Object o && o == null)) { sources.RemoveAt(i); continue; }
            n += sources[i].ClearWithin(centre, radius, drops);
        }
        return n;
    }

    // every enemy shot there is
    public static int ClearAll(bool drops = false) => Clear(Vector2.zero, float.MaxValue, drops);

    // every enemy shot on the screen: a circle from its middle out past its corners
    public static int ClearOnScreen(Camera cam, bool drops = false)
    {
        if (cam == null || !cam.orthographic) return ClearAll(drops);
        float h = cam.orthographicSize, w = h * cam.aspect;
        return Clear(cam.transform.position, Mathf.Sqrt(h * h + w * w) * 1.1f, drops);
    }

    // the little flash a cleared shot leaves (the danmaku's own cancel sparkle), no more than a few
    // hundred a frame
    public static void Sparkle(Vector2 at)
    {
        if (Time.frameCount != sparkleFrame) { sparkleFrame = Time.frameCount; sparkles = 0; }
        if (sparkles++ >= 400) return;
        var frames = YamaArt.Frames("cancel");
        if (frames != null) FxBatch.Play(frames, 22f, at, 1f, "Aura", 201);
    }
    private static int sparkleFrame = -1, sparkles;

    // is `p` inside the circle; a radius of float.MaxValue is everywhere
    public static bool Within(Vector2 p, Vector2 centre, float radius) =>
        radius >= float.MaxValue || (p - centre).sqrMagnitude <= radius * radius;
}
