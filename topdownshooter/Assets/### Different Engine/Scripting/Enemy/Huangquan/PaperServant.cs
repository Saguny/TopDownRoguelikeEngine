using UnityEngine;

// Zhǐ Rén, the paper servants: funeral effigies cut from paper, burned for the dead to serve them,
// come back to serve Huangquan instead. they come in small quick bursts and fold under a hit, but
// they don't follow the player, they cut lines: a straight dash at 1.8 times the player's base speed
// at where the player was when it set off, straight through the slow crowd, then a stop (0.3 s) to
// turn, re-aim, and go again. standing still is how they catch you; changing direction as one sets
// off is how they miss
[RequireComponent(typeof(EnemyMovement))]
public class PaperServant : MonoBehaviour
{
    [Tooltip("its dash, as a multiple of the player's base speed")]
    public float dashSpeed = 1.8f;
    [Tooltip("seconds it stands between dashes to re-aim")]
    public float pause = 0.3f;
    [Tooltip("units it carries on past where the player was")]
    public float overshoot = 1.6f;
    [Tooltip("the longest one dash lasts, seconds")]
    public float longestDash = 0.9f;
    [Tooltip("seconds between afterimages while it dashes")]
    public float trailEvery = 0.045f;

    private EnemyMovement move;
    private HqEnemyArt art;
    private bool dashing;
    private float until, nextTrail;
    private Vector2 heading;

    private void Awake()
    {
        move = GetComponent<EnemyMovement>();
        art = GetComponent<HqEnemyArt>();
    }

    private void OnEnable()
    {
        dashing = false;
        // a burst of them doesn't set off as one
        Hold(pause + Random.value * 0.4f);
    }

    private void Hold(float seconds)
    {
        dashing = false;
        until = Time.time + seconds;
        move.Drive(Vector2.zero, seconds + 0.05f);
        if (art != null) art.Play("aim", seconds);
    }

    private void Update()
    {
        float now = Time.time;
        if (dashing)
        {
            if (now >= nextTrail)
            {
                nextTrail = now + trailEvery;
                // scraps of paper shed along the line, and its afterimage paling behind it
                var scraps = YamaArt.Strip("Huangquan/paper_trail");
                if (scraps != null) FxBatch.Play(scraps, 22f, transform.position, 1f, "Enemy", 1);
                var ghost = YamaArt.Strip(heading.x >= 0f ? "Huangquan/paper_ghost_r" : "Huangquan/paper_ghost_l");
                if (ghost != null) FxBatch.Play(ghost, 24f, transform.position, 1f, "Enemy", 1);
            }
            if (now >= until || !move.Driven) Hold(pause);
            return;
        }
        if (now < until) return;
        if (!Hq.FindPlayer(out Vector2 target)) { Hold(pause); return; }

        Vector2 me = transform.position;
        Vector2 to = target - me;
        float dist = to.magnitude;
        heading = dist > 0.01f ? to / dist : Random.insideUnitCircle.normalized;
        float speed = Hq.PlayerBaseSpeed * dashSpeed;
        float seconds = Mathf.Clamp((dist + overshoot) / speed, 0.2f, longestDash);
        dashing = true;
        until = now + seconds;
        nextTrail = now;
        // through the crowd, not round it
        move.Drive(heading * speed, seconds, ghost: true);
        if (art != null) art.Play("dash", seconds, loop: true);
        move.Face(heading, seconds + pause);
        Hq.Sound("hq_paper_dash", me, 0.3f, 0.07f, 1f, 0.12f);
    }
}
