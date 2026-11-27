using System.Globalization;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Domain.Stats;

namespace RollForHonor.Domain.Items.Services;

/// <summary>Turns stored item values into existing stat inputs without recalculating rolls or changing live actors.</summary>
public sealed class ItemModifierProjector
{
    /// <summary>Builds copied stat modifiers with unambiguous item/affix source identities.</summary>
    /// <param name="item">The owned item whose actual values should contribute.</param>
    /// <returns>Implicit and affix modifiers in stored order, suitable for complete profile composition.</returns>
    /// <exception cref="ArgumentNullException">The item is null.</exception>
    public IReadOnlyList<StatModifier> Project(ItemInstance item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var itemSource = $"Item:{Encode(item.Id)}";
        var implicitModifiers = ItemBaseScaling.Modifiers(item)
            .Select(modifier => ToStatModifier(modifier, $"{itemSource}:Base"));
        var affixModifiers = item.Affixes
            .SelectMany(affix => affix.Modifiers
                .Select(modifier => ToStatModifier(modifier, $"{itemSource}:Affix:{Encode(affix.Id)}")));

        var modifiers = implicitModifiers
            .Concat(affixModifiers)
            .ToArray();

        return Array.AsReadOnly(modifiers);
    }

    private static string Encode(string id) => $"{id.Length.ToString(CultureInfo.InvariantCulture)}:{id}";

    private static StatModifier ToStatModifier(ItemModifier modifier, string source) =>
        new(modifier.Key, modifier.Operation, modifier.Amount, source);
}
