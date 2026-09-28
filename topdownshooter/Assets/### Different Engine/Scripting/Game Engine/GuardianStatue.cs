using System.Collections.Generic;
using UnityEngine;

// one of Huangquan Road's two guardian statues, Ox-Head and Horse-Face, standing by the road. when a
// Final Rush calls its guardian (the Playfield's rush bosses) and the player is near enough to see
// it, the statue wakes: its eyes flare, the stone cracks and the guardian steps down from it, and
// the statue stands empty and dark until the rush is won, when it's whole again. its art is
// Resources/Huangquan/statue_* (Tools/VFX/huangquan); the old sprite stays if that's missing
public class GuardianStatue : MonoBehaviour
{
    [Tooltip("the rush boss this statue holds (Bull-Head's or Horse-Face's archetype)")]
    public EnemyArchetype guardian;
    [Tooltip("which art: ox or horse")]
    public string kind = "ox";
    [Tooltip("the farthest from the player, in screen half-diagonals, that it still wakes; further off, its guardian comes from the edge as usual")]
    public float wakeReach = 1.4f;

    private static readonly List<GuardianStatue> all = new List<GuardianStatue>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => all.Clear();

    private SpriteRenderer sr;
    private Sprite[] idle, wake, empty;
    private float clock;
    private float wokeAt = -1f;
    public bool Woken { get; private set; }

    private void OnEnable()
    {
        all.Add(this);
        GameEvents.OnFinalRushEnded += Restore;
    }

    private void OnDisable()
    {
        all.Remove(this);
        GameEvents.OnFinalRushEnded -= Restore;
    }

    private void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        idle = YamaArt.Strip("Huangquan/statue_" + kind);
        wake = YamaArt.Strip("Huangquan/statue_" + kind + "_wake");
        empty = YamaArt.Strip("Huangquan/statue_" + kind + "_empty");
        if (idle != null && idle.Length > 0 && sr != null)
        {
            // the new art replaces the old flipbook
            if (TryGetComponent(out Flipbook book)) book.enabled = false;
            sr.sprite = idle[0];
        }
        clock = Random.value * 10f;
    }

    private void Update()
    {
        if (sr == null) return;
        clock += Time.deltaTime;
        if (Woken)
        {
            float t = Time.time - wokeAt;
            if (wake != null && wake.Length > 0 && t < wake.Length / 14f) sr.sprite = wake[Mathf.Min(wake.Length - 1, (int)(t * 14f))];
            else if (empty != null && empty.Length > 0) sr.sprite = empty[(int)(clock * 5f) % empty.Length];
            return;
        }
        if (idle != null && idle.Length > 0) sr.sprite = idle[(int)(clock * 5f) % idle.Length];
    }

    // wakes the statue holding this guardian, if it's near enough the player; where the guardian steps down
    public static bool Awaken(EnemyArchetype guardian, Vector2 player, out Vector2 at)
    {
        at = default;
        if (guardian == null) return false;
        var cam = Camera.main;
        float reach = cam != null ? cam.orthographicSize * Mathf.Sqrt(1f + cam.aspect * cam.aspect) : 12f;
        GuardianStatue best = null;
        float bestD = float.MaxValue;
        foreach (var s in all)
        {
            if (s == null || s.Woken || s.guardian != guardian) continue;
            float d = ((Vector2)s.transform.position - player).magnitude;
            if (d <= reach * s.wakeReach && d < bestD) { best = s; bestD = d; }
        }
        if (best == null) return false;
        best.Wake();
        // he steps down in front of his plinth
        at = (Vector2)best.transform.position + Vector2.down * 2.9f;
        return true;
    }

    private void Wake()
    {
        Woken = true;
        wokeAt = Time.time;
        var fx = YamaArt.Strip("Huangquan/statue_burst");
        if (fx != null && fx.Length > 0) FxBatch.Play(fx, 18f, (Vector2)transform.position, 1f, "Aura", 12);
        var clip = Resources.Load<AudioClip>("Sfx/hq_statue_wake");
        if (clip != null) SfxPlayer.PlayAt(clip, transform.position, 0.9f);
        Juice.Shake(0.25f);
    }

    private void Restore(int wave)
    {
        if (!Woken) return;
        Woken = false;
        var fx = YamaArt.Strip("Huangquan/statue_restore");
        if (fx != null && fx.Length > 0) FxBatch.Play(fx, 16f, (Vector2)transform.position, 1f, "Aura", 12);
        var clip = Resources.Load<AudioClip>("Sfx/hq_statue_restore");
        if (clip != null) SfxPlayer.PlayAt(clip, transform.position, 0.7f);
    }
}
