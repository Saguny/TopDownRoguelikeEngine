using TMPro;
using UnityEngine;

// one floating number. lives in Juice's ring, never instantiated per hit
public class DamageNumber : MonoBehaviour
{
    private TextMeshPro label;
    private float life;
    private float duration;
    private Vector3 drift;
    private Color tint;
    private float peakScale;

    private void Awake()
    {
        label = gameObject.AddComponent<TextMeshPro>();
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.fontSize = 4f;

        // world space text renders behind sprites without this
        var mr = GetComponent<MeshRenderer>();
        if (mr != null) mr.sortingOrder = 1000;

        gameObject.SetActive(false);
    }

    public void Show(Vector3 position, string text, Color color, float scale, float seconds)
    {
        transform.position = position;
        transform.localScale = Vector3.one * scale;

        tint = color;
        peakScale = scale;
        duration = Mathf.Max(0.05f, seconds);
        life = 0f;
        drift = new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(2.2f, 3.0f), 0f);

        label.text = text;
        label.color = color;

        gameObject.SetActive(true);
    }

    public bool Busy => gameObject.activeSelf;

    private void Update()
    {
        // unscaled so numbers keep rising through hit stop instead of freezing mid air
        life += Time.unscaledDeltaTime;
        if (life >= duration)
        {
            gameObject.SetActive(false);
            return;
        }

        float k = life / duration;

        transform.position += drift * Time.unscaledDeltaTime * (1f - k);
        transform.localScale = Vector3.one * peakScale * (1f + 0.25f * (1f - k));

        var c = tint;
        c.a = 1f - (k * k);  // hold opacity early, drop off fast at the end
        label.color = c;
    }
}
