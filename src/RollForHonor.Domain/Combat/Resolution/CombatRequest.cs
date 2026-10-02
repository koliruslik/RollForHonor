using RollForHonor.Domain.Combat.Attacks;
using RollForHonor.Domain.Combat.State;

namespace RollForHonor.Domain.Combat.Resolution;

public sealed record CombatRequest
{
    public Guid ResolutionId { get; }
    public CombatantId SourceId { get; }
    public CombatantId TargetId { get; }
    public AttackPayload Attack { get; }
    public CombatStateSnapshot StateSnapshot { get; }

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
