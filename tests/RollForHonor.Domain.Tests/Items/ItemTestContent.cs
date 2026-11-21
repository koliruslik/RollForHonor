using RollForHonor.Domain.Dice.Models;
using RollForHonor.Domain.Dice.Services;
using RollForHonor.Domain.Items.Affixes.Models;
using RollForHonor.Domain.Items.Affixes.Services;
using RollForHonor.Domain.Items.Configuration;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Domain.Items.Services;
using RollForHonor.Domain.Stats;

namespace RollForHonor.Domain.Tests.Items;

internal static class ItemTestContent
{
    internal static ItemFactory CreateFactory(ItemRarityRules? rarityRules = null) =>
        new(rarityRules ?? ItemRarityRules.Default, ItemStatRules.Default);

    internal static AffixDefinition CreateAffix(string id, int power = 0, int weight = 1,
        string? family = null, IEnumerable<string>? tags = null) => new(id, family ?? id, weight,
    [
        new AffixTier("first", power,
        [
            new AffixModifierRange(new StatKey(StatType.MaxHealth), StatOperation.Flat, 10, 20)
        ])
    ], tags);

    internal static AffixInstance Roll(AffixDefinition definition, string id, int quality = 100) =>
        new AffixRoller().Roll(id, definition, "first", new SequenceDiceRoller(quality));

    internal static ItemGenerator CreateGenerator(IEnumerable<AffixDefinition> affixes,
        AffixTierSelection policy = AffixTierSelection.HighestEligible, ItemDefinition? item = null)
    {
        var catalog = new ItemCatalog([item ?? new ItemDefinition("base", "Base", ["armor"])], affixes, ItemStatRules.Default);

        return new ItemGenerator(catalog, CreateFactory(), new AffixRoller(), policy);
    }
}

internal sealed class SequenceDiceRoller(params int[] values) : IDiceRoller
{
    /// <inheritdoc />
    public DiceTrace Trace => new(_rolls);

    private readonly Queue<int> _values = new(values);
    private readonly List<DiceRoll> _rolls = [];

    /// <inheritdoc />
    public DiceRoll Roll(DiceFormula formula)
    {
        var value = _values.Dequeue();
        var roll = new DiceRoll(formula, [value], value);
        _rolls.Add(roll);

        return roll;
    }
}

internal sealed class SeededItemDiceRoller(int seed) : IDiceRoller
{
    /// <inheritdoc />
    public DiceTrace Trace => new(_rolls);

    private readonly Random _random = new(seed);
    private readonly List<DiceRoll> _rolls = [];

    /// <inheritdoc />
    public DiceRoll Roll(DiceFormula formula)
    {
        var value = checked((int)_random.NextInt64(1, (long)formula.Sides + 1));
        var roll = new DiceRoll(formula, [value], value);
        _rolls.Add(roll);

        return roll;
    }
}
