namespace RollForHonor.Domain.Combat.Resolution.Models;

/// <summary>
/// Describes a rejected combat request.
/// </summary>
public sealed record CombatRejection
{
    /// <summary>Gets the rejection category.</summary>
    public CombatRejectionReason Reason { get; }

    /// <summary>Gets the human-readable rejection description.</summary>
    public string Description { get; }

    /// <summary>
    /// Creates a rejection with a reason and human-readable description.
    /// </summary>
    /// <param name="reason">A defined rejection category.</param>
    /// <param name="description">A non-empty explanation suitable for diagnostics.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="reason"/> is undefined.</exception>
    /// <exception cref="ArgumentException"><paramref name="description"/> is empty or whitespace.</exception>
    public CombatRejection(CombatRejectionReason reason, string description)
    {
        if (!Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        Reason = reason;
        Description = description;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"{Reason}: {Description}";
    }
}
