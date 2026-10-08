namespace RollForHonor.Domain.Combat.Damage.Models;

/// <summary>
/// Contains attack damage before target defenses are applied.
/// </summary>
public sealed record UnmitigatedDamage
{
    /// <summary>Gets the calculation audit trail.</summary>
    public DamageBreakdown Breakdown { get; }

    /// <summary>Gets the typed damage before mitigation.</summary>
    public DamagePacket Damage { get; }

    /// <summary>
    /// Creates an unmitigated damage result and its breakdown.
    /// </summary>
    /// <param name="breakdown">The ordered calculation trace before defenses.</param>
    /// <param name="damage">Typed damage to pass into defense resolution.</param>
    /// <exception cref="ArgumentNullException">Either argument is null.</exception>
    public UnmitigatedDamage(
        DamageBreakdown breakdown,
        DamagePacket damage)
    {
        ArgumentNullException.ThrowIfNull(breakdown);
        ArgumentNullException.ThrowIfNull(damage);

        Breakdown = breakdown;
        Damage = damage;
    }
}
