using UnityEngine;

// a menu slamming in: it starts many times its size and see-through, drops to its own size, a
// touch under, and settles. runs on real time, since the game is stopped behind it. the buttons
// wait until it has landed, so a click meant for the game can't hit one on the way in
[DisallowMultipleComponent]
public class ScaleIn : MonoBehaviour
{
    [Tooltip("how many times its size it starts at")]
    public float from = 3.2f;
    public float seconds = 0.45f;
    [Tooltip("how far it dips under its size before settling. 0 = no bounce")]
    [Range(0f, 3f)] public float overshoot = 0.8f;

    private CanvasGroup group;
    private Vector3 size = Vector3.one;
    private float t = -1f;

    public static ScaleIn Play(GameObject menu, float from = 3.2f, float seconds = 0.45f)
    {
        if (menu == null) return null;
        var s = menu.GetComponent<ScaleIn>();
        if (s == null)
        {
            s = menu.AddComponent<ScaleIn>();
            s.size = menu.transform.localScale;
        }
        s.from = from;
        s.seconds = seconds;
        s.Begin();
        return s;
    }

    private void Begin()
    {
        if (group == null && !TryGetComponent(out group)) group = gameObject.AddComponent<CanvasGroup>();
        t = 0f;
        Apply(0f);
    }

    private void Update()
    {
        if (t < 0f) return;
        t += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
        float k = Mathf.Clamp01(t / Mathf.Max(0.01f, seconds));
        Apply(k);
        if (k >= 1f) t = -1f;
    }

    private void Apply(float k)
    {
        // ease out with a little overshoot: fast from far away, a dip under its size, settled
        float c = overshoot, u = k - 1f;
        float e = 1f + (c + 1f) * u * u * u + c * u * u;
        transform.localScale = size * Mathf.LerpUnclamped(from, 1f, e);

        if (group != null)
        {
            group.alpha = Mathf.Clamp01(k / 0.35f);
            group.interactable = group.blocksRaycasts = k >= 1f;
        }
    }
}
