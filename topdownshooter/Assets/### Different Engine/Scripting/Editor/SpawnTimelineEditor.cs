using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// a picture of the whole stage above the lists: the crowd each beat keeps up (solid) and lets it
// grow to (faint), a line per wave, and a tick per event coloured by kind. hover a tick for what
// it is
[CustomEditor(typeof(SpawnTimeline))]
public class SpawnTimelineEditor : Editor
{
    private static readonly Color Back = new Color(0.13f, 0.13f, 0.14f);
    private static readonly Color Grid = new Color(1f, 1f, 1f, 0.07f);
    private static readonly Color Minimum = new Color(0.42f, 1f, 0f);        // the shop's maxed green
    private static readonly Color Cap = new Color(0.42f, 1f, 0f, 0.22f);

    private static Color KindColor(SpawnTimeline.EventKind kind)
    {
        switch (kind)
        {
            case SpawnTimeline.EventKind.Swarm: return new Color(0.55f, 0.9f, 1f);
            case SpawnTimeline.EventKind.Stampede: return new Color(1f, 0.62f, 0.3f);
            case SpawnTimeline.EventKind.Ring: return new Color(0.8f, 0.6f, 1f);
            case SpawnTimeline.EventKind.Elite: return new Color(1f, 0.84f, 0.3f);
            default: return new Color(1f, 0.38f, 0.35f);
        }
    }

    public override void OnInspectorGUI()
    {
        var t = (SpawnTimeline)target;
        DrawGraph(t);
        DrawLegend();
        EditorGUILayout.Space(6);
        DrawDefaultInspector();
    }

    private static void DrawGraph(SpawnTimeline t)
    {
        var beats = t.beats.Where(b => b != null).OrderBy(b => b.fromMinute).ToList();
        float end = Mathf.Max(30f,
            beats.Count > 0 ? beats[beats.Count - 1].fromMinute + t.waveMinutes : 0f,
            t.events.Count > 0 ? t.events.Where(e => e != null).Select(e => e.atMinute + 1f).DefaultIfEmpty(0f).Max() : 0f);
        float top = Mathf.Max(10f, beats.Count > 0 ? beats.Max(b => Mathf.Min(t.hardCap, b.cap * t.crowdScale)) : 10f) * 1.1f;

        Rect area = GUILayoutUtility.GetRect(10f, 150f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(area, Back);
        Rect plot = new Rect(area.x + 30f, area.y + 16f, area.width - 38f, area.height - 32f);

        float X(float minute) => plot.x + plot.width * Mathf.Clamp01(minute / end);
        float Y(float count) => plot.yMax - plot.height * Mathf.Clamp01(count / top);

        var small = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(1f, 1f, 1f, 0.45f) } };

        // waves and minute labels
        for (float m = 0f; m <= end + 0.001f; m += t.waveMinutes)
        {
            EditorGUI.DrawRect(new Rect(X(m), plot.y, 1f, plot.height), Grid);
            GUI.Label(new Rect(X(m) - 10f, plot.yMax + 1f, 30f, 14f), m.ToString("0"), small);
        }
        for (int i = 0; i <= 2; i++)
        {
            float c = top / 1.1f * i / 2f;
            EditorGUI.DrawRect(new Rect(plot.x, Y(c), plot.width, 1f), Grid);
            GUI.Label(new Rect(area.x + 2f, Y(c) - 7f, 28f, 14f), c.ToString("0"), small);
        }

        // each beat as a step: cap faint behind, minimum solid in front
        for (int i = 0; i < beats.Count; i++)
        {
            float from = X(beats[i].fromMinute);
            float to = X(i + 1 < beats.Count ? beats[i + 1].fromMinute : end);
            float cap = Mathf.Min(t.hardCap, beats[i].cap * t.crowdScale);
            float min = Mathf.Min(cap, beats[i].minimum * t.crowdScale);
            EditorGUI.DrawRect(Rect.MinMaxRect(from, Y(cap), to, plot.yMax), Cap);
            EditorGUI.DrawRect(Rect.MinMaxRect(from, Y(min), to - 1f, plot.yMax), new Color(Minimum.r, Minimum.g, Minimum.b, 0.55f));
            EditorGUI.DrawRect(Rect.MinMaxRect(from, Y(min) - 1f, to - 1f, Y(min) + 1f), Minimum);
        }

        // events along the top, repeats included as far as the graph goes
        var ticks = new List<(Rect rect, string tip)>();
        foreach (var e in t.events)
        {
            if (e == null) continue;
            for (float m = e.atMinute; m <= end; m += e.repeatEveryMinutes)
            {
                var r = new Rect(X(m) - 1.5f, area.y + 3f, 3f, plot.height + 13f);
                var c = KindColor(e.kind);
                EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 10f), c);
                EditorGUI.DrawRect(new Rect(r.x + 1f, r.y + 10f, 1f, plot.height + 3f), new Color(c.r, c.g, c.b, 0.35f));
                string who = e.archetype != null ? e.archetype.name : "beat's mix";
                ticks.Add((new Rect(r.x - 3f, r.y, 9f, 14f), $"{(int)m}:{(int)(m % 1f * 60f):00}  {e.kind}  {who} x{e.count}{(string.IsNullOrEmpty(e.label) ? "" : "  \"" + e.label + "\"")}"));
                if (e.repeatEveryMinutes <= 0f) break;
            }
        }
        foreach (var (rect, tip) in ticks)
            GUI.Label(rect, new GUIContent(string.Empty, tip));
    }

    private static void DrawLegend()
    {
        var style = new GUIStyle(EditorStyles.miniLabel);
        Rect row = GUILayoutUtility.GetRect(10f, 16f, GUILayout.ExpandWidth(true));
        float x = row.x;
        void Key(Color c, string text)
        {
            EditorGUI.DrawRect(new Rect(x, row.y + 4f, 8f, 8f), c);
            float w = style.CalcSize(new GUIContent(text)).x;
            GUI.Label(new Rect(x + 11f, row.y, w + 4f, 16f), text, style);
            x += w + 22f;
        }
        Key(Minimum, "minimum");
        Key(new Color(0.42f, 1f, 0f, 0.4f), "cap");
        foreach (SpawnTimeline.EventKind k in System.Enum.GetValues(typeof(SpawnTimeline.EventKind)))
            Key(KindColor(k), k.ToString().ToLowerInvariant());
    }
}
