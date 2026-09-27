using UnityEngine;

// a small looping animation on a world sprite (lantern flicker, incense smoke, a talisman lifting
// in the wind) without an Animator. each one starts somewhere random so neighbours don't blink
// in step
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteFlipbook : MonoBehaviour
{
    [SerializeField] private Sprite[] frames = System.Array.Empty<Sprite>();
    [SerializeField, Min(0.1f)] private float framesPerSecond = 8f;
    [SerializeField] private bool randomStart = true;

    private SpriteRenderer sr;
    private float time;

    public void Setup(Sprite[] sprites, float fps)
    {
        frames = sprites;
        framesPerSecond = Mathf.Max(0.1f, fps);
    }

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (randomStart && frames.Length > 0) time = Random.value * frames.Length / framesPerSecond;
    }

    private void Update()
    {
        if (frames.Length < 2) return;
        time += Time.deltaTime;
        var frame = frames[(int)(time * framesPerSecond) % frames.Length];
        if (sr.sprite != frame) sr.sprite = frame;
    }
}
