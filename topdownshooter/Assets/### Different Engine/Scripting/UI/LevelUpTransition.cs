using System.Collections.Generic;
using UnityEngine;

// Vampire Survivors' level up entrance: the window (the cards, their frame and the titles) flies in
// from the left turning, swings a little past straight and settles, while the loadout and the
// attributes slide in from their sides. unscaled time, since the game is paused under it. the
// cards can't be clicked until it has landed, so a click meant for the game doesn't pick one.
// the full-screen backdrop and the wen rain stay where they are
public class LevelUpTransition : MonoBehaviour
{
    [SerializeField, Min(0.05f)] private float duration = 0.34f;
    [Tooltip("how far the window starts turned, in degrees; it unwinds clockwise as it rolls in")]
    [SerializeField] private float startAngle = 45f;
    [Tooltip("its size as it comes in")]
    [SerializeField, Range(0.1f, 1f)] private float startScale = 0.75f;
    [Tooltip("how far it swings past straight before settling")]
    [SerializeField, Min(0f)] private float overshoot = 1.7f;
    [Tooltip("the side panels start this much after the window")]
    [SerializeField, Min(0f)] private float sideDelay = 0.08f;
    [SerializeField, Min(0.05f)] private float sideDuration = 0.26f;

    private RectTransform window;
    private CanvasGroup windowGroup;
    private float windowFrom;
    private readonly List<RectTransform> sides = new List<RectTransform>();
    private readonly List<Vector2> sideRest = new List<Vector2>();
    private readonly List<float> sideFrom = new List<float>();
    private bool built;
    private float clock = -1f;

    // still flying in: the menu ignores its keys meanwhile
    public bool Landing => clock >= 0f;

    public void Play()
    {
        if (!built) Build();
        if (window == null && sides.Count == 0) return;
        clock = 0f;
        if (windowGroup != null) windowGroup.blocksRaycasts = false;
        Pose(0f);
    }

    private void Update()
    {
        if (clock < 0f) return;
        // a long first frame (the menu opening) mustn't skip the entrance
        clock += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
        if (clock < Mathf.Max(duration, sideDelay + sideDuration))
        {
            Pose(clock);
            return;
        }
        Land();
    }

    private void OnDisable()
    {
        if (clock >= 0f) Land();
    }

    private void Land()
    {
        clock = -1f;
        Pose(float.MaxValue);
        if (windowGroup != null) windowGroup.blocksRaycasts = true;
    }

    private void Pose(float t)
    {
        if (window != null)
        {
            float k = Mathf.Clamp01(t / duration);
            float travel = OutBack(k, overshoot * 0.35f), turn = OutBack(k, overshoot);
            window.anchoredPosition = new Vector2(Mathf.LerpUnclamped(windowFrom, 0f, travel), 0f);
            window.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(startAngle, 0f, turn));
            window.localScale = Vector3.one * Mathf.Lerp(startScale, 1f, 1f - (1f - k) * (1f - k));
        }

        float s = Mathf.Clamp01((t - sideDelay) / sideDuration);
        float slide = 1f - (1f - s) * (1f - s) * (1f - s);
        for (int i = 0; i < sides.Count; i++)
            if (sides[i] != null)
                sides[i].anchoredPosition = sideRest[i] + new Vector2(sideFrom[i] * (1f - slide), 0f);
    }

    private static float OutBack(float k, float c1)
    {
        float c3 = c1 + 1f, m = k - 1f;
        return 1f + c3 * m * m * m + c1 * m * m;
    }

    // everything on the panel but the full-screen backdrop, the rain and the side panels goes into
    // one window that turns about the middle of what it holds. built on the first level up, when
    // the canvas has its size
    private void Build()
    {
        built = true;
        var panel = transform as RectTransform;
        if (panel == null) return;
        var area = panel.rect;

        var members = new List<RectTransform>();
        for (int i = 0; i < panel.childCount; i++)
        {
            if (!(panel.GetChild(i) is RectTransform child)) continue;
            if (child.GetComponent<WenRainLayer>() != null) continue;
            if (child.anchorMin == Vector2.zero && child.anchorMax == Vector2.one) continue;
            var box = BoundsIn(panel, child);
            if (child.GetComponent<LoadoutPanel>() != null || child.GetComponent<StatPanel>() != null)
            {
                bool left = box.center.x < area.center.x;
                sides.Add(child);
                sideRest.Add(child.anchoredPosition);
                // from just past its own edge of the screen
                sideFrom.Add(left ? -(box.xMax - area.xMin) - 24f : area.xMax - box.xMin + 24f);
                continue;
            }
            members.Add(child);
        }
        if (members.Count == 0) return;

        var bounds = BoundsIn(panel, members[0]);
        foreach (var m in members)
        {
            var b = BoundsIn(panel, m);
            bounds.min = Vector2.Min(bounds.min, b.min);
            bounds.max = Vector2.Max(bounds.max, b.max);
        }

        var go = new GameObject("Level Up Window", typeof(RectTransform), typeof(CanvasGroup));
        window = (RectTransform)go.transform;
        windowGroup = go.GetComponent<CanvasGroup>();
        window.SetParent(panel, false);
        window.SetSiblingIndex(members[0].GetSiblingIndex());
        window.anchorMin = Vector2.zero;
        window.anchorMax = Vector2.one;
        window.pivot = new Vector2((bounds.center.x - area.xMin) / Mathf.Max(1f, area.width), (bounds.center.y - area.yMin) / Mathf.Max(1f, area.height));
        window.offsetMin = window.offsetMax = Vector2.zero;
        foreach (var m in members) m.SetParent(window, false);

        // off the left edge, far enough that its turned corners are too
        windowFrom = -(bounds.center.x - area.xMin) - bounds.size.magnitude * 0.6f;
    }

    private static Rect BoundsIn(RectTransform space, RectTransform child)
    {
        var corners = new Vector3[4];
        child.GetWorldCorners(corners);
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
        foreach (var c in corners)
        {
            Vector2 p = space.InverseTransformPoint(c);
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
}
