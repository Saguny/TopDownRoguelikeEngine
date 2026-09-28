using UnityEngine;

// the arena the run takes place in, e.g. the temple courtyard. its four walls are the edges the
// SpawnDirector keeps enemies inside, and it brings its own enemy schedule, so a new playfield
// needs no rewiring in the scene
public class Playfield : MonoBehaviour
{
    public BoxCollider2D north, south, east, west;

    [Tooltip("who comes when on this stage. the SpawnDirector's own Timeline overrides it")]
    public SpawnTimeline spawnTimeline;

    [Tooltip("how tough its enemies get over the run and how fast levels come. empty uses the scene's")]
    public DifficultyCurve difficulty;

    [Tooltip("this map's own final boss, e.g. Yama for the Courtyard. empty: the scene's")]
    public EnemyArchetype finalBoss;

    [Header("Final Rush (empty: the scene's, the Magistrate's procession)")]
    [Tooltip("this map's rush bosses, taken in turn as a rush brings its set")]
    public EnemyArchetype[] rushBosses;
    [Tooltip("the procession's column and ring enemy")]
    public EnemyArchetype processionMain;
    [Tooltip("the procession's fast flankers")]
    public EnemyArchetype processionFast;

    [Tooltip("where the run begins, from the playfield's centre. used when this map is swapped into a scene")]
    public Vector2 playerStart = new Vector2(0f, -4f);

    public static Playfield Active { get; private set; }

    public BoxCollider2D[] Bounds => new[] { west, east, south, north };

    public bool HasBounds => north != null && south != null && east != null && west != null;

    private void OnEnable() => Active = this;

    private void OnDisable()
    {
        if (Active == this) Active = null;
    }
}
