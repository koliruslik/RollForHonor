using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Resources.Models;

namespace RollForHonor.Application.Combat.Models;

/// <summary>Launch inputs, cost reductions and atomic start result. No hit or resource debit is repeated.</summary>
/// <param name="RequestId">Stable launch command identity.</param>
/// <param name="SourceId">Participant attempting to launch.</param>
/// <param name="Definition">Attack payload, base costs and cooldown.</param>
/// <param name="State">Consistent launch read, absent when reading failed.</param>
/// <param name="Now">Observed game time, absent before time was read.</param>
/// <param name="Proposal">Prepared launch changes, absent before preparation completed.</param>
/// <param name="Result">Acceptance result, absent when execution throws.</param>
/// <param name="Error">Exception type and message, or null on normal completion.</param>
public sealed record AttackStartDiagnosticReport(Guid RequestId, CombatantId SourceId,
    AttackDefinition Definition, AttackInitiationState? State, TimeSpan? Now,
    AttackStartProposal? Proposal, AttackStartResult? Result, string? Error)
{
    /// <summary>Gets the serialized record discriminator for a launch attempt.</summary>
    public string Kind => "attackStart";
    /// <summary>Gets the diagnostic export schema version serialized on every launch record.</summary>
    public int SchemaVersion => 1;
    /// <summary>Gets projections from the accepted proposal, not a second live-state read.</summary>
    public IReadOnlyList<ResourceBalanceDiagnostic> ProposedResourceChanges => Proposal is null || State is null ? []
        : Proposal.Costs.Costs.Select(cost =>
        {
            var before = State.Resources.SingleOrDefault(value => value.Resource == cost.Resource)?.Amount ?? 0;
            return new ResourceBalanceDiagnostic(cost.Resource, before, cost.Amount, before - cost.Amount);
        }).ToArray();
    /// <summary>Gets whether the attempt threw or did not create a newly accepted attack.</summary>
    public bool IsError => Error is not null || Result?.Status != AttackStartStatus.Started;
}

/// <summary>Proposed resource debit; the enclosing result indicates whether it was applied.</summary>
/// <param name="Resource">Resource whose balance is projected.</param>
/// <param name="Before">Read balance before the proposed debit.</param>
/// <param name="Cost">Calculated nonnegative debit.</param>
/// <param name="After">Projected balance; the enclosing result determines whether it was applied.</param>
public sealed record ResourceBalanceDiagnostic(ResourceId Resource, decimal Before, decimal Cost, decimal After);
