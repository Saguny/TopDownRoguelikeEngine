using UnityEngine;

public enum SpawnPattern { Horde, Cluster, OnScreen, Burst, Edge, Rare }

[CreateAssetMenu(menuName = "Rogue/EnemyArchetype")]
public class EnemyArchetype : ScriptableObject
{
    public GameObject prefab;
    [Min(1)] public int cost = 1;               // how much of the spawn budget it costs
    [Range(0f, 1f)] public float weight = 0.5f;  // relative selection weight
    public float baseHealth = 10f;
    public float baseSpeed = 1.5f;
    public float baseDamage = 5f;
    [Tooltip("share of every hit its armour turns aside: 0.3 = 30% less damage. the Armour Piercing passive cuts through it")]
    [Range(0f, 0.9f)] public float armour = 0f;
    [Tooltip("share of a hit's knockback it shrugs off: 0 is pushed the full way, 1 not at all (bosses are never pushed, whatever this says)")]
    [Range(0f, 1f)] public float knockbackResist = 0f;
    [Header("What harms it (the weapons' attack classes)")]
    [Tooltip("damage it takes from Physical weapons (arrows, blades): 0.6 = blades pass through it, 1.3 = soft to them")]
    [Range(0.25f, 2f)] public float physicalTaken = 1f;
    [Tooltip("damage it takes from Magical weapons (talismans, spells, summoned things)")]
    [Range(0.25f, 2f)] public float magicalTaken = 1f;

    [Header("How it arrives (the timeline spawner)")]
    [Tooltip("Horde: the usual, flocks of the small ones from the front. Cluster: a big tight crowd of them at once. OnScreen: " +
             "somewhere on the screen itself, away from the player (a plant that grows there). Burst: a few together from one point " +
             "of the edge. Edge: one at a time at the screen's edge. Rare: at the edge, seldom (keep its weight low and Max Alive 1)")]
    public SpawnPattern pattern = SpawnPattern.Horde;
    [Tooltip("how many come together for Cluster and Burst: a random count in this range")]
    public Vector2Int group = new Vector2Int(1, 1);
    [Tooltip("the most of its kind alive at once. 0 = no limit")]
    [Min(0)] public int maxAlive = 0;
    [Tooltip("a tighter Max Alive early in the run, until the minute below: for the ones that fill the screen with shots (a spider lily, a burner), so the middle of a run doesn't turn into a bullet hell before the player's build can take it. 0 = Max Alive all run")]
    [Min(0)] public int maxAliveEarly = 0;
    [Tooltip("the run minute Max Alive Early gives way to Max Alive")]
    [Min(0f)] public float earlyUntilMinute = 0f;

    // the most of its kind alive at once at this run minute, 0 = no limit
    public int MaxAliveAt(float minute) => maxAliveEarly > 0 && minute < earlyUntilMinute ? maxAliveEarly : maxAlive;

    [Header("Contact Damage")]
    public float contactTickInterval = 0.5f;
}
