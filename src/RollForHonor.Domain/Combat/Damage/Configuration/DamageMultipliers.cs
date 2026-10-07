namespace RollForHonor.Domain.Combat.Damage.Configuration;

/// <summary>
/// Defines the non-negative damage multiplier applied for each attack outcome.
/// Multipliers are applied after component rolls and flat damage, but before defenses.
/// </summary>
public sealed record DamageMultipliers
{
    /// <summary>Gets the multiplier for an evaded attack.</summary>
    public decimal Evaded { get; }

    /// <summary>Gets the multiplier for a glancing hit.</summary>
    public decimal GlancingHit { get; }

    /// <summary>Gets the multiplier for a normal hit.</summary>
    public decimal Hit { get; }

    /// <summary>Gets the multiplier for a critical hit.</summary>
    public decimal CriticalHit { get; }

    /// <summary>Creates outcome-specific damage multipliers.</summary>
    /// <param name="evaded">Multiplier applied to an evaded attack.</param>
    /// <param name="glancingHit">Multiplier applied to a glancing hit.</param>
    /// <param name="hit">Multiplier applied to a normal hit.</param>
    /// <param name="criticalHit">Multiplier applied to a critical hit.</param>
    /// <exception cref="ArgumentOutOfRangeException">Any multiplier is negative.</exception>
    public DamageMultipliers(
        decimal evaded,
        decimal glancingHit,
        decimal hit,
        decimal criticalHit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(evaded);
        ArgumentOutOfRangeException.ThrowIfNegative(glancingHit);
        ArgumentOutOfRangeException.ThrowIfNegative(hit);
        ArgumentOutOfRangeException.ThrowIfNegative(criticalHit);

        Evaded = evaded;
        GlancingHit = glancingHit;
        Hit = hit;
        CriticalHit = criticalHit;
    }
}
