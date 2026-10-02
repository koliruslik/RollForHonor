using RollForHonor.Domain.Combat.Attacks;
using RollForHonor.Domain.Combat.Damage;
using RollForHonor.Domain.Combat.Effects;
using RollForHonor.Domain.Combat.Resolution;
using RollForHonor.Domain.Combat.Services;
using RollForHonor.Domain.Combat.State;
using RollForHonor.Domain.Combat.StateChanges;
using RollForHonor.Domain.Combat.StateMutations;
using RollForHonor.Domain.Dice;

namespace RollForHonor.Domain.Tests.Combat;

public sealed class CombatResolverTests
{
    [Fact]
    public void Resolve_WhenCombinedDamageDefeatsTarget_ProducesCanonicalHealthChangesAndDefeat()
    {
        var source = CreateCombatant(health: 50, maxHealth: 100);
        var target = CreateCombatant(health: 20, maxHealth: 100);
        var damage = CreateDamage(amount: 15);
        var effectResolution = new EffectResolution(
            effects: [],
            healthAdjustments:
            [
                new HealthAdjustment(target.Id, -10),
                new HealthAdjustment(source.Id, 10)
            ],
            stateChanges: []);

        var resolver = CreateResolver(damage, effectResolution);

        var request = new CombatRequest(
            Guid.NewGuid(),
            source.Id,
            target.Id,
            CreateAttack(),
            new CombatStateSnapshot(1, source, target));

        var outcome = resolver.Resolve(request, new FixedDiceRoller());

        var resolved = Assert.IsType<ResolvedCombat>(outcome);
        var healthChanges = resolved.Resolution.Changes
            .OfType<HealthChanged>()
            .ToArray();

        Assert.Collection(
            healthChanges,
            sourceChange =>
            {
                Assert.Equal(source.Id, sourceChange.CombatantId);
                Assert.Equal(50, sourceChange.PreviousHealth);
                Assert.Equal(60, sourceChange.CurrentHealth);
            },
            targetChange =>
            {
                Assert.Equal(target.Id, targetChange.CombatantId);
                Assert.Equal(20, targetChange.PreviousHealth);
                Assert.Equal(0, targetChange.CurrentHealth);
            });

        var defeated = Assert.Single(
            resolved.Resolution.Changes.OfType<CombatantDefeated>());

        Assert.Equal(target.Id, defeated.CombatantId);
        Assert.True(resolved.Resolution.Result.Target.IsDefeated);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public void Resolve_WhenAttackAppliesManyEffects_PreservesEveryEffectAndStateChange(
        int effectCount)
    {
        var source = CreateCombatant(health: 50, maxHealth: 100);
        var target = CreateCombatant(health: 50, maxHealth: 100);
        var definitions = Enumerable.Range(0, effectCount)
            .Select(_ => new TestEffectDefinition(
                new EffectDefinitionId(Guid.NewGuid())))
            .ToArray();

        var resolvedEffects = definitions
            .Select(definition => new ResolvedCombatEffect(target.Id, definition))
            .ToArray();

        var appliedEffects = definitions
            .Select(definition => new EffectApplied(
                target.Id,
                new CombatEffectSnapshot(
                    new EffectInstanceId(Guid.NewGuid()),
                    definition)))
            .ToArray();

        var effectResolution = new EffectResolution(
            resolvedEffects,
            healthAdjustments: [],
            appliedEffects);

        var damage = CreateDamage(amount: 0);
        var resolver = CreateResolver(damage, effectResolution);
        var request = new CombatRequest(
            Guid.NewGuid(),
            source.Id,
            target.Id,
            CreateAttack(definitions),
            new CombatStateSnapshot(1, source, target));

        var outcome = resolver.Resolve(request, new FixedDiceRoller());

        var resolved = Assert.IsType<ResolvedCombat>(outcome);
        var actualEffects = resolved.Resolution.Result.Target.Effects;
        var actualChanges = resolved.Resolution.Changes;

        Assert.Equal(effectCount, actualEffects.Count);
        Assert.Equal(
            definitions.Select(definition => definition.Id),
            actualEffects.Select(effect => effect.Definition.Id));

        Assert.Equal(effectCount, actualChanges.Count);
        Assert.All(
            actualChanges,
            change => Assert.IsType<EffectApplied>(change));
        Assert.False(resolved.Resolution.Result.Target.IsDefeated);
    }

    private static CombatResolver CreateResolver(
        DamageResolution damage,
        EffectResolution effectResolution)
    {
        return new CombatResolver(
            new AcceptingRequestValidator(),
            new FixedAttackRollResolver(),
            new HitOutcomePolicy(),
            new FixedAttackDamageResolver(damage.UnmitigatedDamage),
            new FixedDefenseResolver(damage.FinalDamage),
            new FixedEffectResolver(effectResolution),
            new HealthStateProjector());
    }

    private static AttackPayload CreateAttack(
        IReadOnlyList<CombatEffectDefinition>? effects = null)
    {
        return new AttackPayload(
            new DiceFormula(1, 20),
            attackModifier: 0,
            damage: [],
            effects ?? [],
            AttackTags.Melee);
    }

    private static DamageResolution CreateDamage(int amount)
    {
        var packet = new DamagePacket(
            [new DamageAmount(DamageType.Slashing, amount)]);

        var breakdown = new DamageBreakdown(
            [new DamageStep(DamageStepType.Finalization, packet, packet)]);

        return new DamageResolution(
            new UnmitigatedDamage(breakdown, packet),
            new FinalDamage(breakdown, packet));
    }

    private static CombatantSnapshot CreateCombatant(int health, int maxHealth)
    {
        return new CombatantSnapshot(
            new CombatantId(Guid.NewGuid()),
            health,
            maxHealth,
            armor: 0,
            evasion: 0,
            resistances: [],
            effects: []);
    }

    private sealed class AcceptingRequestValidator : ICombatRequestValidator
    {
        public CombatRejection? Validate(CombatRequest request)
        {
            return null;
        }
    }

    private sealed class FixedAttackRollResolver : IAttackRollResolver
    {
        public AttackRollResult Resolve(
            AttackPayload attack,
            CombatantSnapshot source,
            CombatantSnapshot target,
            IDiceRoller dice)
        {
            var roll = new DiceRoll(attack.AttackRoll, [10], 10);
            return new AttackRollResult(roll, attack.AttackModifier, 10);
        }
    }

    private sealed class HitOutcomePolicy : IAttackOutcomePolicy
    {
        public AttackOutcome Determine(
            AttackPayload attack,
            CombatantSnapshot source,
            CombatantSnapshot target,
            AttackRollResult attackRoll)
        {
            return AttackOutcome.Hit;
        }
    }

    private sealed class FixedAttackDamageResolver : IAttackDamageResolver
    {
        private readonly UnmitigatedDamage _damage;

        public FixedAttackDamageResolver(UnmitigatedDamage damage)
        {
            _damage = damage;
        }

        public UnmitigatedDamage Resolve(
            AttackPayload attack,
            CombatantSnapshot source,
            AttackRollResult attackRoll,
            AttackOutcome outcome,
            IDiceRoller dice)
        {
            return _damage;
        }
    }

    private sealed class FixedDefenseResolver : IDefenseResolver
    {
        private readonly FinalDamage _damage;

        public FixedDefenseResolver(FinalDamage damage)
        {
            _damage = damage;
        }

        public FinalDamage Resolve(
            UnmitigatedDamage unmitigatedDamage,
            CombatantSnapshot target)
        {
            return _damage;
        }
    }

    private sealed class FixedEffectResolver : ICombatEffectResolver
    {
        private readonly EffectResolution _resolution;

        public FixedEffectResolver(EffectResolution resolution)
        {
            _resolution = resolution;
        }

        public EffectResolution Resolve(
            AttackPayload attack,
            CombatantSnapshot source,
            CombatantSnapshot target,
            AttackRollResult attackRoll,
            AttackOutcome outcome,
            UnmitigatedDamage damage,
            FinalDamage finalDamage)
        {
            return _resolution;
        }
    }

    private sealed class FixedDiceRoller : IDiceRoller
    {
        public DiceTrace Trace { get; } = new([]);

        public DiceRoll Roll(DiceFormula formula)
        {
            var results = Enumerable.Repeat(1, formula.Count).ToArray();
            return new DiceRoll(formula, results, results.Sum());
        }
    }

    private sealed record TestEffectDefinition : CombatEffectDefinition
    {
        public TestEffectDefinition(EffectDefinitionId id)
            : base(id)
        {
        }
    }
}
