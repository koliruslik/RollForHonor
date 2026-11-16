using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Combat.Resolution.Services;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Dice.Models;

namespace RollForHonor.Application.Combat.Models;

/// <summary>One impact attempt, including rejected calculations and failed or conflicting commits.</summary>
/// <param name="AttackId">Attack identity shared by its impacts.</param>
/// <param name="ResolutionId">Unique identity of this impact attempt.</param>
/// <param name="SourceId">Attacking participant identity.</param>
/// <param name="TargetId">Contacted participant identity.</param>
/// <param name="Request">Calculation inputs, absent if preparation failed.</param>
/// <param name="Steps">Captured steps in execution order, including any failed step.</param>
/// <param name="Dice">Completed dice trace, absent before a dice source was obtained.</param>
/// <param name="Calculation">Domain outcome, retained even when commit fails.</param>
/// <param name="Result">Completed Application response, absent when execution throws.</param>
/// <param name="Phase">Last reached Application phase.</param>
/// <param name="Error">Exception type and message, or null when execution returned normally.</param>
public sealed record CombatDiagnosticReport(Guid AttackId, Guid ResolutionId, CombatantId SourceId,
    CombatantId TargetId, CombatRequest? Request, IReadOnlyList<CombatCapturedStep> Steps,
    DiceTrace? Dice, ICombatResolutionOutcome? Calculation, CombatImpactResult? Result, string Phase, string? Error)
{
    /// <summary>Gets the serialized impact-record discriminator.</summary>
    public string Kind => "impact";
    /// <summary>Gets the diagnostic export schema version.</summary>
    public int SchemaVersion => 1;
    /// <summary>Gets whether execution threw or returned without newly applied changes.</summary>
    public bool IsError => Error is not null || Result is { IsCommitted: false };
}
