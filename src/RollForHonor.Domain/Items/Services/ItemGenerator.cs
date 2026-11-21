using System.Globalization;
using RollForHonor.Domain.Dice.Models;
using RollForHonor.Domain.Dice.Services;
using RollForHonor.Domain.Items.Affixes.Models;
using RollForHonor.Domain.Items.Affixes.Services;
using RollForHonor.Domain.Items.Configuration;
using RollForHonor.Domain.Items.Models;

namespace RollForHonor.Domain.Items.Services;

/// <summary>Generates items from an injected content pool with bounded weighted selection and caller-owned randomness.</summary>
public sealed class ItemGenerator
{
    private readonly ItemCatalog _catalog;
    private readonly ItemFactory _factory;
    private readonly AffixRoller _affixRoller;
    private readonly AffixTierSelection _tierSelection;

    /// <summary>Captures generation dependencies and requires an explicit tier policy.</summary>
    /// <param name="catalog">The validated immutable content pool.</param>
    /// <param name="factory">The capacity and stat-validation boundary.</param>
    /// <param name="affixRoller">The d100 value generator.</param>
    /// <param name="tierSelection">The explicit policy for choosing among eligible tiers.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The tier policy is undefined.</exception>
    public ItemGenerator(ItemCatalog catalog, ItemFactory factory, AffixRoller affixRoller, AffixTierSelection tierSelection)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(affixRoller);

        if (!Enum.IsDefined(tierSelection))
        {
            throw new ArgumentOutOfRangeException(nameof(tierSelection));
        }

        _catalog = catalog;
        _factory = factory;
        _affixRoller = affixRoller;
        _tierSelection = tierSelection;
    }

    /// <summary>Selects compatible families, eligible tiers and concrete values without inferring rarity from count.</summary>
    /// <param name="request">The base, rarity, occupied slots, identity and externally supplied power result.</param>
    /// <param name="dice">The caller-owned random source, reusable with a fixed seed.</param>
    /// <returns>A fully validated item retaining all rolled values.</returns>
    /// <exception cref="ArgumentNullException">An input is null.</exception>
    /// <exception cref="ArgumentException">The count exceeds capacity or content violates the factory's stat rules.</exception>
    /// <exception cref="KeyNotFoundException">The base ID is absent from the catalog.</exception>
    /// <exception cref="InvalidOperationException">Too few compatible families have an eligible tier; no dice are consumed for this failure.</exception>
    /// <exception cref="OverflowException">Selection weights or numeric interpolation exceed their representable range.</exception>
    public ItemInstance Generate(ItemGenerationRequest request, IDiceRoller dice)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(dice);

        var definition = _catalog.GetItem(request.BaseId);
        var emptyItem = _factory.Create(request.InstanceId, definition, request.Rarity);
        ValidateCapacity(request);

        if (request.AffixCount == 0)
        {
            return emptyItem;
        }

        var candidates = GetCandidates(definition, request.AvailablePower);
        ValidatePool(candidates, request.AffixCount);
        var affixes = GenerateAffixes(request, candidates, dice);

        return _factory.Create(request.InstanceId, definition, request.Rarity, affixes);
    }

    private void ValidateCapacity(ItemGenerationRequest request)
    {
        if (request.AffixCount > _factory.RarityRules.GetCapacity(request.Rarity))
        {
            throw new ArgumentException("Requested affixes exceed the rarity's capacity.", nameof(request));
        }
    }

    private AffixDefinition[] GetCandidates(ItemDefinition definition, int power) => _catalog.Affixes
        .Where(affix => affix.RequiredItemTags.IsSubsetOf(definition.Tags))
        .Where(affix => affix.Tiers.Any(tier => tier.RequiredPower <= power))
        .ToArray();

    private static void ValidatePool(AffixDefinition[] candidates, int count)
    {
        var familyCount = candidates
            .DistinctBy(affix => affix.FamilyId)
            .Count();

        if (familyCount < count)
        {
            throw new InvalidOperationException("Too few compatible affix families have an eligible tier.");
        }

        _ = candidates.Sum(affix => affix.SelectionWeight);
    }

    private AffixInstance[] GenerateAffixes(ItemGenerationRequest request, AffixDefinition[] candidates, IDiceRoller dice)
    {
        var result = new List<AffixInstance>(request.AffixCount);

        for (var index = 0; index < request.AffixCount; index++)
        {
            var definition = SelectAffix(candidates, dice);
            var tier = SelectTier(definition, request.AvailablePower, dice);
            var id = $"{request.InstanceId}:{index.ToString(CultureInfo.InvariantCulture)}";
            result.Add(_affixRoller.Roll(id, definition, tier.Id, dice));

            candidates = candidates
                .Where(candidate => candidate.FamilyId != definition.FamilyId)
                .ToArray();
        }

        return result.ToArray();
    }

    private static AffixDefinition SelectAffix(AffixDefinition[] candidates, IDiceRoller dice)
    {
        var totalWeight = candidates.Sum(affix => affix.SelectionWeight);
        var roll = RollIndex(totalWeight, dice);

        foreach (var candidate in candidates)
        {
            roll -= candidate.SelectionWeight;

            if (roll < 0)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Weighted selection requires a nonempty valid pool.");
    }

    private AffixTier SelectTier(AffixDefinition definition, int power, IDiceRoller dice)
    {
        var tiers = definition.Tiers
            .Where(tier => tier.RequiredPower <= power)
            .OrderByDescending(tier => tier.RequiredPower)
            .ThenBy(tier => tier.Id, StringComparer.Ordinal)
            .ToArray();
        var index = _tierSelection == AffixTierSelection.HighestEligible ? 0 : RollIndex(tiers.Length, dice);

        return tiers[index];
    }

    private static int RollIndex(int count, IDiceRoller dice) => count == 1 ? 0 : dice.Roll(new DiceFormula(1, count)).Total - 1;
}
