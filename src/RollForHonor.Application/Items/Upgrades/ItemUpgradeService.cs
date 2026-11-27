using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Dice.Services;
using RollForHonor.Domain.Items.Affixes.Models;
using RollForHonor.Domain.Items.Affixes.Services;
using RollForHonor.Domain.Items.Configuration;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Domain.Items.Services;

namespace RollForHonor.Application.Items.Upgrades;

/// <summary>Quotes paid item transformations and commits one captured result through an atomic port.</summary>
public sealed class ItemUpgradeService
{
    private readonly IItemUpgradeStateAdapter _state;
    private readonly ItemUpgradeRules _rules;
    private readonly ItemFactory _factory;
    private readonly IItemUpgradeAffixSource _affixes;
    private readonly Func<IDiceRoller> _dice;

    /// <summary>Composes replaceable prices, content, validation and randomness.</summary>
    /// <param name="state">The atomic ownership/resource port.</param>
    /// <param name="rules">Captured operation prices and progression rules.</param>
    /// <param name="factory">The item-validation boundary.</param>
    /// <param name="affixes">Live eligible affix content.</param>
    /// <param name="dice">Creates randomness only when an admitted random operation executes.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    public ItemUpgradeService(IItemUpgradeStateAdapter state, ItemUpgradeRules rules, ItemFactory factory,
        IItemUpgradeAffixSource affixes, Func<IDiceRoller> dice)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _affixes = affixes ?? throw new ArgumentNullException(nameof(affixes));
        _dice = dice ?? throw new ArgumentNullException(nameof(dice));
    }

    /// <summary>Quotes current eligibility and cost without rolling or spending resources.</summary>
    /// <param name="owner">The registered owner.</param>
    /// <param name="item">The exact selected snapshot.</param>
    /// <param name="operation">The requested operation.</param>
    /// <param name="lockedIds">Optional distinct existing affix identities for full reroll.</param>
    /// <returns>A quote with a deterministic preview or an unavailable reason.</returns>
    /// <exception cref="ArgumentException">The operation or lock identities are invalid.</exception>
    /// <exception cref="OverflowException">Price or progression arithmetic overflows.</exception>
    public ItemUpgradeQuote Quote(CombatantId owner, ItemInstance item, ItemUpgradeOperation operation,
        IEnumerable<string>? lockedIds = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(item);
        if (!Enum.IsDefined(operation))
        {
            throw new ArgumentException("Unknown upgrade operation.", nameof(operation));
        }

        var ids = lockedIds?.ToArray() ?? [];
        if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Length || ids.Any(id => !item.Affixes.Any(affix => affix.Id == id))
            || operation != ItemUpgradeOperation.Reroll && ids.Length > 0)
        {
            throw new ArgumentException("Locks must identify distinct existing affixes during reroll.", nameof(lockedIds));
        }

        var read = _state.ReadUpgrade(owner, item.Id, _rules.ResourceId);
        var locked = item.Affixes.Where(affix => ids.Contains(affix.Id, StringComparer.Ordinal)).ToArray();
        var status = !ReferenceEquals(read.Item, item) ? ItemUpgradeStatus.Stale
            : read.Defeated ? ItemUpgradeStatus.Defeated
            : !item.Base.Requirements.Released || item.Base.Requirements.Level > _rules.MaximumBaseLevel
                ? ItemUpgradeStatus.UnsupportedBase : ItemUpgradeStatus.Ready;
        long price = 0;
        ItemInstance? preview = null;
        IReadOnlyList<AffixDefinition> pool = [];

        if (status == ItemUpgradeStatus.Ready)
        {
            switch (operation)
            {
                case ItemUpgradeOperation.Enhance when item.Progression.Enhancement < _rules.EnhancementLimit(item.Rarity):
                    price = _rules.EnhancementPrice(item.Progression.Enhancement + 1);
                    preview = Copy(item, item.Rarity, item.Affixes, new(item.Progression.Enhancement + 1,
                        item.Progression.RarityMultiplier, item.Progression.Step));
                    break;
                case ItemUpgradeOperation.Evolve when item.Rarity != ItemRarity.Legendary
                    && item.Progression.Enhancement >= _rules.EvolutionGate(item.Rarity):
                    var gate = _rules.EvolutionGate(item.Rarity);
                    price = _rules.EvolutionPrice(item.Rarity);
                    preview = Copy(item, item.Rarity + 1, item.Affixes, new(item.Progression.Enhancement - gate,
                        item.Progression.RarityMultiplier * (1 + item.Progression.Step * gate), item.Progression.Step));
                    break;
                case ItemUpgradeOperation.Reroll when item.Affixes.Count > locked.Length:
                case ItemUpgradeOperation.AddAffix when item.Affixes.Count < _factory.RarityRules.GetCapacity(item.Rarity):
                    price = operation == ItemUpgradeOperation.Reroll
                        ? _rules.RerollPrice(item.Affixes.Count, locked.Length) : _rules.AdditionPrice(item.Affixes.Count);
                    var retained = operation == ItemUpgradeOperation.AddAffix ? item.Affixes : locked;
                    pool = _affixes.GetAffixes(item)
                        .Where(definition => definition.RequiredItemTags.IsSubsetOf(item.Base.Tags))
                        .Where(definition => !retained.Any(affix => affix.FamilyId == definition.FamilyId))
                        .Where(definition => definition.Tiers.Any(tier => tier.RequiredItemLevel <= item.GenerationLevel))
                        .ToArray();
                    var needed = operation == ItemUpgradeOperation.AddAffix ? 1 : item.Affixes.Count - locked.Length;
                    if (pool.Select(definition => definition.FamilyId).Distinct().Count() < needed
                        || pool.Any(definition => !definition.Tiers.Any(tier => tier.RequiredPower <= 1
                            && tier.RequiredItemLevel <= item.GenerationLevel)))
                    {
                        status = ItemUpgradeStatus.NoEligibleAffixes;
                    }
                    break;
                default:
                    status = ItemUpgradeStatus.Unavailable;
                    break;
            }
        }

        return new(this, owner, item, operation, new(_rules.ResourceId, price), read.Luck, locked, status, pool, preview);
    }

    /// <summary>Executes this service's quote, capturing a random result once for safe retries.</summary>
    /// <param name="quote">The unchanged quote issued by this service.</param>
    /// <returns>The paid commit result, or a rejection without partial changes.</returns>
    /// <exception cref="ArgumentException">The quote belongs to a different service.</exception>
    /// <exception cref="OverflowException">Projection or numeric arithmetic overflows without publishing payment.</exception>
    public ItemUpgradeStatus Execute(ItemUpgradeQuote quote)
    {
        ArgumentNullException.ThrowIfNull(quote);
        if (!ReferenceEquals(quote.Issuer, this))
        {
            throw new ArgumentException("A quote must be executed by its issuing service.", nameof(quote));
        }

        lock (quote.Gate)
        {
            if (quote.Status != ItemUpgradeStatus.Ready)
            {
                return quote.Status;
            }

            var read = _state.ReadUpgrade(quote.Owner, quote.Item.Id, quote.Cost.ResourceId);
            if (!ReferenceEquals(read.Item, quote.Item))
            {
                return quote.Result is not null
                    ? _state.TryCommitUpgrade(quote, quote.Result, read.Version) : ItemUpgradeStatus.Stale;
            }

            if (read.Defeated)
            {
                return ItemUpgradeStatus.Defeated;
            }

            if (read.Luck != quote.Luck)
            {
                return ItemUpgradeStatus.Stale;
            }

            if (read.Balance < quote.Cost.Amount)
            {
                return ItemUpgradeStatus.InsufficientFunds;
            }

            quote.Result ??= Generate(quote);
            return _state.TryCommitUpgrade(quote, quote.Result, read.Version);
        }
    }

    private ItemInstance Generate(ItemUpgradeQuote quote)
    {
        var item = quote.Item;
        var add = quote.Operation == ItemUpgradeOperation.AddAffix;
        var retained = add ? item.Affixes : quote.Locked;
        var count = add ? 1 : item.Affixes.Count - retained.Count;
        var catalog = new ItemCatalog([item.Base], quote.Pool, ItemStatRules.Default);
        var generated = new ItemGenerator(catalog, _factory, new AffixRoller(), AffixTierSelection.HighestEligible)
            .GenerateWithLuck(new(quote.Id.ToString("N"), item.Base.Id, item.Rarity, count, int.MaxValue,
                item.GenerationLevel), quote.Luck, _dice());
        if (add)
        {
            return Copy(item, item.Rarity, item.Affixes.Concat(generated.Affixes), item.Progression);
        }

        var next = new Queue<AffixInstance>(generated.Affixes);
        return Copy(item, item.Rarity, item.Affixes
            .Select(affix => retained.Contains(affix) ? affix : next.Dequeue()), item.Progression);
    }

    private ItemInstance Copy(ItemInstance item, ItemRarity rarity, IEnumerable<AffixInstance> affixes, ItemProgression progression) =>
        _factory.Create(item.Id, item.Base, rarity, affixes, item.GenerationLevel, progression);
}
