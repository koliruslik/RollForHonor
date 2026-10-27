namespace RollForHonor.Domain.Combat.Damage.Models;

/// <summary>Pairs typed damage at any calculation stage with its ordered arithmetic history.</summary>
public sealed class DamageCalculationResult
{
    /// <summary>Gets the calculation audit trail.</summary>
    public DamageBreakdown Breakdown { get; }

    /// <summary>Gets the typed damage at this stage.</summary>
    public DamagePacket Damage { get; }

    /// <summary>
    /// Creates a damage calculation result and its breakdown.
    /// </summary>
    /// <param name="breakdown">The ordered history up to this stage.</param>
    /// <param name="damage">Typed damage at this stage.</param>
    /// <exception cref="ArgumentNullException">Either argument is null.</exception>
    public DamageCalculationResult(
        DamageBreakdown breakdown,
        DamagePacket damage)
    {
        ArgumentNullException.ThrowIfNull(breakdown);
        ArgumentNullException.ThrowIfNull(damage);

        Breakdown = breakdown;
        Damage = damage;
    }

    /// <summary>
    /// Returns the total damage at this stage across all damage types.
    /// </summary>
    /// <returns>The checked sum of all remaining damage.</returns>
    /// <exception cref="OverflowException">The total exceeds the integer range.</exception>
    public int Sum()
    {
        return Damage.Sum();
    }
}
