namespace RollForHonor.Domain.Resources.Services;

/// <summary>Adds percentage reductions and floors the resulting cost at zero, without integer rounding.</summary>
public sealed class AdditiveResourceCostPolicy : IResourceCostPolicy
{
    /// <inheritdoc />
    public decimal Calculate(decimal baseCost, IReadOnlyList<decimal> reductions)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(baseCost);
        ArgumentNullException.ThrowIfNull(reductions);
        if (reductions.Any(value => value is < 0m or > 1m))
            throw new ArgumentOutOfRangeException(nameof(reductions));
        return checked(baseCost * Math.Max(0m, 1m - reductions.Sum()));
    }
}
