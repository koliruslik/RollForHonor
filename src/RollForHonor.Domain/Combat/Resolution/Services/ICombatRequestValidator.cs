using RollForHonor.Domain.Combat.Resolution.Models;

namespace RollForHonor.Domain.Combat.Resolution.Services;

/// <summary>
/// Validates whether a combat request can enter the resolution pipeline.
/// </summary>
public interface ICombatRequestValidator
{
    /// <summary>
    /// Returns a rejection when the request is invalid; otherwise returns null.
    /// </summary>
    CombatRejection? Validate(CombatRequest request);
}
