namespace RollForHonor.Domain.Resources.Services;

/// <summary>Defines how percentage reductions combine for one base cost.</summary>
public interface IResourceCostPolicy
{
    /// <summary>Calculates a nonnegative cost without mutating its inputs.</summary>
    /// <param name="baseCost">The nonnegative base quantity.</param>
    /// <param name="reductions">Reduction fractions belonging to this resource.</param>
    /// <returns>The exact cost, with rounding determined explicitly by the policy.</returns>
    decimal Calculate(decimal baseCost, IReadOnlyList<decimal> reductions);
}
