using RollForHonor.Application.Combat.Models;
using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Application.Combat.Ports;

/// <summary>Assembles per-impact calculation snapshots from a consistent state read.</summary>
public interface ICombatSnapshotBuilder
{
    /// <summary>Builds an immutable snapshot retaining the supplied version and participant identities.</summary>
    /// <param name="state">The current adapter read.</param>
    /// <returns>The snapshot to pass to Domain and pipeline composition.</returns>
    CombatStateSnapshot Build(CombatStateRead state);
}
