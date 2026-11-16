using Microsoft.Extensions.Logging;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Infrastructure.Diagnostics;

namespace RollForHonor.ConsoleHost;

/// <summary>Shared output settings for fixed impacts and the tool-local text battle.</summary>
/// <param name="View">Text or JSONL projection written to standard output.</param>
/// <param name="Filter">Combined identity and error filters.</param>
/// <param name="JsonlPath">Optional full sidecar path, relative to the working directory.</param>
/// <param name="Seed">Seed used only by the battle's random dice.</param>
/// <param name="Rounds">Maximum number of battle rounds, from 1 through 1000.</param>
internal sealed record DiagnosticOptions(CombatDiagnosticView View, CombatDiagnosticFilter Filter,
    string? JsonlPath, int Seed, int Rounds)
{
    internal static DiagnosticOptions Parse(string[] args)
    {
        var options = new Dictionary<string, string>();
        for (var i = 1; i < args.Length; i++)
        {
            var key = args[i];
            if (key == "--errors-only")
            {
                if (!options.TryAdd(key, "true")) throw new ArgumentException("Repeated --errors-only.");
            }
            else if ((key is "--view" or "--jsonl" or "--attack" or "--source" or "--target"
                || args[0] == "battle" && key is "--seed" or "--rounds")
                && ++i < args.Length && options.TryAdd(key, args[i])) { }
            else throw new ArgumentException($"Invalid, repeated or incomplete option '{key}'.");
        }
        var view = options.GetValueOrDefault("--view", "summary") switch
        {
            "summary" => CombatDiagnosticView.Summary, "steps" => CombatDiagnosticView.Steps,
            "full" => CombatDiagnosticView.Full, "jsonl" => CombatDiagnosticView.Jsonl,
            _ => throw new ArgumentException("Expected summary, steps, full or jsonl view.")
        };
        Guid? Identity(string key) => !options.TryGetValue(key, out var text) ? null
            : Guid.TryParse(text, out var id) && id != Guid.Empty ? id : throw new ArgumentException($"Invalid {key} identity.");
        int Number(string key, int fallback, int min, int max) => !options.TryGetValue(key, out var text) ? fallback
            : int.TryParse(text, out var number) && number >= min && number <= max ? number
            : throw new ArgumentException($"{key} must be an integer from {min} to {max}.");
        var source = Identity("--source");
        var target = Identity("--target");
        return new(view, new CombatDiagnosticFilter(Identity("--attack"),
            source is { } sourceId ? new CombatantId(sourceId) : null,
            target is { } targetId ? new CombatantId(targetId) : null, options.ContainsKey("--errors-only")),
            options.GetValueOrDefault("--jsonl"), Number("--seed", 42, int.MinValue, int.MaxValue),
            Number("--rounds", 30, 1, 1000));
    }

    internal StreamWriter? OpenWriter()
    {
        if (JsonlPath is null) return null;
        var path = Path.GetFullPath(JsonlPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var writer = new StreamWriter(path);
        System.Console.Error.WriteLine($"JSONL report: {path}");
        return writer;
    }
}

internal sealed class ConsoleDiagnosticLogger : ILogger
{
    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    /// <inheritdoc />
    public bool IsEnabled(LogLevel level) => true;
    /// <inheritdoc />
    public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error,
        Func<TState, Exception?, string> formatter) => System.Console.WriteLine(formatter(state, error));
}
