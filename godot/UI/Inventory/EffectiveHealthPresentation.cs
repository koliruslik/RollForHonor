using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.Damage.Services;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Stats;
using RollForHonor.Godot.UI.Common;
using RollForHonor.Godot.UI.Localization;

namespace RollForHonor.Godot.UI.Inventory;

internal static class EffectiveHealthPresentation
{
    private static readonly EffectiveHealthCalculator Calculator = new();

    internal static (string Average, InspectionTopic Topic) Build(CombatantSnapshot actor)
    {
        var types = DamageTypes.HitTypes.ToArray();
        var values = types.Select(type => Calculator.Calculate(actor, type)).ToArray();
        var average = values.Any(value => value is null) ? null : values.Average();
        var links = types.Select(type => CombatGlossary.Link(new(StatType.Resistance, type))).ToList();
        var facts = new List<InspectionFact> { new(L.Text("By damage type"), HonorTheme.Brass) };
        facts.AddRange(types.Select((type, index) => new InspectionFact(
            L.Format($"{type}: {Format(values[index])}"), CombatGlossary.DamageColor(type), LinkIndex: index)));
        facts.Add(new(L.Text("Combined damage · 50/50"), HonorTheme.Brass));

        for (var first = 0; first < types.Length; first++)
        {
            for (var second = first + 1; second < types.Length; second++)
            {
                var left = types[first];
                var right = types[second];
                var packet = new DamagePacket([new(left, 5), new(right, 5)]);
                var value = Format(Calculator.Calculate(actor, packet));
                var title = L.Format($"{left} + {right}");
                var index = links.Count;
                links.Add(new(title, HonorTheme.Parchment, () => new(title,
                    L.Text("One hit: 5 damage of each type, 10 total. Each component keeps its own resistance and rounding; armor reduces the combined physical portion. This value is calculated from the mixed hit, not by averaging the two separate EHP values."),
                    HonorTheme.Parchment,
                    [CombatGlossary.Link(new(StatType.Resistance, left)), CombatGlossary.Link(new(StatType.Resistance, right)),
                        CombatGlossary.Link(new(StatType.Armor)), CombatGlossary.Link(new(StatType.Evasion))],
                    [new(value, HonorTheme.Brass), new(L.Format($"{left}: 5"), CombatGlossary.DamageColor(left), LinkIndex: 0),
                        new(L.Format($"{right}: 5"), CombatGlossary.DamageColor(right), LinkIndex: 1)])));
                facts.Add(new(L.Format($"{title}: {value}"), HonorTheme.Parchment, LinkIndex: index));
            }
        }

        var description = L.Text("The character sheet shows the arithmetic average of the six individual damage types, with equal weight. Combined categories are separate 50/50 hits and do not count again in this average. All estimates use full health, 10 total incoming damage and a d20 bonus of +4. Criticals, recovery, periodic damage and attack frequency are excluded. Armor protection changes with hit size. An immune category is shown as infinity; an average containing infinity is also infinite.");
        return (Format(average), new(L.Text("Effective health (EHP)"), description, HonorTheme.Brass, links, facts));
    }

    private static string Format(decimal? value) => value?.ToString("0.#", CultureInfo.InvariantCulture) ?? "∞";
}
