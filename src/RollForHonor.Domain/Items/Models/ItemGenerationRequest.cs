namespace RollForHonor.Domain.Items.Models;

/// <summary>Supplies generation context; rarity, occupied count and available tier power are independent inputs.</summary>
public sealed class ItemGenerationRequest
{
    /// <summary>Gets the instance identity allocated outside random stat generation.</summary>
    public string InstanceId { get; }

    /// <summary>Gets the base identifier in the supplied catalog.</summary>
    public string BaseId { get; }

    /// <summary>Gets the explicitly requested rarity.</summary>
    public ItemRarity Rarity { get; }

    /// <summary>Gets the requested number of occupied affix slots, not the rarity's full capacity.</summary>
    public int AffixCount { get; }

    /// <summary>Gets a nonnegative tier-eligibility result supplied by the caller; it is not consumed per affix.</summary>
    public int AvailablePower { get; }

    /// <summary>Gets the positive item-level eligibility context; it does not derive from Luck.</summary>
    public int ItemLevel { get; }

    /// <summary>Captures generation inputs without guessing d20/Luck or rarity-drop rules.</summary>
    /// <param name="instanceId">A nonblank owned item identity.</param>
    /// <param name="baseId">A nonblank catalog base identifier.</param>
    /// <param name="rarity">A defined explicit rarity.</param>
    /// <param name="affixCount">A nonnegative requested affix count.</param>
    /// <param name="availablePower">A nonnegative tier-eligibility cap, independent of item level.</param>
    /// <param name="itemLevel">The positive item level; existing callers default to level 1.</param>
    /// <exception cref="ArgumentException">An identifier is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Rarity is undefined, count/power is negative or item level is not positive.</exception>
    public ItemGenerationRequest(string instanceId, string baseId, ItemRarity rarity, int affixCount, int availablePower, int itemLevel = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseId);
        ArgumentOutOfRangeException.ThrowIfNegative(affixCount);
        ArgumentOutOfRangeException.ThrowIfNegative(availablePower);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(itemLevel);

        if (!Enum.IsDefined(rarity))
        {
            throw new ArgumentOutOfRangeException(nameof(rarity));
        }

        InstanceId = instanceId;
        BaseId = baseId;
        Rarity = rarity;
        AffixCount = affixCount;
        AvailablePower = availablePower;
        ItemLevel = itemLevel;
    }
}
