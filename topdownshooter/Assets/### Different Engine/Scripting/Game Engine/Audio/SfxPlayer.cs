using System.Collections.Generic;
using UnityEngine;

// AudioSource.PlayClipAtPoint spawns and destroys a GameObject per sound. at horde scale
// (a hit sound per arrow, a death sound per kill) that is the single biggest source of
// garbage in the game, so one-shots run through a fixed ring of reusable voices instead.
public class SfxPlayer : MonoBehaviour
{
    private const int Voices = 24;

    // identical clips started in the same frame sum in phase: five copies is +14 dB, which
    // reads as the sound suddenly getting much louder when a pulse hits a group at once.
    // must stay under SpawnDirector.purgeStepDelay (0.02) or the wave clear cascade, which
    // staggers deaths by exactly that much, would collapse into a single blip.
    private const float CoalesceWindow = 0.015f;

    // a coalesced hit still nudges the voice already playing, so five enemies read as
    // meatier than one instead of simply being dropped
    private const float StackGain = 0.15f;

    private static SfxPlayer instance;

    private AudioSource[] sources;
    private readonly Dictionary<int, (float time, int voice)> recent = new();
    private int next;

    // statics survive play sessions when domain reload is off
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    // pitch: 1 as recorded; a little either side of 1 keeps a sound heard many times from repeating
    public static void PlayAt(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f)
    {
        if (clip == null || !Application.isPlaying) return;

        Ensure();
        if (instance != null) instance.Play(clip, position, volume, pitch);
    }

    private static void Ensure()
    {
        if (instance != null) return;

        var host = new GameObject("SfxPlayer");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<SfxPlayer>();
    }

    private void Awake()
    {
        if (instance == null) instance = this;

        sources = new AudioSource[Voices];
        for (int i = 0; i < Voices; i++)
        {
            var go = new GameObject($"Voice {i}");
            go.transform.SetParent(transform, false);

            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 1f;  // match PlayClipAtPoint's 3d falloff
            sources[i] = src;
        }
    }

    // round robin, so a burst steals the oldest voice rather than stacking hundreds of
    // overlapping copies of the same clip into a clipping mess
    private void Play(AudioClip clip, Vector3 position, float volume, float pitch)
    {
        int id = clip.GetInstanceID();
        float now = Time.unscaledTime;  // unscaled: the upgrade menu parks timeScale at 0

        if (recent.TryGetValue(id, out var last) && now - last.time < CoalesceWindow)
        {
            var playing = sources[last.voice];
            if (playing.clip == clip && playing.isPlaying)
                playing.volume = Mathf.Clamp01(playing.volume + volume * StackGain);

            return;
        }

        int index = next;
        next = (next + 1) % sources.Length;
        recent[id] = (now, index);

        var src = sources[index];
        AudioRouting.Route(src);        // into the SFX group, so the SFX volume reaches it
        src.transform.position = position;
        src.clip = clip;
        src.volume = Mathf.Clamp01(volume);
        src.pitch = pitch;
        src.Play();
    }
}
