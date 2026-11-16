namespace RollForHonor.Domain.Resources.Models;

/// <summary>Retains base costs, supplied reductions and final costs without changing resource pools.</summary>
public sealed record ResourceCostResolution
{
    /// <summary>Gets copied base costs.</summary>
    public IReadOnlyList<ResourceAmount> BaseCosts { get; }
    /// <summary>Gets copied reduction inputs.</summary>
    public IReadOnlyList<ResourceCostReduction> Reductions { get; }
    /// <summary>Gets the exact calculated costs.</summary>
    public IReadOnlyList<ResourceAmount> Costs { get; }

    internal ResourceCostResolution(ResourceAmount[] baseCosts, ResourceCostReduction[] reductions, ResourceAmount[] costs)
    {
        BaseCosts = Array.AsReadOnly(baseCosts.ToArray());
        Reductions = Array.AsReadOnly(reductions.ToArray());
        Costs = Array.AsReadOnly(costs.ToArray());
    }
}
