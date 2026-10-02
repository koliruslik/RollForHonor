namespace RollForHonor.Domain.Combat.Damage;

public sealed record DamageBreakdown
{
    public IReadOnlyList<DamageStep> Steps { get; }

    public DamageBreakdown(IReadOnlyList<DamageStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);

        if (steps.Count == 0)
        {
            throw new ArgumentException(
                "A damage breakdown must contain at least one step.",
                nameof(steps));
        }

        if (steps.Any(step => step is null))
        {
            throw new ArgumentException(
                "A damage breakdown cannot contain null steps.",
                nameof(steps));
        }

        Steps = steps.ToArray();
    }
}
