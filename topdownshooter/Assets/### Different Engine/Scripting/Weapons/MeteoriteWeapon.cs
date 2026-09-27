using UnityEngine;

// the Meteorite's level applied to the player's AOEAttack, which picks the spots and drops them
public class MeteoriteWeapon : Weapon<MeteoriteData>
{
    private AOEAttack attack;

    protected override void OnLevelChanged()
    {
        if (attack == null) attack = GetComponentInChildren<AOEAttack>(true);
        if (attack == null || Data == null)
        {
            Debug.LogWarning("Meteorite: the player has no AOEAttack to drop meteors", this);
            return;
        }

        var s = Data.At(Level);
        attack.Configure(s.cooldown, s.damage, s.radius, s.meteors, s.smart);
        attack.Source = this;
        if (!attack.IsActive) attack.Activate();
    }
}
