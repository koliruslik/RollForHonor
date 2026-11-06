using RollForHonor.Domain.Combat.Attacks.Services;
using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Dice.Services;

namespace RollForHonor.Domain.Combat.Resolution.Services.Steps;

/// <summary>Produces the attack roll once, retaining every preceding calculation result.</summary>
public sealed class AttackRollStep : ICombatStep
{
    private readonly IAttackRollResolver _rollResolver;

    /// <summary>Creates a step with explicit attack-roll rules.</summary>
    /// <param name="rollResolver">Non-null roll calculation.</param>
    /// <exception cref="ArgumentNullException">The resolver is null.</exception>
    public AttackRollStep(IAttackRollResolver rollResolver)
    {
        ArgumentNullException.ThrowIfNull(rollResolver);

        _rollResolver = rollResolver;
    }

    /// <inheritdoc />
    public CombatContext Execute(CombatContext context, IDiceRoller dice)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(dice);

        if (context.AttackRoll is not null)
        {
            throw new InvalidOperationException("Attack roll has already been resolved");
        }

        var attackRollResult = _rollResolver.Resolve(context.Request.Attack, context.Request.StateSnapshot.Source,
            context.Request.StateSnapshot.Target, dice);

        return context with { AttackRoll = attackRollResult };
    }
}
