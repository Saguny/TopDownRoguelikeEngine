using UnityEngine;

// Bǐ'àn Huā Jīng, the spider lily demon: a red lily of the underworld sprung up out of the road
// anywhere on the screen. it never moves. every couple of seconds its petals draw in, glow, and
// throw out a ring of eight slow red spirit orbs, the ring turned a little each time so the gaps
// never stay put. the danger isn't one lily, it's a field of them making the ground you can stand on
// smaller while the horde closes in
[RequireComponent(typeof(EnemyMovement))]
public class SpiderLilyDemon : MonoBehaviour
{
    [Tooltip("seconds between rings")]
    public float interval = 2.8f;
    [Tooltip("seconds its petals draw in and glow before a ring: the warning")]
    public float windUp = 0.55f;
    [Tooltip("seconds after it springs up before its first ring")]
    public float firstDelay = 1.4f;
    public int orbs = 8;
    [Tooltip("degrees the ring turns from one to the next")]
    public float ringTurn = 22.5f;
    public float orbSpeed = 2.1f;
    public float orbLife = 7f;
    [Tooltip("a share of the player's max health an orb takes")]
    public float orbDamage = 0.05f;
    [Tooltip("it doesn't fire at a player further than this: off-screen lilies stay quiet")]
    public float range = 16f;

    private EnemyMovement move;
    private HqEnemyArt art;
    private float nextRing, ringAngle;
    private bool winding;

    private void Awake()
    {
        move = GetComponent<EnemyMovement>();
        art = GetComponent<HqEnemyArt>();
    }

    private void OnEnable()
    {
        move.Rooted = true;
        winding = false;
        Hq.Sound("hq_lily_rise", transform.position, 0.4f, 0.15f);
        ringAngle = Random.value * 360f;
        // not all in step: a field of them pulses like something breathing
        nextRing = Time.time + firstDelay + Random.value * interval * 0.6f;
    }

    private void Update()
    {
        float now = Time.time;
        if (!winding && now >= nextRing - windUp)
        {
            if (!Hq.FindPlayer(out Vector2 p) || ((Vector2)transform.position - p).sqrMagnitude > range * range)
            {
                nextRing = now + interval * 0.5f;
                return;
            }
            winding = true;
            if (art != null) art.Play("act", windUp + 0.25f);
            Hq.Sound("hq_lily_charge", transform.position, 0.35f, 0.18f);
        }
        if (winding && now >= nextRing)
        {
            winding = false;
            nextRing = now + interval * Random.Range(0.9f, 1.1f);
            Fire();
        }
    }

    private void Fire()
    {
        Vector2 at = (Vector2)transform.position + new Vector2(0f, 0.2f);
        // the more of their weapons the player has evolved, the faster the petals fly
        float speed = Hq.ByEvolutions(orbSpeed, 0.12f, 1.7f);
        var orb = Shot.Of(BulletType.Orb, BulletColor.Red, speed)
            .Accel(-0.35f, speed * 0.6f).Life(orbLife).Hurts(Hq.Hurt(orbDamage)).Silent();
        Danmaku.Ring(at, orbs, ringAngle, orb);
        ringAngle += ringTurn;
        var bloom = YamaArt.Strip("Huangquan/lily_bloom");
        if (bloom != null) FxBatch.Play(bloom, 20f, at, 1f, "Aura", 40);
        Hq.Sound("hq_lily_fire", at, 0.45f, 0.12f);
    }
}
