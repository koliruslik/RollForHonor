using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.Effects.Models;
using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Combat.State.Changes;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Dice.Models;

namespace RollForHonor.Domain.Tests;

public sealed class ModelImmutabilityTests
{
    [Theory]
    [InlineData("DamagePacket.Components")]
    [InlineData("DamageBreakdown.Steps")]
    [InlineData("DiceRoll.Results")]
    [InlineData("DiceTrace.Rolls")]
    [InlineData("AttackPayload.Damage")]
    [InlineData("AttackPayload.Effects")]
    [InlineData("CombatantSnapshot.Resistances")]
    [InlineData("CombatantSnapshot.Effects")]
    [InlineData("EffectResolution.Effects")]
    [InlineData("EffectResolution.HealthAdjustments")]
    [InlineData("EffectResolution.StateChanges")]
    [InlineData("TargetCombatResult.Effects")]
    [InlineData("CombatResolution.Changes")]
    public void Collections_RejectWritesThroughCastsAndDoNotAliasConstructorInputs(string property)
    {
        var id = new CombatantId(Guid.NewGuid());
        DamageAmount[] amounts = [new(DamageType.Slashing, 10)];
        var packet = new DamagePacket(amounts);
        DamageStep[] steps = [new(DamageStepType.WeaponRoll, packet, packet)];
        var breakdown = new DamageBreakdown(steps);
        int[] values = [1];
        var roll = new DiceRoll(new DiceFormula(1, 20), values, 1);
        DiceRoll[] rolls = [roll];
        var trace = new DiceTrace(rolls);
        CombatEffectDefinition[] definitions = [new TestDefinition()];
        DamageComponent[] components = [new(new DiceFormula(1, 6), 0, DamageType.Slashing)];
        var attack = new AttackPayload(new DiceFormula(1, 20), 0,
            NaturalOutcomeShiftRules.Standard, components, definitions, AttackTags.Melee);
        ResistanceValue[] resistances = [new(DamageType.Fire, 0.5m)];
        CombatEffectSnapshot[] effects = [new(new EffectInstanceId(Guid.NewGuid()), definitions[0])];
        var snapshot = new CombatantSnapshot(id, 50, 100, 0, 0, resistances, effects);
        ResolvedCombatEffect[] resolved = [new(id, definitions[0])];
        HealthAdjustment[] adjustments = [new(id, 10)];
        ICombatStateChange[] changes = [new EffectApplied(id, effects[0])];
        var effectResult = new EffectResolution(resolved, adjustments, changes);
        var targetResult = new TargetCombatResult(id, null, null, null, resolved, false);
        var resolution = new CombatResolution(Guid.NewGuid(), 1,
            new CombatResult(id, targetResult), changes, trace);

        var inspections = new Dictionary<string, Action>
        {
            ["DamagePacket.Components"] = () => Check(amounts, packet.Components, new DamageAmount(DamageType.Fire, 999)),
            ["DamageBreakdown.Steps"] = () => Check(steps, breakdown.Steps, new DamageStep(DamageStepType.Finalization, packet, packet)),
            ["DiceRoll.Results"] = () => Check(values, roll.Results, 20),
            ["DiceTrace.Rolls"] = () => Check(rolls, trace.Rolls, new DiceRoll(new DiceFormula(1, 6), [6], 6)),
            ["AttackPayload.Damage"] = () => Check(components, attack.Damage, new DamageComponent(new DiceFormula(1, 20), 999, DamageType.Fire)),
            ["AttackPayload.Effects"] = () => Check(definitions, attack.Effects, new TestDefinition()),
            ["CombatantSnapshot.Resistances"] = () => Check(resistances, snapshot.Resistances, new ResistanceValue(DamageType.Fire, 2m)),
            ["CombatantSnapshot.Effects"] = () => Check(effects, snapshot.Effects, new CombatEffectSnapshot(new EffectInstanceId(Guid.NewGuid()), new TestDefinition())),
            ["EffectResolution.Effects"] = () => Check(resolved, effectResult.Effects, new ResolvedCombatEffect(id, new TestDefinition())),
            ["EffectResolution.HealthAdjustments"] = () => Check(adjustments, effectResult.HealthAdjustments, new HealthAdjustment(id, 999)),
            ["EffectResolution.StateChanges"] = () => Check(changes, effectResult.StateChanges, new HealthChanged(id, 50, 0)),
            ["TargetCombatResult.Effects"] = () => Check(resolved, targetResult.Effects, new ResolvedCombatEffect(id, new TestDefinition())),
            ["CombatResolution.Changes"] = () => Check(changes, resolution.Changes, new HealthChanged(id, 50, 0))
        };

        inspections[property]();
    }

    private static void Check<T>(T[] original, IReadOnlyList<T> exposed, T replacement)
    {
        var expected = original[0];
        original[0] = replacement;
        Assert.Equal(expected, exposed[0]);
        // A read-only interface alone is insufficient: arrays also implement writable IList<T>.
        if (exposed is IList<T> list)
            Assert.Throws<NotSupportedException>(() => list[0] = replacement);
        Assert.Equal(expected, exposed[0]);
    }

    private sealed record TestDefinition() : CombatEffectDefinition(new EffectDefinitionId(Guid.NewGuid()));
}
