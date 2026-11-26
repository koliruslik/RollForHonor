using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.Effects.Models;
using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Combat.Resolution.Services.Steps;
using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Domain.Tests.Combat.Resolution;

public sealed partial class CombatPipelineTests
{
    /// <summary>Flat bonuses introduce new damage types before scaling, without rolling extra dice or applying twice.</summary>
    [Fact]
    public void AddedDamageIsScaledOnceWithoutAdditionalDice()
    {
        var offense = new CombatOffense(multipliers: [new(DamageType.Fire, 2)], addedDamage: [new(DamageType.Fire, 3)]);
        var source = new CombatantSnapshot(new(Guid.NewGuid()), 100, 100, 0, 0, [], [], offense);
        var request = CreateRequest(source: source);
        var dice = new CountingDice();
        var raw = RawDamage().Execute(new CombatContext(request), dice);
        var result = new SourceDamageStep().Execute(raw, dice);
        Assert.Equal(10, raw.BeforeDefenses!.Sum());
        Assert.Equal(16, result.BeforeDefenses!.Sum());
        Assert.Equal(6, Assert.Single(result.BeforeDefenses.Damage.Components.Where(part => part.Type == DamageType.Fire)).Amount);
        Assert.Single(dice.Trace.Rolls);
        Assert.Throws<InvalidOperationException>(() => new SourceDamageStep().Execute(result, dice));
    }

    /// <summary>Extra typed dice enter raw damage once and are scaled before defenses with their captured trace.</summary>
    [Fact]
    public void ExtraDamageDiceAreRolledAndScaledOnce()
    {
        var offense = new CombatOffense(multipliers: [new(DamageType.Fire, 2)],
            addedDice: [new(new(2, 4), 0, DamageType.Fire)]);
        var source = new CombatantSnapshot(new(Guid.NewGuid()), 100, 100, 0, 0, [], [], offense);
        var dice = new CountingDice();
        var raw = RawDamage().Execute(new CombatContext(CreateRequest(source: source)), dice);
        var result = new SourceDamageStep().Execute(raw, dice);

        Assert.Equal(18, raw.BeforeDefenses!.Sum());
        Assert.Equal(26, result.BeforeDefenses!.Sum());
        Assert.Equal(2, dice.Trace.Rolls.Count);
        Assert.Equal(4, dice.Trace.Rolls[1].Formula.Sides);
    }

    /// <summary>Periodic ticks do not acquire flat bonuses intended for attacks.</summary>
    [Fact]
    public void AddedAttackDamageDoesNotLeakIntoPeriodicTicks()
    {
        var source = new CombatantSnapshot(new(Guid.NewGuid()), 100, 100, 0, 0, [], [], new CombatOffense(addedDamage: [new(DamageType.Fire, 99)], addedDice: [new(new(2, 12), 0, DamageType.Fire)]));
        var effect = new CombatEffectSnapshot(new(Guid.NewGuid()), new PeriodicDamageEffectDefinition(new(Guid.NewGuid()), [new(new(1, 6), 0, DamageType.Fire)]), source);
        var target = Combatant(100, effects: [effect]);
        var request = new CombatRequest(Guid.NewGuid(), effect, target, 1);
        var dice = new CountingDice();
        var raw = RawDamage().Execute(new CombatContext(request), dice);
        var result = new SourceDamageStep().Execute(raw, dice);
        Assert.Equal(6, result.BeforeDefenses!.Sum());
        Assert.Single(dice.Trace.Rolls);
    }
}
