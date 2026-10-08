using RollForHonor.Domain.Combat.Damage.Configuration;

namespace RollForHonor.Domain.Tests.Combat.Damage;

public sealed class DamageMultipliersTests
{
    public static TheoryData<decimal, decimal, decimal, decimal> NegativeValues =>
        new()
        {
            { -0.1m, 0m, 0m, 0m },
            { 0m, -0.1m, 0m, 0m },
            { 0m, 0m, -0.1m, 0m },
            { 0m, 0m, 0m, -0.1m }
        };

    [Fact]
    public void Constructor_WhenValuesAreValid_PreservesThem()
    {
        var multipliers = new DamageMultipliers(
            evaded: 0m,
            glancingHit: 0.5m,
            hit: 1m,
            criticalHit: 2m);

        Assert.Equal(0m, multipliers.Evaded);
        Assert.Equal(0.5m, multipliers.GlancingHit);
        Assert.Equal(1m, multipliers.Hit);
        Assert.Equal(2m, multipliers.CriticalHit);
    }

    [Theory]
    [MemberData(nameof(NegativeValues))]
    public void Constructor_WhenAnyValueIsNegative_Throws(
        decimal evaded,
        decimal glancingHit,
        decimal hit,
        decimal criticalHit)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new DamageMultipliers(
                evaded,
                glancingHit,
                hit,
                criticalHit));
    }
}
