namespace RollForHonor.Domain.Combat.Damage;

public sealed record UnmitigatedDamage
{
    public DamageBreakdown Breakdown { get; }

    public DamagePacket Damage { get; }

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
