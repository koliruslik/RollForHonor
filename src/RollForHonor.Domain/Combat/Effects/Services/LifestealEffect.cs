using RollForHonor.Domain.Combat.Effects.Models;
using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Combat.State.Services;
using RollForHonor.Domain.Dice.Services;

namespace RollForHonor.Domain.Combat.Effects.Services;

/// <summary>Proposes source healing from the opponent's final projected HP loss without additional dice or mutation.</summary>
/// <remarks>Schedule after defenses and all target health adjustments. Later steps must not change target HP/damage.
/// Does not heal from self-damage, ticks or zero HP loss. Finalization applies source healing and target damage atomically.</remarks>
public sealed class LifestealEffect : ICombatEffect
{
    private readonly LifestealDefinition _definition;
    private readonly IHealthStateProjector _projector;

    /// <summary>Creates behavior using the same health projection contract as result finalization.</summary>
    /// <param name="definition">The immutable healing rule.</param>
    /// <param name="projector">The deterministic health projector.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    public LifestealEffect(LifestealDefinition definition, IHealthStateProjector projector)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _projector = projector ?? throw new ArgumentNullException(nameof(projector));
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">Context or dice is null.</exception>
    /// <exception cref="InvalidOperationException">An eligible attack has not completed defense calculation.</exception>
    public CombatContext Apply(CombatContext context, IDiceRoller dice)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(dice);
        var request = context.Request;
        if (request.TickEffect is not null || request.SourceId == request.TargetId || _definition.Fraction == 0)
            return context;
        var damage = context.AfterDefenses ?? throw new InvalidOperationException("Lifesteal requires completed defenses.");
        var target = request.StateSnapshot.Target;
        var adjustments = context.HealthAdjustments.Where(value => value.CombatantId == target.Id);
        var amount = damage.Sum();
        if (amount > 0) adjustments = adjustments.Append(new HealthAdjustment(target.Id, -amount));
        var projected = _projector.Project(target, adjustments.ToArray());
        var lost = Math.Max(0, target.Health - projected.CurrentHealth);
        var healing = checked((int)Math.Ceiling(lost * _definition.Fraction));
        return healing == 0 ? context : context with
        {
            HealthAdjustments = context.HealthAdjustments.Add(new(request.SourceId, healing))
        };
    }
}
