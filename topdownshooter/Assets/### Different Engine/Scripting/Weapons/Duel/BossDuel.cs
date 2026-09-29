using System.Collections;
using UnityEngine;

// an end boss with a fight of its own (Yama, Meng Po; see IFightBoss) is a duel. as he comes,
// every weapon the player has is put away (WeaponHold.Duel: their shots go out in a cascade of
// seal-bursts) and stays away the whole fight; then a pillar of light comes down on the player
// and the character's starting weapon comes back in a form only this fight has (DuelWeapon): the
// Bow as the Sun-Shooter's Bow, the Seven Star Swords as the Big Dipper's formation, the Peach
// Talismans as the Peach Wood Decree, its name along the bottom of the screen. one clean weapon
// against his danmaku, instead of six evolutions' worth of noise over it. the Command Token
// stays: its shockwave is the duel's bomb, clearing his bullets off the screen
public class BossDuel : MonoBehaviour
{
    private const float PutAwaySeconds = 0.85f, PillarFps = 18f;

    // the duel's footing: the player at +80% move speed whatever their build (the Move Speed
    // passive neither adds to it nor falls short of it), and the boss's bullets twice as fast to
    // match. Meng Po's river current is only a little stronger: doubled, it would carry the player
    public const float PlayerSpeed = 1.8f, BulletSpeed = 2f, CurrentScale = 1.4f;

    private static BossDuel instance;

    // the boss the duel is with
    public static EnemyHealth Boss { get; private set; }
    public static bool Active => instance != null;
    // the duel's middle: where the player stood as the boss came. he comes in above it
    public static Vector2 Home { get; private set; }
    // how far above the player the boss comes in, and keeps to until he moves
    public static readonly Vector2 Above = new Vector2(0f, 4.6f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; Boss = null; Home = default; }

    public static void Begin(GameObject boss)
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null || boss == null || instance != null) return;
        // a scene object: it goes with the run
        instance = new GameObject("Boss Duel").AddComponent<BossDuel>();
        Boss = boss.GetComponent<EnemyHealth>();
        Home = player.transform.position;
        PlayerMovement.SpeedOverride = PlayerSpeed;
        Danmaku.SpeedScale = BulletSpeed;
        instance.StartCoroutine(instance.Run(player));
    }

    private void OnDestroy()
    {
        if (instance != this) return;
        instance = null;
        Boss = null;
        PlayerMovement.SpeedOverride = 0f;
        PlayerMovement.Held = false;
        Danmaku.SpeedScale = 1f;
    }

    // between the boss's health bars, a fresh start: the player drawn back to the middle of the
    // duel, easing in, trailing light, while the boss (who sets his own keep back to Above) glides
    // in over them. the bullets are already gone by then (each break cancels them)
    public static IEnumerator Regroup(float seconds = 0.6f)
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null || !player.TryGetComponent(out Rigidbody2D rb) || !Active) yield break;
        Vector2 from = rb.position;
        if ((from - Home).sqrMagnitude < 0.04f) yield break;
        PlayerMovement.Held = true;
        PlayerMovement.Drift = Vector2.zero;
        var trail = YamaArt.Frames("cancel");
        float nextTrail = 0f;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            if (rb == null) break;
            float k = t / seconds, e = k * k * (3f - 2f * k);
            rb.MovePosition(Vector2.Lerp(from, Home, e));
            if (trail != null && t >= nextTrail)
            {
                nextTrail = t + 0.05f;
                FxBatch.Play(trail, 22f, rb.position, 1f, "Aura", 201);
            }
            yield return null;
        }
        if (rb != null) rb.position = Home;
        PlayerMovement.Held = false;
    }

    // the same fresh start in an instant, for a spell card's cut-in to hide: the player simply is
    // back in the middle, a puff of light where they were and where they land
    public static void Snap()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null || !player.TryGetComponent(out Rigidbody2D rb) || !Active) return;
        var puff = YamaArt.Frames("cancel");
        if (puff != null) FxBatch.Play(puff, 22f, rb.position, 1.4f, "Aura", 201);
        PlayerMovement.Drift = Vector2.zero;
        rb.position = Home;
        rb.linearVelocity = Vector2.zero;
        player.transform.position = Home;
        if (puff != null) FxBatch.Play(puff, 22f, Home, 1.4f, "Aura", 201);
    }

    private IEnumerator Run(GameObject player)
    {
        WeaponHold.Duel();
        yield return new WaitForSeconds(PutAwaySeconds);
        if (player == null) yield break;

        var stats = player.GetComponent<StatContext>();
        var starting = stats != null && stats.Character != null ? stats.Character.startingWeapon as WeaponData : null;
        var type = starting is SevenStarSwordsData ? typeof(DipperFormation)
                 : starting is PeachTalismansData ? typeof(PeachDecree)
                 : typeof(SunShooterBow); // the Bow, and any starting weapon without a duel of its own

        // the pillar first, the weapon inside it as it peaks
        var probe = (DuelWeapon)player.AddComponent(type);
        probe.enabled = false;
        var pillar = YamaArt.Strip("Duel/" + probe.Pillar);
        Vector2 at = player.transform.position;
        YamaArt.Play("duel_manifest", at, 1f);
        Juice.Shake(0.15f);
        var screen = YamaScreen.Get();
        if (screen != null)
        {
            screen.Flash(probe.Tint, 0.28f, 0.5f);
            screen.Duel(probe.Title, probe.Line, probe.Tint);
        }

        SpriteRenderer sr = null;
        if (pillar != null && pillar.Length > 0)
        {
            sr = new GameObject("Duel Pillar").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(transform, false);
            sr.sortingLayerName = DuelWeapon.Layer;
            sr.sortingOrder = DuelWeapon.Order + 6;
        }
        float seconds = pillar != null ? pillar.Length / PillarFps : 0.6f;
        bool armed = false;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            if (player == null) break;
            if (sr != null)
            {
                // it rides the player, its foot drawn at their feet
                sr.transform.position = player.transform.position;
                sr.sprite = pillar[Mathf.Min(pillar.Length - 1, (int)(t * PillarFps))];
            }
            if (!armed && t >= seconds * 0.35f)
            {
                armed = true;
                Arm(probe, player, starting);
            }
            yield return null;
        }
        if (!armed && probe != null) Arm(probe, player, starting);
        if (sr != null) Destroy(sr.gameObject);
    }

    // the weapon takes up its duel form: its own record in the run's stats, under the duel's name
    private static void Arm(DuelWeapon weapon, GameObject player, WeaponData starting)
    {
        if (weapon == null || player == null) return;
        // the run's copy of the weapon it comes from (its icons), else the asset, else a bare one
        var kind = weapon is DipperFormation ? typeof(SevenStarSwordsData) : weapon is PeachDecree ? typeof(PeachTalismansData) : typeof(BowData);
        WeaponData source = null;
        foreach (var w in player.GetComponents<Weapon>())
            if (w != weapon && w.Asset != null && w.Asset.GetType() == kind) { source = w.Asset; break; }
        if (source == null && starting != null && starting.GetType() == kind) source = starting;
        var asset = source != null ? Instantiate(source) : (WeaponData)ScriptableObject.CreateInstance(kind);
        asset.name = weapon.Title;
        asset.evolvedTitle = weapon.Title;
        weapon.Init(asset);
        weapon.SetLevel(Mathf.Max(1, asset.EvolutionLevel));
        weapon.enabled = true;
        // and the player lights up with it, blazing white-blue for the rest of the fight
        DuelOutline.On(player);
    }
}
