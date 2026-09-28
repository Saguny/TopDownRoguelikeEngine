using UnityEngine;
using UnityEngine.Audio;

// every sound effect through the mixer's SFX group, so the SFX volume slider reaches it. the one-shot
// voices (SfxPlayer), the voices weapons and bosses make for themselves and the audio sources on
// prefabs all used to play straight to the listener, past the slider. Route sets a source's group
// where it has none; a sweep every couple of seconds catches any source nobody routed (pooled
// enemies, prefabs), leaving the music (BGMManager's) alone. it makes itself
public class AudioRouting : MonoBehaviour
{
    private static AudioMixerGroup sfx;
    private static AudioRouting instance;
    private float nextSweep;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { sfx = null; instance = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (instance != null) return;
        var go = new GameObject("Audio Routing");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<AudioRouting>();
    }

    public static AudioMixerGroup Sfx
    {
        get
        {
            if (sfx == null && GameSettings.Mixer != null)
            {
                var groups = GameSettings.Mixer.FindMatchingGroups("SFX");
                if (groups != null && groups.Length > 0) sfx = groups[0];
            }
            return sfx;
        }
    }

    // a sound effect's source into the SFX group, if it isn't routed anywhere yet
    public static AudioSource Route(AudioSource source)
    {
        if (source != null && source.outputAudioMixerGroup == null)
        {
            var group = Sfx;
            if (group != null) source.outputAudioMixerGroup = group;
        }
        return source;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextSweep) return;
        nextSweep = Time.unscaledTime + 2f;
        if (Sfx == null) return;
        foreach (var s in FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (s == null || s.outputAudioMixerGroup != null) continue;
            if (s.GetComponentInParent<BGMManager>(true) != null) continue;    // the music has its own group
            s.outputAudioMixerGroup = sfx;
        }
    }
}
