using System.Collections.Generic;
using UnityEngine;

// every upgrade the level up can offer, for screens outside a run that need to list them (the
// map selection's ticket picker, FavorPicker): the run's own list lives on the player
// (PlayerInventory), which the menu doesn't have. Resources/UpgradeCatalog. in the editor it
// fills itself whenever an upgrade asset is added, removed or taken out of the pool
// (UpgradeCatalogRefresher), so it never needs setting up by hand
[CreateAssetMenu(menuName = "Rogue/Upgrade Catalog", fileName = "UpgradeCatalog")]
public class UpgradeCatalog : ScriptableObject
{
    [Tooltip("weapons, then passives, then abilities, each by name. filled automatically")]
    public List<UpgradeData> upgrades = new List<UpgradeData>();

    private static UpgradeCatalog loaded;

    public static UpgradeCatalog Load()
    {
        if (loaded == null) loaded = Resources.Load<UpgradeCatalog>("UpgradeCatalog");
        return loaded;
    }

    // what a list should hold: in the pool, not a gift, in the catalog's order
    public static bool Belongs(UpgradeData u) => u != null && u.includeInPool && !(u is GiftUpgrade);

    public static int Order(UpgradeData a, UpgradeData b)
    {
        int c = ((int)a.Category).CompareTo((int)b.Category);
        return c != 0 ? c : string.CompareOrdinal(a.GetBaseTitle(), b.GetBaseTitle());
    }
}
