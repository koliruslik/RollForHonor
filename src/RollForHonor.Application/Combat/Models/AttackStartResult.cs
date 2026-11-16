using RollForHonor.Domain.Resources.Models;

namespace RollForHonor.Application.Combat.Models;

/// <summary>Exposes a launchable impact context only after a new initiation commit succeeds.</summary>
public sealed record AttackStartResult
{
    /// <summary>Gets the acceptance result.</summary>
    public AttackStartStatus Status { get; }
    /// <summary>Gets a newly accepted attack, or null for rejection/replay.</summary>
    public AttackInstance? Attack { get; }
    /// <summary>Gets the accepted attack identity, including an optional replay receipt.</summary>
    public Guid? AttackId { get; }
    /// <summary>Gets calculated costs, if the attempt reached cost calculation.</summary>
    public ResourceCostResolution? Costs { get; }

    internal AttackStartResult(AttackStartStatus status, AttackInstance? attack = null,
        Guid? attackId = null, ResourceCostResolution? costs = null)
    {
        Status = status;
        Attack = attack;
        AttackId = attackId ?? attack?.Id;
        Costs = costs;
    }
}
