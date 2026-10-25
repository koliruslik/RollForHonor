using RollForHonor.Domain.Combat.Damage.Services;

namespace RollForHonor.Domain.Tests.Combat.Damage;

public sealed class HitMagnitudeArmorFormulaTests
{
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(0, 500, 0)]
    [InlineData(100, 0, 100)]
    [InlineData(100, -1, 100)]
    [InlineData(10, 500, 1)]
    [InlineData(100, 500, 50)]
    [InlineData(1000, 500, 910)]
    [InlineData(100, 1500, 25)]
    [InlineData(1, int.MaxValue, 1)]
    [InlineData(int.MaxValue, 0, int.MaxValue)]
    [InlineData(int.MaxValue, int.MaxValue, 1789569706)]
    public void Apply_UsesHitMagnitudeAndRoundsUpWithoutOverflow(int damage, int armor, int expected)
    {
        Assert.Equal(expected, new HitMagnitudeArmorFormula().Apply(damage, armor));
    }

    [Fact]
    public void Apply_WhenDamageIsNegative_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new HitMagnitudeArmorFormula().Apply(-1, 500));
    }
}
