using RollForHonor.Application.Combat.Models;
using RollForHonor.Application.Combat.Ports;
using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Combat.Resolution.Services;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Dice.Services;

namespace RollForHonor.Application.Combat.Diagnostics;

/// <summary>Owns per-impact trace data and isolates optional diagnostic configuration/output failures.</summary>
internal sealed class CombatImpactDiagnostics : DiagnosticOperation<CombatImpactResult>
{
    private readonly ICombatDiagnostics? _output;
    private readonly AttackInstance _attack;
    private readonly CombatantId _targetId;
    internal Guid ResolutionId { get; } = Guid.NewGuid();
    internal CombatTraceCapture? Capture { get; }
    internal string Phase { get; set; } = "Read";
    internal CombatRequest? Request { get; set; }
    internal IDiceRoller? Dice { get; set; }
    internal ICombatResolutionOutcome? Calculation { get; set; }

    private CombatImpactDiagnostics(ICombatDiagnostics? output, AttackInstance attack, CombatantId targetId)
    {
        _output = output;
        _attack = attack;
        _targetId = targetId;
        if (IsEnabledSafely(() => output?.IsEnabled(attack.Id, attack.SourceId, targetId) == true))
            Capture = new CombatTraceCapture();
    }

    internal static CombatImpactResult Run(ICombatDiagnostics? output, AttackInstance attack,
        CombatantId targetId, Func<CombatImpactDiagnostics, CombatImpactResult> operation)
    {
        var diagnostics = new CombatImpactDiagnostics(output, attack, targetId);
        return diagnostics.Execute(() => operation(diagnostics));
    }

    protected override void Publish(CombatImpactResult? result, Exception? error)
    {
        if (Capture is null) return;
        if (result is not null) Phase = "Completed";
        _output!.Write(new CombatDiagnosticReport(_attack.Id, ResolutionId, _attack.SourceId, _targetId,
            Request, Capture.Steps.ToArray(), Dice?.Trace, Calculation, result, Phase,
            error is null ? null : $"{error.GetType().Name}: {error.Message}"));
    }
}
