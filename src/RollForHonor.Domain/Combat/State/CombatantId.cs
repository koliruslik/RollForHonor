namespace RollForHonor.Domain.Combat.State;

public sealed record CombatantId
{
    public Guid Value { get; }

    public CombatantId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException(
                "A combatant identifier cannot be empty.",
                nameof(value));
        }

        Value = value;
    }
}
