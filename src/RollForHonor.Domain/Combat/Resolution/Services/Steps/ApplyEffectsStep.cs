using RollForHonor.Domain.Combat.Effects.Services;
using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Dice.Services;

namespace RollForHonor.Domain.Combat.Resolution.Services.Steps;

/// <summary>
/// Runs an explicit, copied list of behaviors at the position chosen by composition.
/// Does not discover effects, choose phases, or activate newly applied statuses.
/// </summary>
public sealed class ApplyEffectsStep : ICombatStep
{
    private readonly CombatPipeline _pipeline;

    /// <inheritdoc />
    public string Name { get; }

    /// <summary>Creates a reusable effect group, which may be empty.</summary>
    /// <param name="effects">Behaviors in execution order.</param>
    /// <param name="name">Diagnostic group name, for example BeforeDefensesEffects.</param>
    /// <exception cref="ArgumentNullException">Effects are null.</exception>
    /// <exception cref="ArgumentException">An effect is null or the name is empty.</exception>
    public ApplyEffectsStep(IEnumerable<ICombatEffect> effects, string name = nameof(ApplyEffectsStep))
    {
        ArgumentNullException.ThrowIfNull(effects);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var items = effects.ToArray();
        if (items.Any(effect => effect is null))
            throw new ArgumentException("Effects cannot contain null.", nameof(effects));
        Name = name;
        _pipeline = new CombatPipeline(items.Select(effect => new EffectBehaviorStep(effect)), name);
    }

    /// <inheritdoc />
    public CombatContext Execute(CombatContext context, IDiceRoller dice)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(dice);
        return _pipeline.Execute(context, dice);
    }

    private sealed class EffectBehaviorStep(ICombatEffect effect) : ICombatStep
    {
        public string Name => effect.Name;
        public CombatContext Execute(CombatContext context, IDiceRoller dice) => effect.Apply(context, dice);
    }
}
