using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// a Final Rush won, the weapons are put away until the next wave: every shot of the player's in
// flight or lying on the ground (blades, stars, talismans, spheres, dragons, clouds, ink, arrows,
// meteors) goes out in a quick cascade of little gold seal-bursts from the player outward, a bell
// ticking higher as it goes, as the spirit seal's wave rolls out over the horde (SealWave). no
// weapon fires again until the next wave starts (or the final boss comes). it makes itself
public class WeaponHold : MonoBehaviour
{
    private const int MostPops = 140;
    private const float CascadeSeconds = 0.6f;

    private static WeaponHold instance;
    private readonly List<Behaviour> held = new List<Behaviour>();
    private readonly List<Vector2> pops = new List<Vector2>(256);
    private Sprite[] burst;
    private AudioClip tick;

    public static bool Holding { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; Holding = false; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (instance != null) return;
        var go = new GameObject("Weapon Hold");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<WeaponHold>();
    }

    private void OnEnable()
    {
        GameEvents.OnFinalRushEnded += OnRushEnded;
        GameEvents.OnWaveStarted += OnWaveStarted;
        GameEvents.OnFinalBossStarted += Release;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        GameEvents.OnFinalRushEnded -= OnRushEnded;
        GameEvents.OnWaveStarted -= OnWaveStarted;
        GameEvents.OnFinalBossStarted -= Release;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m)
    {
        held.Clear();
        Holding = false;
    }

    private void OnWaveStarted(int wave) => Release();

    private void OnRushEnded(int wave)
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;
        Vector2 me = player.transform.position;
        pops.Clear();

        // where every shot is, before they go
        foreach (var w in player.GetComponentsInChildren<Weapon>())
        {
            if (w == null || !w.enabled || w is CommandToken) continue;
            Collect(w);
            w.ClearShots();
            w.enabled = false;
            held.Add(w);
        }
        foreach (var p in FindObjectsByType<Projectile>(FindObjectsSortMode.None))
        {
            if (!p.isActiveAndEnabled) continue;
            pops.Add(p.transform.position);
            ObjectPool.Recycle(p.gameObject);
        }
        foreach (var p in FindObjectsByType<AOEProjectile>(FindObjectsSortMode.None))
        {
            if (!p.isActiveAndEnabled) continue;
            pops.Add(p.transform.position);
            Destroy(p.gameObject);
        }
        // the old-style weapons' own drivers: the bow's shooter, the aura, the meteors
        Hold(player.GetComponentInChildren<AutoShooter>());
        Hold(player.GetComponentInChildren<Aura>());
        Hold(player.GetComponentInChildren<AOEAttack>());
        Holding = true;

        pops.Sort((a, b) => (a - me).sqrMagnitude.CompareTo((b - me).sqrMagnitude));
        if (pops.Count > MostPops) pops.RemoveRange(MostPops, pops.Count - MostPops);
        StopAllCoroutines();
        StartCoroutine(Cascade());
    }

    // the visible shots of a weapon: every sprite showing in its effects holder
    private void Collect(Weapon w)
    {
        var fx = w.Effects;
        if (fx == null) return;
        foreach (var sr in fx.GetComponentsInChildren<SpriteRenderer>())
            if (sr.enabled && sr.sprite != null && sr.gameObject.activeInHierarchy) pops.Add(sr.transform.position);
    }

    private void Hold(Behaviour b)
    {
        if (b == null || !b.enabled) return;
        b.enabled = false;
        held.Add(b);
    }

    private void Release()
    {
        StopAllCoroutines();
        foreach (var b in held) if (b != null) b.enabled = true;
        held.Clear();
        Holding = false;
    }

    private IEnumerator Cascade()
    {
        if (burst == null) burst = YamaArt.Strip("FinalRush/rush_burst");
        if (tick == null) tick = Resources.Load<AudioClip>("Sfx/rush_seal");
        int n = pops.Count;
        float nextTick = 0f;
        startTime = Time.time;
        for (int i = 0; i < n; i++)
        {
            float due = CascadeSeconds * i / Mathf.Max(1, n);
            while (Time.time - startTime < due) yield return null;
            if (burst != null && burst.Length > 0) FxBatch.Play(burst, 26f, pops[i], 0.45f, "Aura", 30);
            if (tick != null && Time.time >= nextTick)
            {
                nextTick = Time.time + 0.045f;
                SfxPlayer.PlayAt(tick, pops[i], 0.35f, 1.2f + 0.8f * i / Mathf.Max(1, n));
            }
        }
    }

    private float startTime;
}
