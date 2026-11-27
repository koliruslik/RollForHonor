using RollForHonor.Domain.Combat.Effects.Models;
using RollForHonor.Domain.Stats;

namespace RollForHonor.Domain.Combat.Effects.Services;

/// <summary>Projects active captured slowing over resolved movement speed without mutating stored stat inputs.</summary>
public static class StatusMovementProjector
{
    /// <summary>Multiplies resolved movement speed by active periodic slowing; expired instances contribute nothing.</summary>
    /// <param name="stats">The immutable base/equipment/buff result.</param>
    /// <param name="effects">Currently active effect instances.</param>
    /// <returns>An immutable set with effective movement speed, or the original set when no slow is active.</returns>
    /// <exception cref="ArgumentNullException">An input is null.</exception>
    public static StatBlock Apply(StatBlock stats, IReadOnlyList<CombatEffectSnapshot> effects)
    {
        ArgumentNullException.ThrowIfNull(stats);
        ArgumentNullException.ThrowIfNull(effects);
        var factor = effects
            .Select(effect => effect.Definition)
            .OfType<PeriodicDamageEffectDefinition>()
            .Where(definition => definition.MovementSlow > 0)
            .Aggregate(1m, (value, definition) => value * (1 - definition.MovementSlow));

        return factor == 1 ? stats : stats.With(new(StatType.MovementSpeed), stats.Get(new(StatType.MovementSpeed)) * factor);
    }
}
