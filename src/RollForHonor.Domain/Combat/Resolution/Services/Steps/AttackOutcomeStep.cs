using RollForHonor.Domain.Combat.Attacks.Services;
using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Dice.Services;

namespace RollForHonor.Domain.Combat.Resolution.Services.Steps;

/// <summary>Classifies an existing attack roll without consuming another dice roll.</summary>
public sealed class AttackOutcomeStep : ICombatStep
{
    private readonly IAttackOutcomePolicy _outcomePolicy;

    /// <summary>Creates a step with explicit attack outcome rules.</summary>
    /// <param name="outcomePolicy">Non-null outcome policy.</param>
    /// <exception cref="ArgumentNullException">The policy is null.</exception>
    public AttackOutcomeStep(IAttackOutcomePolicy outcomePolicy)
    {
        ArgumentNullException.ThrowIfNull(outcomePolicy);

        _outcomePolicy = outcomePolicy;
    }

    /// <inheritdoc />
    public CombatContext Execute(CombatContext context, IDiceRoller dice)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(dice);
        var attackRoll = context.AttackRoll
            ?? throw new InvalidOperationException("Attack outcome requires an attack roll.");
        if (context.AttackOutcome is not null)
            throw new InvalidOperationException("Attack outcome has already been resolved.");

        var outcome = _outcomePolicy.Determine(context.Request.Attack, context.Request.StateSnapshot.Source,
            context.Request.StateSnapshot.Target, attackRoll);

        return context with { AttackOutcome = outcome };
    }
}
