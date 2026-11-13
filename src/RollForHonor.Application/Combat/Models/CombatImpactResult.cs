using RollForHonor.Domain.Combat.Resolution.Services;

namespace RollForHonor.Application.Combat.Models;

/// <summary>Keeps a calculated Domain outcome separate from whether it was committed.</summary>
public sealed record CombatImpactResult
{
    /// <summary>Gets the originating attack identity, shared by its separate impacts.</summary>
    public Guid AttackId { get; }
    /// <summary>Gets the unique identifier of this impact attempt.</summary>
    public Guid ResolutionId { get; }
    /// <summary>Gets the Domain rejection or calculated proposal.</summary>
    public ICombatResolutionOutcome Outcome { get; }
    /// <summary>Gets the commit result, or null if Domain rejected the request.</summary>
    public CombatCommitStatus? CommitStatus { get; }
    /// <summary>Gets whether this invocation published a new state transition.</summary>
    public bool IsCommitted => CommitStatus == CombatCommitStatus.Applied;

    internal CombatImpactResult(Guid attackId, Guid resolutionId, ICombatResolutionOutcome outcome,
        CombatCommitStatus? commitStatus)
    {
        AttackId = attackId;
        ResolutionId = resolutionId;
        Outcome = outcome;
        CommitStatus = commitStatus;
    }
}
