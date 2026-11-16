using RollForHonor.Application.Combat.Models;
using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Application.Combat.Ports;

/// <summary>Provides consistent launch reads and atomic cost/cooldown/request-receipt commits.</summary>
public interface IAttackInitiationAdapter
{
    /// <summary>Reads source, balances, cooldown and any receipt for the stable request identity.</summary>
    /// <param name="sourceId">The source attempting to launch.</param>
    /// <param name="definitionKey">The source-local attack/cooldown key.</param>
    /// <param name="requestId">The stable command identity, reused only for the same request.</param>
    /// <returns>A consistent read; replay receipts retain the originally accepted attack identity.</returns>
    AttackInitiationState ReadForStart(CombatantId sourceId, string definitionKey, Guid requestId);

    /// <summary>Atomically publishes resource deductions, cooldown and the request receipt.</summary>
    /// <param name="proposal">The complete initiation proposal.</param>
    /// <returns>Applied, stale-version or already-applied status.</returns>
    /// <remarks>
    /// No partial deductions, cooldown changes or receipts may remain on failure/exception.
    /// Implementations recheck request identity, version and affordability before publishing.
    /// Initiation and impact adapters over the same live state must use compatible versions.
    /// </remarks>
    CombatCommitStatus TryStart(AttackStartProposal proposal);
}
