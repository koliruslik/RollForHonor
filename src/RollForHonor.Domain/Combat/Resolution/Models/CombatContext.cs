using System.Collections.Immutable;
using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.Effects.Models;
using RollForHonor.Domain.Combat.State.Changes;
using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Domain.Combat.Resolution.Models;

/// <summary>
/// Holds immutable inputs and intermediate results for one impact. Each step returns
/// a new context; snapshots and earlier contexts are never changed.
/// </summary>
public sealed record CombatContext
{
    /// <summary>Gets the original request, shared by every step.</summary>
    public CombatRequest Request { get; }
    /// <summary>Gets the optional resolved attack roll.</summary>
    public AttackRollResult? AttackRoll { get; init; }
    /// <summary>Gets the optional classified attack outcome.</summary>
    public AttackOutcome? AttackOutcome { get; init; }
    /// <summary>Gets damage before defenses, if this action calculates damage.</summary>
    public DamageCalculationResult? BeforeDefenses { get; init; }
    /// <summary>Gets damage after defenses, if a defense step was included.</summary>
    public DamageCalculationResult? AfterDefenses { get; init; }
    /// <summary>Gets effect selections reported by this action, including source recipients.</summary>
    public ImmutableArray<ResolvedCombatEffect> EffectsApplied { get; init; } =
        [];

    /// <summary>Gets signed health proposals; positive values heal.</summary>
    public ImmutableArray<HealthAdjustment> HealthAdjustments { get; init; } =
        [];

    /// <summary>Gets proposed changes, in application order; none are committed here.</summary>
    public ImmutableArray<ICombatStateChange> StateChanges { get; init; } =
        [];

    /// <summary>Gets whether the attack's new statuses have been selected.</summary>
    public bool NewEffectsResolved { get; init; }
    /// <summary>Gets ordered, nested step execution records; damage arithmetic has its own breakdown.</summary>
    public ImmutableArray<CombatStepTrace> StepTrace { get; init; } = [];

    /// <summary>Creates the initial context without calculated results.</summary>
    /// <param name="request">The non-null request.</param>
    /// <exception cref="ArgumentNullException">The request is null.</exception>
    public CombatContext(CombatRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        Request = request;
    }

}
