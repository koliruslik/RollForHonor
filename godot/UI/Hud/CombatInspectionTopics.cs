using System.Linq;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Stats;
using RollForHonor.Godot.UI.Common;
using RollForHonor.Godot.UI.Localization;

namespace RollForHonor.Godot.UI.Hud;

/// <summary>Builds explanation snapshots from committed combat results without recalculation or randomness.</summary>
public static class CombatInspectionTopics
{
    /// <summary>Captures the attack roll, bonus and every actual damage die.</summary>
    /// <param name="source">The source display name.</param>
    /// <param name="target">The target display name.</param>
    /// <param name="resolved">The committed result.</param>
    /// <returns>A roll card retaining its original facts.</returns>
    public static InspectionTopic Dice(string source, string target, ResolvedCombat resolved)
    {
        var roll = resolved.Resolution.Result.Target.AttackRoll;
        var facts = resolved.Resolution.DiceTrace.Rolls
            .Select(value => new InspectionFact($"{value.Formula} [{string.Join(", ", value.Results)}] = {value.Total}", HonorTheme.Parchment));

        return new(L.Format($"{source} → {target} · d20 {roll?.Roll.Total.ToString() ?? "—"}"),
            roll?.ToString() ?? L.Text("This action did not include an attack roll."), HonorTheme.Secondary,
            [CombatGlossary.Link(new StatKey(StatType.AttackModifier))], facts);
    }

    /// <summary>Captures typed totals and links to every recorded arithmetic stage.</summary>
    /// <param name="result">The committed target result.</param>
    /// <returns>A damage explanation, including an explicit absent-stage state.</returns>
    public static InspectionTopic Damage(TargetCombatResult result)
    {
        if (result.DamageResolution is not { } damage)
        {
            return new(L.Text("Damage"), L.Text("This action did not include a damage stage."), HonorTheme.Secondary);
        }

        var links = damage.AfterDefenses.Breakdown.Steps
            .Select(step => new InspectionLink(StepName(step.Type), HonorTheme.Secondary, () => Stage(step)));

        return new(L.Text("Damage breakdown"), L.Text("Recorded values before and after target defenses."), HonorTheme.Secondary, links,
            TypedFacts(damage.BeforeDefenses.Damage, damage.AfterDefenses.Damage));
    }

    private static string StepName(DamageStepType type) => type switch
    {
        DamageStepType.WeaponRoll => L.Text("Weapon dice"),
        DamageStepType.AttackerModifier => L.Text("Source modifiers"),
        DamageStepType.AttackQuality => L.Text("Attack quality"),
        DamageStepType.ArmorMitigation => L.Text("Armor reduction"),
        DamageStepType.ResistanceMitigation => L.Text("Resistance reduction"),
        DamageStepType.Finalization => L.Text("Minimum physical damage"),
        _ => type.ToString()
    };

    private static InspectionTopic Stage(DamageStep step)
    {
        var types = step.Input.Components
            .Concat(step.Output.Components)
            .Select(component => component.Type)
            .Distinct()
            .Select(type => new InspectionLink(L.Format($"{type} damage"), CombatGlossary.DamageColor(type), () => CombatGlossary.Damage(type)));

        return new(StepName(step.Type), L.Text("Input → output for this recorded stage."), HonorTheme.Secondary, types, TypedFacts(step.Input, step.Output));
    }

    private static InspectionFact[] TypedFacts(DamagePacket before, DamagePacket after) => before.Components
        .Concat(after.Components)
        .Select(component => component.Type)
        .Distinct()
        .Order()
        .Select(type => new InspectionFact(L.Format($"{type}  {Amount(before, type)} → {Amount(after, type)}"), CombatGlossary.DamageColor(type)))
        .ToArray();

    private static int Amount(DamagePacket packet, DamageType type) => packet.Components
        .Where(component => component.Type == type)
        .Sum(component => component.Amount);
}
