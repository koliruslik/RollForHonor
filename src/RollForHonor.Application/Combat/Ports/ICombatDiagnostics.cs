using RollForHonor.Application.Combat.Models;
using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Application.Combat.Ports;

/// <summary>Optional output boundary. Implementations must never be required for gameplay.</summary>
public interface ICombatDiagnostics
{
    /// <summary>Checks whether an impact should collect and export diagnostic data.</summary>
    /// <param name="attackId">The originating attack identity.</param>
    /// <param name="sourceId">The current source identity.</param>
    /// <param name="targetId">The contacted target identity.</param>
    /// <returns>True when output settings and participant filters enable capture.</returns>
    bool IsEnabled(Guid attackId, CombatantId sourceId, CombatantId targetId);
    /// <summary>Exports a completed or failed impact attempt without changing game state.</summary>
    /// <param name="report">Captured inputs, calculation and separate commit result.</param>
    void Write(CombatDiagnosticReport report);
    /// <summary>Checks whether a launch attempt should export diagnostics.</summary>
    /// <param name="sourceId">The source attempting the launch.</param>
    /// <returns>True when launch reporting is enabled for this source.</returns>
    bool IsStartEnabled(CombatantId sourceId);
    /// <summary>Exports a launch attempt including partial inputs on failure.</summary>
    /// <param name="report">Launch inputs, proposed costs and actual acceptance result.</param>
    void Write(AttackStartDiagnosticReport report);
}
