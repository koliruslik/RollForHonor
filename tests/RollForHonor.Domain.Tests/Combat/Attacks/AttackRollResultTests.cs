using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Domain.Dice.Models;

namespace RollForHonor.Domain.Tests.Combat.Attacks;

public sealed class AttackRollResultTests
{
    [Fact]
    public void Constructor_RejectsAnOverflowWrappedTotal()
    {
        var roll = new DiceRoll(new DiceFormula(1, 20), [1], 1);

        Assert.Throws<ArgumentException>(() => new AttackRollResult(roll, int.MaxValue, int.MinValue));
    }

    [Theory]
    [InlineData(int.MaxValue - 1, int.MaxValue)]
    [InlineData(int.MinValue, int.MinValue + 1)]
    [InlineData(-1, 0)]
    public void Constructor_AcceptsMathematicallyCorrectTotalsWithinRange(int modifier, int total)
    {
        var roll = new DiceRoll(new DiceFormula(1, 20), [1], 1);

        Assert.Equal(total, new AttackRollResult(roll, modifier, total).Total);
    }
}
