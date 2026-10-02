namespace RollForHonor.Domain.Combat.Damage;

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
    public int Sum()
    {
        return Damage.Sum();
    }
}
