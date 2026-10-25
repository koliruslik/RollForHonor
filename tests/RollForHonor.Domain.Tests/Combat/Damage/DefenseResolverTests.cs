using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.Damage.Services;
using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Domain.Tests.Combat.Damage;

public sealed class DefenseResolverTests
{
    [Fact]
    public void Constructor_WhenFormulaIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new DefenseResolver(null!));
    }

    [Fact]
    public void Resolve_WhenDamageIsMixed_AppliesArmorOnceToPhysicalSumAndPreservesHistory()
    {
        var input = CreateDamage(
            (DamageType.Slashing, 30),
            (DamageType.Piercing, 20),
            (DamageType.Bludgeoning, 10),
            (DamageType.Fire, 40));
        var target = CreateTarget(armor: 123);
        var calls = 0;
        var resolver = new DefenseResolver(new TestArmorFormula((damage, armor) =>
        {
            calls++;
            Assert.Equal(60, damage);
            Assert.Equal(123, armor);
            return 30;
        }));

        var result = resolver.Resolve(input, target);

        Assert.Equal(1, calls);
        AssertDamage(result.Damage,
            (DamageType.Slashing, 15),
            (DamageType.Piercing, 10),
            (DamageType.Bludgeoning, 5),
            (DamageType.Fire, 40));
        Assert.Collection(result.Breakdown.Steps,
            previous => Assert.Same(input.Breakdown.Steps[0], previous),
            armor =>
            {
                Assert.Equal(DamageStepType.ArmorMitigation, armor.Type);
                Assert.Same(input.Damage, armor.Input);
                Assert.Equal(result.Damage.Components.ToArray(), armor.Output.Components.ToArray());
            },
            resistance =>
            {
                Assert.Equal(DamageStepType.ResistanceMitigation, resistance.Type);
                Assert.Same(result.Breakdown.Steps[1].Output, resistance.Input);
                Assert.Same(result.Damage, resistance.Output);
            });
        Assert.Single(input.Breakdown.Steps);
        AssertDamage(input.Damage,
            (DamageType.Slashing, 30), (DamageType.Piercing, 20),
            (DamageType.Bludgeoning, 10), (DamageType.Fire, 40));
        Assert.Equal(100, target.Health);
        Assert.Equal(123, target.Armor);
    }

    [Fact]
    public void Resolve_WhenPhysicalResistanceExists_AppliesItAfterArmorAndReturnsResistedDamage()
    {
        var resolver = new DefenseResolver(new TestArmorFormula((damage, _) => damage / 2));
        var target = CreateTarget(resistances:
        [
            new ResistanceValue(DamageType.Slashing, 0.5m),
            new ResistanceValue(DamageType.Fire, 0.75m)
        ]);

        var result = resolver.Resolve(
            CreateDamage((DamageType.Slashing, 100), (DamageType.Fire, 100)), target);

        AssertDamage(result.Breakdown.Steps[1].Output,
            (DamageType.Slashing, 50), (DamageType.Fire, 100));
        AssertDamage(result.Damage,
            (DamageType.Slashing, 25), (DamageType.Fire, 25));
        Assert.Equal(50, result.Sum());
        Assert.Equal(0.5m, target.Resistances[0].Value);
    }

    [Theory]
    [InlineData(DamageType.Fire)]
    [InlineData(DamageType.Cold)]
    [InlineData(DamageType.Lightning)]
    [InlineData(DamageType.Poison)]
    public void Resolve_WhenDamageIsNonPhysical_SkipsArmorAndUsesMatchingResistance(DamageType type)
    {
        var resolver = new DefenseResolver(UnexpectedArmorFormula());
        var target = CreateTarget(armor: 999, resistances: [new ResistanceValue(type, 0.25m)]);

        var result = resolver.Resolve(CreateDamage((type, 100)), target);

        AssertDamage(result.Damage, (type, 75));
    }

    [Fact]
    public void Resolve_WhenResistanceIsMissing_LeavesThatTypeUnchanged()
    {
        var resolver = new DefenseResolver(UnexpectedArmorFormula());
        var target = CreateTarget(resistances: [new ResistanceValue(DamageType.Fire, 0.75m)]);

        var result = resolver.Resolve(CreateDamage((DamageType.Cold, 100)), target);

        AssertDamage(result.Damage, (DamageType.Cold, 100));
    }

    [Fact]
    public void Resolve_WhenResistanceIsNegative_AmplifiesOnlyItsTypeAndRoundsUp()
    {
        var resolver = new DefenseResolver(UnexpectedArmorFormula());
        var target = CreateTarget(resistances: [new ResistanceValue(DamageType.Fire, -0.5m)]);

        var result = resolver.Resolve(
            CreateDamage((DamageType.Fire, 3), (DamageType.Cold, 3)), target);

        AssertDamage(result.Damage, (DamageType.Fire, 5), (DamageType.Cold, 3));
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(0.5, 2)]
    [InlineData(1, 0)]
    public void Resolve_WhenResistanceChanges_UsesFractionalReduction(double resistance, int expected)
    {
        var resolver = new DefenseResolver(UnexpectedArmorFormula());
        var target = CreateTarget(resistances: [new ResistanceValue(DamageType.Fire, (decimal)resistance)]);

        var result = resolver.Resolve(CreateDamage((DamageType.Fire, 3)), target);

        AssertDamage(result.Damage, (DamageType.Fire, expected));
    }

    [Fact]
    public void Resolve_WhenArmorProducesFractions_RoundsEachComponentUpAndPreservesDuplicates()
    {
        var resolver = new DefenseResolver(new TestArmorFormula((_, _) => 3));

        var result = resolver.Resolve(
            CreateDamage((DamageType.Slashing, 3), (DamageType.Slashing, 3)), CreateTarget());

        AssertDamage(result.Damage, (DamageType.Slashing, 2), (DamageType.Slashing, 2));
        Assert.Equal(4, result.Sum());
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(10, 10)]
    public void Resolve_WhenArmorFullyAbsorbsOrPassesDamage_PreservesTypedComponents(int remaining, int expected)
    {
        var resolver = new DefenseResolver(new TestArmorFormula((_, _) => remaining));

        var result = resolver.Resolve(CreateDamage((DamageType.Piercing, 10)), CreateTarget());

        AssertDamage(result.Damage, (DamageType.Piercing, expected));
    }

    [Fact]
    public void Resolve_WhenDamageIsZero_SkipsArmorAndKeepsZeroComponents()
    {
        var resolver = new DefenseResolver(UnexpectedArmorFormula());

        var result = resolver.Resolve(
            CreateDamage((DamageType.Piercing, 0), (DamageType.Fire, 0)), CreateTarget());

        AssertDamage(result.Damage, (DamageType.Piercing, 0), (DamageType.Fire, 0));
    }

    [Fact]
    public void Resolve_WhenDamageIsEmpty_PreservesHistoryAndAppendsEmptyDefenseStages()
    {
        var resolver = new DefenseResolver(UnexpectedArmorFormula());

        var result = resolver.Resolve(CreateDamage(), CreateTarget());

        Assert.Empty(result.Damage.Components);
        Assert.Equal(3, result.Breakdown.Steps.Count);
        Assert.All(result.Breakdown.Steps, step => Assert.Empty(step.Output.Components));
    }

    [Fact]
    public void Resolve_WhenDamageIsNull_Throws()
    {
        var resolver = new DefenseResolver(UnexpectedArmorFormula());
        Assert.Throws<ArgumentNullException>(() => resolver.Resolve(null!, CreateTarget()));
    }

    [Fact]
    public void Resolve_WhenTargetIsNull_Throws()
    {
        var resolver = new DefenseResolver(UnexpectedArmorFormula());
        Assert.Throws<ArgumentNullException>(() => resolver.Resolve(CreateDamage(), null!));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    public void Resolve_WhenFormulaReturnsInvalidDamage_Throws(int remaining)
    {
        var resolver = new DefenseResolver(new TestArmorFormula((_, _) => remaining));

        Assert.Throws<InvalidOperationException>(() =>
            resolver.Resolve(CreateDamage((DamageType.Piercing, 10)), CreateTarget()));
    }

    [Fact]
    public void Resolve_WhenResistanceExceedsOne_RejectsBeforeArmor()
    {
        var resolver = new DefenseResolver(UnexpectedArmorFormula());
        var target = CreateTarget(resistances: [new ResistanceValue(DamageType.Fire, 1.01m)]);

        Assert.Throws<ArgumentException>(() =>
            resolver.Resolve(CreateDamage((DamageType.Piercing, 10)), target));
    }

    [Fact]
    public void Resolve_WhenResistanceTypesAreDuplicated_RejectsBeforeArmor()
    {
        var resolver = new DefenseResolver(UnexpectedArmorFormula());
        var target = CreateTarget(resistances:
        [
            new ResistanceValue(DamageType.Fire, 0.25m),
            new ResistanceValue(DamageType.Fire, 0.5m)
        ]);

        Assert.Throws<ArgumentException>(() =>
            resolver.Resolve(CreateDamage((DamageType.Piercing, 10)), target));
    }

    [Fact]
    public void Resolve_WhenPhysicalSumOverflows_ThrowsBeforeArmor()
    {
        var resolver = new DefenseResolver(UnexpectedArmorFormula());

        Assert.Throws<OverflowException>(() => resolver.Resolve(
            CreateDamage((DamageType.Piercing, int.MaxValue), (DamageType.Slashing, 1)), CreateTarget()));
    }

    [Fact]
    public void Resolve_WhenWeaknessOverflowsComponent_Throws()
    {
        var resolver = new DefenseResolver(UnexpectedArmorFormula());
        var target = CreateTarget(resistances: [new ResistanceValue(DamageType.Fire, -1m)]);

        Assert.Throws<OverflowException>(() =>
            resolver.Resolve(CreateDamage((DamageType.Fire, int.MaxValue)), target));
    }

    [Fact]
    public void Resolve_WhenAllPhysicalTypesAreResisted_RestoresOnlyOnePointAndRecordsFinalization()
    {
        var input = CreateDamage(
            (DamageType.Fire, 10), (DamageType.Piercing, 0),
            (DamageType.Slashing, 30), (DamageType.Slashing, 20),
            (DamageType.Bludgeoning, 10));
        var target = CreateTarget(armor: 500, resistances:
        [
            new ResistanceValue(DamageType.Slashing, 1m),
            new ResistanceValue(DamageType.Bludgeoning, 1m)
        ]);
        var resolver = new DefenseResolver(new HitMagnitudeArmorFormula());

        var result = resolver.Resolve(input, target);

        AssertDamage(result.Damage,
            (DamageType.Fire, 10), (DamageType.Piercing, 0),
            (DamageType.Slashing, 1), (DamageType.Slashing, 0),
            (DamageType.Bludgeoning, 0));
        var finalization = result.Breakdown.Steps[^1];
        Assert.Equal(DamageStepType.Finalization, finalization.Type);
        Assert.Same(result.Breakdown.Steps[^2].Output, finalization.Input);
        Assert.Same(result.Damage, finalization.Output);
        AssertDamage(finalization.Input,
            (DamageType.Fire, 10), (DamageType.Piercing, 0),
            (DamageType.Slashing, 0), (DamageType.Slashing, 0),
            (DamageType.Bludgeoning, 0));
        Assert.Equal(30, input.Damage.Components[2].Amount);
    }

    [Fact]
    public void Resolve_WhenPhysicalDamageSurvives_DoesNotAddMinimumToOtherComponents()
    {
        var resolver = new DefenseResolver(new HitMagnitudeArmorFormula());
        var target = CreateTarget(resistances: [new ResistanceValue(DamageType.Slashing, 1m)]);

        var result = resolver.Resolve(
            CreateDamage((DamageType.Slashing, 5), (DamageType.Piercing, 3)), target);

        AssertDamage(result.Damage, (DamageType.Slashing, 0), (DamageType.Piercing, 3));
        Assert.DoesNotContain(result.Breakdown.Steps, s => s.Type == DamageStepType.Finalization);
    }

    [Fact]
    public void Resolve_WhenOnlyNonPhysicalDamageIsPositive_DoesNotCreatePhysicalDamage()
    {
        var resolver = new DefenseResolver(UnexpectedArmorFormula());
        var target = CreateTarget(resistances: [new ResistanceValue(DamageType.Fire, 1m)]);

        var result = resolver.Resolve(
            CreateDamage((DamageType.Piercing, 0), (DamageType.Fire, 10)), target);

        AssertDamage(result.Damage, (DamageType.Piercing, 0), (DamageType.Fire, 0));
        Assert.DoesNotContain(result.Breakdown.Steps, s => s.Type == DamageStepType.Finalization);
    }

    private static UnmitigatedDamage CreateDamage(params (DamageType Type, int Amount)[] components)
    {
        var packet = new DamagePacket(components.Select(c => new DamageAmount(c.Type, c.Amount)).ToArray());
        return new UnmitigatedDamage(
            new DamageBreakdown([new DamageStep(DamageStepType.AttackQuality, packet, packet)]), packet);
    }

    private static CombatantSnapshot CreateTarget(int armor = 0, IReadOnlyList<ResistanceValue>? resistances = null)
    {
        return new CombatantSnapshot(new CombatantId(Guid.NewGuid()),
            health: 100, maxHealth: 100, armor: armor, evasion: 10,
            resistances: resistances ?? [], effects: []);
    }

    private static void AssertDamage(DamagePacket packet, params (DamageType Type, int Amount)[] expected)
    {
        Assert.Equal(expected, packet.Components.Select(c => (c.Type, c.Amount)).ToArray());
    }

    private static TestArmorFormula UnexpectedArmorFormula() => new((_, _) =>
        throw new InvalidOperationException("Armor must not be evaluated in this scenario."));

    private sealed class TestArmorFormula(Func<int, int, int> apply) : IArmorFormula
    {
        public int Apply(int damage, int armor) => apply(damage, armor);
    }
}
