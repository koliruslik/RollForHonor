using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.Effects.Models;
using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Combat.Resolution.Services;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Dice.Models;

namespace RollForHonor.Domain.Tests.Combat.Resolution;

public sealed class CombatRequestValidatorTests
{
    private readonly CombatRequestValidator _validator = new();

    [Fact]
    public void Validate_WhenRequestIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => _validator.Validate(null!));
    }

    [Fact]
    public void Validate_WhenRequestIsValid_ReturnsNull()
    {
        var request = CreateRequest();

        var rejection = _validator.Validate(request);

        Assert.Null(rejection);
    }

    [Fact]
    public void Validate_WhenSourceDoesNotMatchSnapshot_ReturnsSourceNotFound()
    {
        var request = CreateRequest(sourceId: CreateCombatantId());

        var rejection = _validator.Validate(request);

        AssertRejection(rejection, CombatRejectionReason.SourceNotFound);
    }

    [Fact]
    public void Validate_WhenTargetDoesNotMatchSnapshot_ReturnsTargetNotFound()
    {
        var request = CreateRequest(targetId: CreateCombatantId());

        var rejection = _validator.Validate(request);

        AssertRejection(rejection, CombatRejectionReason.TargetNotFound);
    }

    [Fact]
    public void Validate_WhenSourceIsDefeated_ReturnsSourceDefeated()
    {
        var source = CreateCombatant(health: 0);
        var request = CreateRequest(source: source);

        var rejection = _validator.Validate(request);

        AssertRejection(rejection, CombatRejectionReason.SourceDefeated);
    }

    [Fact]
    public void Validate_WhenSelfTargetUsesSameSnapshot_ReturnsNull()
    {
        var combatant = CreateCombatant();
        var request = CreateRequest(
            source: combatant,
            target: combatant);

        var rejection = _validator.Validate(request);

        Assert.Null(rejection);
    }

    [Fact]
    public void Validate_WhenSelfTargetUsesDifferentSnapshots_ReturnsInvalidState()
    {
        var combatantId = CreateCombatantId();
        var source = CreateCombatant(id: combatantId);
        var target = CreateCombatant(id: combatantId);
        var request = CreateRequest(
            source: source,
            target: target);

        var rejection = _validator.Validate(request);

        AssertRejection(rejection, CombatRejectionReason.InvalidState);
    }

    [Fact]
    public void Validate_WhenAttackContainsOnlyDamage_ReturnsNull()
    {
        var request = CreateRequest(attack: CreateDamageAttack());

        var rejection = _validator.Validate(request);

        Assert.Null(rejection);
    }

    [Fact]
    public void Validate_WhenAttackContainsOnlyEffect_ReturnsNull()
    {
        var effect = new TestEffectDefinition(
            new EffectDefinitionId(Guid.NewGuid()));
        var request = CreateRequest(
            attack: CreateAttack(effects: [effect]));

        var rejection = _validator.Validate(request);

        Assert.Null(rejection);
    }

    [Fact]
    public void Validate_WhenAttackHasNoDamageOrEffects_ReturnsInvalidAttack()
    {
        var request = CreateRequest(attack: CreateAttack());

        var rejection = _validator.Validate(request);

        AssertRejection(rejection, CombatRejectionReason.InvalidAttack);
    }

    [Fact]
    public void Validate_WhenAttackRollIsNotD20_ReturnsInvalidAttack()
    {
        var request = CreateRequest(
            attack: CreateDamageAttack(new DiceFormula(2, 10)));

        var rejection = _validator.Validate(request);

        AssertRejection(rejection, CombatRejectionReason.InvalidAttack);
    }

    private static CombatRequest CreateRequest(
        CombatantSnapshot? source = null,
        CombatantSnapshot? target = null,
        CombatantId? sourceId = null,
        CombatantId? targetId = null,
        AttackPayload? attack = null)
    {
        source ??= CreateCombatant();
        target ??= CreateCombatant();

        return new CombatRequest(
            Guid.NewGuid(),
            sourceId ?? source.Id,
            targetId ?? target.Id,
            attack ?? CreateDamageAttack(),
            new CombatStateSnapshot(1, source, target));
    }

    private static CombatantSnapshot CreateCombatant(
        CombatantId? id = null,
        int health = 10)
    {
        return new CombatantSnapshot(
            id ?? CreateCombatantId(),
            health,
            maxHealth: 10,
            armor: 0,
            evasion: 0,
            resistances: [],
            effects: []);
    }

    private static CombatantId CreateCombatantId()
    {
        return new CombatantId(Guid.NewGuid());
    }

    private static AttackPayload CreateDamageAttack(
        DiceFormula? attackRoll = null)
    {
        return CreateAttack(
            attackRoll: attackRoll,
            damage:
            [
                new DamageComponent(
                    new DiceFormula(1, 6),
                    flatDamage: 0,
                    DamageType.Slashing)
            ]);
    }

    private static AttackPayload CreateAttack(
        DiceFormula? attackRoll = null,
        IReadOnlyList<DamageComponent>? damage = null,
        IReadOnlyList<CombatEffectDefinition>? effects = null)
    {
        return new AttackPayload(
            attackRoll ?? new DiceFormula(1, 20),
            attackModifier: 0,
            NaturalOutcomeShiftRules.Standard,
            damage ?? [],
            effects ?? [],
            AttackTags.Melee);
    }

    private static void AssertRejection(
        CombatRejection? rejection,
        CombatRejectionReason expectedReason)
    {
        Assert.NotNull(rejection);
        Assert.Equal(expectedReason, rejection.Reason);
        Assert.False(string.IsNullOrWhiteSpace(rejection.Description));
    }

    private sealed record TestEffectDefinition : CombatEffectDefinition
    {
        public TestEffectDefinition(EffectDefinitionId id)
            : base(id)
        {
        }
    }
}
