using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Resources.Models;

namespace RollForHonor.Application.Combat.Ports;

/// <summary>Selects applicable cost reductions from current source data, outside Domain calculation.</summary>
public interface IResourceCostModifierProvider
{
    /// <summary>Selects explicit cost reduction inputs for one launch.</summary>
    /// <param name="source">The current, consistently read source and its effects.</param>
    /// <returns>The cost modifiers to supply to Domain.</returns>
    IReadOnlyList<ResourceCostReduction> GetReductions(CombatantSnapshot source);
}
