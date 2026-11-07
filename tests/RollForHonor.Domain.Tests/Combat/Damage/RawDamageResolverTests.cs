using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.Damage.Services;
using RollForHonor.Domain.Dice.Models;
using RollForHonor.Domain.Dice.Services;

namespace RollForHonor.Domain.Tests.Combat.Damage;

public sealed class RawDamageResolverTests
{
    [Fact]
    public void Resolve_RollsEachComponentOnce_AddsFlatDamage_AndClampsAtZero()
    {
        DamageComponent[] components =
        [
            new(new DiceFormula(2, 6), 3, DamageType.Slashing),
            new(new DiceFormula(1, 4), -10, DamageType.Fire)
        ];
        var dice = new SequenceDice([2, 4], [3]);

        var result = new RawDamageResolver().Resolve(components, dice);

        Assert.Equal(new[] { new DamageAmount(DamageType.Slashing, 9), new DamageAmount(DamageType.Fire, 0) },
            result.Damage.Components.ToArray());
        Assert.Equal(components.Select(c => c.Dice), dice.Trace.Rolls.Select(r => r.Formula));
        var roll = Assert.Single(result.Breakdown.Steps);
        Assert.Equal(DamageStepType.WeaponRoll, roll.Type);
        Assert.Empty(roll.Input.Components);
        Assert.Same(result.Damage, roll.Output);
        Assert.Equal(3, components[0].FlatDamage);
    }

    [Fact]
    public void Resolve_EmptyComponentsReturnEmptyDamageWithoutRolling()
    {
        var dice = new SequenceDice();

        var result = new RawDamageResolver().Resolve([], dice);

        Assert.Empty(result.Damage.Components);
        Assert.Empty(dice.Trace.Rolls);
        Assert.Equal(DamageStepType.WeaponRoll, Assert.Single(result.Breakdown.Steps).Type);
    }

    [Fact]
    public void Resolve_FlatDamageOverflowThrows()
    {
        var dice = new SequenceDice([1]);

        Assert.Throws<OverflowException>(() => new RawDamageResolver().Resolve(
            [new DamageComponent(new DiceFormula(1, 6), int.MaxValue, DamageType.Lightning)], dice));
        Assert.Single(dice.Trace.Rolls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Resolve_InvalidComponentsAreRejectedBeforeRolling(bool nullItem)
    {
        var dice = new SequenceDice();
        var resolver = new RawDamageResolver();
        if (nullItem)
            Assert.Throws<ArgumentException>(() => resolver.Resolve([null!], dice));
        else
            Assert.Throws<ArgumentNullException>(() => resolver.Resolve(null!, dice));
        Assert.Empty(dice.Trace.Rolls);
    }

    [Fact]
    public void Resolve_NullDiceThrows() =>
        Assert.Throws<ArgumentNullException>(() => new RawDamageResolver().Resolve([], null!));

    [Fact]
    public void Resolve_DiceFailurePropagatesWithoutReturningPartialDamage()
    {
        var dice = new SequenceDice([2]);
        DamageComponent[] components =
        [
            new(new DiceFormula(1, 6), 0, DamageType.Slashing),
            new(new DiceFormula(1, 6), 0, DamageType.Fire)
        ];

        Assert.Throws<InvalidOperationException>(() => new RawDamageResolver().Resolve(components, dice));
        Assert.Single(dice.Trace.Rolls);
    }

    private sealed class SequenceDice(params IReadOnlyList<int>[] results) : IDiceRoller
    {
        private readonly Queue<IReadOnlyList<int>> _results = new(results);
        private readonly List<DiceRoll> _rolls = [];
        public DiceTrace Trace => new(_rolls);

        public DiceRoll Roll(DiceFormula formula)
        {
            if (_results.Count == 0)
                throw new InvalidOperationException("No configured dice result is available.");
            var values = _results.Dequeue();
            var roll = new DiceRoll(formula, values, values.Sum());
            _rolls.Add(roll);
            return roll;
        }
    }
}
