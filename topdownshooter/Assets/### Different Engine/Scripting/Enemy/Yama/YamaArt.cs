using System.Collections.Generic;
using UnityEngine;

// Yama's art and sounds, loaded from Resources on first use: Resources/Yama (made by
// Tools/VFX/yama) and Resources/Sfx (made by Tools/SFX/yama.py). strips of square frames are cut
// into sprites at the world's pixel size, so nothing needs setting up in Unity
public static class YamaArt
{
    public const float WorldPpu = 37f / 1.3f;

    private static readonly Dictionary<string, Sprite[]> strips = new Dictionary<string, Sprite[]>();
    private static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    private static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        strips.Clear();
        clips.Clear();
        textures.Clear();
    }

    // "yama", "halo", "gate"...: the strip's frames, or none if it's missing
    public static Sprite[] Frames(string name, float ppu = WorldPpu) => Strip("Yama/" + name, ppu);

    // any strip of square frames under Resources, by its path there ("UI/boss_ring", "Hazards/...")
    public static Sprite[] Strip(string resourcePath, float ppu = WorldPpu)
    {
        string key = resourcePath + "@" + ppu;
        if (strips.TryGetValue(key, out var frames)) return frames;
        var strip = Texture(resourcePath);
        if (strip == null) { strips[key] = null; return null; }
        int size = strip.height, n = Mathf.Max(1, strip.width / size);
        frames = new Sprite[n];
        for (int i = 0; i < n; i++)
            frames[i] = Sprite.Create(strip, new Rect(i * size, 0, size, size), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
        strips[key] = frames;
        return frames;
    }

    // a whole image as one sprite (the boss bar's frame)
    public static Sprite Whole(string name, float ppu = WorldPpu)
    {
        string key = name + "#" + ppu;
        if (strips.TryGetValue(key, out var one)) return one != null && one.Length > 0 ? one[0] : null;
        var tex = Texture("Yama/" + name);
        var s = tex == null ? null : Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
        strips[key] = s == null ? null : new[] { s };
        return s;
    }

    public static Texture2D Texture(string path)
    {
        if (textures.TryGetValue(path, out var t)) return t;
        t = Resources.Load<Texture2D>(path);
        if (t != null)
        {
            t.filterMode = FilterMode.Point;
            t.wrapMode = TextureWrapMode.Clamp;
        }
        textures[path] = t;
        return t;
    }

    // "yama_roar" and the like, from Resources/Sfx
    public static AudioClip Clip(string name)
    {
        if (clips.TryGetValue(name, out var c)) return c;
        c = Resources.Load<AudioClip>("Sfx/" + name);
        clips[name] = c;
        return c;
    }

    public static void Play(string name, Vector3 at, float volume = 1f, float pitch = 1f)
    {
        var c = Clip(name);
        if (c != null) SfxPlayer.PlayAt(c, at, volume, pitch);
    }

    // frame i of a strip, looping, or null
    public static Sprite Frame(Sprite[] frames, float time, float fps)
    {
        if (frames == null || frames.Length == 0) return null;
        return frames[Mathf.Abs((int)(time * fps)) % frames.Length];
    }
}
