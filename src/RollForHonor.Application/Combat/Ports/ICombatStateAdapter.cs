using RollForHonor.Application.Combat.Models;
using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Application.Combat.Ports;

/// <summary>Exposes consistent state reads and atomic version-checked commits without an engine dependency.</summary>
public interface ICombatStateAdapter
{
    /// <summary>Reads current participants at one consistent version, with no mutation.</summary>
    /// <param name="sourceId">The source to read afresh for this impact.</param>
    /// <param name="targetId">The current target.</param>
    /// <returns>Immutable participant data; self-target reads share the participant view.</returns>
    CombatStateRead Read(CombatantId sourceId, CombatantId targetId);

    /// <summary>Atomically publishes a complete proposal if its version is current and identity is new.</summary>
    /// <param name="resolution">The complete Domain proposal.</param>
    /// <returns>Applied, stale-version or already-applied status.</returns>
    /// <remarks>
    /// Failure, rejection or an exception must leave the entire proposal unpublished. Implementations
    /// validate previous values and prepare all changes before publishing. Deduplication is by
    /// resolution identity, not by attack or target. Successful commit makes subsequent reads current.
    /// </remarks>
    CombatCommitStatus TryCommit(CombatResolution resolution);
}
