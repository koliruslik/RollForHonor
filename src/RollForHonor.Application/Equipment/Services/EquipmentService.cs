using RollForHonor.Application.Equipment.Models;
using RollForHonor.Application.Equipment.Ports;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Items.Equipment.Models;
using RollForHonor.Domain.Items.Equipment.Services;

namespace RollForHonor.Application.Equipment.Services;

/// <summary>Coordinates equip/unequip requests without owning live combat arithmetic or storage.</summary>
public sealed class EquipmentService
{
    private readonly IEquipmentStateAdapter _state;

    /// <summary>Uses a shared versioned equipment/stat adapter.</summary>
    /// <param name="state">The atomic ownership and equipment port.</param>
    /// <exception cref="ArgumentNullException">The adapter is null.</exception>
    public EquipmentService(IEquipmentStateAdapter state)
    {
        ArgumentNullException.ThrowIfNull(state);

        _state = state;
    }

    /// <summary>Equips an owned instance, replacing one slot without accumulating old bonuses.</summary>
    /// <param name="ownerId">The owning stat-managed actor.</param>
    /// <param name="itemId">The nonblank owned item identity.</param>
    /// <param name="slot">The defined target slot.</param>
    /// <returns>The applied/no-op/conflict status, or an ownership/compatibility rejection.</returns>
    /// <exception cref="ArgumentNullException">The owner is null.</exception>
    /// <exception cref="ArgumentException">The item ID is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The slot is undefined.</exception>
    /// <exception cref="KeyNotFoundException">The actor is not stat-managed.</exception>
    /// <exception cref="InvalidOperationException">The port returns inconsistent data or an invalid commit status.</exception>
    /// <exception cref="OverflowException">Stat projection or publication overflows.</exception>
    public EquipmentChangeStatus EquipItem(CombatantId ownerId, string itemId, EquipmentSlot slot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);
        var state = Read(ownerId, slot);

        if (!state.OwnedItems.TryGetValue(itemId, out var item))
        {
            return EquipmentChangeStatus.ItemNotOwned;
        }

        if (!state.Rules.CanEquip(item, slot))
        {
            return EquipmentChangeStatus.IncompatibleSlot;
        }

        if (state.Loadout.Slots.Any(pair => pair.Key != slot && pair.Value.Id == itemId))
        {
            return EquipmentChangeStatus.AlreadyEquippedElsewhere;
        }

        var loadout = new EquipmentLoadoutService(state.Rules).Equip(state.Loadout, item, slot);

        return Commit(state, loadout);
    }

    /// <summary>Empties one slot; removing an already empty slot is a version-checked no-op.</summary>
    /// <param name="ownerId">The owning stat-managed actor.</param>
    /// <param name="slot">The defined slot to empty.</param>
    /// <returns>Applied, Unchanged or VersionConflict.</returns>
    /// <exception cref="ArgumentNullException">The owner is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The slot is undefined.</exception>
    /// <exception cref="KeyNotFoundException">The actor is not stat-managed.</exception>
    /// <exception cref="InvalidOperationException">The port returns inconsistent data or an invalid commit status.</exception>
    /// <exception cref="OverflowException">Stat projection or publication overflows.</exception>
    public EquipmentChangeStatus UnequipItem(CombatantId ownerId, EquipmentSlot slot)
    {
        var state = Read(ownerId, slot);
        var loadout = new EquipmentLoadoutService(state.Rules).Unequip(state.Loadout, slot);

        return Commit(state, loadout);
    }

    private EquipmentStateRead Read(CombatantId ownerId, EquipmentSlot slot)
    {
        ArgumentNullException.ThrowIfNull(ownerId);

        if (!Enum.IsDefined(slot))
        {
            throw new ArgumentOutOfRangeException(nameof(slot));
        }

        var state = _state.ReadEquipment(ownerId);

        if (state is null || state.OwnerId != ownerId)
        {
            throw new InvalidOperationException("The equipment read does not match the requested owner.");
        }

        return state;
    }

    private EquipmentChangeStatus Commit(EquipmentStateRead state, EquipmentLoadout loadout)
    {
        var status = _state.TryCommitEquipment(new EquipmentChangeProposal(state.Version, state.OwnerId, loadout));

        return status switch
        {
            EquipmentChangeStatus.Applied or EquipmentChangeStatus.Unchanged or EquipmentChangeStatus.VersionConflict => status,
            _ => throw new InvalidOperationException("The equipment adapter returned an invalid commit status.")
        };
    }
}
