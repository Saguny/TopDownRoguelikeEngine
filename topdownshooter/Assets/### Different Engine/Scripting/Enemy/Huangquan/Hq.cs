using System.Collections.Generic;
using UnityEngine;

// what Huangquan Road's enemies share: where the player is, how much of the world the camera shows,
// and their sounds, each kept to a few a second however many of them act at once (thirty spider
// lilies blooming together are one bloom, not thirty)
public static class Hq
{
    public const float PlayerBaseSpeed = 6f;     // PlayerMovement's own, unchanged by upgrades

    private static Transform player;
    private static PlayerHealth playerHealth;
    private static Camera cam;
    private static readonly Dictionary<string, float> nextSound = new Dictionary<string, float>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        player = null;
        playerHealth = null;
        cam = null;
        nextSound.Clear();
    }

    // a Final Rush won: the lilies' and lanterns' orbs still in the air go out with the horde
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Hook()
    {
        GameEvents.OnFinalRushEnded -= OnRushEnded;
        GameEvents.OnFinalRushEnded += OnRushEnded;
    }

    private static void OnRushEnded(int wave) => Danmaku.Cancel(false);

    // a hit that takes a share of the player's max health, grown with the run like contact damage
    // (SpawnDirector.ShotGrowth): a lily's orb takes 5% at the start, some 13% by 20:00
    public static float Hurt(float share) => share * (SpawnDirector.Active != null ? SpawnDirector.Active.ShotGrowth : 1f);

    public static bool FindPlayer(out Vector2 at)
    {
        if (player == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null)
            {
                player = go.transform;
                go.TryGetComponent(out playerHealth);
            }
        }
        at = player != null ? (Vector2)player.position : Vector2.zero;
        return player != null;
    }

    public static PlayerHealth PlayerHealth
    {
        get
        {
            if (playerHealth == null) FindPlayer(out _);
            return playerHealth;
        }
    }

    private static Camera Cam
    {
        get
        {
            if (cam == null) cam = Camera.main;
            return cam;
        }
    }

    // half the screen's height and width, in world units
    public static float HalfHeight => Cam != null && Cam.orthographic ? Cam.orthographicSize : 7f;
    public static float HalfWidth => Cam != null && Cam.orthographic ? Cam.orthographicSize * Cam.aspect : 12f;

    // on the screen, `margin` units in from its edges (negative: that far off it still counts)
    public static bool OnScreen(Vector2 p, float margin = 0f)
    {
        if (Cam == null) return true;
        Vector2 c = Cam.transform.position;
        return Mathf.Abs(p.x - c.x) <= HalfWidth - margin && Mathf.Abs(p.y - c.y) <= HalfHeight - margin;
    }

    // a sound from Resources/Sfx, at most once every `gap` seconds whoever asks
    public static void Sound(string name, Vector3 at, float volume, float gap, float pitch = 1f, float jitter = 0.06f)
    {
        float now = Time.time;
        if (nextSound.TryGetValue(name, out float next) && now < next) return;
        nextSound[name] = now + gap;
        YamaArt.Play(name, at, volume, pitch * Random.Range(1f - jitter, 1f + jitter));
    }

    public static float Angle(Vector2 v) => Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
    public static Vector2 Dir(float degrees) => new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));
}
