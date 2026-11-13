using RollForHonor.Application.Combat.Models;
using RollForHonor.Application.Combat.Ports;
using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Application.Combat.Services;

/// <summary>Assembles a versioned snapshot from immutable combatant views supplied by an adapter.</summary>
/// <remarks>Does not introduce equipment, modifier-stacking or derived-stat gameplay rules.</remarks>
public sealed class CombatSnapshotBuilder : ICombatSnapshotBuilder
{
    /// <inheritdoc />
    public CombatStateSnapshot Build(CombatStateRead state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return new CombatStateSnapshot(state.Version, state.Source, state.Target);
    }
}
