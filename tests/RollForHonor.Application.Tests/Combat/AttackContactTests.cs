using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;
using RollForHonor.Application.Combat.Models;
using RollForHonor.Application.Combat.Ports;
using RollForHonor.Application.Combat.Services;
using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Domain.Combat.Attacks.Services;
using RollForHonor.Domain.Combat.Damage.Configuration;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.Damage.Services;
using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Combat.Resolution.Services;
using RollForHonor.Domain.Combat.Resolution.Services.Steps;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Dice.Models;
using RollForHonor.Domain.Dice.Services;
using RollForHonor.Domain.Resources.Models;
using RollForHonor.Domain.Resources.Services;
using RollForHonor.Domain.Tests.Diagnostics;
using RollForHonor.Infrastructure.Diagnostics;
using Xunit.Abstractions;

namespace RollForHonor.Application.Tests.Combat;

/// <summary>Exercises real launches and impacts behind per-instance contact/lifetime admission.</summary>
public sealed class AttackContactTests : CombatDiagnosticTestBase
{
    /// <summary>Creates tests using the existing opt-in outcome logging helper.</summary>
    /// <param name="output">The current xUnit test output.</param>
    public AttackContactTests(ITestOutputHelper output) : base(output) { }

    /// <summary>Charges once while processing distinct targets and ignoring repeated contacts.</summary>
    [Fact]
    public void OncePerTarget_HitsTwoTargetsWithoutChargingAgain()
    {
        var rig = new Rig();
        var attack = rig.Start(new(AttackRepeatPolicy.OncePerTarget));
        var first = Contact(attack, rig.Target.Id);
        var repeated = Contact(attack, rig.Target.Id);
        var second = Contact(attack, rig.Other.Id);

        Assert.True(first.Impact!.IsCommitted);
        Assert.True(second.Impact!.IsCommitted);
        Assert.Equal(AttackContactStatus.TargetAlreadyContacted, repeated.Status);
        Assert.Null(repeated.Impact);
        Assert.Equal(44, rig.State.Get(rig.Target.Id).Health);
        Assert.Equal(44, rig.State.Get(rig.Other.Id).Health);
        Assert.Equal(2, rig.DiceCreated);
        Assert.Equal(2, rig.State.Reads.Count);
        Assert.Equal(80, rig.State.Balance(rig.Source.Id, Rig.Mana));
        Assert.Single(rig.State.Started);
    }

    /// <summary>An evaded first contact still consumes the target and cannot be rerolled by another callback.</summary>
    [Fact]
    public void OncePerTarget_EvadedContactStillConsumesTarget()
    {
        var rig = new Rig { AttackRoll = 1 };
        var attack = rig.Start(new(AttackRepeatPolicy.OncePerTarget));
        var first = Contact(attack, rig.Target.Id);
        rig.AttackRoll = 20;
        var second = Contact(attack, rig.Target.Id);

        Assert.Equal(AttackOutcome.Evaded, ((ResolvedCombat)first.Impact!.Outcome).Resolution.Result.Target.Outcome);
        Assert.Equal(AttackContactStatus.TargetAlreadyContacted, second.Status);
        Assert.Equal(50, rig.State.Get(rig.Target.Id).Health);
        Assert.Equal(1, rig.DiceCreated);
        Assert.True(attack.IsActive);
    }

    /// <summary>Unrestricted contacts preserve the original separate-resolution behavior.</summary>
    [Fact]
    public void EveryContact_ProducesIndependentResolutionsAtFreshVersions()
    {
        var rig = new Rig();
        var attack = rig.Start(new());
        var first = Contact(attack, rig.Target.Id);
        var second = Contact(attack, rig.Target.Id);

        Assert.NotEqual(first.Impact!.ResolutionId, second.Impact!.ResolutionId);
        Assert.Equal(38, rig.State.Get(rig.Target.Id).Health);
        Assert.Equal(2, rig.DiceCreated);
        Assert.Equal(80, rig.State.Balance(rig.Source.Id, Rig.Mana));
        var versions = rig.State.Committed.Select(result => result.BasedOnStateVersion).ToArray();
        Assert.Equal(versions[0] + 1, versions[1]);
    }

    /// <summary>First-contact termination applies on a target, even when no damage is dealt.</summary>
    [Fact]
    public void StopOnAnyContact_EvadedHitEndsAttackBeforeAnotherTarget()
    {
        var rig = new Rig { AttackRoll = 1 };
        var attack = rig.Start(new(stopOn: AttackStopCondition.CombatantContact | AttackStopCondition.EnvironmentContact));
        Contact(attack, rig.Target.Id);
        var later = Contact(attack, rig.Other.Id);

        Assert.Equal(AttackContactStatus.AttackEnded, later.Status);
        Assert.Equal(AttackEndReason.CombatantContact, attack.EndReason);
        Assert.False(attack.IsActive);
        Assert.Equal(1, rig.DiceCreated);
        Assert.Equal(50, rig.State.Get(rig.Other.Id).Health);
        Assert.False(attack.End(AttackEndReason.AnimationFinished));
        Assert.Equal(AttackEndReason.CombatantContact, attack.EndReason);
    }

    /// <summary>Piercing contacts continue until an obstacle ends the instance.</summary>
    [Fact]
    public void StopOnEnvironment_ContinuesThroughTargetsAndStopsAtWall()
    {
        var rig = new Rig();
        var attack = rig.Start(new(AttackRepeatPolicy.OncePerTarget, AttackStopCondition.EnvironmentContact));
        Contact(attack, rig.Target.Id);
        Contact(attack, rig.Other.Id);
        Assert.True(attack.IsActive);
        var wall = attack.ContactEnvironment();
        var later = Contact(attack, rig.Source.Id);

        Assert.Equal(AttackContactStatus.Processed, wall.Status);
        Assert.Null(wall.Impact);
        Assert.Equal(AttackEndReason.EnvironmentContact, wall.EndReason);
        Assert.Equal(AttackContactStatus.AttackEnded, later.Status);
        Assert.Equal(2, rig.DiceCreated);
    }

    /// <summary>Obstacle contacts perform no combat calculation and obey the configured termination flags.</summary>
    /// <param name="stopOn">The configured termination contact flags.</param>
    /// <param name="remainsActive">Expected activity after the obstacle contact.</param>
    [Theory]
    [InlineData(AttackStopCondition.None, true)]
    [InlineData(AttackStopCondition.CombatantContact, true)]
    [InlineData(AttackStopCondition.EnvironmentContact, false)]
    [InlineData(AttackStopCondition.CombatantContact | AttackStopCondition.EnvironmentContact, false)]
    public void EnvironmentContact_DoesNotCreateDice(AttackStopCondition stopOn, bool remainsActive)
    {
        var rig = new Rig();
        var attack = rig.Start(new(stopOn: stopOn));
        var result = attack.ContactEnvironment();

        Assert.Null(result.Impact);
        Assert.Equal(remainsActive, attack.IsActive);
        Assert.Equal(0, rig.DiceCreated);
        Assert.Empty(rig.State.Reads);
    }

    /// <summary>Lifetime uses the original launch time and rejects a contact exactly at expiry.</summary>
    [Fact]
    public void TimedAttack_ExpiresAtExactDeadlineWithoutAnotherImpact()
    {
        var rig = new Rig { Now = TimeSpan.FromSeconds(10) };
        var attack = rig.Start(new(maxLifetime: TimeSpan.FromSeconds(3)));
        Assert.Equal(TimeSpan.FromSeconds(13), attack.ExpiresAt);
        rig.Now = TimeSpan.FromSeconds(12);
        Contact(attack, rig.Target.Id);
        rig.Now = TimeSpan.FromSeconds(13);
        var expired = Contact(attack, rig.Other.Id);

        Assert.Equal(AttackContactStatus.AttackEnded, expired.Status);
        Assert.Equal(AttackEndReason.LifetimeExpired, attack.EndReason);
        Assert.Equal(1, rig.DiceCreated);
        Assert.False(attack.UpdateLifetime());
        Assert.Equal(80, rig.State.Balance(rig.Source.Id, Rig.Mana));
    }

    /// <summary>A host can expire an attack without any collision, while cooldown and costs remain launch-only.</summary>
    [Fact]
    public void LifetimeUpdate_ExpiresWithoutContactOrRefund()
    {
        var rig = new Rig();
        var attack = rig.Start(new(maxLifetime: TimeSpan.FromSeconds(1)));
        rig.Now = TimeSpan.FromSeconds(1);

        Assert.False(attack.UpdateLifetime());
        Assert.Equal(AttackEndReason.LifetimeExpired, attack.EndReason);
        Assert.Empty(rig.State.Reads);
        Assert.Equal(0, rig.DiceCreated);
        Assert.Equal(80, rig.State.Balance(rig.Source.Id, Rig.Mana));
        Assert.Equal(TimeSpan.FromSeconds(2), rig.State.ReadyAt(rig.Source.Id, "contact-test"));
    }

    /// <summary>Animation completion is idempotent and cannot reopen the attack or reroll another contact.</summary>
    [Fact]
    public void AnimationEnd_IsIdempotentAndBlocksBothContactMethods()
    {
        var rig = new Rig();
        var attack = rig.Start(new());
        Assert.True(attack.End(AttackEndReason.AnimationFinished));
        Assert.False(attack.End());
        Assert.Equal(AttackEndReason.AnimationFinished, attack.EndReason);
        Assert.Equal(AttackContactStatus.AttackEnded, Contact(attack, rig.Target.Id).Status);
        Assert.Equal(AttackContactStatus.AttackEnded, attack.ContactEnvironment().Status);
        Assert.Throws<InvalidOperationException>(() => attack.Resolve(rig.Target.Id));
        Assert.Equal(0, rig.DiceCreated);
    }

    /// <summary>Another accepted attack receives its own target history and pays its own launch cost.</summary>
    [Fact]
    public void NewAttack_HasIndependentContactHistory()
    {
        var rig = new Rig();
        var rules = new AttackExecutionRules(AttackRepeatPolicy.OncePerTarget);
        var first = rig.Start(rules);
        Contact(first, rig.Target.Id);
        rig.Now = TimeSpan.FromSeconds(2);
        var second = rig.Start(rules);
        Contact(second, rig.Target.Id);

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(38, rig.State.Get(rig.Target.Id).Health);
        Assert.Equal(60, rig.State.Balance(rig.Source.Id, Rig.Mana));
    }

    /// <summary>A conflict consumes the admitted contact without retrying or rolling new dice.</summary>
    [Fact]
    public void OncePerTarget_ConflictIsNotAutomaticallyRetried()
    {
        var rig = new Rig();
        var attack = rig.Start(new(AttackRepeatPolicy.OncePerTarget));
        rig.State.BeforeCommit = _ => rig.State.ExternalUpdate(rig.Source);
        var first = Contact(attack, rig.Target.Id);
        var repeated = Contact(attack, rig.Target.Id);

        Assert.Equal(CombatCommitStatus.VersionConflict, first.Impact!.CommitStatus);
        Assert.Equal(AttackContactStatus.TargetAlreadyContacted, repeated.Status);
        Assert.Equal(50, rig.State.Get(rig.Target.Id).Health);
        Assert.Equal(1, rig.DiceCreated);
        Assert.Equal(1, rig.State.CommitAttempts);
    }

    /// <summary>A failed commit preserves state and does not make the same target eligible again.</summary>
    [Fact]
    public void OncePerTarget_ExceptionDoesNotReleaseContactReservation()
    {
        var rig = new Rig();
        var attack = rig.Start(new(AttackRepeatPolicy.OncePerTarget));
        rig.State.FailAfterStaging = true;
        Assert.Throws<InvalidOperationException>(() => attack.ResolveContact(rig.Target.Id));
        Assert.Equal(AttackContactStatus.TargetAlreadyContacted, Contact(attack, rig.Target.Id).Status);
        Assert.Equal(50, rig.State.Get(rig.Target.Id).Health);
        Assert.Equal(1, rig.DiceCreated);
    }

    /// <summary>Concurrent callbacks for a once-only target admit exactly one calculation.</summary>
    [Fact]
    public void OncePerTarget_ConcurrentCallbacksAdmitOneImpact()
    {
        var rig = new Rig();
        var attack = rig.Start(new(AttackRepeatPolicy.OncePerTarget));
        var contacts = new ConcurrentBag<AttackContactResult>();
        Parallel.For(0, 20, _ => contacts.Add(attack.ResolveContact(rig.Target.Id)));

        Assert.Single(contacts, result => result.Status == AttackContactStatus.Processed);
        Assert.Equal(19, contacts.Count(result => result.Status == AttackContactStatus.TargetAlreadyContacted));
        Assert.Equal(44, rig.State.Get(rig.Target.Id).Health);
        Assert.Equal(1, rig.DiceCreated);
    }

    /// <summary>Serializing launch reports and reading status properties cannot consult or advance the game clock.</summary>
    [Fact]
    public void DiagnosticsAndStatusReads_DoNotAdvanceLifetimeClock()
    {
        using var writer = new StringWriter();
        var rig = new Rig(new LoggerCombatDiagnostics(NullLogger.Instance, jsonl: writer));
        var attack = rig.Start(new(maxLifetime: TimeSpan.FromSeconds(1)));
        Assert.Equal(1, rig.ClockReads);
        rig.Now = TimeSpan.FromSeconds(10);
        Assert.True(attack.IsActive);
        Assert.Null(attack.EndReason);
        Assert.Equal(1, rig.ClockReads);
        Assert.False(attack.UpdateLifetime());
        Assert.Equal(2, rig.ClockReads);
        Assert.NotEmpty(writer.ToString());
    }

    /// <summary>Rejects invalid lifetime durations instead of accepting unusable attack definitions.</summary>
    /// <param name="seconds">The nonpositive configured duration in seconds.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonpositiveLifetime_IsInvalid(int seconds) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new AttackExecutionRules(maxLifetime: TimeSpan.FromSeconds(seconds)));

    /// <summary>Unknown enum values and contact flags cannot silently define attack behavior.</summary>
    [Fact]
    public void UnknownPoliciesAndEndReason_AreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AttackExecutionRules((AttackRepeatPolicy)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AttackExecutionRules(stopOn: (AttackStopCondition)8));
        var rig = new Rig();
        var attack = rig.Start(new());
        Assert.Throws<ArgumentOutOfRangeException>(() => attack.End((AttackEndReason)99));
        Assert.True(attack.IsActive);
    }

    private AttackContactResult Contact(AttackInstance attack, CombatantId target)
    {
        var contact = attack.ResolveContact(target);
        if (contact.Impact is { } impact) LogOutcome(impact.Outcome);
        return contact;
    }

    private sealed class Rig
    {
        internal static readonly ResourceId Mana = new("mana");
        internal CombatantSnapshot Source { get; } = Character();
        internal CombatantSnapshot Target { get; } = Character();
        internal CombatantSnapshot Other { get; } = Character();
        internal TestCombatStateAdapter State { get; }
        internal AttackInitiationService Starts { get; }
        internal TimeSpan Now { get; set; }
        internal int AttackRoll { get; set; } = 15;
        internal int ClockReads { get; private set; }
        internal int DiceCreated { get; private set; }

        internal Rig(ICombatDiagnostics? diagnostics = null)
        {
            State = new(Source, Target, Other);
            State.SetResources(Source.Id, new ResourceAmount(Mana, 100));
            var impacts = new CombatImpactService(State, new CombatSnapshotBuilder(), new PipelineFactory(), () =>
            {
                DiceCreated++;
                return new FixedDice(AttackRoll);
            }, diagnostics);
            Starts = new(State, new ResourceCostResolver(new AdditiveResourceCostPolicy()), new NoReductions(), impacts,
                () => { ClockReads++; return Now; }, diagnostics);
        }

        internal AttackInstance Start(AttackExecutionRules rules)
        {
            var payload = new AttackPayload(new(1, 20), 0, NaturalOutcomeShiftRules.Standard,
                [new(new(1, 6), 3, DamageType.Slashing)], [], AttackTags.Projectile);
            var definition = new AttackDefinition("contact-test", payload, TimeSpan.FromSeconds(2), [new(Mana, 20)], rules);
            var result = Starts.Start(Guid.NewGuid(), Source.Id, definition);
            Assert.Equal(AttackStartStatus.Started, result.Status);
            return result.Attack!;
        }

        private static CombatantSnapshot Character() => new(new(Guid.NewGuid()), 50, 100, 0, 15, [], []);
    }

    private sealed class PipelineFactory : ICombatPipelineFactory
    {
        /// <inheritdoc />
        public ICombatStep Create(AttackInstance attack, CombatStateSnapshot snapshot) => new CombatPipeline([
            new AttackRollStep(new AttackRollResolver()), new AttackOutcomeStep(new D20AttackOutcomePolicy()),
            new RawDamageStep(new RawDamageResolver()),
            new AttackQualityStep(new AttackQualityDamageResolver(new DamageMultipliers(0, 0.5m, 1, 2))),
            new DefenseStep(new DefenseResolver(new HitMagnitudeArmorFormula()))
        ]);
    }

    private sealed class NoReductions : IResourceCostModifierProvider
    {
        /// <inheritdoc />
        public IReadOnlyList<ResourceCostReduction> GetReductions(CombatantSnapshot source) => [];
    }

    private sealed class FixedDice(int attackRoll) : IDiceRoller
    {
        private readonly List<DiceRoll> _rolls = [];
        /// <inheritdoc />
        public DiceTrace Trace => new(_rolls);
        /// <inheritdoc />
        public DiceRoll Roll(DiceFormula formula)
        {
            var values = Enumerable.Repeat(formula.Sides == 20 ? attackRoll : 3, formula.Count).ToArray();
            var roll = new DiceRoll(formula, values, values.Sum());
            _rolls.Add(roll);
            return roll;
        }
    }
}
