using UnityEngine;
using UnityEngine.UI;

// loops an icon's frames on a UI Image in unscaled time: the animated weapon icons on the level
// up cards, which play while the game is paused
[RequireComponent(typeof(Image))]
public class IconAnimator : MonoBehaviour
{
    private Image image;
    private Sprite[] frames;
    private float fps = 6f;
    private float time;

    // fewer than two frames stops the loop and leaves the image as it is
    public void Play(Sprite[] sprites, float framesPerSecond)
    {
        if (image == null) image = GetComponent<Image>();
        frames = sprites != null && sprites.Length > 1 ? sprites : null;
        fps = Mathf.Max(0.1f, framesPerSecond);
        time = 0f;
        enabled = frames != null;
        if (frames != null && frames[0] != null) image.sprite = frames[0];
    }

    private void Update()
    {
        if (frames == null || image == null) return;
        time += Time.unscaledDeltaTime;
        var frame = frames[(int)(time * fps) % frames.Length];
        if (frame != null && image.sprite != frame) image.sprite = frame;
    }
}
