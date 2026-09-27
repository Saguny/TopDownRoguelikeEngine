using UnityEngine;

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
    [Header("What harms it (the weapons' attack classes)")]
    [Tooltip("damage it takes from Physical weapons (arrows, blades): 0.6 = blades pass through it, 1.3 = soft to them")]
    [Range(0.25f, 2f)] public float physicalTaken = 1f;
    [Tooltip("damage it takes from Magical weapons (talismans, spells, summoned things)")]
    [Range(0.25f, 2f)] public float magicalTaken = 1f;

    [Header("Contact Damage")]
    public float contactTickInterval = 0.5f;
}
