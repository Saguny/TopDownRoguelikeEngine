using UnityEngine;

public enum GiftKind { Coins, Heal }

// a gift the level up offers once there's nothing left to level: a string of coins or a peach of
// immortality, the way Vampire Survivors falls back to gold and a chicken. never in the pool, never
// capped, never held: taking one just pays out (PlayerInventory). a fortune envelope with nothing
// left to level pays out the same way
[CreateAssetMenu(menuName = "Rogue/Gift Upgrade", fileName = "Gift_")]
public class GiftUpgrade : UpgradeData
{
    [Header("Gift")]
    public GiftKind kind;
    // coins are envelopes' now (a common pays 100 to 200): the bag is a common envelope's worth
    public const int DefaultCoins = 150;

    [Tooltip("coins paid, before Greed")]
    [Min(0)] public int coins = DefaultCoins;
    [Tooltip("share of max health restored")]
    [Range(0f, 1f)] public float healShare = 0.3f;

    public override int MaxLevel => int.MaxValue;
    public override bool TakesSlot => false;
    public override string CategoryLabel => "Gift";
    public override string GetLevelProgress() => "";

    public override string GetDisplayDescription() => kind == GiftKind.Coins
        ? $"+{Coins.WithGreed(coins)} coins for the shop."
        : $"Restores {healShare * 100f:0}% of your health.";

    // pays it out to the player
    public void Give(GameObject player)
    {
        if (kind == GiftKind.Coins) Coins.Gift(coins);
        else if (player != null && player.TryGetComponent(out PlayerHealth health))
            health.Heal(health.Max * healShare);
    }

    // one made on the spot, for a scene whose inventory wasn't given the assets
    public static GiftUpgrade Make(GiftKind kind)
    {
        var gift = CreateInstance<GiftUpgrade>();
        gift.kind = kind;
        gift.title = kind == GiftKind.Coins ? "String of Wen" : "Peach of Immortality";
        gift.includeInPool = false;
        var lib = VfxLibrary.Get;
        if (lib != null)
        {
            var frames = kind == GiftKind.Coins ? lib.coinIcon : lib.peachIcon;
            if (frames != null && frames.Length > 0)
            {
                gift.icon = frames[0];
                gift.iconFrames = frames;
            }
        }
        return gift;
    }

    protected virtual void OnEnable()
    {
        includeInPool = false;
        overcharge = false;
        type = UpgradeType.BowUnlock;      // a type no stat reads, so the level up's preview ignores it
    }
}
