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
        bool evolved = Data.IsEvolved(Level);

        // evolved it looses a volley every Evolved Interval, whatever the Cooldown stat says, and
        // every arrow shines
        shooter.SetLevelStats(s.arrows, evolved ? s.damage * Data.evolvedDamageMul : s.damage, evolved ? Data.evolvedInterval : s.cooldown, s.speed);
        shooter.Unbound = evolved;
        shooter.Shine = evolved ? Data : null;
        shooter.Armed = true;
        shooter.Source = this;
    }
}
