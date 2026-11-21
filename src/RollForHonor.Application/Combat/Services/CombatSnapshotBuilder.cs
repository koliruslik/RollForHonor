using RollForHonor.Application.Combat.Models;
using RollForHonor.Application.Combat.Ports;
using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Application.Combat.Services;

/// <summary>Assembles a versioned snapshot from immutable combatant views supplied by an adapter.</summary>
/// <remarks>The owning adapter publishes stat projections with current state at one version; this builder does not reread equipment or recalculate modifiers.</remarks>
public sealed class CombatSnapshotBuilder : ICombatSnapshotBuilder
{
    /// <inheritdoc />
    public CombatStateSnapshot Build(CombatStateRead state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return new CombatStateSnapshot(state.Version, state.Source, state.Target);
    }
}
