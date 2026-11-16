using Microsoft.Extensions.Logging;
using RollForHonor.Application.Combat.Models;
using RollForHonor.Application.Combat.Ports;
using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Infrastructure.Diagnostics;

/// <summary>Filters are combined with AND; absent identities accept every participant.</summary>
/// <param name="AttackId">Optional originating attack identity filter.</param>
/// <param name="SourceId">Optional source identity filter.</param>
/// <param name="TargetId">Optional target filter; excludes launch records because they have no target.</param>
/// <param name="ErrorsOnly">Whether to export only exceptions and non-applied outcomes.</param>
public sealed record CombatDiagnosticFilter(Guid? AttackId = null, CombatantId? SourceId = null,
    CombatantId? TargetId = null, bool ErrorsOnly = false)
{
    internal bool Matches(Guid attackId, CombatantId sourceId, CombatantId targetId) =>
        (AttackId is null || AttackId == attackId) && (SourceId is null || SourceId == sourceId)
        && (TargetId is null || TargetId == targetId);
}

/// <summary>Optional ILogger output plus an independent full JSONL sink, owned/disposed by the host.</summary>
/// <param name="logger">Standard logger used for the selected view.</param>
/// <param name="view">Projection sent to the logger.</param>
/// <param name="filter">Optional combined identity and non-applied-outcome filters.</param>
/// <param name="jsonl">Optional independent full JSONL writer, owned and disposed by the host.</param>
public sealed class LoggerCombatDiagnostics(ILogger logger, CombatDiagnosticView view = CombatDiagnosticView.Summary,
    CombatDiagnosticFilter? filter = null, TextWriter? jsonl = null) : ICombatDiagnostics
{
    /// <inheritdoc />
    public bool IsEnabled(Guid attackId, CombatantId sourceId, CombatantId targetId) =>
        (filter?.Matches(attackId, sourceId, targetId) ?? true)
        && (jsonl is not null || logger.IsEnabled(LogLevel.Information));

    /// <inheritdoc />
    public bool IsStartEnabled(CombatantId sourceId) =>
        (filter?.SourceId is null || filter.SourceId == sourceId) && filter?.TargetId is null
        && (jsonl is not null || logger.IsEnabled(LogLevel.Information));

    /// <inheritdoc />
    public void Write(AttackStartDiagnosticReport report)
    {
        if ((filter?.SourceId is not null && filter.SourceId != report.SourceId)
            || filter?.TargetId is not null
            || (filter?.AttackId is not null && filter.AttackId != report.Result?.AttackId)
            || filter?.ErrorsOnly == true && !report.IsError) return;
        try
        {
            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation(new EventId(2000, "AttackStart"), "{CombatDiagnostic}", CombatDiagnosticFormatter.Format(report, view));
        }
        catch { }
        try
        {
            if (jsonl is not null)
            {
                var line = CombatDiagnosticFormatter.Format(report, CombatDiagnosticView.Jsonl);
                lock (jsonl)
                {
                    jsonl.WriteLine(line);
                    jsonl.Flush();
                }
            }
        }
        catch { }
    }

    /// <inheritdoc />
    public void Write(CombatDiagnosticReport report)
    {
        if (!(filter?.Matches(report.AttackId, report.SourceId, report.TargetId) ?? true)
            || filter?.ErrorsOnly == true && !report.IsError) return;
        try
        {
            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation(new EventId(2001, "CombatImpact"), "{CombatDiagnostic}", CombatDiagnosticFormatter.Format(report, view));
        }
        catch { }
        try
        {
            if (jsonl is not null)
            {
                var line = CombatDiagnosticFormatter.Format(report, CombatDiagnosticView.Jsonl);
                lock (jsonl)
                {
                    jsonl.WriteLine(line);
                    jsonl.Flush();
                }
            }
        }
        catch { }
    }
}
