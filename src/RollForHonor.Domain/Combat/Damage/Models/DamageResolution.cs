namespace RollForHonor.Domain.Combat.Damage.Models;

/// <summary>
/// Combines damage before and after target mitigation.
/// </summary>
public sealed record DamageResolution
{
    /// <summary>Gets damage before target defenses.</summary>
    public UnmitigatedDamage UnmitigatedDamage { get; }

    /// <summary>Gets damage remaining after target defenses.</summary>
    public FinalDamage FinalDamage { get; }

    /// <summary>
    /// Creates a complete damage resolution.
    /// </summary>
    public DamageResolution(
        UnmitigatedDamage unmitigatedDamage,
        FinalDamage finalDamage)
    {
        ArgumentNullException.ThrowIfNull(unmitigatedDamage);
        ArgumentNullException.ThrowIfNull(finalDamage);

        UnmitigatedDamage = unmitigatedDamage;
        FinalDamage = finalDamage;
    }
}
