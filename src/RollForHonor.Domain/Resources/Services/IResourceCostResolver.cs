using RollForHonor.Domain.Resources.Models;

namespace RollForHonor.Domain.Resources.Services;

/// <summary>Calculates supplied resource costs without spending live resources.</summary>
public interface IResourceCostResolver
{
    /// <summary>Applies reductions only to their matching base resource costs.</summary>
    /// <param name="baseCosts">Unique, nonnegative resource costs.</param>
    /// <param name="reductions">Explicitly selected reduction modifiers.</param>
    /// <returns>The base inputs and calculated costs.</returns>
    ResourceCostResolution Resolve(IReadOnlyList<ResourceAmount> baseCosts, IReadOnlyList<ResourceCostReduction> reductions);
}
