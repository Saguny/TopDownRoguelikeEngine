using System.Collections.Generic;
using UnityEngine;

// the hell money burners' embers: a burning wad of spirit money lobbed high over the horde, its
// shadow and a ring on the ground showing where it'll come down, then a pool of paper fire there
// for a couple of seconds that hurts to stand in (a share of the player's max health a tick, like
// the bosses' bullets, so it never scales into a one-shot). one manager for all of them, made on
// first use; its sprites are pooled
public class EmberLobs : MonoBehaviour
{
    private const float ArcHeight = 2.2f;
    private const float TickSeconds = 0.5f;
    private const float FadeSeconds = 0.35f;

    private sealed class Ember
    {
        public Vector2 from, to;
        public float start, seconds, poolSeconds, radius, share;
        public SpriteRenderer body, shadow, mark;
    }

    private sealed class Pool
    {
        public Vector2 at;
        public float start, seconds, radius, share;
        public SpriteRenderer sr;
    }

    private static EmberLobs instance;
    private readonly List<Ember> embers = new List<Ember>();
    private readonly List<Pool> pools = new List<Pool>();
    private readonly Stack<SpriteRenderer> spare = new Stack<SpriteRenderer>();
    private Sprite[] emberArt, markArt, poolArt, splashArt;
    private Sprite shadowArt;
    private float nextTick;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    private static EmberLobs Get()
    {
        if (instance == null && Application.isPlaying) instance = new GameObject("Ember Lobs").AddComponent<EmberLobs>();
        return instance;
    }

    private void Awake()
    {
        emberArt = YamaArt.Strip("Huangquan/ember");
        markArt = YamaArt.Strip("Huangquan/ember_mark");
        poolArt = YamaArt.Strip("Huangquan/fire_pool");
        splashArt = YamaArt.Strip("Huangquan/ember_splash");
        var s = YamaArt.Strip("Huangquan/ember_shadow");
        shadowArt = s != null && s.Length > 0 ? s[0] : null;
        GameEvents.OnFinalRushEnded += ClearAll;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        GameEvents.OnFinalRushEnded -= ClearAll;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this) instance = null;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) => ClearAll(0);

    // one ember from `from` to land at `to` after `seconds`, leaving fire for `poolSeconds`
    public static void Lob(Vector2 from, Vector2 to, float seconds, float poolSeconds, float radius, float share)
    {
        var d = Get();
        if (d == null) return;
        var e = new Ember
        {
            from = from, to = to, start = Time.time, seconds = Mathf.Max(0.2f, seconds),
            poolSeconds = poolSeconds, radius = radius, share = share,
            body = d.Take("Aura", 30),
            shadow = d.Take("Player", -4),
            mark = d.Take("Player", -5),
        };
        e.shadow.sprite = d.shadowArt;
        e.shadow.color = new Color(0f, 0f, 0f, 0.45f);
        d.embers.Add(e);
    }

    // everything gone (a Final Rush won, a new run)
    private void ClearAll(int wave)
    {
        foreach (var e in embers) { Give(e.body); Give(e.shadow); Give(e.mark); }
        foreach (var p in pools) Give(p.sr);
        embers.Clear();
        pools.Clear();
    }

    private SpriteRenderer Take(string layer, int order)
    {
        SpriteRenderer sr;
        if (spare.Count > 0) sr = spare.Pop();
        else
        {
            var go = new GameObject("Ember");
            go.transform.SetParent(transform, false);
            sr = go.AddComponent<SpriteRenderer>();
        }
        sr.gameObject.SetActive(true);
        sr.sortingLayerName = layer;
        sr.sortingOrder = order;
        sr.color = Color.white;
        sr.transform.localScale = Vector3.one;
        sr.transform.rotation = Quaternion.identity;
        return sr;
    }

    private void Give(SpriteRenderer sr)
    {
        if (sr == null) return;
        sr.gameObject.SetActive(false);
        spare.Push(sr);
    }

    private void Update()
    {
        float now = Time.time;

        for (int i = embers.Count - 1; i >= 0; i--)
        {
            var e = embers[i];
            float k = (now - e.start) / e.seconds;
            if (k >= 1f)
            {
                Land(e);
                Give(e.body); Give(e.shadow); Give(e.mark);
                embers.RemoveAt(i);
                continue;
            }
            Vector2 ground = Vector2.Lerp(e.from, e.to, k);
            float lift = 4f * ArcHeight * k * (1f - k);
            e.body.transform.position = ground + new Vector2(0f, lift);
            e.body.sprite = YamaArt.Frame(emberArt, now - e.start, 16f);
            e.body.transform.rotation = Quaternion.Euler(0f, 0f, -(now - e.start) * 540f);
            e.shadow.transform.position = ground;
            // the shadow grows as it falls back to the ground
            float sh = Mathf.Lerp(1f, 0.55f, lift / ArcHeight);
            e.shadow.transform.localScale = new Vector3(sh, sh, 1f);
            // where it lands: a ring drawing in, brighter as it comes down
            e.mark.transform.position = e.to;
            e.mark.sprite = markArt != null && markArt.Length > 0 ? markArt[Mathf.Min(markArt.Length - 1, (int)(k * markArt.Length))] : null;
            float ms = e.radius / 0.75f;
            e.mark.transform.localScale = new Vector3(ms, ms, 1f);
            e.mark.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.35f, 0.9f, k));
        }

        bool hurtNow = false;
        Hq.FindPlayer(out Vector2 player);
        for (int i = pools.Count - 1; i >= 0; i--)
        {
            var p = pools[i];
            float age = now - p.start;
            if (age >= p.seconds) { Give(p.sr); pools.RemoveAt(i); continue; }
            p.sr.sprite = YamaArt.Frame(poolArt, age + p.at.x, 12f);
            float fade = Mathf.Clamp01((p.seconds - age) / FadeSeconds);
            float grow = Mathf.Clamp01(age / 0.12f);
            float s = p.radius / 0.75f * Mathf.Lerp(0.6f, 1f, grow) * Mathf.Lerp(0.8f, 1f, fade);
            p.sr.transform.localScale = new Vector3(s, s, 1f);
            p.sr.color = new Color(1f, 1f, 1f, fade);
            if (!hurtNow && (player - p.at).sqrMagnitude <= p.radius * p.radius && age > 0.1f && fade > 0.3f)
            {
                hurtNow = true;
                if (now >= nextTick) Burn(p, player);
            }
        }
    }

    private void Land(Ember e)
    {
        if (splashArt != null) FxBatch.Play(splashArt, 20f, e.to, e.radius / 0.75f, "Aura", 31);
        Hq.Sound("hq_ember_land", e.to, 0.35f, 0.08f);
        var p = new Pool { at = e.to, start = Time.time, seconds = e.poolSeconds, radius = e.radius, share = e.share, sr = Take("Player", -5) };
        p.sr.transform.position = e.to;
        pools.Add(p);
    }

    private void Burn(Pool p, Vector2 player)
    {
        var health = Hq.PlayerHealth;
        if (health == null || health.IsDead) return;
        nextTick = Time.time + TickSeconds;
        float before = health.Current;
        health.TakeDamage(health.Max * p.share);
        if (health.Current < before) Hq.Sound("hq_ember_burn", player, 0.4f, 0.2f);
    }
}
