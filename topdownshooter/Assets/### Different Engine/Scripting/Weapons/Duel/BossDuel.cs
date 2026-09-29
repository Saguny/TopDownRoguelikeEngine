using System.Collections;
using UnityEngine;

// an end boss with a fight of its own (Yama, Meng Po; see IFightBoss) is a duel. as he comes,
// every weapon the player has is put away (WeaponHold.Duel: their shots go out in a cascade of
// seal-bursts) and stays away the whole fight; then a pillar of light comes down on the player
// and the character's starting weapon comes back in a form only this fight has (DuelWeapon): the
// Bow as the Sun-Shooter's Bow, the Seven Star Swords as the Big Dipper's formation, the Peach
// Talismans as the Peach Wood Decree, its name along the bottom of the screen. one clean weapon
// against his danmaku, instead of six evolutions' worth of noise over it
public class BossDuel : MonoBehaviour
{
    private const float PutAwaySeconds = 0.85f, PillarFps = 18f;

    private static BossDuel instance;

    // the boss the duel is with
    public static EnemyHealth Boss { get; private set; }
    public static bool Active => instance != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; Boss = null; }

    public static void Begin(GameObject boss)
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null || boss == null || instance != null) return;
        // a scene object: it goes with the run
        instance = new GameObject("Boss Duel").AddComponent<BossDuel>();
        Boss = boss.GetComponent<EnemyHealth>();
        instance.StartCoroutine(instance.Run(player));
    }

    private void OnDestroy()
    {
        if (instance != this) return;
        instance = null;
        Boss = null;
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
    }
}
