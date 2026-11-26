using RollForHonor.Domain.Dice.Models;
using RollForHonor.Domain.Dice.Services;
using RollForHonor.Domain.Items.Affixes.Models;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Domain.Rewards;

namespace RollForHonor.Domain.Items.Affixes.Services;

/// <summary>Rolls one shared d100 quality for an explicitly selected tier and stores concrete modifier values.</summary>
public sealed class AffixRoller
{
    /// <summary>Rolls a natural d100, adds Luck up to 100 and interpolates the effective quality inclusively.</summary>
    /// <param name="instanceId">A nonblank identity unique within the eventual item.</param>
    /// <param name="definition">The immutable affix definition.</param>
    /// <param name="tierId">The tier explicitly selected by the caller.</param>
    /// <param name="dice">The caller-owned random source and trace.</param>
    /// <param name="luck">Captured character Luck; negative values behave as zero.</param>
    /// <param name="tierRoll">Optional natural d20 already used to select the tier.</param>
    /// <param name="tierProgressionBonus">Captured item-level progression bonus, between zero and three.</param>
    /// <returns>An immutable result with one shared quality roll and rounded values.</returns>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    /// <exception cref="ArgumentException">An identifier is blank or tier provenance is not a natural d20.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The progression bonus is outside zero through three.</exception>
    /// <exception cref="KeyNotFoundException">The definition has no matching tier.</exception>
    /// <exception cref="OverflowException">Decimal range interpolation exceeds its representable range.</exception>
    public AffixInstance Roll(string instanceId, AffixDefinition definition, string tierId, IDiceRoller dice,
        decimal luck = 0, DiceRoll? tierRoll = null, int tierProgressionBonus = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(tierId);
        ArgumentNullException.ThrowIfNull(dice);

        ArgumentOutOfRangeException.ThrowIfLessThan(tierProgressionBonus, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(tierProgressionBonus, 3);

        var tier = definition.Tiers.FirstOrDefault(candidate => candidate.Id == tierId)
            ?? throw new KeyNotFoundException("The affix has no matching tier.");

        if (tierRoll is not null && (tierRoll.Formula.Count != 1 || tierRoll.Formula.Sides != 20))
        {
            throw new ArgumentException("Tier provenance must be a natural d20.", nameof(tierRoll));
        }

        var quality = dice.Roll(new DiceFormula(1, 100));
        var context = new AffixRollContext(new GenerationLuck(luck), tierRoll, quality, tierProgressionBonus);
        var modifiers = tier.Modifiers
            .Select(range => RollValue(range, context.EffectiveQualityRoll));

        return new AffixInstance(instanceId, definition, tier.Id, quality.Total, modifiers, context);
    }

    private static ItemModifier RollValue(AffixModifierRange range, decimal quality)
    {
        var position = (quality - 1) / 99m;
        var value = range.Minimum + (range.Maximum - range.Minimum) * position;
        var rounded = decimal.Round(value, range.DecimalPlaces, MidpointRounding.AwayFromZero);

        return new ItemModifier(range.Key, range.Operation, rounded);
    }
}
