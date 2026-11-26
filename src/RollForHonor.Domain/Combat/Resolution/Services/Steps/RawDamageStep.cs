using RollForHonor.Domain.Combat.Damage.Services;
using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Dice.Services;

namespace RollForHonor.Domain.Combat.Resolution.Services.Steps;

/// <summary>Calculates unscaled damage once, including source extra dice for attacks but not periodic ticks.</summary>
public sealed class RawDamageStep : ICombatStep
{
    private readonly IRawDamageResolver _resolver;

    /// <summary>Creates a raw component calculation step.</summary>
    /// <param name="resolver">The non-null component calculation policy.</param>
    /// <exception cref="ArgumentNullException">The resolver is null.</exception>
    public RawDamageStep(IRawDamageResolver resolver) =>
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">Damage or defenses have already been calculated.</exception>
    public CombatContext Execute(CombatContext context, IDiceRoller dice)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(dice);
        if (context.BeforeDefenses is not null || context.AfterDefenses is not null)
        {
            throw new InvalidOperationException("Damage has already been resolved.");
        }
        var damage = context.Request.Attack is null
            ? context.Request.Damage
            : context.Request.Damage
                .Concat(context.Request.StateSnapshot.Source.Offense.AddedDice)
                .ToArray();

        return context with { BeforeDefenses = _resolver.Resolve(damage, dice) };
    }
}
