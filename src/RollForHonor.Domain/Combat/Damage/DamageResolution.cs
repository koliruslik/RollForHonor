namespace RollForHonor.Domain.Combat.Damage;

public sealed record DamageResolution
{
    public UnmitigatedDamage UnmitigatedDamage { get; }

    public FinalDamage FinalDamage { get; }

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
