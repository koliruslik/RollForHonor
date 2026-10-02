namespace RollForHonor.Domain.Combat.Damage;

/// <summary>
/// Provides the ordered audit trail of a damage calculation.
/// </summary>
public sealed record DamageBreakdown
{
    /// <summary>Gets the ordered damage-calculation stages.</summary>
    public IReadOnlyList<DamageStep> Steps { get; }

    /// <summary>
    /// Creates an immutable non-empty damage breakdown.
    /// </summary>
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
