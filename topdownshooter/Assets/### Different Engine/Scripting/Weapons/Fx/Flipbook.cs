using UnityEngine;

// a sprite animation without an Animator: frames played at a fixed rate on this object's
// SpriteRenderer. for effects that come and go by the hundred (hits, projectiles), where an
// Animator each would cost too much. restarts every time the object is switched on, so pooled
// effects play from the top
[RequireComponent(typeof(SpriteRenderer))]
public class Flipbook : MonoBehaviour
{
    public Sprite[] frames = new Sprite[0];
    [Min(0.01f)] public float fps = 20f;
    public bool loop = true;
    [Tooltip("seconds before the first frame shows")]
    [Min(0f)] public float delay;
    [Tooltip("after the last frame of a one-shot: seconds it stays on that frame, fading out. 0 = gone at once")]
    [Min(0f)] public float fadeOut;
    [Tooltip("loops start on a random frame, so a crowd of them doesn't flap in step")]
    public bool randomStart;

    private SpriteRenderer sr;
    private float time, baseAlpha = 1f, shownAlpha = -1f;
    private int shownFrame = -1;

    public float Length => frames != null && frames.Length > 0 ? frames.Length / fps : 0f;
    public bool Finished => !loop && time - delay >= Length + fadeOut;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        baseAlpha = sr.color.a;
    }

    private void OnEnable() => Restart();

    public void Restart()
    {
        time = randomStart && loop ? Random.value * Length : 0f;
        shownFrame = -1;
        Apply();
    }

    // plays the one-shot so its last frame lands exactly `seconds` from now, e.g. a meteor's
    // target seal timed to the meteor's flight
    public void PlayOver(float seconds)
    {
        if (frames == null || frames.Length == 0) return;
        loop = false;
        fps = frames.Length / Mathf.Max(0.05f, seconds);
        Restart();
    }

    private void Update()
    {
        time += Time.deltaTime;
        Apply();
    }

    private void Apply()
    {
        if (frames == null || frames.Length == 0) return;

        float local = time - delay;
        if (local < 0f)
        {
            sr.enabled = false;
            return;
        }

        int i = (int)(local * fps);
        float alpha = baseAlpha;
        if (loop) i %= frames.Length;
        else if (i >= frames.Length)
        {
            float over = local - Length;
            if (over >= fadeOut)
            {
                sr.enabled = false;
                return;
            }
            i = frames.Length - 1;
            alpha = baseAlpha * (1f - over / fadeOut);
        }

        sr.enabled = true;
        if (i != shownFrame)
        {
            shownFrame = i;
            sr.sprite = frames[i];
        }
        if (!Mathf.Approximately(alpha, shownAlpha))
        {
            shownAlpha = alpha;
            var c = sr.color;
            c.a = alpha;
            sr.color = c;
        }
    }
}
