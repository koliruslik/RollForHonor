using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Domain.Combat.Resolution.Models;

/// <summary>
/// Requests one atomic attack resolution against one target snapshot.
/// </summary>
public sealed record CombatRequest
{
    /// <summary>Gets the identifier shared with the resulting resolution.</summary>
    public Guid ResolutionId { get; }

    /// <summary>Gets the attacking combatant identifier.</summary>
    public CombatantId SourceId { get; }

    /// <summary>Gets the target combatant identifier.</summary>
    public CombatantId TargetId { get; }

    /// <summary>Gets the attack being resolved.</summary>
    public AttackPayload Attack { get; }

    /// <summary>Gets the state snapshot used for calculation.</summary>
    public CombatStateSnapshot StateSnapshot { get; }

    /// <summary>
    /// Creates a validated combat resolution request.
    /// </summary>
    /// <param name="resolutionId">The non-empty identifier copied into the result.</param>
    /// <param name="sourceId">The expected source combatant identifier.</param>
    /// <param name="targetId">The expected target combatant identifier.</param>
    /// <param name="attack">The immutable attack payload to resolve.</param>
    /// <param name="state">The versioned source and target snapshot.</param>
    /// <exception cref="ArgumentException"><paramref name="resolutionId"/> is empty.</exception>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
    public CombatRequest(
        Guid resolutionId,
        CombatantId sourceId,
        CombatantId targetId,
        AttackPayload attack,
        CombatStateSnapshot state)
    {
        if (resolutionId == Guid.Empty)
        {
            throw new ArgumentException("A resolution identifier cannot be empty.", nameof(resolutionId));
        }

        ArgumentNullException.ThrowIfNull(sourceId);
        ArgumentNullException.ThrowIfNull(targetId);
        ArgumentNullException.ThrowIfNull(attack);
        ArgumentNullException.ThrowIfNull(state);

        ResolutionId = resolutionId;
        SourceId = sourceId;
        TargetId = targetId;
        Attack = attack;
        StateSnapshot = state;
    }
}
