using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// keeps Resources/UpgradeCatalog listing every upgrade in the pool: after any .asset is imported,
// moved or deleted, the catalog is rebuilt and saved if it changed
public class UpgradeCatalogRefresher : AssetPostprocessor
{
    private const string CatalogPath = "Assets/### Different Engine/Resources/UpgradeCatalog.asset";

    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (Touches(imported) || Touches(deleted) || Touches(moved))
        {
            EditorApplication.delayCall -= Refresh;
            EditorApplication.delayCall += Refresh;
        }
    }

    private static bool Touches(string[] paths)
    {
        foreach (var p in paths)
            if (p.EndsWith(".asset") && p != CatalogPath) return true;
        return false;
    }

    [MenuItem("Tools/Upgrades/Refresh Upgrade Catalog")]
    public static void Refresh()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var catalog = AssetDatabase.LoadAssetAtPath<UpgradeCatalog>(CatalogPath);
        if (catalog == null) return;

        var found = new List<UpgradeData>();
        foreach (var guid in AssetDatabase.FindAssets("t:UpgradeData"))
        {
            var u = AssetDatabase.LoadAssetAtPath<UpgradeData>(AssetDatabase.GUIDToAssetPath(guid));
            if (UpgradeCatalog.Belongs(u)) found.Add(u);
        }
        found.Sort(UpgradeCatalog.Order);

        if (Same(catalog.upgrades, found)) return;
        catalog.upgrades = found;
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssetIfDirty(catalog);
    }

    private static bool Same(List<UpgradeData> a, List<UpgradeData> b)
    {
        if (a == null || a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
        return true;
    }
}
