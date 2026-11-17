using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Domain.Combat.Attacks.Services;
using RollForHonor.Domain.Combat.Damage.Configuration;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.Damage.Services;
using RollForHonor.Domain.Combat.Effects.Models;
using RollForHonor.Domain.Combat.Effects.Services;
using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Combat.Resolution.Services;
using RollForHonor.Domain.Combat.Resolution.Services.Steps;
using RollForHonor.Domain.Combat.State.Changes;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Combat.State.Services;
using RollForHonor.Domain.Dice.Models;
using RollForHonor.Domain.Dice.Services;
using RollForHonor.Domain.Tests.Diagnostics;
using Xunit.Abstractions;

namespace RollForHonor.Domain.Tests.Combat.Effects;

/// <summary>Proves actual-HP-loss healing with the production finalizer, defenses and shared dice trace.</summary>
public sealed class LifestealTests : CombatDiagnosticTestBase
{
    /// <summary>Creates effect tests with existing opt-in diagnostic output.</summary>
    /// <param name="output">The xUnit output destination.</param>
    public LifestealTests(ITestOutputHelper output) : base(output) { }

    /// <summary>Healing excludes overkill, rounds upward and is clamped at the source's maximum health.</summary>
    /// <param name="sourceHp">Initial source HP.</param>
    /// <param name="targetHp">Initial target HP.</param>
    /// <param name="damage">Damage before defenses.</param>
    /// <param name="percent">Lifesteal percentage.</param>
    /// <param name="expectedSource">Expected final source HP.</param>
    /// <param name="expectedTarget">Expected final target HP.</param>
    [Theory]
    [InlineData(50, 100, 10, 50, 55, 90)]
    [InlineData(50, 3, 30, 50, 52, 0)]
    [InlineData(98, 100, 10, 50, 100, 90)]
    [InlineData(50, 1, 30, 1, 51, 0)]
    [InlineData(50, 100, 0, 50, 50, 100)]
    [InlineData(50, 100, 10, 0, 50, 90)]
    [InlineData(50, 0, 10, 50, 50, 0)]
    public void HealingUsesActualHealthLoss(int sourceHp, int targetHp, int damage, int percent, int expectedSource, int expectedTarget)
    {
        var source = Actor(sourceHp);
        var target = Actor(targetHp);
        var resolution = Resolve(source, target, damage, percent / 100m);
        Assert.Equal(expectedSource, FinalHp(resolution, source));
        Assert.Equal(expectedTarget, FinalHp(resolution, target));
        Assert.Equal(2, resolution.DiceTrace.Rolls.Count);
        Assert.True(resolution.Changes.OfType<HealthChanged>().GroupBy(change => change.CombatantId).All(group => group.Count() == 1));
        Assert.Equal(sourceHp, source.Health);
    }

    /// <summary>Defenses and minimum physical damage precede healing, while full fire immunity supplies no healing.</summary>
    /// <param name="physical">Whether the hit is subject to the minimum physical damage rule.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HealingUsesFinalDefenses(bool physical)
    {
        var type = physical ? DamageType.Slashing : DamageType.Fire;
        var source = Actor(50);
        var target = new CombatantSnapshot(new(Guid.NewGuid()), 100, 100, 0, 0, [new(type, 1)], []);
        var result = Resolve(source, target, 20, 0.5m, type: type);
        Assert.Equal(physical ? 51 : 50, FinalHp(result, source));
        Assert.Equal(physical ? 99 : 100, FinalHp(result, target));
    }

    /// <summary>Earlier healing or extra loss on the victim changes the final actual loss used as the basis.</summary>
    /// <param name="adjustment">Prior target health adjustment.</param>
    /// <param name="expectedHealing">Expected source healing at 50 percent.</param>
    [Theory]
    [InlineData(10, 0)]
    [InlineData(5, 3)]
    [InlineData(-10, 10)]
    public void EarlierTargetHealthAdjustmentsAreIncluded(int adjustment, int expectedHealing)
    {
        var source = Actor(50);
        var target = Actor(50);
        var result = Resolve(source, target, 10, 0.5m, adjustment);
        Assert.Equal(50 + expectedHealing, FinalHp(result, source));
    }

    /// <summary>Self-inflicted damage cannot create a circular healing basis or be used to farm health.</summary>
    [Fact]
    public void SelfDamageDoesNotTriggerLifesteal()
    {
        var actor = Actor(50);
        var result = Resolve(actor, actor, 10, 1);
        Assert.Equal(40, FinalHp(result, actor));
    }

    /// <summary>Payloads copy behavior choices and reject null entries; invalid fractions cannot become content.</summary>
    [Fact]
    public void BehaviorDefinitionsAreValidatedAndCopied()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LifestealDefinition(-0.01m));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LifestealDefinition(1.01m));
        AttackBehaviorDefinition[] choices = [new LifestealDefinition(0.25m)];
        var payload = Payload(1, DamageType.Fire, choices);
        choices[0] = new LifestealDefinition(1);
        Assert.Equal(0.25m, Assert.IsType<LifestealDefinition>(Assert.Single(payload.Behaviors)).Fraction);
        Assert.Throws<ArgumentException>(() => Payload(1, DamageType.Fire, [null!]));
    }

    private CombatResolution Resolve(CombatantSnapshot source, CombatantSnapshot target, int damage, decimal fraction,
        int targetAdjustment = 0, DamageType type = DamageType.Fire)
    {
        var request = new CombatRequest(Guid.NewGuid(), source.Id, target.Id, Payload(damage, type), new(1, source, target));
        var pipeline = new CombatPipeline([
            new AttackRollStep(new AttackRollResolver()), new AttackOutcomeStep(new D20AttackOutcomePolicy()),
            new RawDamageStep(new RawDamageResolver()), new AttackQualityStep(new AttackQualityDamageResolver(new DamageMultipliers(0, 1, 1, 1))),
            new DefenseStep(new DefenseResolver(new HitMagnitudeArmorFormula())), new TargetAdjustment(targetAdjustment),
            new ApplyEffectsStep([new LifestealEffect(new(fraction), new HealthStateProjector())])
        ]);
        var outcome = LogOutcome(new CombatResolver(new CombatRequestValidator(), pipeline,
            new CombatResolutionBuilder(new HealthStateProjector())).Resolve(request, new OnesDice()));
        return Assert.IsType<ResolvedCombat>(outcome).Resolution;
    }
    private static AttackPayload Payload(int damage, DamageType type, IReadOnlyList<AttackBehaviorDefinition>? behaviors = null) =>
        new(new(1, 20), 30, NaturalOutcomeShiftRules.Standard, [new(new(1, 2), damage - 1, type)], [], AttackTags.Melee, behaviors);
    private static CombatantSnapshot Actor(int health) => new(new(Guid.NewGuid()), health, 100, 0, 0, [], []);
    private static int FinalHp(CombatResolution resolution, CombatantSnapshot actor) =>
        resolution.Changes.OfType<HealthChanged>().SingleOrDefault(value => value.CombatantId == actor.Id)?.CurrentHealth ?? actor.Health;
    private sealed class TargetAdjustment(int amount) : ICombatStep
    {
        /// <inheritdoc />
        public CombatContext Execute(CombatContext context, IDiceRoller dice) => amount == 0 ? context : context with
        {
            HealthAdjustments = context.HealthAdjustments.Add(new(context.Request.TargetId, amount))
        };
    }
    private sealed class OnesDice : IDiceRoller
    {
        private readonly List<DiceRoll> _rolls = [];
        /// <inheritdoc />
        public DiceTrace Trace => new(_rolls);
        /// <inheritdoc />
        public DiceRoll Roll(DiceFormula formula)
        {
            var result = new DiceRoll(formula, Enumerable.Repeat(1, formula.Count).ToArray(), formula.Count);
            _rolls.Add(result);
            return result;
        }
    }
}
