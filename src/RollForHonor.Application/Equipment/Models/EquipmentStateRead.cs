using System.Collections.Immutable;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Items.Equipment.Configuration;
using RollForHonor.Domain.Items.Equipment.Models;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Domain.Stats;

namespace RollForHonor.Application.Equipment.Models;

/// <summary>Captures owned items, loadout, non-equipment inputs and compatibility rules at one session version.</summary>
public sealed class EquipmentStateRead
{
    /// <summary>Gets the version which must remain current when the proposal is published.</summary>
    public long Version { get; }

    /// <summary>Gets the actor whose items are visible in this read.</summary>
    public CombatantId OwnerId { get; }

    /// <summary>Gets immutable canonical items owned by this actor, indexed by instance ID.</summary>
    public IReadOnlyDictionary<string, ItemInstance> OwnedItems { get; }

    /// <summary>Gets the current immutable equipment.</summary>
    public EquipmentLoadout Loadout { get; }

    /// <summary>Gets inputs which must survive replacing or removing equipment.</summary>
    public StatProfile NonEquipmentProfile { get; }

    /// <summary>Gets the slot rules used by the owning adapter.</summary>
    public EquipmentRules Rules { get; }

    /// <summary>Copies a consistent adapter read without exposing mutable collections.</summary>
    /// <param name="version">The nonnegative session version.</param>
    /// <param name="ownerId">The owning stat-managed actor.</param>
    /// <param name="ownedItems">Nonnull items with unique instance IDs.</param>
    /// <param name="loadout">The current captured equipment.</param>
    /// <param name="nonEquipmentProfile">The input profile excluding equipment contributions.</param>
    /// <param name="rules">The immutable active compatibility rules.</param>
    /// <exception cref="ArgumentNullException">An input is null.</exception>
    /// <exception cref="ArgumentException">An item is null or instance IDs repeat.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The version is negative.</exception>
    public EquipmentStateRead(long version, CombatantId ownerId, IEnumerable<ItemInstance> ownedItems,
        EquipmentLoadout loadout, StatProfile nonEquipmentProfile, EquipmentRules rules)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(version);
        ArgumentNullException.ThrowIfNull(ownerId);
        ArgumentNullException.ThrowIfNull(ownedItems);
        ArgumentNullException.ThrowIfNull(loadout);
        ArgumentNullException.ThrowIfNull(nonEquipmentProfile);
        ArgumentNullException.ThrowIfNull(rules);

        var items = ownedItems.ToArray();

        if (items.Any(item => item is null))
        {
            throw new ArgumentException("Owned items cannot contain null.", nameof(ownedItems));
        }

        Version = version;
        OwnerId = ownerId;
        OwnedItems = items.ToImmutableDictionary(item => item.Id, StringComparer.Ordinal);
        Loadout = loadout;
        NonEquipmentProfile = nonEquipmentProfile;
        Rules = rules;
    }
}
