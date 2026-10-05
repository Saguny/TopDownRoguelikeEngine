using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// two panels sharing a place, a button turning between them like a card: the pause menu's Current
// Upgrades and Favored Items (its NextPanel arrow). the face showing folds edge-on, the other
// unfolds past flat and settles, the way the game's menus slam in, and the
// arrow turns to point back. smooth, on real time (the game is paused). the pause menu always opens on
// the first face. Wire puts one on the button, finding the three by name
public class PanelFlip : MonoBehaviour
{
    public RectTransform front, back;
    [Min(0.05f)] public float seconds = 0.3f;

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

        // folding edge-on, gathering speed
        for (float t = 0f; t < half; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.Clamp01(t / half);
            float fold = Mathf.Sin(k * Mathf.PI * 0.5f);
            from.localScale = new Vector3(Mathf.Cos(k * Mathf.PI * 0.5f), 1f + 0.05f * fold, 1f);
            Arrow(fold * 0.5f, toBack);
            yield return null;
        }
        from.localScale = Vector3.one;
        from.gameObject.SetActive(false);
        to.gameObject.SetActive(true);

        // and the other face unfolding, a touch past flat and back, like a slam landing
        for (float t = 0f; t < rest; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.Clamp01(t / rest);
            float x = BackOut(k);
            to.localScale = new Vector3(Mathf.Max(0.02f, x), 1f + 0.05f * (1f - k) * (1f - k), 1f);
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

    // eases out, a little past 1 before it settles
    private static float BackOut(float k)
    {
        const float s = 1.4f;
        float x = k - 1f;
        return 1f + x * x * ((s + 1f) * x + s);
    }
}
