using RollForHonor.Application.Combat.Models;
using RollForHonor.Application.Combat.Ports;
using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Application.Combat.Diagnostics;

/// <summary>Owns optional reporting and failure isolation for exactly one launch attempt.</summary>
internal sealed class AttackStartDiagnostics : DiagnosticOperation<AttackStartResult>
{
    private readonly ICombatDiagnostics? _output;
    private readonly Guid _requestId;
    private readonly CombatantId _sourceId;
    private readonly AttackDefinition _definition;
    private readonly bool _enabled;
    private AttackInitiationState? _state;
    private AttackStartProposal? _proposal;
    private TimeSpan? _now;

    private AttackStartDiagnostics(ICombatDiagnostics? output, Guid requestId,
        CombatantId sourceId, AttackDefinition definition)
    {
        _output = output;
        _requestId = requestId;
        _sourceId = sourceId;
        _definition = definition;
        _enabled = IsEnabledSafely(() => output?.IsStartEnabled(sourceId) == true);
    }

    internal static AttackStartResult Run(ICombatDiagnostics? output, Guid requestId,
        CombatantId sourceId, AttackDefinition definition, Func<AttackStartDiagnostics, AttackStartResult> operation)
    {
        var diagnostics = new AttackStartDiagnostics(output, requestId, sourceId, definition);
        return diagnostics.Execute(() => operation(diagnostics));
    }

    internal void ObserveState(AttackInitiationState state) => _state = state;
    internal void ObserveTime(TimeSpan now) => _now = now;
    internal void ObserveProposal(AttackStartProposal proposal) => _proposal = proposal;

    protected override void Publish(AttackStartResult? result, Exception? error)
    {
        if (!_enabled) return;
        _output!.Write(new AttackStartDiagnosticReport(_requestId, _sourceId, _definition, _state,
            _now, _proposal, result, error is null ? null : $"{error.GetType().Name}: {error.Message}"));
    }
}
