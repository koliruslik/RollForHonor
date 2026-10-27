using RollForHonor.Domain.Combat.Effects.Models;
using RollForHonor.Domain.Combat.Resolution.Models;

namespace RollForHonor.Domain.Combat.Effects.Services;

/// <summary>Selects new statuses and proposes replacements, without executing their behavior.</summary>
public interface ICombatEffectResolver
{
    /// <summary>Resolves statuses at the point explicitly chosen by pipeline composition.</summary>
    /// <param name="context">Original participants, payload and available calculation results.</param>
    /// <returns>Selections and non-health status changes; health adjustments must be empty.</returns>
    /// <remarks>Immunity and application-condition policies may be added here; none are currently inferred.</remarks>
    EffectResolution Resolve(CombatContext context);
}
