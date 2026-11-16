using RollForHonor.Domain.Resources.Models;

namespace RollForHonor.Domain.Resources.Services;

/// <summary>Resolves each resource independently using an explicitly replaceable reduction policy.</summary>
public sealed class ResourceCostResolver : IResourceCostResolver
{
    private readonly IResourceCostPolicy _policy;
    /// <summary>Creates a resolver with an explicit reduction/rounding policy.</summary>
    /// <param name="policy">The non-null calculation policy.</param>
    public ResourceCostResolver(IResourceCostPolicy policy) => _policy = policy ?? throw new ArgumentNullException(nameof(policy));

    /// <inheritdoc />
    public ResourceCostResolution Resolve(IReadOnlyList<ResourceAmount> baseCosts, IReadOnlyList<ResourceCostReduction> reductions)
    {
        ArgumentNullException.ThrowIfNull(baseCosts);
        ArgumentNullException.ThrowIfNull(reductions);
        var bases = baseCosts.ToArray();
        var modifiers = reductions.ToArray();
        if (bases.Any(value => value is null) || bases.Select(value => value.Resource).Distinct().Count() != bases.Length)
            throw new ArgumentException("Base costs must be non-null and unique by resource.", nameof(baseCosts));
        if (modifiers.Any(value => value is null))
            throw new ArgumentException("Reductions cannot contain null.", nameof(reductions));
        var costs = bases.Select(cost => new ResourceAmount(cost.Resource,
            _policy.Calculate(cost.Amount, modifiers.Where(value => value.Resource == cost.Resource)
                .Select(value => value.Fraction).ToArray()))).ToArray();
        return new ResourceCostResolution(bases, modifiers, costs);
    }
}
