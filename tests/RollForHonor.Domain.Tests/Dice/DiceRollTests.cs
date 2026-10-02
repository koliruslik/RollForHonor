using RollForHonor.Domain.Dice;

namespace RollForHonor.Domain.Tests.Dice;

public sealed class DiceRollTests
{
    [Fact]
    public void Constructor_WhenTotalDoesNotMatchResults_Throws()
    {
        var formula = new DiceFormula(2, 6);

        Assert.Throws<ArgumentException>(
            () => new DiceRoll(formula, [2, 3], 6));
    }
}
