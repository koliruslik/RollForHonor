namespace RollForHonor.Domain.Combat.Resolution;

public sealed record CombatRejection
{
    public CombatRejectionReason Reason { get; }
    public string Description { get; }

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
}
