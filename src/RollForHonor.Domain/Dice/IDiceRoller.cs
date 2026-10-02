namespace RollForHonor.Domain.Dice;

public interface IDiceRoller
{
    DiceRoll Roll(DiceFormula formula);

    DiceTrace Trace { get; }
}
