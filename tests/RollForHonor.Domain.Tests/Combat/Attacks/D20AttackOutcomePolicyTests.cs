using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Domain.Combat.Attacks.Services;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Dice.Models;

namespace RollForHonor.Domain.Tests.Combat.Attacks;

public sealed class D20AttackOutcomePolicyTests
{
    [Theory]
    [InlineData(-11, AttackOutcome.Evaded)]
    [InlineData(-10, AttackOutcome.Evaded)]
    [InlineData(-9, AttackOutcome.GlancingHit)]
    [InlineData(-1, AttackOutcome.GlancingHit)]
    [InlineData(0, AttackOutcome.Hit)]
    [InlineData(9, AttackOutcome.Hit)]
    [InlineData(10, AttackOutcome.CriticalHit)]
    [InlineData(11, AttackOutcome.CriticalHit)]
    public void Determine_ClassifiesExactMarginBoundaries(int margin, AttackOutcome expected) =>
        Assert.Equal(expected, Determine(10, margin, 0));

    [Theory]
    [InlineData(1, -10, AttackOutcome.Evaded)]
    [InlineData(1, -1, AttackOutcome.Evaded)]
    [InlineData(1, 0, AttackOutcome.GlancingHit)]
    [InlineData(1, 10, AttackOutcome.Hit)]
    [InlineData(20, -10, AttackOutcome.GlancingHit)]
    [InlineData(20, -1, AttackOutcome.Hit)]
    [InlineData(20, 0, AttackOutcome.CriticalHit)]
    [InlineData(20, 10, AttackOutcome.CriticalHit)]
    public void Determine_NaturalRollShiftsExactlyOneDegree_AndSaturatesAtOuterOutcomes(
        int natural, int margin, AttackOutcome expected) =>
        Assert.Equal(expected, Determine(natural, margin, 0));

    [Theory]
    [InlineData(3, AttackOutcome.GlancingHit)]
    [InlineData(4, AttackOutcome.Hit)]
    [InlineData(17, AttackOutcome.Hit)]
    [InlineData(18, AttackOutcome.CriticalHit)]
    public void Determine_ConfigurableNaturalThresholdsAreInclusive(int natural, AttackOutcome expected) =>
        Assert.Equal(expected, Determine(natural, 0, 0, new NaturalOutcomeShiftRules(3, 18)));

    [Theory]
    [InlineData(int.MaxValue, int.MinValue, AttackOutcome.CriticalHit)]
    [InlineData(int.MinValue + 10, int.MaxValue, AttackOutcome.Evaded)]
    public void Determine_MarginUsesWideArithmetic(int total, int evasion, AttackOutcome expected) =>
        Assert.Equal(expected, Determine(10, total, evasion));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Determine_RejectsNonD20PayloadOrMismatchedRollFormula(bool invalidPayload)
    {
        var attackFormula = new DiceFormula(1, invalidPayload ? 6 : 20);
        var rollFormula = new DiceFormula(1, 6);
        var attack = new AttackPayload(attackFormula, 0, NaturalOutcomeShiftRules.Standard, [], [], AttackTags.Melee);
        var roll = new DiceRoll(rollFormula, [3], 3);
        var participant = Combatant(0);

        var error = Assert.Throws<ArgumentException>(() => new D20AttackOutcomePolicy().Determine(
            attack, participant, participant, new AttackRollResult(roll, 0, 3)));

        Assert.Equal(invalidPayload ? "attack" : "attackRoll", error.ParamName);
    }

    private static AttackOutcome Determine(int natural, int total, int evasion, NaturalOutcomeShiftRules? shifts = null)
    {
        var formula = new DiceFormula(1, 20);
        var modifier = checked(total - natural);
        var attack = new AttackPayload(formula, modifier, shifts ?? NaturalOutcomeShiftRules.Standard, [], [], AttackTags.Melee);
        var roll = new DiceRoll(formula, [natural], natural);
        return new D20AttackOutcomePolicy().Determine(attack, Combatant(0), Combatant(evasion),
            new AttackRollResult(roll, modifier, total));
    }

    private static CombatantSnapshot Combatant(int evasion) =>
        new(new CombatantId(Guid.NewGuid()), 50, 100, 0, evasion, [], []);
}
