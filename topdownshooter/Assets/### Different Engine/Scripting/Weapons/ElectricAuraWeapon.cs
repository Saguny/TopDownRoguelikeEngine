using UnityEngine;

// the Electrical Aura's level applied to the Aura under the player, which does the shocking
public class ElectricAuraWeapon : Weapon<ElectricAuraData>
{
    private Aura aura;

    protected override void OnLevelChanged()
    {
        if (aura == null) aura = GetComponentInChildren<Aura>(true);
        if (aura == null || Data == null)
        {
            Debug.LogWarning("Electrical Aura: the player has no Aura to switch on", this);
            return;
        }

        var s = Data.At(Level);
        bool grew = s.radius > aura.radius + 0.001f;
        aura.damage = s.damage;
        aura.radius = s.radius;
        aura.damageInterval = s.interval;
        aura.source = this;

        if (!aura.gameObject.activeSelf) aura.gameObject.SetActive(true);
        else if (grew) aura.OnRadiusUpgraded();
    }
}
