using RollForHonor.Domain.Items.Models;

namespace RollForHonor.Domain.Rewards;

/// <summary>Describes an empty, gold-range or equipment-pool table result before item creation.</summary>
public sealed record LootOutcome
{
    /// <summary>Gets the inclusive gold range, or null for equipment or no reward.</summary>
    public GoldRange? Gold { get; }

    /// <summary>Gets the eligible item-pool identity, or null for gold or no reward.</summary>
    public string? ItemPoolId { get; }

    /// <summary>Gets the requested item rarity, distinct from later affix count and ranks.</summary>
    public ItemRarity? Rarity { get; }

    /// <summary>Gets whether the table result intentionally grants nothing.</summary>
    public bool IsEmpty => Gold is null && ItemPoolId is null;

    private LootOutcome(GoldRange? gold, string? itemPoolId, ItemRarity? rarity)
    {
        Gold = gold;
        ItemPoolId = itemPoolId;
        Rarity = rarity;
    }

    /// <summary>Defines a group's no-reward entry.</summary>
    /// <returns>An immutable result requiring no quantity or item-generation rolls.</returns>
    public static LootOutcome Nothing() => new(null, null, null);

    /// <summary>Defines a gold entry, whose amount needs a separate natural d100 roll.</summary>
    /// <param name="range">The nonnull inclusive amount bounds.</param>
    /// <returns>An immutable gold outcome definition.</returns>
    /// <exception cref="ArgumentNullException">The range is null.</exception>
    public static LootOutcome GoldEntry(GoldRange range)
    {
        ArgumentNullException.ThrowIfNull(range);

        return new(range, null, null);
    }

    /// <summary>Defines an equipment result for the separate item-creation service.</summary>
    /// <param name="poolId">The nonblank eligible base/pool identity.</param>
    /// <param name="rarity">The defined requested item rarity.</param>
    /// <returns>The captured request context, without generating any item or affixes.</returns>
    /// <exception cref="ArgumentException">The pool identity is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The rarity is undefined.</exception>
    public static LootOutcome EquipmentEntry(string poolId, ItemRarity rarity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolId);

        if (!Enum.IsDefined(rarity))
        {
            throw new ArgumentOutOfRangeException(nameof(rarity));
        }

        return new(null, poolId, rarity);
    }
}
