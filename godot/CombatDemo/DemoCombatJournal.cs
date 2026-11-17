using System.Linq;
using System.Text;
using RollForHonor.Application.Combat.Models;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Combat.State.Changes;

namespace RollForHonor.Godot.CombatDemo;

/// <summary>Formats returned calculation facts for the scene journal without parsing diagnostic output or rerolling dice.</summary>
public static class DemoCombatJournal
{
    /// <summary>Renders one contact with actual input views, all dice, damage stages and separate publication status.</summary>
    /// <param name="contact">The actual contact/calculation/commit response.</param>
    /// <param name="input">Views observed immediately before the synchronous impact.</param>
    /// <param name="sourceName">The host's source display name.</param>
    /// <param name="targetName">The host's target display name.</param>
    /// <returns>BBCode journal text suitable for the demo's RichTextLabel.</returns>
    public static string Format(AttackContactResult contact, CombatStateRead input, string sourceName, string targetName)
    {
        var text = new StringBuilder().AppendLine($"\n[b]{sourceName} → {targetName}[/b] | {contact.Status}");
        if (contact.Impact is not { } impact) return text.AppendLine($"No calculation. End: {contact.EndReason}").ToString();
        text.AppendLine($"Publication: {impact.CommitStatus?.ToString() ?? "not attempted"}");
        if (impact.Outcome is not ResolvedCombat resolved)
            return text.AppendLine($"Domain: {impact.Outcome}").ToString();
        var result = resolved.Resolution;
        var target = result.Result.Target;
        text.AppendLine($"[b]{target.Outcome}[/b] | {string.Join(" + ", target.DamageResolution?.AfterDefenses.Damage.Components.Select(Colored) ?? [])} ({targetName})");
        if (!impact.IsCommitted) text.AppendLine("Calculation proposal only: these changes were not newly applied.");
        if (target.AttackRoll is { } attack)
            text.AppendLine($"Attack: {attack.Roll.Formula} [{string.Join(", ", attack.Roll.Results)}] + {attack.Modifier} = {attack.Total}; evasion {input.Target.Evasion}; margin {attack.Total - input.Target.Evasion} (before natural shift)");
        text.AppendLine($"Target input: HP {input.Target.Health}/{input.Target.MaxHealth}; armor {input.Target.Armor}; " +
            $"resistances {string.Join(", ", input.Target.Resistances.Select(value => $"{value.Type} {value.Value:P0}"))}");
        if (target.DamageResolution is { } damage)
        {
            foreach (var step in damage.AfterDefenses.Breakdown.Steps)
            {
                text.AppendLine($"  {step.Type}: {Packet(step.Input)} → {Packet(step.Output)}" +
                    (step.Multiplier is { } multiplier ? $" | ×{multiplier}" : "") +
                    (step.FlatBonus is { } bonus ? $" | flat {bonus}" : "") +
                    (step.Origin is { } origin ? $" | origin {origin}" : ""));
                foreach (var component in step.ComponentRolls)
                    text.AppendLine($"    {component.Component.Type}: {component.Roll.Formula} [{string.Join(", ", component.Roll.Results)}] + flat {component.Component.FlatDamage} = {component.Amount}");
            }
        }
        text.AppendLine("Complete dice trace:");
        foreach (var roll in result.DiceTrace.Rolls)
            text.AppendLine($"  {roll.Formula}: [{string.Join(", ", roll.Results)}] = {roll.Total}");
        text.AppendLine(impact.IsCommitted ? "Applied changes:" : "Proposed changes:");
        foreach (var change in result.Changes)
        {
            text.AppendLine(change switch
            {
                HealthChanged health => $"  {(health.CombatantId == input.Source.Id ? sourceName : targetName)} HP: {health.PreviousHealth} → {health.CurrentHealth} (actual clamped change)",
                EffectApplied applied => $"  Apply {DemoEffectCatalog.Name(applied.Effect.Definition.Id)} to {(applied.CombatantId == input.Source.Id ? sourceName : targetName)}; source captured at application",
                EffectRemoved removed => $"  Remove status {removed.EffectId} from {(removed.CombatantId == input.Source.Id ? sourceName : targetName)}",
                CombatantDefeated => "  Combatant defeated",
                _ => $"  {change}"
            });
        }
        if (result.Changes.Count == 0) text.AppendLine("  No state changes.");
        return text.ToString();
    }

    private static string Packet(DamagePacket packet) => packet.Components.Count == 0 ? "0" : string.Join(" + ", packet.Components.Select(Colored));
    private static string Colored(DamageAmount amount)
    {
        var color = amount.Type switch
        {
            DamageType.Fire => "#ff9955", DamageType.Cold => "#77d5ff", DamageType.Lightning => "#f3dd63",
            DamageType.Poison => "#97d977", DamageType.Slashing => "#ffaaaa",
            DamageType.Bludgeoning => "#c1bdd6", _ => "#e1c8b1"
        };
        return $"[color={color}]{amount.Amount} {amount.Type}[/color]";
    }
}
