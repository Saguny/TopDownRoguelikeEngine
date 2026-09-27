using UnityEngine;
public class IntroLowpassSweep : MonoBehaviour
{
    public AudioLowPassFilter lpf;
    public float duration = 2.5f;
    float t;
    void OnEnable() => Restart();

    // muffled again, opening up over Duration: the music manager does it as the music fades in
    public void Restart() { if (lpf) { lpf.enabled = true; t = 0f; } }
    void Update()
    {
        if (!lpf) return;
        t += Time.unscaledDeltaTime / duration;
        lpf.cutoffFrequency = Mathf.Lerp(500f, 22000f, Mathf.SmoothStep(0f, 1f, t));
        if (t >= 1f) lpf.enabled = false;
    }
}
