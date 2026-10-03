namespace RollForHonor.Domain.Combat.Resolution;

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

    public override string ToString()
    {
        return $"{Reason}: {Description}";
    }
}
