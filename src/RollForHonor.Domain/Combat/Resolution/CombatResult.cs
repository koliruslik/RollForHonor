using RollForHonor.Domain.Combat.State;

namespace RollForHonor.Domain.Combat.Resolution;

public sealed record CombatResult
{
    public CombatantId SourceId { get; }
    public TargetCombatResult Target { get; }

    public CombatResult(
        CombatantId sourceId,
        TargetCombatResult target)
    {
        ArgumentNullException.ThrowIfNull(sourceId);
        ArgumentNullException.ThrowIfNull(target);

        SourceId = sourceId;
        Target = target;
    }
}
