namespace RollForHonor.Domain.Tests.Diagnostics;

public sealed class CombatDiagnosticOutputTests
{
    [Theory]
    [InlineData("1", true)]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("0", false)]
    [InlineData("false", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsEnabled_ReturnsExpectedResult(
        string? setting,
        bool expected)
    {
        var actual = CombatDiagnosticOutput.IsEnabled(setting);

        Assert.Equal(expected, actual);
    }
}