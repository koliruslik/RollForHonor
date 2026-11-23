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
        new(EquipmentSlot.RingRight, "ring"),
        new(EquipmentSlot.OffHand, "offhand")
    ], [new("quiver", "bow"), new("shield", "one-handed", true)]);

    private readonly ImmutableDictionary<EquipmentSlot, string> _requiredTags;
    private readonly ImmutableArray<EquipmentPairRule> _handPairs;

    /// <summary>Copies one required tag for every defined slot.</summary>
    /// <param name="requiredTags">Unique defined slots and nonblank ordinal tags; all slots must be included.</param>
    /// <param name="handPairs">Optional offhand/main-hand pairs; absent allows any tag-compatible offhand.</param>
    /// <exception cref="ArgumentNullException">The mapping is null.</exception>
    /// <exception cref="ArgumentException">The mapping is incomplete, duplicated or contains invalid slots/tags, or a hand pair is null.</exception>
    public EquipmentRules(IEnumerable<KeyValuePair<EquipmentSlot, string>> requiredTags, IEnumerable<EquipmentPairRule>? handPairs = null)
    {
        ArgumentNullException.ThrowIfNull(requiredTags);

        _requiredTags = requiredTags.ToImmutableDictionary();
        _handPairs = handPairs?.ToImmutableArray() ?? [];

        if (_requiredTags.Count != Enum.GetValues<EquipmentSlot>().Length
            || _requiredTags.Any(pair => !Enum.IsDefined(pair.Key) || string.IsNullOrWhiteSpace(pair.Value)))
        {
            throw new ArgumentException("Define a nonblank tag for every equipment slot.", nameof(requiredTags));
        }

        if (_handPairs.Any(pair => pair is null))
        {
            throw new ArgumentException("Hand pairing rules cannot contain null.", nameof(handPairs));
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

    /// <summary>Checks slot tags and the offhand's compatibility with the current main-hand weapon.</summary>
    /// <param name="loadout">The current immutable equipment.</param>
    /// <param name="item">The item to place.</param>
    /// <param name="slot">The defined destination slot.</param>
    /// <returns>True when the slot and any applicable hand pairing permit the item.</returns>
    /// <exception cref="ArgumentNullException">The loadout or item is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The slot is undefined.</exception>
    public bool CanEquip(EquipmentLoadout loadout, ItemInstance item, EquipmentSlot slot)
    {
        ArgumentNullException.ThrowIfNull(loadout);

        if (!CanEquip(item, slot))
        {
            return false;
        }

        if (slot != EquipmentSlot.OffHand || _handPairs.IsEmpty)
        {
            return true;
        }

        var mainHand = loadout.GetItem(EquipmentSlot.Weapon);

        return _handPairs.Any(pair => item.Base.Tags.Contains(pair.OffHandTag)
            && (mainHand is null ? pair.AllowsEmptyMainHand : mainHand.Base.Tags.Contains(pair.MainHandTag)));
    }
}
