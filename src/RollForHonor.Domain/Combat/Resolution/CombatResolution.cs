using RollForHonor.Domain.Combat.StateChanges;
using RollForHonor.Domain.Dice;

namespace RollForHonor.Domain.Combat.Resolution;

/// <summary>
/// Contains a complete atomic combat calculation ready for commit.
/// </summary>
public sealed record CombatResolution
{
    /// <summary>Gets the resolution identifier copied from the request.</summary>
    public Guid ResolutionId { get; }

    /// <summary>Gets the state version on which the calculation was based.</summary>
    public long BasedOnStateVersion { get; }

    /// <summary>Gets the structured combat result.</summary>
    public CombatResult Result { get; }

    /// <summary>Gets the canonical state changes ready for atomic commit.</summary>
    public IReadOnlyList<ICombatStateChange> Changes { get; }

    /// <summary>Gets the dice rolls performed during the resolution.</summary>
    public DiceTrace DiceTrace { get; }

    /// <summary>
    /// Creates a versioned resolution with its result, state changes, and dice trace.
    /// </summary>
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
