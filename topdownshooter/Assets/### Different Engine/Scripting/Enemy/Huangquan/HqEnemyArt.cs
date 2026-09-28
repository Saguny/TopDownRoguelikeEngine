using UnityEngine;

// a Huangquan Road enemy's look, drawn from Resources/Huangquan (made by Tools/VFX/huangquan):
// {look}.png is its idle or walk loop, and any other strip {look}_{state}.png a state its behaviour
// plays for a while (a lily's bloom before it fires, a bull's charge, a lantern's flare).
// {look}_rise.png, if there is one, plays once as it appears, and {look}_death.png is its own death
// in place of the usual one. strips missing leave the prefab's own sprite
[DisallowMultipleComponent]
public class HqEnemyArt : MonoBehaviour
{
    [Tooltip("the art's name under Resources/Huangquan, e.g. wandering_soul")]
    public string look = "wandering_soul";
    public float fps = 8f;
    [Tooltip("frames a second for its states, its rise and its death")]
    public float stateFps = 14f;
    public float deathScale = 1f;

    private SpriteRenderer sr;
    private EnemyHealth health;
    private Sprite[] idle, rise, playing;
    private float clock, playStart, playUntil = -1f, playFps;
    private bool playLoops;
    private Sprite shown;

    public SpriteRenderer Renderer => sr;
    public bool HasArt => idle != null && idle.Length > 0;
    // playing a state right now
    public bool Busy => Time.time < playUntil;

    private void Awake()
    {
        sr = GetComponentInChildren<SpriteRenderer>();
        health = GetComponent<EnemyHealth>();
        idle = YamaArt.Strip("Huangquan/" + look);
        rise = YamaArt.Strip("Huangquan/" + look + "_rise");
        if (HasArt && TryGetComponent(out Animator animator)) animator.enabled = false;
        if (HasArt && sr != null) sr.sprite = idle[0];
    }

    private void OnEnable()
    {
        clock = Random.value * 10f;
        playUntil = -1f;
        playing = null;
        shown = null;
        if (health != null)
        {
            health.deathFrames = YamaArt.Strip("Huangquan/" + look + "_death");
            health.deathFps = stateFps * 1.3f;
            health.deathScale = deathScale;
        }
        if (rise != null && rise.Length > 0) Play(rise, rise.Length / stateFps, false);
    }

    // one of its states' strips ("act", "charge"...), or null if it hasn't one
    public Sprite[] Strip(string state) => YamaArt.Strip("Huangquan/" + look + "_" + state);

    // shows a state for `seconds`: looping, or once through and holding its last frame
    public void Play(string state, float seconds, bool loop = false, float framesPerSecond = 0f) =>
        Play(Strip(state), seconds, loop, framesPerSecond);

    public void Play(Sprite[] strip, float seconds, bool loop = false, float framesPerSecond = 0f)
    {
        if (strip == null || strip.Length == 0) return;
        playing = strip;
        playStart = Time.time;
        playUntil = Time.time + seconds;
        playLoops = loop;
        // once through: its frames spread over the time it's given, unless a speed is asked for
        playFps = framesPerSecond > 0f ? framesPerSecond : loop ? stateFps : strip.Length / Mathf.Max(0.05f, seconds);
    }

    public void Stop() => playUntil = -1f;

    private void LateUpdate()
    {
        if (sr == null || !HasArt) return;
        clock += Time.deltaTime;
        Sprite want;
        if (Time.time < playUntil && playing != null)
        {
            int f = (int)((Time.time - playStart) * playFps);
            want = playing[playLoops ? f % playing.Length : Mathf.Min(f, playing.Length - 1)];
        }
        else want = idle[(int)(clock * fps) % idle.Length];
        if (want != shown)
        {
            shown = want;
            sr.sprite = want;
        }
    }
}
