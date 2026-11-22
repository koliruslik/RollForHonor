using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Items.Equipment.Models;

namespace RollForHonor.Application.Equipment.Models;

/// <summary>Requests one version-checked loadout change; the adapter derives stats from its authoritative non-equipment inputs.</summary>
public sealed class EquipmentChangeProposal
{
    /// <summary>Gets the expected session version.</summary>
    public long ExpectedVersion { get; }

    /// <summary>Gets the actor whose loadout is changing.</summary>
    public CombatantId OwnerId { get; }

    /// <summary>Gets the complete proposed occupied slots.</summary>
    public EquipmentLoadout Loadout { get; }

    /// <summary>Captures a proposed immutable loadout without publishing any part of it.</summary>
    /// <param name="expectedVersion">The nonnegative version used for the equipment read.</param>
    /// <param name="ownerId">The stat-managed actor.</param>
    /// <param name="loadout">The proposed complete equipment.</param>
    /// <exception cref="ArgumentNullException">An input is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The expected version is negative.</exception>
    public EquipmentChangeProposal(long expectedVersion, CombatantId ownerId, EquipmentLoadout loadout)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(expectedVersion);
        ArgumentNullException.ThrowIfNull(ownerId);
        ArgumentNullException.ThrowIfNull(loadout);

        ExpectedVersion = expectedVersion;
        OwnerId = ownerId;
        Loadout = loadout;
    }
}
