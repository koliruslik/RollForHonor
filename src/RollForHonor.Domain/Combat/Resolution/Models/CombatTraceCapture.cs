using System.Collections.Immutable;
using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.Effects.Models;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Combat.State.Changes;

namespace RollForHonor.Domain.Combat.Resolution.Models;

/// <summary>Optional per-resolution intermediate data, with no logging or random operations.</summary>
public sealed class CombatTraceCapture
{
    private readonly List<CombatCapturedStep> _steps = [];
    private int _depth;
    /// <summary>Gets the ordered capture records, including currently executing and failed steps.</summary>
    public IReadOnlyList<CombatCapturedStep> Steps => _steps.AsReadOnly();

    internal int Begin(string name, CombatContext context, int diceIndex)
    {
        var index = _steps.Count;
        _steps.Add(new(name, _depth++, diceIndex, diceIndex, CombatStepState.From(context), null, null));
        return index;
    }

    internal void SetDetails(int index, object? details) => _steps[index] = _steps[index] with { Details = details };

    internal void Complete(int index, CombatContext? context, int diceIndex, Exception? error = null)
    {
        _depth--;
        _steps[index] = _steps[index] with
        {
            DiceEndIndex = diceIndex,
            After = context is null ? null : CombatStepState.From(context),
            Error = error is null ? null : $"{error.GetType().Name}: {error.Message}"
        };
    }
}

/// <summary>One attempted step, including failures and nested pipeline depth.</summary>
/// <param name="Name">Diagnostic name of the attempted step.</param>
/// <param name="Depth">Zero-based nesting depth.</param>
/// <param name="DiceStartIndex">Inclusive index before the step began.</param>
/// <param name="DiceEndIndex">Exclusive index after the step completed or failed.</param>
/// <param name="Before">Calculation values at entry.</param>
/// <param name="After">Calculation values on success, or null on failure.</param>
/// <param name="Error">Exception type and message, absent on success.</param>
/// <param name="Details">Immutable finalization output, when this step reports additional values.</param>
public sealed record CombatCapturedStep(string Name, int Depth, int DiceStartIndex, int DiceEndIndex,
    CombatStepState Before, CombatStepState? After, string? Error, object? Details = null);

/// <summary>Immutable calculation values without recursive traces or live state references.</summary>
/// <param name="AttackRoll">Resolved attack dice and modifier, if available.</param>
/// <param name="Outcome">Classified attack quality, if available.</param>
/// <param name="BeforeDefenses">Current unmitigated damage and its breakdown.</param>
/// <param name="AfterDefenses">Current mitigated damage and its breakdown.</param>
/// <param name="Effects">Selected effects and recipients.</param>
/// <param name="HealthAdjustments">Signed health proposals before final projection.</param>
/// <param name="NewEffectsResolved">Whether new status selection has completed.</param>
/// <param name="StateChanges">Proposed state changes at this point; none are committed here.</param>
public sealed record CombatStepState(AttackRollResult? AttackRoll, AttackOutcome? Outcome,
    DamageCalculationResult? BeforeDefenses, DamageCalculationResult? AfterDefenses,
    ImmutableArray<ResolvedCombatEffect> Effects, ImmutableArray<HealthAdjustment> HealthAdjustments,
    bool NewEffectsResolved, ImmutableArray<ICombatStateChange> StateChanges)
{
    internal static CombatStepState From(CombatContext context) => new(context.AttackRoll,
        context.AttackOutcome, context.BeforeDefenses, context.AfterDefenses, context.EffectsApplied,
        context.HealthAdjustments, context.NewEffectsResolved, context.StateChanges);
}
