using RollForHonor.Domain.Combat.Damage.Services;
using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Dice.Services;

namespace RollForHonor.Domain.Combat.Resolution.Services.Steps;

/// <summary>Applies target defenses to existing damage without rerolling or collecting health changes.</summary>
public sealed class DefenseStep : ICombatStep
{
    private readonly IDefenseResolver _resolver;

    /// <summary>Creates a defense step.</summary>
    /// <param name="resolver">Non-null defense calculation.</param>
    /// <exception cref="ArgumentNullException">The resolver is null.</exception>
    public DefenseStep(IDefenseResolver resolver) =>
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));

    /// <inheritdoc />
    public CombatContext Execute(CombatContext context, IDiceRoller dice)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(dice);
        var damage = context.BeforeDefenses
            ?? throw new InvalidOperationException("Defenses require damage before defenses.");
        if (context.AfterDefenses is not null)
            throw new InvalidOperationException("Defenses have already been resolved.");
        return context with { AfterDefenses = _resolver.Resolve(damage, context.Request.StateSnapshot.Target) };
    }
}
