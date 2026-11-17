using RollForHonor.Application.Combat.Ports;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Resources.Models;

namespace RollForHonor.Application.Combat.Services;

/// <summary>Explicit composition choice for content without cost-reduction behaviors; replace when such content is introduced.</summary>
public sealed class NoResourceCostModifiers : IResourceCostModifierProvider
{
    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">The source is null.</exception>
    public IReadOnlyList<ResourceCostReduction> GetReductions(CombatantSnapshot source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return [];
    }
}
