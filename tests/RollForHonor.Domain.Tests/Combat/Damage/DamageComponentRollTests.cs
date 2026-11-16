using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Dice.Models;

namespace RollForHonor.Domain.Tests.Combat.Damage;

/// <summary>Checks that retained damage facts are exact, immutable and cannot contradict their dice formula.</summary>
public sealed class DamageComponentRollTests
{
    /// <summary>Retains the actual component and die values while calculating the zero-clamped amount once.</summary>
    /// <param name="flat">The supplied signed flat damage.</param>
    /// <param name="expected">The expected unscaled amount after the roll and zero clamp.</param>
    [Theory]
    [InlineData(3, 5)]
    [InlineData(-10, 0)]
    public void RetainsRollAndFlatIncludingZeroClamp(int flat, int expected)
    {
        var component = new DamageComponent(new(1, 6), flat, DamageType.Fire);
        var roll = new DiceRoll(new(1, 6), [2], 2);
        var result = new DamageComponentRoll(component, roll);

        Assert.Same(component, result.Component);
        Assert.Same(roll, result.Roll);
        Assert.Equal(expected, result.Amount);
        Assert.Equal(flat, result.Component.FlatDamage);
    }

    /// <summary>Rejects dice facts that do not match the component's formula.</summary>
    [Fact]
    public void MismatchedFormulaCannotBeReported()
    {
        var component = new DamageComponent(new(1, 6), 0, DamageType.Fire);
        Assert.Throws<ArgumentException>(() => new DamageComponentRoll(component, new(new(1, 4), [2], 2)));
    }

    /// <summary>Preserves checked arithmetic instead of reporting a wrapped component amount.</summary>
    [Fact]
    public void OverflowIsNotSilentlyWrapped()
    {
        var component = new DamageComponent(new(1, 6), int.MaxValue, DamageType.Fire);
        Assert.Throws<OverflowException>(() => new DamageComponentRoll(component, new(new(1, 6), [1], 1)));
    }

    /// <summary>Missing component or dice data cannot become a valid retained calculation fact.</summary>
    /// <param name="missingComponent">Whether the component rather than the roll is missing.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MissingInputsAreRejected(bool missingComponent)
    {
        var component = new DamageComponent(new(1, 6), 0, DamageType.Fire);
        var roll = new DiceRoll(new(1, 6), [2], 2);
        Assert.Throws<ArgumentNullException>(() => new DamageComponentRoll(missingComponent ? null! : component,
            missingComponent ? roll : null!));
    }

    /// <summary>Copying the reported collection prevents later caller edits from changing a tooltip's historical facts.</summary>
    [Fact]
    public void DamageStepCopiesComponentFactsAndRejectsNullEntries()
    {
        var first = new DamageComponentRoll(new(new(1, 6), 1, DamageType.Fire), new(new(1, 6), [2], 2));
        var second = new DamageComponentRoll(new(new(1, 6), 5, DamageType.Fire), new(new(1, 6), [3], 3));
        var items = new[] { first };
        var packet = new DamagePacket([new(DamageType.Fire, first.Amount)]);
        var step = new DamageStep(DamageStepType.WeaponRoll, new([]), packet, componentRolls: items);
        items[0] = second;

        Assert.Same(first, Assert.Single(step.ComponentRolls));
        Assert.Throws<NotSupportedException>(() => ((IList<DamageComponentRoll>)step.ComponentRolls).Add(second));
        Assert.Throws<ArgumentException>(() => new DamageStep(DamageStepType.WeaponRoll, new([]), packet, componentRolls: [null!]));
        Assert.Empty(new DamageStep(DamageStepType.ArmorMitigation, packet, packet).ComponentRolls);
    }
}
