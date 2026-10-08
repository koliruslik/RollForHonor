namespace RollForHonor.Domain.Combat.Damage.Models;

/// <summary>
/// Contains typed damage remaining after target defenses.
/// </summary>
public sealed record FinalDamage
{
    /// <summary>Gets the calculation audit trail.</summary>
    public DamageBreakdown Breakdown { get; }

    /// <summary>Gets the typed damage remaining after mitigation.</summary>
    public DamagePacket Damage { get; }

    /// <summary>
    /// Creates a final damage result and its breakdown.
    /// </summary>
    /// <param name="breakdown">The complete ordered calculation trace.</param>
    /// <param name="damage">Typed damage remaining after defenses.</param>
    /// <exception cref="ArgumentNullException">Either argument is null.</exception>
    public FinalDamage(
        DamageBreakdown breakdown,
        DamagePacket damage)
    {
        ArgumentNullException.ThrowIfNull(breakdown);
        ArgumentNullException.ThrowIfNull(damage);

        Breakdown = breakdown;
        Damage = damage;
    }

    /// <summary>
    /// Returns the total final damage across all damage types.
    /// </summary>
    /// <returns>The checked sum of all remaining damage.</returns>
    /// <exception cref="OverflowException">The total exceeds the integer range.</exception>
    public int Sum()
    {
        return Damage.Sum();
    }
}
