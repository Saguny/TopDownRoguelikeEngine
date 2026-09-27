using System;
using System.Collections;
using UnityEngine;

// the root of a hand-made cast animation, e.g. the Command Token's. build the whole effect under
// it (sprites, particles, anything), animate it with an Animator on this same object, and add
// Animation Events to the clip to say when things happen:
//   Hit    deals the weapon's damage. only the first call counts
//   Shake  shakes the camera; put the strength in the event's Float field (0.08 small, 0.3 big)
//   Sound  plays the AudioClip in the event's Object field
//   End    removes the prefab
// a cast that ends without calling Hit still deals its damage then, so a forgotten event can't
// leave the weapon doing nothing
public class CastAnimation : MonoBehaviour
{
    [Tooltip("seconds before the prefab removes itself if the animation never calls End")]
    [SerializeField, Min(0.1f)] private float lifetime = 3f;

    private Action onHit;
    private bool hit;

    public void Play(Action hitAction)
    {
        onHit = hitAction;
        StartCoroutine(Expire());
    }

    private IEnumerator Expire()
    {
        yield return new WaitForSeconds(lifetime);
        End();
    }

    public void Hit()
    {
        if (hit) return;
        hit = true;
        onHit?.Invoke();
    }

    public void Shake(float strength) => Juice.Shake(strength);

    public void Sound(AudioClip clip)
    {
        if (clip != null) SfxPlayer.PlayAt(clip, transform.position);
    }

    public void End()
    {
        Hit();
        Destroy(gameObject);
    }
}
