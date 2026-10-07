using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Domain.Combat.Damage.Configuration;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.Damage.Services;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Dice.Models;
using RollForHonor.Domain.Dice.Services;

namespace RollForHonor.Domain.Tests.Combat.Damage;

public sealed class AttackDamageResolverTests
{
    private static readonly DamageMultipliers StandardMultipliers = new(
        evaded: 0m,
        glancingHit: 0.5m,
        hit: 1m,
        criticalHit: 2m);

    [Fact]
    public void Constructor_WhenMultipliersAreNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new AttackDamageResolver(null!));
    }

    [Fact]
    public void Resolve_WhenComponentsArePresent_RollsOnceAddsFlatDamageAndClampsAtZero()
    {
        var attack = CreateAttack(
        [
            new DamageComponent(
                new DiceFormula(2, 6),
                flatDamage: 3,
                DamageType.Slashing),
            new DamageComponent(
                new DiceFormula(1, 4),
                flatDamage: -10,
                DamageType.Fire)
        ]);
        var dice = new SequenceDiceRoller([2, 4], [3]);
        var resolver = new AttackDamageResolver(StandardMultipliers);

        var result = resolver.Resolve(
            attack,
            CreateCombatant(),
            CreateAttackRoll(attack),
            AttackOutcome.Hit,
            dice);

        AssertDamage(result.Damage, (DamageType.Slashing, 9), (DamageType.Fire, 0));
        Assert.Equal(2, dice.Trace.Rolls.Count);
        Assert.Equal(attack.Damage[0].Dice, dice.Trace.Rolls[0].Formula);
        Assert.Equal(attack.Damage[1].Dice, dice.Trace.Rolls[1].Formula);

        Assert.Collection(
            result.Breakdown.Steps,
            weaponRoll =>
            {
                Assert.Equal(DamageStepType.WeaponRoll, weaponRoll.Type);
                Assert.Empty(weaponRoll.Input.Components);
                AssertDamage(
                    weaponRoll.Output,
                    (DamageType.Slashing, 9),
                    (DamageType.Fire, 0));
            },
            attackQuality =>
            {
                Assert.Equal(DamageStepType.AttackQuality, attackQuality.Type);
                AssertDamage(
                    attackQuality.Input,
                    (DamageType.Slashing, 9),
                    (DamageType.Fire, 0));
                AssertDamage(
                    attackQuality.Output,
                    (DamageType.Slashing, 9),
                    (DamageType.Fire, 0));
            });
    }

    [Fact]
    public void Resolve_WhenMultiplierIsFractional_CeilsEachDamageComponentIndependently()
    {
        var attack = CreateAttack(
        [
            new DamageComponent(
                new DiceFormula(1, 6),
                flatDamage: 0,
                DamageType.Piercing),
            new DamageComponent(
                new DiceFormula(1, 6),
                flatDamage: 0,
                DamageType.Cold)
        ]);
        var dice = new SequenceDiceRoller([3], [1]);
        var resolver = new AttackDamageResolver(StandardMultipliers);

        var result = resolver.Resolve(
            attack,
            CreateCombatant(),
            CreateAttackRoll(attack),
            AttackOutcome.GlancingHit,
            dice);

        AssertDamage(result.Damage, (DamageType.Piercing, 2), (DamageType.Cold, 1));
    }

    [Theory]
    [InlineData(AttackOutcome.Evaded, 0)]
    [InlineData(AttackOutcome.GlancingHit, 2)]
    [InlineData(AttackOutcome.Hit, 4)]
    [InlineData(AttackOutcome.CriticalHit, 8)]
    public void Resolve_WhenOutcomeChanges_UsesItsConfiguredMultiplier(
        AttackOutcome outcome,
        int expectedDamage)
    {
        var attack = CreateAttack(
        [
            new DamageComponent(
                new DiceFormula(1, 6),
                flatDamage: 0,
                DamageType.Bludgeoning)
        ]);
        var dice = new SequenceDiceRoller([4]);
        var resolver = new AttackDamageResolver(StandardMultipliers);

        var result = resolver.Resolve(
            attack,
            CreateCombatant(),
            CreateAttackRoll(attack),
            outcome,
            dice);

        AssertDamage(result.Damage, (DamageType.Bludgeoning, expectedDamage));
    }

    [Fact]
    public void Resolve_WhenDamageIsEmpty_ReturnsEmptyDamageWithoutRolling()
    {
        var attack = CreateAttack([]);
        var dice = new SequenceDiceRoller();
        var resolver = new AttackDamageResolver(StandardMultipliers);

        var result = resolver.Resolve(
            attack,
            CreateCombatant(),
            CreateAttackRoll(attack),
            AttackOutcome.Hit,
            dice);

        Assert.Empty(result.Damage.Components);
        Assert.Empty(dice.Trace.Rolls);
        Assert.Equal(2, result.Breakdown.Steps.Count);
    }

    [Fact]
    public void Resolve_WhenFlatDamageAdditionOverflows_Throws()
    {
        var attack = CreateAttack(
        [
            new DamageComponent(
                new DiceFormula(1, 6),
                flatDamage: int.MaxValue,
                DamageType.Lightning)
        ]);
        var dice = new SequenceDiceRoller([1]);
        var resolver = new AttackDamageResolver(StandardMultipliers);

        Assert.Throws<OverflowException>(
            () => resolver.Resolve(
                attack,
                CreateCombatant(),
                CreateAttackRoll(attack),
                AttackOutcome.Hit,
                dice));
    }

    [Fact]
    public void Resolve_WhenAttackIsNull_Throws()
    {
        var resolver = new AttackDamageResolver(StandardMultipliers);

        Assert.Throws<ArgumentNullException>(
            () => resolver.Resolve(
                null!,
                CreateCombatant(),
                CreateAttackRoll(CreateAttack([])),
                AttackOutcome.Hit,
                new SequenceDiceRoller()));
    }

    [Fact]
    public void Resolve_WhenSourceIsNull_Throws()
    {
        var attack = CreateAttack([]);
        var resolver = new AttackDamageResolver(StandardMultipliers);

        Assert.Throws<ArgumentNullException>(
            () => resolver.Resolve(
                attack,
                null!,
                CreateAttackRoll(attack),
                AttackOutcome.Hit,
                new SequenceDiceRoller()));
    }

    [Fact]
    public void Resolve_WhenAttackRollIsNull_Throws()
    {
        var attack = CreateAttack([]);
        var resolver = new AttackDamageResolver(StandardMultipliers);

        Assert.Throws<ArgumentNullException>(
            () => resolver.Resolve(
                attack,
                CreateCombatant(),
                null!,
                AttackOutcome.Hit,
                new SequenceDiceRoller()));
    }

    [Fact]
    public void Resolve_WhenDiceAreNull_Throws()
    {
        var attack = CreateAttack([]);
        var resolver = new AttackDamageResolver(StandardMultipliers);

        Assert.Throws<ArgumentNullException>(
            () => resolver.Resolve(
                attack,
                CreateCombatant(),
                CreateAttackRoll(attack),
                AttackOutcome.Hit,
                null!));
    }

    private static AttackPayload CreateAttack(
        IReadOnlyList<DamageComponent> damage)
    {
        return new AttackPayload(
            new DiceFormula(1, 20),
            attackModifier: 0,
            NaturalOutcomeShiftRules.Standard,
            damage,
            effects: [],
            AttackTags.Melee);
    }

    private static AttackRollResult CreateAttackRoll(AttackPayload attack)
    {
        var roll = new DiceRoll(
            attack.AttackRoll,
            [10],
            total: 10);

        return new AttackRollResult(
            roll,
            attack.AttackModifier,
            total: 10);
    }

    private static CombatantSnapshot CreateCombatant()
    {
        return new CombatantSnapshot(
            new CombatantId(Guid.NewGuid()),
            health: 10,
            maxHealth: 10,
            armor: 0,
            evasion: 10,
            resistances: [],
            effects: []);
    }

    private static void AssertDamage(
        DamagePacket packet,
        params (DamageType Type, int Amount)[] expected)
    {
        Assert.Equal(expected.Length, packet.Components.Count);

        for (var index = 0; index < expected.Length; index++)
        {
            Assert.Equal(expected[index].Type, packet.Components[index].Type);
            Assert.Equal(expected[index].Amount, packet.Components[index].Amount);
        }
    }

    private sealed class SequenceDiceRoller : IDiceRoller
    {
        private readonly Queue<IReadOnlyList<int>> _results;
        private readonly List<DiceRoll> _rolls = [];

        public SequenceDiceRoller(params IReadOnlyList<int>[] results)
        {
            _results = new Queue<IReadOnlyList<int>>(results);
        }

        public DiceTrace Trace => new(_rolls);

        public DiceRoll Roll(DiceFormula formula)
        {
            if (_results.Count == 0)
            {
                throw new InvalidOperationException(
                    "No configured dice result is available.");
            }

            var results = _results.Dequeue();
            var roll = new DiceRoll(formula, results, results.Sum());

            _rolls.Add(roll);
            return roll;
        }
    }
}
