using RollForHonor.Domain.Combat.Attacks;
using RollForHonor.Domain.Combat.State;
using RollForHonor.Domain.Dice;

namespace RollForHonor.Domain.Combat.Services;

/// <summary>
/// Rolls and modifies an attack using the supplied dice source.
/// </summary>
public sealed class AttackRollResolver : IAttackRollResolver
{
    /// <inheritdoc />
    public AttackRollResult Resolve(
        AttackPayload attack,
        CombatantSnapshot source,
        CombatantSnapshot target,
        IDiceRoller dice)
    {
        ArgumentNullException.ThrowIfNull(attack);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(dice);

        var roll = dice.Roll(attack.AttackRoll);
        var total = checked(roll.Total + attack.AttackModifier);

        return new AttackRollResult(roll, attack.AttackModifier, total);
    }
}
