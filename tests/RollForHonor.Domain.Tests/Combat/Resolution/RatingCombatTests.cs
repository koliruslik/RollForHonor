using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Domain.Combat.Attacks.Services;
using RollForHonor.Domain.Combat.Damage.Services;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Dice.Models;
using RollForHonor.Domain.Dice.Services;
using RollForHonor.Domain.Combat.Damage.Configuration;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Combat.Resolution.Services;
using RollForHonor.Domain.Combat.Resolution.Services.Steps;

namespace RollForHonor.Domain.Tests.Combat.Resolution;

/// <summary>Checks the accepted rating candidate against all die faces and exact first-generation controls.</summary>
public sealed class RatingCombatTests
{
    /// <summary>Only natural one misses, natural twenty stays full, and evasion only changes glancing quality.</summary>
    [Theory]
    [InlineData(100, 10)]
    [InlineData(45, 5)]
    [InlineData(40, 4)]
    [InlineData(40.5, 5)]
    [InlineData(1000000, 18)]
    public void EveryFaceHasTheAgreedQuality(decimal evasion, int glancing)
    {
        var attack = new AttackPayload(new(1, 20), 4, NaturalOutcomeShiftRules.Standard, [], [], AttackTags.None);
        var source = new CombatantSnapshot(new(Guid.NewGuid()), 100, 100, 0, 0, [], []);
        var target = new CombatantSnapshot(new(Guid.NewGuid()), 100, 100, 0, (int)decimal.Floor(evasion), [], [], evasionRating: evasion);
        var outcomes = Enumerable.Range(1, 20)
            .Select(face => new RatingAttackOutcomePolicy().Determine(attack, source, target, new(new(new(1, 20), [face], face), 4, face + 4)))
            .ToArray();

        Assert.Equal(AttackOutcome.Evaded, outcomes[0]);
        Assert.Equal(AttackOutcome.Hit, outcomes[19]);
        Assert.Equal(1, outcomes.Count(value => value == AttackOutcome.Evaded));
        Assert.Equal(glancing, outcomes.Count(value => value == AttackOutcome.GlancingHit));
    }

    /// <summary>Armour is applied after component quality rounding and reproduces reference EHP without criticals.</summary>
    [Theory]
    [InlineData(130, 0, 142.857143)]
    [InlineData(0, 100, 135.714286)]
    [InlineData(45, 45, 130.136986)]
    public void FirstGenerationMatchesReference(int armor, int evasion, decimal expected)
    {
        var damage = Enumerable.Range(2, 19).Sum(face => new RatingArmorFormula().Apply(face + 4 < 6m + evasion / 10m ? 5 : 10, armor));
        var ehp = 100m * 190m / damage;

        Assert.InRange(ehp, expected - 0.00001m, expected + 0.00001m);
        Assert.Equal(7, new RatingArmorFormula().Apply(10, 130));
        Assert.Equal(4, new RatingArmorFormula().Apply(5, 45));
    }

    /// <summary>Criticals use their own actual d20 and preserve preceding glancing quality.</summary>
    [Theory]
    [InlineData(20, 1, 0, 10)]
    [InlineData(2, 20, 0, 10)]
    [InlineData(20, 17, 3, 20)]
    public void CriticalDecisionIsSeparateFromPrimaryQuality(int primary, int critical, int bonus, int expected)
    {
        var source = new CombatantSnapshot(new(Guid.NewGuid()), 100, 100, 0, 0, [], [], new(criticalRollBonus: bonus));
        var target = new CombatantSnapshot(new(Guid.NewGuid()), 100, 100, 0, 100, [], []);
        var attack = new AttackPayload(new(1, 20), 4, NaturalOutcomeShiftRules.Standard, [new(new(1, 2), 8, DamageType.Piercing)], [], AttackTags.None);
        var context = new CombatContext(new(Guid.NewGuid(), source.Id, target.Id, attack, new(0, source, target)));
        var pipeline = new CombatPipeline([
            new AttackRollStep(new AttackRollResolver()), new AttackOutcomeStep(new RatingAttackOutcomePolicy()),
            new RawDamageStep(new RawDamageResolver()), new SourceDamageStep(),
            new AttackQualityStep(new AttackQualityDamageResolver(new DamageMultipliers(0, 0.5m, 1, 2))),
            new IndependentCriticalStep(), new DefenseStep(new DefenseResolver(new RatingArmorFormula()))
        ]);
        var result = pipeline.Execute(context, new SequenceDice([primary, 2, critical]));
        var check = Assert.Single(result.AfterDefenses!.Breakdown.Steps.Where(step => step.Type == DamageStepType.CriticalCheck));

        Assert.Equal(expected, result.AfterDefenses.Sum());
        Assert.Equal(critical, check.DecisionRoll!.Total);
        Assert.Equal(bonus, check.FlatBonus);
        Assert.Equal(primary == 2 ? AttackOutcome.GlancingHit : AttackOutcome.Hit, result.AttackOutcome);
    }

    private sealed class SequenceDice(IEnumerable<int> values) : IDiceRoller
    {
        /// <inheritdoc />
        public DiceTrace Trace => new(_rolls);

        private readonly Queue<int> _values = new(values);
        private readonly List<DiceRoll> _rolls = [];

        /// <inheritdoc />
        public DiceRoll Roll(DiceFormula formula)
        {
            var value = _values.Dequeue();
            var roll = new DiceRoll(formula, [value], value);
            _rolls.Add(roll);

            return roll;
        }
    }
}
