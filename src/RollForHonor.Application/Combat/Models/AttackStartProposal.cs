using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Resources.Models;

namespace RollForHonor.Application.Combat.Models;

/// <summary>Proposes one atomic resource deduction, cooldown start and request receipt.</summary>
public sealed record AttackStartProposal
{
    /// <summary>Gets the stable command identity used to prevent duplicate launches.</summary>
    public Guid RequestId { get; }
    /// <summary>Gets the identity of the impact context to expose after successful commit.</summary>
    public Guid AttackId { get; }
    /// <summary>Gets the source being charged.</summary>
    public CombatantId SourceId { get; }
    /// <summary>Gets the content/cooldown key.</summary>
    public string DefinitionKey { get; }
    /// <summary>Gets the expected version of resources, cooldown and source data.</summary>
    public long ExpectedVersion { get; }
    /// <summary>Gets the immutable cost calculation.</summary>
    public ResourceCostResolution Costs { get; }
    /// <summary>Gets the new cooldown deadline.</summary>
    public TimeSpan ReadyAt { get; }

    internal AttackStartProposal(Guid requestId, Guid attackId, CombatantId sourceId, string definitionKey,
        long expectedVersion, ResourceCostResolution costs, TimeSpan readyAt)
    {
        RequestId = requestId;
        AttackId = attackId;
        SourceId = sourceId;
        DefinitionKey = definitionKey;
        ExpectedVersion = expectedVersion;
        Costs = costs;
        ReadyAt = readyAt;
    }
}
