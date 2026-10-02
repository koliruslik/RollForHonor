using RollForHonor.Domain.Combat.StateChanges;
using RollForHonor.Domain.Dice;

namespace RollForHonor.Domain.Combat.Resolution;

public sealed record CombatResolution
{
    public Guid ResolutionId { get; }
    public long BasedOnStateVersion { get; }
    public CombatResult Result { get; }
    public IReadOnlyList<ICombatStateChange> Changes { get; }
    public DiceTrace DiceTrace { get; }

    public CombatResolution(
        Guid resolutionId,
        long basedOnStateVersion,
        CombatResult result,
        IReadOnlyList<ICombatStateChange> changes,
        DiceTrace diceTrace)
    {
        if (resolutionId == Guid.Empty)
        {
            throw new ArgumentException("A resolution identifier cannot be empty.", nameof(resolutionId));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(basedOnStateVersion);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentNullException.ThrowIfNull(diceTrace);

        if (changes.Any(change => change is null))
        {
            throw new ArgumentException("A combat resolution cannot contain null state changes.", nameof(changes));
        }

        ResolutionId = resolutionId;
        BasedOnStateVersion = basedOnStateVersion;
        Result = result;
        Changes = changes.ToArray();
        DiceTrace = diceTrace;
    }
}
