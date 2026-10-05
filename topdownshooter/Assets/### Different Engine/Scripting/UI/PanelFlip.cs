using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// two panels sharing a place, a button turning between them like a card: the pause menu's Current
// Upgrades and Favored Items (its NextPanel arrow). the face showing folds edge-on in a few
// chunky steps, the other unfolds past flat and settles, the way the game's menus slam in, and the
// arrow turns to point back. on real time (the game is paused). the pause menu always opens on
// the first face. Wire puts one on the button, finding the three by name
public class PanelFlip : MonoBehaviour
{
    public RectTransform front, back;
    [Min(0.05f)] public float seconds = 0.22f;
    [Tooltip("art frames a second the fold steps at, for the game's pixel feel. 0 = smooth")]
    [Min(0f)] public float stepsPerSecond = 30f;

    private bool showingBack;
    private Coroutine flipping;
    private Vector3 arrowScale = Vector3.one;

    // the pause menu's: NextPanel turning between CurrentUpgrades and FavoredItem
    public static PanelFlip Wire(GameObject panel, string button = "NextPanel", string first = "CurrentUpgrades", string second = "FavoredItem")
    {
        if (panel == null) return null;
        Transform b = null, f = null, s = null;
        foreach (var t in panel.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == button && b == null) b = t;
            else if (t.name == first && f == null) f = t;
            else if (t.name == second && s == null) s = t;
        }
        if (b == null || f == null || s == null || !b.TryGetComponent(out Button click)) return null;
        if (!b.TryGetComponent(out PanelFlip flip)) flip = b.gameObject.AddComponent<PanelFlip>();
        flip.front = (RectTransform)f;
        flip.back = (RectTransform)s;
        flip.arrowScale = b.localScale;
        click.onClick.AddListener(flip.Flip);
        flip.Show(false);
        return flip;
    }

    private void OnEnable() => Show(false);

    private void OnDisable()
    {
        if (flipping != null) StopCoroutine(flipping);
        flipping = null;
    }

    // straight to a face, no turning
    private void Show(bool toBack)
    {
        showingBack = toBack;
        if (front != null) { front.gameObject.SetActive(!toBack); front.localScale = Vector3.one; }
        if (back != null) { back.gameObject.SetActive(toBack); back.localScale = Vector3.one; }
        transform.localScale = new Vector3(arrowScale.x * (toBack ? -1f : 1f), arrowScale.y, arrowScale.z);
    }

    public void Flip()
    {
        if (front == null || back == null || !isActiveAndEnabled) return;
        if (flipping != null) { StopCoroutine(flipping); Show(!showingBack); flipping = null; return; }
        flipping = StartCoroutine(Turn(showingBack ? back : front, showingBack ? front : back, !showingBack));
    }

    private IEnumerator Turn(RectTransform from, RectTransform to, bool toBack)
    {
        showingBack = toBack;
        float half = seconds * 0.4f, rest = seconds - half;

        // folding edge-on
        for (float t = 0f; t < half; t += Time.unscaledDeltaTime)
        {
            float k = Stepped(t, half);
            from.localScale = new Vector3(1f - k * k, 1f + 0.06f * k, 1f);
            Arrow(k * 0.5f, toBack);
            yield return null;
        }
        from.localScale = Vector3.one;
        from.gameObject.SetActive(false);
        to.gameObject.SetActive(true);

        // and the other face unfolding, past flat and back, like a slam landing
        for (float t = 0f; t < rest; t += Time.unscaledDeltaTime)
        {
            float k = Stepped(t, rest);
            float x = 1f - (1f - k) * (1f - k) * (1f - 2.6f * k);   // overshoots a touch past 1
            to.localScale = new Vector3(Mathf.Max(0.02f, x), 1f + 0.06f * (1f - k), 1f);
            Arrow(0.5f + k * 0.5f, toBack);
            yield return null;
        }
        to.localScale = Vector3.one;
        Arrow(1f, toBack);
        flipping = null;
    }

    // the arrow turns through edge-on to point the other way
    private void Arrow(float k, bool toBack)
    {
        float from = toBack ? 1f : -1f;
        float x = Mathf.Lerp(from, -from, k);
        transform.localScale = new Vector3(arrowScale.x * x, arrowScale.y, arrowScale.z);
    }

    private float Stepped(float t, float length)
    {
        float k = Mathf.Clamp01(t / length);
        if (stepsPerSecond <= 0f) return k;
        float steps = Mathf.Max(1f, Mathf.Round(length * stepsPerSecond));
        return Mathf.Ceil(k * steps) / steps;
    }
}
