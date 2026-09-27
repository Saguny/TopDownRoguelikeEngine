using UnityEngine;

// the Bow's level applied to the player's AutoShooter, which aims and fires the arrows
public class BowWeapon : Weapon<BowData>
{
    private AutoShooter shooter;

    protected override void OnLevelChanged()
    {
        if (shooter == null) shooter = GetComponent<AutoShooter>();
        if (shooter == null || Data == null)
        {
            Debug.LogWarning("Bow: the player has no AutoShooter to fire it", this);
            return;
        }

        var s = Data.At(Level);
        shooter.SetLevelStats(s.arrows, s.damage, s.cooldown, s.speed);
        shooter.Armed = true;
        shooter.Source = this;
    }
}
