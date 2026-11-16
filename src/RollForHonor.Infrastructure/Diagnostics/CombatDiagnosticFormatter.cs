using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using RollForHonor.Application.Combat.Models;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.Resolution.Models;

namespace RollForHonor.Infrastructure.Diagnostics;

/// <summary>Selects a projection of the same captured combat report.</summary>
public enum CombatDiagnosticView
{
    /// <summary>One line describing the attempt and actual acceptance or commit status.</summary>
    Summary,
    /// <summary>Ordered steps with intermediate damage, dice and finalization values.</summary>
    Steps,
    /// <summary>A complete labeled tree of all report fields.</summary>
    Full,
    /// <summary>One complete JSON object, without a trailing newline.</summary>
    Jsonl
}

/// <summary>All views are projections of the same report; no arithmetic or dice are repeated here.</summary>
public static class CombatDiagnosticFormatter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(), new RuntimeTypeConverterFactory() }
    };

    /// <summary>Formats captured impact data without repeating gameplay calculation.</summary>
    /// <param name="report">The impact inputs, captured steps and execution result.</param>
    /// <param name="view">The desired text or JSON representation.</param>
    /// <returns>The report projection without a trailing newline.</returns>
    public static string Format(CombatDiagnosticReport report, CombatDiagnosticView view)
    {
        if (view == CombatDiagnosticView.Jsonl) return JsonSerializer.Serialize(report, JsonOptions);
        var resolved = report.Calculation as ResolvedCombat;
        var damage = resolved?.Resolution.Result.Target.DamageResolution?.AfterDefenses.Sum();
        var status = report.Error is not null ? $"FAILED/{report.Phase}: {report.Error}"
            : report.Result?.CommitStatus?.ToString() ?? "Rejected";
        var text = new StringBuilder().AppendLine(
            $"attack={report.AttackId} impact={report.ResolutionId} {report.SourceId} -> {report.TargetId} " +
            $"{status}; outcome={resolved?.Resolution.Result.Target.Outcome}; damage={damage}");
        if (view == CombatDiagnosticView.Summary) return text.ToString().TrimEnd();

        if (view == CombatDiagnosticView.Full)
        {
            Render(text, "report", JsonSerializer.SerializeToNode(report, JsonOptions), 0);
            return text.ToString().TrimEnd();
        }

        if (report.Request is { } request)
        {
            text.AppendLine($"version={request.StateSnapshot.Version}; armor={request.StateSnapshot.Target.Armor}; " +
                $"evasion={request.StateSnapshot.Target.Evasion}; attackModifier={request.Attack?.AttackModifier}");
            Render(text, "attack", JsonSerializer.SerializeToNode(request.Attack, JsonOptions), 0);
            Render(text, "resistances", JsonSerializer.SerializeToNode(request.StateSnapshot.Target.Resistances, JsonOptions), 0);
            Render(text, "sourceEffects", JsonSerializer.SerializeToNode(request.StateSnapshot.Source.Effects, JsonOptions), 0);
            Render(text, "targetEffects", JsonSerializer.SerializeToNode(request.StateSnapshot.Target.Effects, JsonOptions), 0);
        }
        foreach (var step in report.Steps)
        {
            var indent = new string(' ', step.Depth * 2);
            text.AppendLine($"{indent}{step.Name} dice=[{step.DiceStartIndex},{step.DiceEndIndex}) {step.Error}");
            text.AppendLine($"{indent}  before: {Describe(step.Before)}");
            if (step.After is { } after) text.AppendLine($"{indent}  after:  {Describe(after)}");
            if (step.After?.BeforeDefenses is { } before)
                Render(text, "damageBreakdown", JsonSerializer.SerializeToNode(before.Breakdown, JsonOptions), step.Depth + 1);
            if (step.After?.AfterDefenses is { } afterDefenses)
                Render(text, "defenseBreakdown", JsonSerializer.SerializeToNode(afterDefenses.Breakdown, JsonOptions), step.Depth + 1);
            if (step.Details is not null)
                Render(text, "result", JsonSerializer.SerializeToNode(step.Details, step.Details.GetType(), JsonOptions), step.Depth + 1);
            if (report.Dice is { } dice)
                for (var i = step.DiceStartIndex; i < step.DiceEndIndex; i++)
                {
                    var roll = dice.Rolls[i];
                    text.AppendLine($"{indent}  roll[{i}]: {roll.Formula.Count}d{roll.Formula.Sides} " +
                        $"[{string.Join(", ", roll.Results)}] = {roll.Total}");
                }
        }
        if (resolved is not null)
            Render(text, "finalization/proposedChanges", JsonSerializer.SerializeToNode(resolved.Resolution.Changes, JsonOptions), 0);
        return text.ToString().TrimEnd();
    }

    /// <summary>Formats captured launch data and resource projections.</summary>
    /// <param name="report">The launch inputs, proposal and actual result.</param>
    /// <param name="view">The desired text or JSON representation.</param>
    /// <returns>The report projection without a trailing newline.</returns>
    public static string Format(AttackStartDiagnosticReport report, CombatDiagnosticView view)
    {
        if (view == CombatDiagnosticView.Jsonl) return JsonSerializer.Serialize(report, JsonOptions);
        var text = new StringBuilder().AppendLine($"start={report.RequestId} attack={report.Result?.AttackId} " +
            $"source={report.SourceId} {report.Result?.Status}; {report.Error}");
        if (view != CombatDiagnosticView.Summary)
            Render(text, "launch", JsonSerializer.SerializeToNode(report, JsonOptions), 0);
        return text.ToString().TrimEnd();
    }

    private static string Describe(Domain.Combat.Resolution.Models.CombatStepState state) =>
        $"attack={state.AttackRoll?.Total} modifier={state.AttackRoll?.Modifier} outcome={state.Outcome}; " +
        $"beforeDefenses={Packet(state.BeforeDefenses)} afterDefenses={Packet(state.AfterDefenses)}; " +
        $"effects={state.Effects.Length} healthAdjustments={state.HealthAdjustments.Length}";

    private static string Packet(DamageCalculationResult? damage) => damage is null ? "<unset>" :
        string.Join(" + ", damage.Damage.Components.Select(value => $"{value.Amount} {value.Type}"));

    private static void Render(StringBuilder text, string label, JsonNode? node, int depth)
    {
        var indent = new string(' ', depth * 2);
        if (node is JsonObject obj)
        {
            text.AppendLine($"{indent}{label}:");
            foreach (var property in obj) Render(text, property.Key, property.Value, depth + 1);
        }
        else if (node is JsonArray array)
        {
            text.AppendLine($"{indent}{label}: [{array.Count}]");
            for (var i = 0; i < array.Count; i++) Render(text, $"[{i}]", array[i], depth + 1);
        }
        else text.AppendLine($"{indent}{label}: {node?.ToString() ?? "<unset>"}");
    }
    private sealed class RuntimeTypeConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type type) => type.IsInterface || type.IsAbstract;
        public override JsonConverter CreateConverter(Type type, JsonSerializerOptions options) =>
            (JsonConverter)Activator.CreateInstance(typeof(RuntimeTypeConverter<>).MakeGenericType(type))!;
    }

    private sealed class RuntimeTypeConverter<T> : JsonConverter<T>
    {
        public override T? Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
            throw new NotSupportedException("Combat diagnostics support export only.");
        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            var type = value!.GetType();
            var element = JsonSerializer.SerializeToElement(value, type, options);
            if (element.ValueKind != JsonValueKind.Object)
            {
                element.WriteTo(writer);
                return;
            }
            writer.WriteStartObject();
            writer.WriteString("$type", type.Name);
            foreach (var property in element.EnumerateObject()) property.WriteTo(writer);
            writer.WriteEndObject();
        }
    }
}
