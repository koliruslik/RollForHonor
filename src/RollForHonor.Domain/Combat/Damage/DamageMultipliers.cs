namespace RollForHonor.Domain.Combat.Damage;

public sealed record DamageMultipliers
{
    public decimal Evaded { get; }

    public decimal GlancingHit { get; }

    public decimal Hit { get; }

    public decimal CriticalHit { get; }

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
