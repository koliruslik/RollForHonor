using RollForHonor.Application.Combat.Models;
using RollForHonor.Application.Combat.Services;
using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Combat.Damage.Configuration;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.Resolution.Services;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Resources.Services;
using RollForHonor.Domain.Stats;
using RollForHonor.Infrastructure.Combat;
using RollForHonor.Infrastructure.Dice;

namespace RollForHonor.Application.Tests.Combat;

/// <summary>Verifies atomic stat ownership, current-resource separation and actual pipeline consumption.</summary>
public sealed class CharacterStatRuntimeTests
{
    /// <summary>Stat changes publish one new version without altering historical views or healing on maximum increases.</summary>
    [Fact]
    public void MaximumChangesClampDownwardWithoutHealingAndPreserveHistoricalReads()
    {
        var state = new InMemoryCombatState();
        var id = new CombatantId(Guid.NewGuid());
        var profile = Profile();
        state.AddStatCombatant(id, profile);
        state.UpdateCombatant(new(id, 70, 100, 2, 10, [], []));
        var before = state.Read(id, id);
        var larger = new StatProfile(profile.BaseValues
            .With(new(StatType.MaxHealth), 200)
            .With(new(StatType.MaxMana), 200));

        state.UpdateStats(id, larger);

        Assert.Equal(70, state.Read(id, id).Source.Health);
        Assert.Equal(200, state.Read(id, id).Source.MaxHealth);
        Assert.Equal(before.Version + 1, state.Read(id, id).Version);
        Assert.Equal(100, before.Source.MaxHealth);
        Assert.Equal(200m, state.GetStatProfile(id).BaseValues.Get(new(StatType.MaxMana)));
        Assert.Equal(100m, state.ReadForStart(id, "shot", Guid.NewGuid()).Resources.Single().Amount);

        state.UpdateStats(id, new(profile.BaseValues
            .With(new(StatType.MaxHealth), 50)
            .With(new(StatType.MaxMana), 30)));

        Assert.Equal(50, state.Read(id, id).Source.Health);
        Assert.Equal(30m, state.ReadForStart(id, "shot", Guid.NewGuid()).Resources.Single().Amount);
        Assert.Same(state.Read(id, id).Source, state.Read(id, id).Target);
    }

    /// <summary>Legacy explicit views still work; stat-managed actors reject conflicting direct defense edits.</summary>
    [Fact]
    public void DerivedAttributesHaveOneAuthoritativeUpdatePath()
    {
        var state = new InMemoryCombatState();
        var id = new CombatantId(Guid.NewGuid());
        state.AddStatCombatant(id, Profile());
        var version = state.Read(id, id).Version;

        Assert.Throws<InvalidOperationException>(() => state.UpdateCombatant(new(id, 100, 100, 99, 10, [], [])));
        Assert.Equal(version, state.Read(id, id).Version);
        Assert.Equal(2, state.Read(id, id).Source.Armor);

        var legacy = new CombatantSnapshot(new(Guid.NewGuid()), 20, 20, 0, 0, [], []);
        state.AddCombatant(legacy);
        state.UpdateCombatant(new(legacy.Id, 10, 20, 5, 3, [], []));

        Assert.Equal(5, state.Read(legacy.Id, legacy.Id).Source.Armor);
    }

    /// <summary>Projection overflow cannot publish a new version, profile, health or resource balance.</summary>
    [Fact]
    public void InvalidProjectionIsAtomic()
    {
        var state = new InMemoryCombatState();
        var id = new CombatantId(Guid.NewGuid());
        var profile = Profile();
        state.AddStatCombatant(id, profile);
        var before = state.Read(id, id);

        Assert.Throws<OverflowException>(() => state.UpdateStats(id,
            new(profile.BaseValues.With(new(StatType.MaxHealth), decimal.MaxValue))));

        Assert.Equal(before.Version, state.Read(id, id).Version);
        Assert.Same(before.Source, state.Read(id, id).Source);
        Assert.Same(profile, state.GetStatProfile(id));
        Assert.Equal(100m, state.ReadForStart(id, "shot", Guid.NewGuid()).Resources.Single().Amount);
    }

    /// <summary>Fresh source scaling, d20 bonuses and target resistances are consumed by the real Application impact path.</summary>
    [Fact]
    public void PipelineUsesFreshResolvedOffenseAndPreservesItAfterCommits()
    {
        var state = new InMemoryCombatState();
        var source = new CombatantId(Guid.NewGuid());
        var target = new CombatantId(Guid.NewGuid());
        var sourceProfile = new StatProfile(Profile().BaseValues
            .With(new(StatType.AttackModifier), 3)
            .With(new(StatType.CriticalChance), 1)
            .With(new(StatType.CriticalMultiplier), 99));
        state.AddStatCombatant(source, sourceProfile);
        state.AddStatCombatant(target, new(Profile().BaseValues
            .With(new(StatType.Armor), 0)
            .With(new(StatType.Evasion), 0)
            .With(new(StatType.Resistance, DamageType.Fire), 0.5m)));
        var impacts = new CombatImpactService(state, new CombatSnapshotBuilder(),
            new BasicAttackPipelineFactory(new DamageMultipliers(1, 1, 1, 1)), () => new RandomDiceRoller(new MinimumRandom()));
        var starts = new AttackInitiationService(state, new ResourceCostResolver(new AdditiveResourceCostPolicy()),
            new NoResourceCostModifiers(), impacts, () => TimeSpan.Zero);
        var definition = new AttackDefinition("stat-shot", new(new(1, 20), 0, NaturalOutcomeShiftRules.Standard,
            [new(new(1, 2), 9, DamageType.Fire)], [], AttackTags.Projectile), TimeSpan.FromSeconds(2), []);
        var attack = starts.Start(Guid.NewGuid(), source, definition).Attack!;
        var historical = state.Read(source, target);

        var first = attack.ResolveContact(target).Impact!;

        Assert.True(first.IsCommitted);
        Assert.Equal(95, state.Read(source, target).Target.Health);
        var firstResult = Assert.IsType<ResolvedCombat>(first.Outcome).Resolution;
        Assert.Equal(3, firstResult.Result.Target.AttackRoll!.Modifier);

        state.UpdateStats(source, new(sourceProfile.BaseValues,
            [new(new(StatType.DamageMultiplier), StatOperation.More, 1, "item:test")]));
        var second = attack.ResolveContact(target).Impact!;

        Assert.True(second.IsCommitted);
        Assert.Equal(85, state.Read(source, target).Target.Health);
        Assert.Equal(2m, state.Read(source, target).Source.Offense.DamageMultiplier(DamageType.Fire));
        Assert.Equal(1m, historical.Source.Offense.DamageMultiplier(DamageType.Fire));
        Assert.Equal(TimeSpan.FromSeconds(2), state.ReadForStart(source, "stat-shot", Guid.NewGuid()).ReadyAt);
        Assert.Equal(2, firstResult.DiceTrace.Rolls.Count);
    }

    /// <summary>Typed flat damage is projected into combat and survives ordinary state publication.</summary>
    [Fact]
    public void ProjectionIncludesTypedAddedDamage()
    {
        var state = new InMemoryCombatState();
        var id = new CombatantId(Guid.NewGuid());
        var profile = Profile();
        state.AddStatCombatant(id, new StatProfile(profile.BaseValues,
            [new(new(StatType.AddedDamage, DamageType.Piercing), StatOperation.Flat, 3, "weapon-affix")]));
        Assert.Equal(3, Assert.Single(state.Read(id, id).Source.Offense.AddedDamage).Amount);
        Assert.Equal(3, state.GetStats(id).Get(new(StatType.AddedDamage, DamageType.Piercing)));
    }

    private static StatProfile Profile() => new(new StatBlock(
    [
        new(new(StatType.MaxHealth), 100),
        new(new(StatType.MaxMana), 100),
        new(new(StatType.Armor), 2),
        new(new(StatType.Evasion), 10),
        new(new(StatType.MovementSpeed), 6)
    ]));

    private sealed class MinimumRandom : Random
    {
        /// <inheritdoc />
        public override long NextInt64(long minValue, long maxValue) => minValue;
    }

}
