using RollForHonor.Domain.Dice.Models;
using RollForHonor.Domain.Dice.Services;

namespace RollForHonor.ConsoleHost;

// Tool-local adapters: production runtime composition remains in Application/Godot.
internal sealed class ScenarioDice(Func<int, int> next) : IDiceRoller
{
    private readonly List<DiceRoll> _rolls = [];
    public DiceTrace Trace => new(_rolls);

    public DiceRoll Roll(DiceFormula formula)
    {
        var values = Enumerable.Range(0, formula.Count).Select(_ => next(formula.Sides)).ToArray();
        var roll = new DiceRoll(formula, values, values.Sum());
        _rolls.Add(roll);
        return roll;
    }

    public static ScenarioDice Scripted(params int[] values)
    {
        var queue = new Queue<int>(values);
        return new ScenarioDice(_ => queue.Dequeue());
    }

    public static ScenarioDice Seeded(Random random) => new(sides => random.Next(1, sides + 1));
}
