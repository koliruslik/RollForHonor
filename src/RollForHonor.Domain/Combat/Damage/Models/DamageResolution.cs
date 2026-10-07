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
    /// <param name="unmitigatedDamage">Damage produced before defenses.</param>
    /// <param name="finalDamage">Damage remaining after defenses.</param>
    /// <exception cref="ArgumentNullException">Either damage stage is null.</exception>
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
