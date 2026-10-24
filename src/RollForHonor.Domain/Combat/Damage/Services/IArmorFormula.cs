namespace RollForHonor.Domain.Combat.Damage.Services;

/// <summary>
/// Defines a replaceable, deterministic armor calculation for one physical hit.
/// </summary>
public interface IArmorFormula
{
    /// <summary>Calculates the combined physical damage remaining after armor.</summary>
    /// <param name="damage">The positive sum of physical damage components in the hit.</param>
    /// <param name="armor">The target's armor value, interpreted by the chosen formula.</param>
    /// <returns>
    /// Remaining damage from zero through <paramref name="damage"/> inclusive.
    /// The resolver distributes this result proportionally across physical components,
    /// rounding each component upward; their sum may exceed this result.
    /// </returns>
    /// <remarks>
    /// The formula must not mutate state or consume randomness. Coefficients and
    /// any dependence on hit magnitude belong to the implementation of this contract.
    /// </remarks>
    int Apply(int damage, int armor);
}
