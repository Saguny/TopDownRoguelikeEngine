using UnityEngine;

// an elite: a stronger copy of a regular enemy, a little bigger than its kind, many times as tough,
// and ringed in a white-blue outline (SilhouetteOutline) so it's picked out of the crowd at a
// glance. pooled enemies come back plain
public class EliteOutline : SilhouetteOutline
{
    public static readonly Color Tint = new Color32(0xc4, 0xf2, 0xff, 0xff);
    protected override Color OutlineColor => Tint;
    public const float Size = 1.25f;
    public const float Health = 25f;

    // makes an enemy just spawned an elite: bigger, tougher, outlined, and worth more
    public static void Promote(GameObject enemy, float size = Size, float health = Health, int bonusWen = 0)
    {
        if (enemy == null || enemy.TryGetComponent(out EliteOutline _)) return;
        enemy.transform.localScale *= size;
        if (enemy.TryGetComponent(out EnemyHealth h))
        {
            h.SetScaled(h.Max * health);
            h.bonusWenDrops = bonusWen;
            h.alwaysDropHeal = true;
        }
        enemy.AddComponent<EliteOutline>();
    }
}
