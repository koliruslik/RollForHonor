using System.Collections.Generic;
using System.Linq;
using Godot;
using RollForHonor.Domain.Items.Affixes.Models;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Domain.Stats;
using RollForHonor.Godot.UI.Common;
using RollForHonor.Godot.UI.Localization;

namespace RollForHonor.Godot.UI.Inventory;

public partial class ItemDetailsPanel
{
    private void BuildProperties()
    {
        _properties.Clear();
        _sources.Clear();
        _inspections.Clear();
        Heading(L.Text("BASE PROPERTIES"));
        WriteWeapon();
        WriteModifiers();

        if (_item.Affixes.Count > 0)
        {
            _properties.AddText("\n");
            Heading(L.Text("AFFIXES"));

            foreach (var affix in _item.Affixes)
            {
                Heading($"{L.Text(affix.DefinitionId.Replace('-', ' '))} · {affix.TierId}");
                WriteAffix(affix);
            }
        }
    }

    private void Heading(string title)
    {
        _properties.PushColor(HonorTheme.Muted);
        _properties.AddText(title + "\n");
        _properties.Pop();
    }

    private void WriteWeapon()
    {
        if (_item.Base.Weapon is not { } weapon)
        {
            return;
        }

        _properties.AddText(L.Format($"Attack speed  {weapon.AttacksPerSecond:0.##}/s\n"));

        if (weapon.ManaCost > 0)
        {
            _properties.AddText(L.Format($"Mana cost  {weapon.ManaCost:0.##}\n"));
        }

        foreach (var damage in weapon.Damage)
        {
            WriteWeaponDamage(damage);
        }
    }

    private void WriteWeaponDamage(RollForHonor.Domain.Combat.Damage.Models.DamageComponent damage)
    {
        var key = new StatKey(StatType.AddedDamage, damage.Type);
        var flat = _item.Base.Modifiers
            .Concat(_item.Affixes
                .SelectMany(affix => affix.Modifiers))
            .Where(modifier => modifier.Key == key)
            .Select(modifier => new StatModifier(modifier.Key, modifier.Operation, modifier.Amount, "item-tooltip"));
        var value = new StatCalculator().Calculate(new StatProfile(new StatBlock(), flat)).Get(key);
        var text = new RollForHonor.Domain.Combat.Damage.Models.DamageComponent(damage.Dice, checked(damage.FlatDamage + (int)decimal.Floor(value)), damage.Type);
        var display = L.Damage(text);
        var contributors = _item.Affixes
            .Where(affix => affix.Modifiers.Any(modifier => modifier.Key == key))
            .ToArray();
        WriteProperty(display, contributors, new StatKey(StatType.DamageMultiplier, damage.Type));
    }

    private void WriteProperty(string text, IReadOnlyList<AffixInstance> contributors, StatKey key)
    {
        if (contributors.Count == 0 && key.DamageKind is null && key.Type is not (StatType.Armor or StatType.DamageMultiplier))
        {
            _properties.AddText(text + "\n");
            return;
        }

        var id = _inspections.Count.ToString();
        _sources[id] = string.Join("\n\n", contributors
            .Select(affix => L.Text(affix.DefinitionId.Replace('-', ' ')) + "\n" + FormatAffix(affix)));
        _inspections[id] = PropertyTopic(text, key, contributors);
        InspectionText.Link(_properties, text, id, CombatGlossary.StatColor(key), contributors.Count > 0);
        _properties.AddText("\n");
    }

    private InspectionTopic PropertyTopic(string value, StatKey key, IReadOnlyList<AffixInstance> contributors)
    {
        var definition = CombatGlossary.Stat(key);
        var affixes = contributors
            .Select(AffixTopic)
            .ToArray();
        var links = definition.Links
            .Concat(affixes
                .Select(topic => new InspectionLink(topic.Title, topic.Accent, () => topic)));

        return new InspectionTopic(definition.Title, definition.Description, definition.Accent, links,
            [new InspectionFact(value, CombatGlossary.StatColor(key), contributors.Count > 0)]);
    }

    private InspectionTopic AffixTopic(AffixInstance affix) => ItemInspectionTopics.Affix(_item.Rarity, affix, _detailed);

    private string FormatAffix(AffixInstance affix)
    {
        var values = string.Join("\n", affix.Modifiers
            .Select((modifier, index) => FormatAffixModifier(affix, modifier, index)));

        return _detailed ? values + L.Text("\nAvailable tiers: ") + FormatAvailableTiers(affix) : values;
    }

    private string FormatAffixModifier(AffixInstance affix, ItemModifier modifier, int index) =>
        ItemInspectionTopics.FormatModifier(affix, modifier, index, _detailed);

    private static string FormatAvailableTiers(AffixInstance affix) => ItemInspectionTopics.FormatAvailableTiers(affix);

    private void WriteModifiers()
    {
        var implicitKeys = _item.Base.Modifiers
            .Select(modifier => (modifier.Key, modifier.Operation))
            .ToHashSet();
        var groups = _item.Base.Modifiers
            .Concat(_item.Affixes
                .SelectMany(affix => affix.Modifiers))
            .GroupBy(modifier => (modifier.Key, modifier.Operation))
            .Where(group => implicitKeys.Contains(group.Key));

        foreach (var group in groups)
        {
            if (group.Key.Key.Type == StatType.AddedDamage && _item.Base.Weapon?.Damage.Any(part => part.Type == group.Key.Key.DamageKind) == true)
            {
                continue;
            }

            var affixes = _item.Affixes
                .Where(affix => affix.Modifiers.Any(modifier => modifier.Key == group.Key.Key && modifier.Operation == group.Key.Operation))
                .ToArray();
            var amount = group.Key.Operation == StatOperation.More
                ? group.Aggregate(1m, (value, modifier) => value * (1 + modifier.Amount)) - 1
                : group.Sum(modifier => modifier.Amount);
            WriteProperty(ItemPresentation.Modifier(new ItemModifier(group.Key.Key, group.Key.Operation, amount)), affixes, group.Key.Key);
        }
    }

    private void WriteAffix(AffixInstance affix)
    {
        var topic = AffixTopic(affix);

        for (var index = 0; index < affix.Modifiers.Count; index++)
        {
            var id = $"affix-{_inspections.Count}";
            var modifier = affix.Modifiers[index];
            _inspections[id] = topic;
            InspectionText.Link(_properties, FormatAffixModifier(affix, modifier, index), id, CombatGlossary.StatColor(modifier.Key), true);
            _properties.AddText("\n");
        }

        if (_detailed)
        {
            InspectionText.Write(_properties, L.Text("Available tiers: ") + FormatAvailableTiers(affix) + "\n", HonorTheme.Muted);
        }
    }
}
