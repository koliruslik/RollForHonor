using System.Text;
using RollForHonor.Domain.Combat.Damage;
using RollForHonor.Domain.Combat.Effects;
using RollForHonor.Domain.Combat.Resolution;

namespace RollForHonor.Domain.Tests.Diagnostics;

internal static class CombatResolutionDiagnosticFormatter
{
    public static string Format(ICombatResolutionOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        
        return outcome switch
        {
            ResolvedCombat resolved => FormatResolved(resolved.Resolution),
            RejectedCombat rejected => FormatRejected(rejected.Rejection),
            _ => throw new NotSupportedException(
                $"Unsupported combat resolution outcome: {outcome.GetType().FullName}.")
        };
    }

    private static string FormatResolved(CombatResolution resolution)
    {
        var result = resolution.Result;
        var target = result.Target;

        var builder = new StringBuilder();

        builder.AppendLine("┌─ COMBAT RESOLVED");
        builder.AppendLine($"│ Resolution : {resolution.ResolutionId}");
        builder.AppendLine(
            $"│ State      : version {resolution.BasedOnStateVersion}");

        builder.AppendLine("├─ PARTICIPANTS");
        builder.AppendLine($"│ Source     : {result.SourceId}");
        builder.AppendLine($"│ Target     : {target.TargetId}");

        builder.AppendLine("├─ RESULT");
        builder.AppendLine($"│ Outcome    : {target.Outcome}");
        builder.AppendLine($"│ Defeated   : {target.IsDefeated}");

        AppendDamage(builder, target.DamageResolution.FinalDamage);
        AppendEffects(builder, target.Effects);

        builder.Append("└─ END");

        return builder.ToString();
    }

    private static string FormatRejected(CombatRejection rejection)
    {
        var builder = new StringBuilder();

        builder.AppendLine("┌─ COMBAT REJECTED");
        builder.AppendLine($"│ Reason      : {rejection.Reason}");
        builder.AppendLine($"│ Description : {rejection.Description}");
        builder.Append("└─ END");

        return builder.ToString();
    }
    
    private static void AppendDamage(
        StringBuilder builder,
        FinalDamage damage)
    {
        var totalsByType = damage.Damage.Components
            .GroupBy(component => component.Type)
            .Select(group => new
            {
                Type = group.Key,
                Amount = group.Sum(component => component.Amount)
            })
            .OrderBy(item => item.Type)
            .ToArray();

        builder.AppendLine("├─ DAMAGE");
        builder.AppendLine($"│ Total      : {damage.Sum()}");

        if (totalsByType.Length == 0)
        {
            builder.AppendLine("│ By type    : none");
            return;
        }

        builder.AppendLine("│ By type    :");

        for (var index = 0; index < totalsByType.Length; index++)
        {
            var item = totalsByType[index];
            var isLast = index == totalsByType.Length - 1;
            var branch = isLast ? "└─" : "├─";

            builder.AppendLine(
                $"│   {branch} {item.Type,-11}: {item.Amount}");
        }
    }

    private static void AppendEffects(
        StringBuilder builder,
        IReadOnlyList<ResolvedCombatEffect> effects)
    {
        builder.AppendLine("├─ EFFECTS");
        builder.AppendLine($"│ Count      : {effects.Count}");

        if (effects.Count == 0)
        {
            builder.AppendLine("│ Effects    : none");
            return;
        }

        for (var index = 0; index < effects.Count; index++)
        {
            var effect = effects[index];
            var isLast = index == effects.Count - 1;
            var branch = isLast ? "└─" : "├─";
            var continuation = isLast ? " " : "│";

            builder.AppendLine(
                $"│   {branch} {effect.Definition.GetType().Name}");
            builder.AppendLine(
                $"│   {continuation}  ID     : {effect.Definition.Id}");
            builder.AppendLine(
                $"│   {continuation}  Target : {effect.TargetId}");
        }
    }
}