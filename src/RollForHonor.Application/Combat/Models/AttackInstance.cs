using RollForHonor.Application.Combat.Services;
using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Application.Combat.Models;

/// <summary>Binds an attack description and source identity to independently resolved target impacts.</summary>
/// <remarks>
/// This is an impact context, not attack acceptance or a projectile. Construction does not spend
/// resources or start cooldowns. Runtime lifetime and contact eligibility are separate policies.
/// Source statistics are read afresh for each impact rather than retained here.
/// </remarks>
public sealed class AttackInstance
{
    private readonly CombatImpactService _impacts;

    /// <summary>Gets the identity shared by all impacts produced through this instance.</summary>
    public Guid Id { get; } = Guid.NewGuid();
    /// <summary>Gets the source to read at impact time.</summary>
    public CombatantId SourceId { get; }
    /// <summary>Gets immutable attack rules and base damage formulas.</summary>
    public AttackPayload Attack { get; }

    /// <summary>Creates an impact context without performing launch or lifetime operations.</summary>
    /// <param name="sourceId">The non-null source identity.</param>
    /// <param name="attack">The non-null base attack description.</param>
    /// <param name="impacts">The non-null Application impact use case.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public AttackInstance(CombatantId sourceId, AttackPayload attack, CombatImpactService impacts)
    {
        ArgumentNullException.ThrowIfNull(sourceId);
        ArgumentNullException.ThrowIfNull(attack);
        ArgumentNullException.ThrowIfNull(impacts);
        SourceId = sourceId;
        Attack = attack;
        _impacts = impacts;
    }

    /// <summary>Calculates a new impact against current source and target state and attempts atomic commit.</summary>
    /// <param name="targetId">The target supplied by an eligible contact.</param>
    /// <returns>The calculated outcome and its distinct commit status.</returns>
    /// <remarks>Each call is a new impact. Collision deduplication and automatic conflict retries are not performed.</remarks>
    public CombatImpactResult Resolve(CombatantId targetId) => _impacts.Resolve(this, targetId);
}
