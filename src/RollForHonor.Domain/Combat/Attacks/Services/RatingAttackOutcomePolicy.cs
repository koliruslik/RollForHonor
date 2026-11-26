using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Domain.Combat.Attacks.Services;

/// <summary>Uses natural-one misses and fractional evasion ratings without margin-based criticals.</summary>
public sealed class RatingAttackOutcomePolicy : IAttackOutcomePolicy
{
    /// <inheritdoc />
    public AttackOutcome Determine(AttackPayload attack, CombatantSnapshot source, CombatantSnapshot target, AttackRollResult attackRoll)
    {
        ArgumentNullException.ThrowIfNull(attack);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(attackRoll);

        if (attackRoll.Roll.Formula.Count != 1 || attackRoll.Roll.Formula.Sides != 20 || attackRoll.Roll.Formula != attack.AttackRoll)
        {
            throw new ArgumentException("Rating quality requires a natural d20.", nameof(attackRoll));
        }

        var natural = attackRoll.Roll.Results[0];

        return natural == 1 ? AttackOutcome.Evaded
            : natural == 20 ? AttackOutcome.Hit
            : attackRoll.Total < 6m + target.EvasionRating / 10m ? AttackOutcome.GlancingHit : AttackOutcome.Hit;
    }
}
