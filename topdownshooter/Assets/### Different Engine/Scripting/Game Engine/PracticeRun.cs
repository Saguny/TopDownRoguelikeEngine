using System.Collections;
using System.Linq;
using UnityEngine;

// practising a map's final boss: the map selection's Practice button, open once that boss has
// been met in a run (MapProgress.BossSeen). the run starts the way the duel comes at the end of a
// strong run: a full build at its caps (six weapons, six passives, the abilities, evolutions and
// all), then the clock set just short of the boss. nothing it does counts: no coins, no credit,
// no run towards Endless, no map cleared, and the favours waiting for the next real run stay
// waiting. the Duel test in the dev tools does the same
public static class PracticeRun
{
    public const float Lead = 3f;          // seconds from the jump to the boss coming

    public static IEnumerator Begin(GameLoopController loop)
    {
        // the player is up, their starting weapon held
        PlayerInventory inventory = null;
        for (int i = 0; i < 120 && inventory == null; i++)
        {
            yield return null;
            inventory = Object.FindFirstObjectByType<PlayerInventory>();
        }
        yield return null;
        if (inventory == null || loop == null) yield break;

        int picks = MaxBuild(inventory);
        inventory.SetLevel(Mathf.Max(inventory.CurrentLevel, 1 + picks));

        float was = loop.RunSeconds;
        if (loop.JumpToFinalBoss(Lead) && SpawnDirector.Active != null) SpawnDirector.Active.SkipTime(loop.RunSeconds - was);
        if (inventory.TryGetComponent(out PlayerHealth health) && health.Current < health.Max) health.Heal(health.Max - health.Current, false);
    }

    // every slot filled and every pick at its cap: what's held first, then new ones until the
    // slots are full. returns how many picks that took
    public static int MaxBuild(PlayerInventory inventory)
    {
        if (inventory == null) return 0;
        int picks = 0;
        picks += FillAndMax(inventory, UpgradeCategory.Weapon, inventory.WeaponSlots);
        picks += FillAndMax(inventory, UpgradeCategory.Passive, inventory.PassiveSlots);
        picks += FillAndMax(inventory, UpgradeCategory.Ability, inventory.AbilitySlots);
        return picks;
    }

    private static int FillAndMax(PlayerInventory inventory, UpgradeCategory kind, int slots)
    {
        var all = inventory.RunUpgrades.Where(u => u != null && !(u is GiftUpgrade) && u.Category == kind).ToList();
        int picks = 0, held = 0;
        foreach (var u in all.Where(u => u.Level > 0)) { picks += Max(inventory, u); held++; }
        foreach (var u in all.Where(u => u.Level == 0))
        {
            if (slots > 0 && held >= slots) break;
            picks += Max(inventory, u);
            held++;
        }
        return picks;
    }

    private static int Max(PlayerInventory inventory, UpgradeData u)
    {
        int picks = 0;
        for (int guard = 0; guard < 32 && u.CanOffer && !u.IsAtCap; guard++)
        {
            inventory.TakeUpgrade(u);
            picks++;
        }
        return picks;
    }
}
