using System.Collections.Generic;
using UnityEngine;

public enum EnvelopeRarity { Common, Rare, Legendary }

// who dropped an envelope: the rarer the enemy, the better the odds
public enum EnvelopeSource { Elite, Boss, FinalBoss }

// one thing an envelope gave, as its scroll shows it
public struct EnvelopeReward
{
    public UpgradeData item;
    public bool evolution;
    public int level;           // the item's level after it
    public int coins;           // or, for coins, how many (after Greed)

    public bool IsCoins => item == null;
    public static EnvelopeReward Coins(int amount) => new EnvelopeReward { coins = amount };
}

// a fortune envelope lying where an elite or a boss fell, like Vampire Survivors' chests: lacquered
// red and gold on a pool of gold light, a beam rising out of it that's seen from across the screen
// and a chevron bobbing over it. walking into it opens it (EnvelopeOpening). off screen, an arrow at
// the screen's edge points to it (EnvelopeIndicators). its rarity is rolled as it drops and only
// shown when it's opened
public class FortuneEnvelope : MonoBehaviour
{
    // how many of the pool's upgrades each rarity gives
    public static int Upgrades(EnvelopeRarity r) => r == EnvelopeRarity.Legendary ? 5 : r == EnvelopeRarity.Rare ? 3 : 1;

    // the coins in it, before Greed: a roll between its rarity's least and most, the way Vampire
    // Survivors' chests pay gold. envelopes are the only place coins come from, and there are few:
    // one a Final Rush (only its last boss drops one), the elites', the final boss's, some 15 in a
    // full run, which with the String of Wen once maxed comes to about 13k
    public static Vector2Int CoinRange(EnvelopeRarity r) =>
        r == EnvelopeRarity.Legendary ? new Vector2Int(1000, 1600) : r == EnvelopeRarity.Rare ? new Vector2Int(400, 700) : new Vector2Int(150, 300);
    public static int RollCoins(EnvelopeRarity r)
    {
        var range = CoinRange(r);
        return Random.Range(range.x, range.y + 1);
    }

    // a clean spell card's bonus in the final boss's duel goes into the envelope it drops
    public static int FinalBonus { get; private set; }
    public static void AddFinalBonus(int coins) => FinalBonus += Mathf.Max(0, coins);
    // the coins an envelope from `source` holds on top of its roll: the final boss's, the bonus
    public static int TakeBonus(EnvelopeSource source)
    {
        if (source != EnvelopeSource.FinalBoss) return 0;
        int b = FinalBonus;
        FinalBonus = 0;
        return b;
    }

    // the odds, common / rare / legendary, by who dropped it
    public static EnvelopeRarity Roll(EnvelopeSource source)
    {
        Vector3 odds = source == EnvelopeSource.FinalBoss ? new Vector3(0f, 0.35f, 0.65f)
            : source == EnvelopeSource.Boss ? new Vector3(0.45f, 0.42f, 0.13f)
            : new Vector3(0.7f, 0.25f, 0.05f);
        float r = Random.value * (odds.x + odds.y + odds.z);
        if (r < odds.x) return EnvelopeRarity.Common;
        if (r < odds.x + odds.y) return EnvelopeRarity.Rare;
        return EnvelopeRarity.Legendary;
    }

    private const float Reach = 0.75f;          // how close the player walks to it to pick it up
    private const float WorldPpu = 37f / 1.3f;
    private const float PopSeconds = 0.5f;

    private static readonly List<FortuneEnvelope> lying = new List<FortuneEnvelope>();
    public static IReadOnlyList<FortuneEnvelope> Lying => lying;

    // like Vampire Survivors' chests, only an envelope dropped from this far into the run (on the
    // run clock) can evolve a weapon; one dropped earlier gives levels even if it's opened later
    public const float EvolvesFromSeconds = 600f;
    private static float runSeconds;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        lying.Clear();
        runSeconds = 0f;
        FinalBonus = 0;
        GameEvents.OnRunTimeChanged -= OnRunTime;
        GameEvents.OnRunTimeChanged += OnRunTime;
    }

    private static void OnRunTime(float seconds) => runSeconds = seconds;

    public EnvelopeRarity Rarity { get; private set; }
    public EnvelopeSource Source { get; private set; }
    // whether it can evolve a weapon: dropped at 10:00 or later, or by the final boss
    public bool Evolves { get; private set; }

    private SpriteRenderer body, glow, beam, marker;
    private Sprite[] bodyFrames, glowFrames, beamFrames, markerFrames;
    private Vector3 from, rest;
    private float age;
    private bool landed;
    private Transform player;

    // an envelope thrown out of an enemy that fell at `at`
    public static FortuneEnvelope Drop(Vector3 at, EnvelopeSource source)
    {
        var go = new GameObject("Fortune Envelope");
        var env = go.AddComponent<FortuneEnvelope>();
        env.Rarity = Roll(source);
        env.Source = source;
        env.Evolves = source == EnvelopeSource.FinalBoss || runSeconds >= EvolvesFromSeconds;
        env.from = at;
        // it lands a little way off, so it isn't hidden under the body
        Vector2 off = Random.insideUnitCircle.normalized * Random.Range(0.4f, 0.8f);
        env.rest = at + new Vector3(off.x, off.y, 0f);
        env.rest.z = 0f;
        go.transform.position = at;
        return env;
    }

    private void Awake()
    {
        var lib = VfxLibrary.Get;
        bodyFrames = lib != null ? lib.envelope : null;
        glowFrames = lib != null ? lib.envelopeGlow : null;
        beamFrames = lib != null ? lib.envelopePillar : null;
        markerFrames = lib != null ? lib.envelopeMarker : null;

        glow = Part("Glow", glowFrames, "Player", -5, WeaponFx.Disc, new Color(1f, 0.8f, 0.3f, 0.5f));
        beam = Part("Beam", beamFrames, "Aura", 3, WeaponFx.Square, new Color(1f, 0.85f, 0.4f, 0.5f));
        body = Part("Envelope", bodyFrames, "Player", 2, WeaponFx.Square, new Color(0.85f, 0.15f, 0.2f));
        marker = Part("Marker", markerFrames, "Aura", 20, WeaponFx.Disc, new Color(1f, 0.85f, 0.3f));
        glow.enabled = beam.enabled = marker.enabled = false;
        if (!Animated(glowFrames)) glow.transform.localScale = new Vector3(1.3f, 0.5f, 1f);
        if (!Animated(beamFrames)) beam.transform.localScale = new Vector3(0.35f, 2.6f, 1f);
        if (!Animated(bodyFrames)) body.transform.localScale = new Vector3(0.7f, 0.9f, 1f);
        if (!Animated(markerFrames)) marker.transform.localScale = Vector3.one * 0.35f;
    }

    private static bool Animated(Sprite[] f) => f != null && f.Length > 0 && f[0] != null;

    private SpriteRenderer Part(string name, Sprite[] frames, string layer, int order, Sprite placeholder, Color placeholderColor)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        bool art = Animated(frames);
        sr.sprite = art ? frames[0] : placeholder;
        sr.color = art ? Color.white : placeholderColor;
        sr.sortingLayerName = layer;
        sr.sortingOrder = order;
        return sr;
    }

    private void OnEnable() => lying.Add(this);
    private void OnDisable() => lying.Remove(this);

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        age += dt;

        if (!landed)
        {
            // thrown up out of the body in an arc, spinning once, and down with a bounce
            float t = Mathf.Clamp01(age / PopSeconds);
            Vector3 p = Vector3.Lerp(from, rest, t);
            p.y += Mathf.Sin(t * Mathf.PI) * 1.1f;
            transform.position = p;
            body.transform.localRotation = Quaternion.Euler(0f, 0f, (1f - t) * 360f);
            if (t >= 1f) Land();
            else Frame(body, bodyFrames, 11f);
            return;
        }

        // at rest: bobbing a pixel or two, the beam and glow breathing, the chevron bobbing over it
        float bob = Mathf.Round(Mathf.Sin(age * 3f) * 1.5f) / WorldPpu;
        body.transform.localPosition = new Vector3(0f, 0.12f + bob, 0f);
        marker.transform.localPosition = new Vector3(0f, 0.95f + Mathf.Round(Mathf.Sin(age * 5f) * 2f) / WorldPpu, 0f);
        Frame(body, bodyFrames, 11f);
        Frame(glow, glowFrames, 11f);
        Frame(beam, beamFrames, 12.5f);
        Frame(marker, markerFrames, 11f);
        // the beam rises up out of it once it has landed
        float rise = Mathf.Clamp01((age - PopSeconds) / 0.35f);
        if (Animated(beamFrames))
        {
            float h = beam.sprite.rect.height / beam.sprite.pixelsPerUnit;
            beam.transform.localScale = new Vector3(1f, rise, 1f);
            beam.transform.localPosition = new Vector3(0f, h * 0.5f * rise, 0f);
        }
        WeaponFx.SetAlpha(beam, 0.55f + 0.15f * Mathf.Sin(age * 2.2f));

        TryPickUp();
    }

    private void Land()
    {
        landed = true;
        transform.position = rest;
        body.transform.localRotation = Quaternion.identity;
        glow.enabled = beam.enabled = marker.enabled = true;
        Juice.Shake(0.05f);
        var lib = VfxLibrary.Get;
        if (lib != null && Animated(lib.envelopePickup)) FxBatch.Play(lib.envelopePickup, 25f, rest, 0.6f);
        EnvelopeIndicators.Ensure();
    }

    private void TryPickUp()
    {
        // not while the game is stopped: a menu, a pause, another envelope opening
        if (Time.timeScale <= 0f || EnvelopeOpening.Busy) return;
        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p == null) return;
            player = p.transform;
        }
        if (player.TryGetComponent(out PlayerHealth health) && health.IsDead) return;
        if (((Vector2)player.position - (Vector2)transform.position).sqrMagnitude > Reach * Reach) return;

        var lib = VfxLibrary.Get;
        if (lib != null)
        {
            if (Animated(lib.envelopePickup)) FxBatch.Play(lib.envelopePickup, 25f, transform.position + Vector3.up * 0.2f, 1.3f);
            if (lib.envelopePickupSound != null) SfxPlayer.PlayAt(lib.envelopePickupSound, transform.position, lib.envelopeVolume * 1.6f);
        }
        EnvelopeOpening.Open(Rarity, Source, Evolves);
        Destroy(gameObject);
    }

    private void Frame(SpriteRenderer sr, Sprite[] frames, float fps)
    {
        if (!Animated(frames)) return;
        sr.sprite = frames[(int)(age * fps) % frames.Length];
    }
}
