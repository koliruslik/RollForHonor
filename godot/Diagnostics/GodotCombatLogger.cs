#nullable enable
using System;
using Godot;
using Microsoft.Extensions.Logging;

namespace RollForHonor.Godot.Diagnostics;

/// <summary>Forwards standard informational combat diagnostics to Godot's output without gameplay responsibilities.</summary>
internal sealed class GodotCombatLogger : ILogger
{
    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => logLevel is >= LogLevel.Information and < LogLevel.None;
    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (IsEnabled(logLevel)) GD.Print(formatter(state, exception));
    }
}
