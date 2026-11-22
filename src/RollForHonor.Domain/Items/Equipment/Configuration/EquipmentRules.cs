using System.Collections.Immutable;
using RollForHonor.Domain.Items.Equipment.Models;
using RollForHonor.Domain.Items.Models;

namespace RollForHonor.Domain.Items.Equipment.Configuration;

/// <summary>Maps slots to item-base tags without embedding compatibility into item instances.</summary>
public sealed class EquipmentRules
{
    /// <summary>Gets the initial slot tags; both ring slots require ring and torso armor requires body-armor.</summary>
    public static EquipmentRules Default { get; } = new(
    [
        new(EquipmentSlot.Weapon, "weapon"),
        new(EquipmentSlot.BodyArmor, "body-armor"),
        new(EquipmentSlot.Helmet, "helmet"),
        new(EquipmentSlot.Boots, "boots"),
        new(EquipmentSlot.Amulet, "amulet"),
        new(EquipmentSlot.RingLeft, "ring"),
        new(EquipmentSlot.RingRight, "ring")
    ]);

    private readonly ImmutableDictionary<EquipmentSlot, string> _requiredTags;

    /// <summary>Copies one required tag for every defined slot.</summary>
    /// <param name="requiredTags">Unique defined slots and nonblank ordinal tags; all slots must be included.</param>
    /// <exception cref="ArgumentNullException">The mapping is null.</exception>
    /// <exception cref="ArgumentException">The mapping is incomplete, duplicated or contains invalid slots/tags.</exception>
    public EquipmentRules(IEnumerable<KeyValuePair<EquipmentSlot, string>> requiredTags)
    {
        ArgumentNullException.ThrowIfNull(requiredTags);

        _requiredTags = requiredTags.ToImmutableDictionary();

        if (_requiredTags.Count != Enum.GetValues<EquipmentSlot>().Length
            || _requiredTags.Any(pair => !Enum.IsDefined(pair.Key) || string.IsNullOrWhiteSpace(pair.Value)))
        {
            throw new ArgumentException("Define a nonblank tag for every equipment slot.", nameof(requiredTags));
        }
    }

    /// <summary>Checks the captured item's base tags against one slot.</summary>
    /// <param name="item">The owned item to inspect.</param>
    /// <param name="slot">A defined target slot.</param>
    /// <returns>True when the required tag is present, using ordinal tag matching.</returns>
    /// <exception cref="ArgumentNullException">The item is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The slot is undefined.</exception>
    public bool CanEquip(ItemInstance item, EquipmentSlot slot)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (!Enum.IsDefined(slot))
        {
            throw new ArgumentOutOfRangeException(nameof(slot));
        }

        return item.Base.Tags.Contains(_requiredTags[slot]);
    }
}
