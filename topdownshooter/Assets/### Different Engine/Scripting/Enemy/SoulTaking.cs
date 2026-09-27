using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// the end of a normal run, the way Vampire Survivors' Reaper ends one: the Wuchang have come for the
// player (RunTimeLimit), and the first to get close enough takes them. it stops and throws its
// soul-catching chain; the shackle snaps shut on the player, who goes white and still; the soul is
// dragged out of the body along the chain as it's reeled in; a whirl of ink opens at the Wuchang's
// feet and the soul is swallowed into it; ink floods the screen, and the run's results come up as a
// run survived. once it starts nothing can stop it
public class SoulTaking : MonoBehaviour
{
    private const float WorldPpu = 37f / 1.3f;

    public static bool Running { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Running = false;

    private Transform wuchang, player;
    private SpriteRenderer body, wuchangSprite;
    private VfxLibrary lib;
    private readonly List<SpriteRenderer> links = new List<SpriteRenderer>();

    public static void Begin(Wuchang taker, Transform player)
    {
        if (Running || taker == null || player == null) return;
        Running = true;
        var go = new GameObject("Soul Taking");
        var s = go.AddComponent<SoulTaking>();
        s.wuchang = taker.transform;
        s.player = player;
        s.wuchangSprite = taker.GetComponentInChildren<SpriteRenderer>();
        s.StartCoroutine(s.Take());
    }

    private void OnDestroy() => Running = false;

    // where the chain leaves its hand: the front of it, at chest height
    private Vector3 Hand
    {
        get
        {
            if (wuchangSprite == null || wuchangSprite.sprite == null) return wuchang.position + Vector3.up * 0.4f;
            var b = wuchangSprite.bounds;
            float facing = wuchangSprite.flipX ? -1f : 1f;
            return new Vector3(b.center.x + facing * b.extents.x * 0.5f, b.center.y + b.extents.y * 0.1f, 0f);
        }
    }

    private Vector3 Feet
    {
        get
        {
            if (wuchangSprite == null) return wuchang.position;
            var b = wuchangSprite.bounds;
            return new Vector3(b.center.x, b.min.y + 0.1f, 0f);
        }
    }

    private IEnumerator Take()
    {
        lib = VfxLibrary.Get;
        body = player.GetComponentInChildren<SpriteRenderer>();
        if (player.TryGetComponent(out PlayerHealth health)) health.Seize();
        if (player.TryGetComponent(out PlayerInventory inventory)) inventory.LockLeveling();

        // it faces the player and lets the chain fly
        if (wuchangSprite != null) wuchangSprite.flipX = player.position.x < wuchang.position.x;
        Play(lib != null ? lib.chainSound : null, player.position);
        Juice.Shake(0.15f);

        // ---- the chain thrown: it pays out from the hand, sagging, the shackle at its end
        var hook = Sprite("Shackle", lib != null ? lib.chainHook : null, 30);
        float throwTime = 0.32f;
        for (float t = 0f; t < throwTime; t += Time.deltaTime)
        {
            float k = 1f - (1f - t / throwTime) * (1f - t / throwTime);
            LayChain(Hand, player.position + Vector3.up * 0.1f, k, 0.9f * (1f - k) + 0.25f, hook);
            yield return null;
        }

        // ---- caught: the shackle snaps shut, the body goes white and still
        LayChain(Hand, player.position + Vector3.up * 0.1f, 1f, 0.2f, hook);
        Juice.Shake(0.45f);
        var white = Shader.Find("Rogue/Silhouette");
        Material rest = body != null ? body.sharedMaterial : null;
        if (body != null && white != null) { body.sharedMaterial = new Material(white); body.color = Color.white; }
        for (float t = 0f; t < 0.22f; t += Time.deltaTime)
        {
            LayChain(Hand, player.position + Vector3.up * 0.1f, 1f, 0.2f * (1f - t / 0.22f), hook);
            yield return null;
        }

        // ---- the soul drawn out: it rises out of the body and is dragged along the chain as the
        // chain is reeled in; the body sags, greys and is left behind
        if (body != null) { body.sharedMaterial = rest; }
        var soul = Sprite("Soul", Frame(lib?.soul, 0f), 31);
        Vector3 from = player.position + Vector3.up * 0.2f;
        float pull = 0.85f;
        for (float t = 0f; t < pull; t += Time.deltaTime)
        {
            float k = t / pull, e = k * k * (3f - 2f * k);
            // first it lifts out, then it's hauled in
            Vector3 lift = from + Vector3.up * 0.45f * Mathf.Clamp01(k * 3f);
            Vector3 at = Vector3.Lerp(lift, Hand, Mathf.Clamp01((e - 0.15f) / 0.85f));
            soul.transform.position = at + Vector3.up * Mathf.Sin(k * Mathf.PI * 3f) * 0.08f;
            soul.sprite = Frame(lib?.soul, t * 12.5f % 1f) ?? soul.sprite;
            soul.color = new Color(1f, 1f, 1f, Mathf.Clamp01(k * 4f));
            LayChain(Hand, at, 1f, 0.05f, hook);
            if (body != null)
            {
                body.color = Color.Lerp(Color.white, new Color(0.42f, 0.4f, 0.48f, 0.85f), e);
                player.localScale = new Vector3(player.localScale.x, Mathf.Lerp(Mathf.Abs(player.localScale.y), Mathf.Abs(player.localScale.y) * 0.9f, e) * Mathf.Sign(player.localScale.y), 1f);
            }
            yield return null;
        }
        foreach (var l in links) if (l != null) l.enabled = false;
        hook.enabled = false;

        // ---- swallowed: the dark opens at its feet, the soul spirals down into it, it closes
        var maw = Sprite("Maw", Frame(lib?.maw, 0f), 4);
        maw.sortingLayerName = "Player";
        maw.sortingOrder = -4;
        maw.transform.position = Feet;
        Play(lib != null ? lib.swallowSound : null, wuchang.position);
        Vector3 start = soul.transform.position;
        float fall = 0.95f;
        for (float t = 0f; t < fall + 0.4f; t += Time.deltaTime)
        {
            float open = Mathf.Clamp01(t / 0.25f) * (1f - Mathf.Clamp01((t - fall) / 0.4f));
            maw.transform.localScale = new Vector3(open, open, 1f) * 1.2f;
            maw.sprite = Frame(lib?.maw, t * 14f % 1f) ?? maw.sprite;
            if (t < fall)
            {
                float k = t / fall;
                float r = (1f - k) * 0.9f, a = k * Mathf.PI * 5f;
                soul.transform.position = Vector3.Lerp(start, Feet, k * k) + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.45f, 0f);
                soul.transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.25f, k);
                soul.sprite = Frame(lib?.soul, t * 12.5f % 1f) ?? soul.sprite;
            }
            else if (soul.enabled)
            {
                soul.enabled = false;
                Juice.Shake(0.5f);
            }
            yield return null;
        }
        maw.enabled = false;

        // ---- the ink, then the run's results, as a run survived
        yield return InkWipe();
        GameEvents.OnRunWon?.Invoke();
        GameOverScreen screen = player.TryGetComponent(out PlayerHealth h) ? h.GameOverScreen : null;
        if (screen == null) screen = FindFirstObjectByType<GameOverScreen>(FindObjectsInactive.Include);
        if (screen != null) screen.ShowVictory();
        yield return InkClear();
        Destroy(gameObject);
    }

    // the chain from the hand toward `to`, paid out to `reach` of the way, sagging by `sag`: links
    // turning flat and edge on in turn along it, each laid along its stretch
    private void LayChain(Vector3 from, Vector3 to, float reach, float sag, SpriteRenderer hook)
    {
        Vector3 end = Vector3.Lerp(from, to, reach);
        float length = Vector3.Distance(from, end);
        const float spacing = 6f / WorldPpu;
        int n = Mathf.Max(1, Mathf.CeilToInt(length / spacing));
        for (int i = 0; i < n; i++)
        {
            float u = (i + 0.5f) / n, u2 = (i + 1.5f) / n;
            Vector3 p = Along(from, end, u, sag), q = Along(from, end, u2, sag);
            var l = Link(i);
            l.enabled = true;
            l.transform.position = p;
            Vector3 d = q - p;
            l.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        }
        for (int i = n; i < links.Count; i++) links[i].enabled = false;
        if (hook != null)
        {
            hook.enabled = true;
            hook.transform.position = end;
            Vector3 d = end - Along(from, end, 0.95f, sag);
            hook.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        }
    }

    private static Vector3 Along(Vector3 a, Vector3 b, float u, float sag) =>
        Vector3.Lerp(a, b, u) + Vector3.down * Mathf.Sin(u * Mathf.PI) * sag;

    private SpriteRenderer Link(int i)
    {
        while (links.Count <= i)
        {
            var frames = lib != null ? lib.chainLink : null;
            bool art = frames != null && frames.Length > 1;
            var sr = Sprite("Link", art ? frames[links.Count % 2] : null, 29);
            if (!art) sr.transform.localScale = new Vector3(0.18f, 0.08f, 1f);
            links.Add(sr);
        }
        return links[i];
    }

    private SpriteRenderer Sprite(string name, Sprite art, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = art != null ? art : WeaponFx.Square;
        if (art == null) { sr.color = new Color(0.8f, 0.85f, 1f); go.transform.localScale = Vector3.one * 0.3f; }
        sr.sortingLayerName = "Aura";
        sr.sortingOrder = order;
        return sr;
    }

    private static Sprite Frame(Sprite[] frames, float k)
    {
        if (frames == null || frames.Length == 0) return null;
        return frames[Mathf.Clamp((int)(k * frames.Length), 0, frames.Length - 1)];
    }

    private static void Play(AudioClip clip, Vector3 at)
    {
        if (clip != null) SfxPlayer.PlayAt(clip, at, 1f);
    }

    // ---------------------------------------------------------------- the ink

    private Image ink;

    // blots of ink blooming and running together until the screen is black, a seal pressed last
    private IEnumerator InkWipe()
    {
        var go = new GameObject("Ink", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(transform, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 470;
        ink = new GameObject("Wipe", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        ink.rectTransform.SetParent(go.transform, false);
        ink.rectTransform.anchorMin = Vector2.zero;
        ink.rectTransform.anchorMax = Vector2.one;
        ink.rectTransform.sizeDelta = Vector2.zero;
        ink.raycastTarget = false;
        var frames = lib != null ? lib.inkWipe : null;
        if (frames == null || frames.Length == 0)
        {
            ink.color = new Color(0f, 0f, 0f, 0f);
            for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime) { ink.color = new Color(0f, 0f, 0f, t / 0.6f); yield return null; }
            ink.color = Color.black;
            yield break;
        }
        // the frames as drawn: quick blooms, then the seal held a moment
        float[] seconds = { 0.06f, 0.06f, 0.06f, 0.06f, 0.06f, 0.06f, 0.06f, 0.08f, 0.5f };
        for (int i = 0; i < frames.Length; i++)
        {
            ink.sprite = frames[i];
            float hold = i < seconds.Length ? seconds[i] : 0.06f;
            for (float t = 0f; t < hold; t += Time.unscaledDeltaTime) yield return null;
        }
    }

    // the ink draining off as the results slam in (the game is stopped under them)
    private IEnumerator InkClear()
    {
        if (ink == null) yield break;
        var c = ink.color;
        for (float t = 0f; t < 0.45f; t += Time.unscaledDeltaTime)
        {
            ink.color = new Color(c.r, c.g, c.b, 1f - t / 0.45f);
            yield return null;
        }
    }
}
