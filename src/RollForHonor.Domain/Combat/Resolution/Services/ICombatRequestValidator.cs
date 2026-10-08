using RollForHonor.Domain.Combat.Resolution.Models;

namespace RollForHonor.Domain.Combat.Resolution.Services;

/// <summary>
/// Validates whether a combat request can enter the resolution pipeline.
/// </summary>
public interface ICombatRequestValidator
{
    /// <summary>
    /// Checks identities, source state, attack-roll shape, and whether the attack
    /// contains damage or effects before any dice are rolled.
    /// </summary>
    /// <param name="request">The complete combat request to validate.</param>
    /// <returns>The first rejection found, or <see langword="null"/> when valid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    CombatRejection? Validate(CombatRequest request);
}
