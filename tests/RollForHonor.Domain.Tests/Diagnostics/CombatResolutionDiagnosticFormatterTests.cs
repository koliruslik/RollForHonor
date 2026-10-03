using RollForHonor.Domain.Combat.Attacks;
using RollForHonor.Domain.Combat.Damage;
using RollForHonor.Domain.Combat.Effects;
using RollForHonor.Domain.Combat.Resolution;
using RollForHonor.Domain.Combat.State;
using RollForHonor.Domain.Dice;

namespace RollForHonor.Domain.Tests.Diagnostics;

public sealed class CombatResolutionDiagnosticFormatterTests
{
    [Fact]
    public void Format_WhenOutcomeIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => CombatResolutionDiagnosticFormatter.Format(null!));
    }

    [Fact]
    public void Format_WhenCombatIsRejected_IncludesRejectionDetails()
    {
        var rejection = new CombatRejection(
            CombatRejectionReason.SourceDefeated,
            "The source combatant is already defeated.");

        var outcome = new RejectedCombat(rejection);

        var text = CombatResolutionDiagnosticFormatter.Format(outcome);

        Assert.Contains("COMBAT REJECTED", text);
        Assert.Contains("SourceDefeated", text);
        Assert.Contains(rejection.Description, text);
    }

    [Fact]
    public void Format_WhenCombatIsResolved_IncludesResolutionSummary()
    {
        var resolution = CreateResolution();
        var outcome = new ResolvedCombat(resolution);

        var text = CombatResolutionDiagnosticFormatter.Format(outcome);

        Assert.Contains("COMBAT RESOLVED", text);
        Assert.Contains(resolution.ResolutionId.ToString(), text);
        Assert.Contains(resolution.Result.SourceId.ToString(), text);
        Assert.Contains(resolution.Result.Target.TargetId.ToString(), text);
        Assert.Contains("Outcome    : Hit", text);
        Assert.Contains("Defeated   : False", text);
        Assert.Contains("Total      : 7", text);
        Assert.Contains("Slashing   : 7", text);
        Assert.Contains("Count      : 0", text);
        Assert.Contains("Effects    : none", text);
    }

    [Fact]
    public void Format_WhenDamageTypesRepeat_GroupsAndSumsDamageByType()
    {
        var resolution = CreateResolution(
            damageComponents:
            [
                new DamageAmount(DamageType.Slashing, 5),
                new DamageAmount(DamageType.Lightning, 3),
                new DamageAmount(DamageType.Slashing, 7)
            ]);

        var text = CombatResolutionDiagnosticFormatter.Format(
            new ResolvedCombat(resolution));

        Assert.Contains("Total      : 15", text);
        Assert.Contains("Slashing   : 12", text);
        Assert.Contains("Lightning  : 3", text);
        Assert.Equal(
            1,
            text.Split(Environment.NewLine)
                .Count(line => line.Contains("Slashing")));
        Assert.Equal(
            1,
            text.Split(Environment.NewLine)
                .Count(line => line.Contains("Lightning")));
    }

    [Fact]
    public void Format_WhenEffectsArePresent_ListsEveryEffect()
    {
        CombatEffectDefinition[] definitions =
        [
            new TestEffectDefinition(
                new EffectDefinitionId(
                    Guid.Parse("44444444-4444-4444-4444-444444444444"))),
            new TestEffectDefinition(
                new EffectDefinitionId(
                    Guid.Parse("55555555-5555-5555-5555-555555555555")))
        ];

        var resolution = CreateResolution(effectDefinitions: definitions);

        var text = CombatResolutionDiagnosticFormatter.Format(
            new ResolvedCombat(resolution));

        Assert.Contains("EFFECTS", text);
        Assert.Contains("Count      : 2", text);
        Assert.All(
            definitions,
            definition => Assert.Contains(definition.Id.ToString(), text));
        Assert.Equal(
            definitions.Length,
            text.Split(Environment.NewLine)
                .Count(line => line.Contains(nameof(TestEffectDefinition))));
        Assert.Equal(
            definitions.Length,
            text.Split(Environment.NewLine)
                .Count(line => line.Contains(
                    $"Target : {resolution.Result.Target.TargetId}")));
    }

    [Fact]
    public void Format_WhenOutcomeTypeIsUnsupported_ThrowsNotSupportedException()
    {
        var outcome = new UnsupportedOutcome();

        var exception = Assert.Throws<NotSupportedException>(
            () => CombatResolutionDiagnosticFormatter.Format(outcome));

        Assert.Contains(nameof(UnsupportedOutcome), exception.Message);
    }

    private static CombatResolution CreateResolution(
        IReadOnlyList<DamageAmount>? damageComponents = null,
        IReadOnlyList<CombatEffectDefinition>? effectDefinitions = null)
    {
        var sourceId = new CombatantId(
            Guid.Parse("11111111-1111-1111-1111-111111111111"));

        var targetId = new CombatantId(
            Guid.Parse("22222222-2222-2222-2222-222222222222"));

        var attackRoll = new AttackRollResult(
            new DiceRoll(
                new DiceFormula(1, 20),
                [14],
                total: 14),
            modifier: 3,
            total: 17);

        var damagePacket = new DamagePacket(
            damageComponents ??
            [
                new DamageAmount(DamageType.Slashing, 7)
            ]);

        var damageBreakdown = new DamageBreakdown(
        [
            new DamageStep(
                DamageStepType.Finalization,
                damagePacket,
                damagePacket)
        ]);

        var damageResolution = new DamageResolution(
            new UnmitigatedDamage(damageBreakdown, damagePacket),
            new FinalDamage(damageBreakdown, damagePacket));

        var effects = (effectDefinitions ?? [])
            .Select(definition => new ResolvedCombatEffect(
                targetId,
                definition))
            .ToArray();

        var targetResult = new TargetCombatResult(
            targetId,
            attackRoll,
            AttackOutcome.Hit,
            damageResolution,
            effects,
            isDefeated: false);

        return new CombatResolution(
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            basedOnStateVersion: 4,
            new CombatResult(sourceId, targetResult),
            changes: [],
            new DiceTrace([attackRoll.Roll]));
    }

    private sealed record UnsupportedOutcome
        : ICombatResolutionOutcome;

    private sealed record TestEffectDefinition : CombatEffectDefinition
    {
        public TestEffectDefinition(EffectDefinitionId id)
            : base(id)
        {
        }
    }
}
