using RollForHonor.Domain.Combat.Resolution.Services;
using Xunit.Abstractions;

namespace RollForHonor.Domain.Tests.Diagnostics;

public abstract class CombatDiagnosticTestBase
{
    private readonly ITestOutputHelper _output;

    protected CombatDiagnosticTestBase(ITestOutputHelper output)
    {
        ArgumentNullException.ThrowIfNull(output);
        _output = output;
    }

    protected void LogDiagnosticText(string text)
    {
        if (CombatDiagnosticOutput.IsEnabled(Environment.GetEnvironmentVariable("RFH_COMBAT_DIAGNOSTICS")))
            _output.WriteLine(text);
    }

    protected TOutcome LogOutcome<TOutcome>(TOutcome outcome)
        where TOutcome : ICombatResolutionOutcome
    {
        ArgumentNullException.ThrowIfNull(outcome);

        CombatDiagnosticOutput.WriteIfEnabled(_output, outcome);
        return outcome;
    }
}
