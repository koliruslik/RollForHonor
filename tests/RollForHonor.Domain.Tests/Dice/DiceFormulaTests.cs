using RollForHonor.Domain.Dice;

namespace RollForHonor.Domain.Tests.Dice;

public sealed class DiceFormulaTests
{
    [Theory]
    [InlineData(0, 6)]
    [InlineData(1, 1)]
    public void Constructor_WhenFormulaIsInvalid_Throws(
        int count,
        int sides)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new DiceFormula(count, sides));
    }

    [Fact]
    public void Constructor_WhenFormulaIsValid_CreatesFormula()
    {
        var formula = new DiceFormula(2, 12);

        Assert.Equal(2, formula.Count);
        Assert.Equal(12, formula.Sides);
    }
}
