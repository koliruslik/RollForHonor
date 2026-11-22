using System.Collections.Immutable;
using RollForHonor.Domain.Items.Models;

namespace RollForHonor.Domain.Items.Equipment.Models;

/// <summary>Stores immutable occupied slots; one owned item identity cannot occupy multiple slots.</summary>
public sealed class EquipmentLoadout
{
    /// <summary>Gets an immutable loadout with every slot empty.</summary>
    public static EquipmentLoadout Empty { get; } = new();

    /// <summary>Gets occupied slots and their captured owned item values.</summary>
    public IReadOnlyDictionary<EquipmentSlot, ItemInstance> Slots { get; }

    /// <summary>Copies occupied slots and enforces structural identity constraints.</summary>
    /// <param name="slots">Optional unique defined slots and nonnull items with unique instance IDs.</param>
    /// <remarks>Compatibility with a particular rule set is validated by EquipmentLoadoutService and the state adapter.</remarks>
    /// <exception cref="ArgumentException">Slots repeat, a slot is undefined, an item is null or an item identity repeats.</exception>
    public EquipmentLoadout(IEnumerable<KeyValuePair<EquipmentSlot, ItemInstance>>? slots = null)
    {
        var entries = slots?.ToArray() ?? [];
        ValidateEntries(entries);

        Slots = entries.ToImmutableDictionary();
    }

    /// <summary>Reads an occupied slot without modifying the loadout.</summary>
    /// <param name="slot">A defined slot.</param>
    /// <returns>The item, or null when the slot is empty.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The slot is undefined.</exception>
    public ItemInstance? GetItem(EquipmentSlot slot)
    {
        if (!Enum.IsDefined(slot))
        {
            throw new ArgumentOutOfRangeException(nameof(slot));
        }

        return Slots.GetValueOrDefault(slot);
    }

    private static void ValidateEntries(KeyValuePair<EquipmentSlot, ItemInstance>[] entries)
    {
        if (entries.Any(pair => !Enum.IsDefined(pair.Key) || pair.Value is null))
        {
            throw new ArgumentException("Equipment requires defined slots and nonnull items.", nameof(entries));
        }

        var identityCount = entries
            .Select(pair => pair.Value.Id)
            .Distinct(StringComparer.Ordinal)
            .Count();

        if (identityCount != entries.Length)
        {
            throw new ArgumentException("An owned item cannot occupy more than one slot.", nameof(entries));
        }
    }
}
