namespace RollForHonor.Domain.Combat.Damage;

public sealed record FinalDamage
{
    public DamageBreakdown Breakdown { get; }

    public DamagePacket Damage { get; }

    public FinalDamage(
        DamageBreakdown breakdown,
        DamagePacket damage)
    {
        ArgumentNullException.ThrowIfNull(breakdown);
        ArgumentNullException.ThrowIfNull(damage);

        Breakdown = breakdown;
        Damage = damage;
    }

    public int Sum()
    {
        return Damage.Sum();
    }
}