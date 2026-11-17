using RollForHonor.Domain.Combat.Effects.Models;
using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Combat.Resolution.Services;
using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Application.Combat.Models;

/// <summary>Returns one independent periodic calculation and its separate publication status, without a fictitious attack.</summary>
public sealed record PeriodicTickResult
{
    /// <summary>Gets this attempt's fresh resolution identity.</summary>
    public Guid ResolutionId { get; }
    /// <summary>Gets the recipient identity.</summary>
    public CombatantId TargetId { get; }
    /// <summary>Gets the requested active effect identity.</summary>
    public EffectInstanceId EffectId { get; }
    /// <summary>Gets captured-source/current-recipient inputs, or null when preconditions reject the tick.</summary>
    public CombatRequest? Request { get; }
    /// <summary>Gets the calculated result or explicit rejection.</summary>
    public ICombatResolutionOutcome Outcome { get; }
    /// <summary>Gets publication status, or null when calculation was rejected.</summary>
    public CombatCommitStatus? CommitStatus { get; }
    /// <summary>Gets whether this invocation newly changed session state.</summary>
    public bool IsCommitted => CommitStatus == CombatCommitStatus.Applied;

    internal PeriodicTickResult(Guid resolutionId, CombatantId targetId, EffectInstanceId effectId,
        CombatRequest? request, ICombatResolutionOutcome outcome, CombatCommitStatus? commitStatus)
    {
        ResolutionId = resolutionId;
        TargetId = targetId;
        EffectId = effectId;
        Request = request;
        Outcome = outcome;
        CommitStatus = commitStatus;
    }
}
