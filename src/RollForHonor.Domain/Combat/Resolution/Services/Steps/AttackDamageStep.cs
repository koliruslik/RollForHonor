using RollForHonor.Domain.Combat.Damage.Services;
using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Dice.Services;

namespace RollForHonor.Domain.Combat.Resolution.Services.Steps;

/// <summary>Calculates pre-defense damage from an existing roll and outcome, exactly once.</summary>
public sealed class AttackDamageStep : ICombatStep
{
    private readonly IAttackDamageResolver _damageResolver;

    /// <summary>Creates a step with explicit weapon damage rules.</summary>
    /// <param name="damageResolver">Non-null raw damage calculation.</param>
    /// <exception cref="ArgumentNullException">The resolver is null.</exception>
    public AttackDamageStep(IAttackDamageResolver damageResolver)
    {
        ArgumentNullException.ThrowIfNull(damageResolver);

        _damageResolver = damageResolver;
    }

    /// <inheritdoc />
    public CombatContext Execute(CombatContext context, IDiceRoller dice)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(dice);

        if (context.BeforeDefenses is not null)
        {
            throw new InvalidOperationException("Attack damage has already been resolved.");
        }

        var attackRoll = context.AttackRoll
                         ?? throw new InvalidOperationException(
                             "Attack damage requires an attack roll.");

        var outcome = context.AttackOutcome
                      ?? throw new InvalidOperationException(
                          "Attack damage requires an attack outcome.");

        var damage = _damageResolver.Resolve(context.Request.Attack, context.Request.StateSnapshot.Source,
            attackRoll, outcome, dice);

        return context with { BeforeDefenses = damage };
    }
}
