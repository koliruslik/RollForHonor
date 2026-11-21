using RollForHonor.Domain.Dice.Models;
using RollForHonor.Domain.Dice.Services;
using RollForHonor.Domain.Items.Affixes.Models;
using RollForHonor.Domain.Items.Models;

namespace RollForHonor.Domain.Items.Affixes.Services;

/// <summary>Rolls one shared d100 quality for an explicitly selected tier and stores concrete modifier values.</summary>
public sealed class AffixRoller
{
    /// <summary>Generates values with inclusive endpoints: 1 maps to minimum and 100 maps to maximum.</summary>
    /// <param name="instanceId">A nonblank identity unique within the eventual item.</param>
    /// <param name="definition">The immutable affix definition.</param>
    /// <param name="tierId">The tier explicitly selected by the caller.</param>
    /// <param name="dice">The caller-owned random source and trace.</param>
    /// <returns>An immutable result with one shared quality roll and rounded values.</returns>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    /// <exception cref="ArgumentException">An identifier is blank.</exception>
    /// <exception cref="KeyNotFoundException">The definition has no matching tier.</exception>
    /// <exception cref="OverflowException">Decimal range interpolation exceeds its representable range.</exception>
    public AffixInstance Roll(string instanceId, AffixDefinition definition, string tierId, IDiceRoller dice)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(tierId);
        ArgumentNullException.ThrowIfNull(dice);

        var tier = definition.Tiers.FirstOrDefault(candidate => candidate.Id == tierId)
            ?? throw new KeyNotFoundException("The affix has no matching tier.");
        var quality = dice.Roll(new DiceFormula(1, 100)).Total;
        var modifiers = tier.Modifiers
            .Select(range => RollValue(range, quality));

        return new AffixInstance(instanceId, definition, tier.Id, quality, modifiers);
    }

    private static ItemModifier RollValue(AffixModifierRange range, int quality)
    {
        var position = (quality - 1) / 99m;
        var value = range.Minimum + (range.Maximum - range.Minimum) * position;
        var rounded = decimal.Round(value, range.DecimalPlaces, MidpointRounding.AwayFromZero);

        return new ItemModifier(range.Key, range.Operation, rounded);
    }
}
