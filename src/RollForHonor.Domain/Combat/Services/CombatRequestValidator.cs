using RollForHonor.Domain.Combat.Resolution;

namespace RollForHonor.Domain.Combat.Services;

/// <summary>
/// Validates the consistency of a combat request before resolution begins.
/// </summary>
public sealed class CombatRequestValidator : ICombatRequestValidator
{
    /// <inheritdoc />
    public CombatRejection? Validate(CombatRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.SourceId != request.StateSnapshot.Source.Id)
        {
            return new CombatRejection(CombatRejectionReason.SourceNotFound,
                $"Source combatant '{request.SourceId}' does not match snapshot source '{request.StateSnapshot.Source.Id}'.");
        }

        if (request.TargetId != request.StateSnapshot.Target.Id)
        {
            return new CombatRejection(CombatRejectionReason.TargetNotFound,
                $"Target combatant '{request.TargetId}' does not match snapshot target '{request.StateSnapshot.Target.Id}'.");
        }

        if (request.StateSnapshot.Source.IsDefeated)
        {
            return new CombatRejection(CombatRejectionReason.SourceDefeated,
                $"Source combatant '{request.SourceId}' is defeated and cannot initiate an attack.");
        }

        if (request.SourceId == request.TargetId &&
            !ReferenceEquals(request.StateSnapshot.Source, request.StateSnapshot.Target))
        {
            return new CombatRejection(CombatRejectionReason.InvalidState,
                $"Combatant '{request.SourceId}' has conflicting source and target snapshots.");
        }

        if (request.Attack.AttackRoll.Count != 1 ||
            request.Attack.AttackRoll.Sides != 20)
        {
            return new CombatRejection(
                CombatRejectionReason.InvalidAttack,
                "An attack roll must use exactly 1d20.");
        }

        if (request.Attack.Effects.Count == 0 && request.Attack.Damage.Count == 0)
        {
            return new CombatRejection(CombatRejectionReason.InvalidAttack,
                "An attack must contain at least one damage component or combat effect.");
        }

        return null;
    }
}
