using RollForHonor.Domain.Combat.Resolution.Services;
using Xunit.Abstractions;

namespace RollForHonor.Domain.Tests.Diagnostics;

internal static class CombatDiagnosticOutput
{
    private const string EnvironmentVariableName =
        "RFH_COMBAT_DIAGNOSTICS";

    public static void WriteIfEnabled(
        ITestOutputHelper output,
        ICombatResolutionOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(outcome);

        var setting = Environment.GetEnvironmentVariable(
            EnvironmentVariableName);

        if (!IsEnabled(setting))
        {
            return;
        }

        output.WriteLine(
            CombatResolutionDiagnosticFormatter.Format(outcome));
    }

    internal static bool IsEnabled(string? setting)
    {
        return string.Equals(
                   setting,
                   "1",
                   StringComparison.Ordinal)
               || string.Equals(
                   setting,
                   "true",
                   StringComparison.OrdinalIgnoreCase);
    }
}