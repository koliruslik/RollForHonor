using RollForHonor.Application.Combat.Models;
using RollForHonor.Domain.Combat.Resolution.Services;
using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Application.Combat.Ports;

/// <summary>Selects and composes calculation steps and applicable effects for the current impact.</summary>
public interface ICombatPipelineFactory
{
    /// <summary>Creates the explicit Domain pipeline using fresh participant data.</summary>
    /// <param name="attack">The source-bound base attack description.</param>
    /// <param name="snapshot">The current impact snapshot, including active effects.</param>
    /// <returns>The composed calculation; mandatory finalization is added by the use case.</returns>
    ICombatStep Create(AttackInstance attack, CombatStateSnapshot snapshot);
}
