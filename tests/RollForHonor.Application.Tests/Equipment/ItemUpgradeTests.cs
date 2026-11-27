using RollForHonor.Application.Items.Upgrades;
using RollForHonor.Application.Rewards;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.Damage.Services;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Items.Affixes.Models;
using RollForHonor.Domain.Items.Affixes.Services;
using RollForHonor.Domain.Items.Configuration;
using RollForHonor.Domain.Items.Equipment.Models;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Domain.Items.Services;
using RollForHonor.Domain.Stats;
using RollForHonor.Infrastructure.Dice;

namespace RollForHonor.Application.Tests.Equipment;

/// <summary>Verifies paid crafting admission, captured randomness and atomic ownership/stat/resource publication.</summary>
public sealed class ItemUpgradeTests
{
    private static ItemFactory Factory => new(ItemRarityRules.Default, ItemStatRules.Default);

    /// <summary>The first paid journey consumes exactly 96 gold and preserves item identity/base.</summary>
    [Fact]
    public void FirstEvolutionCosts96AndTransfersBaseStrength()
    {
        var session = Funded(96);
        var item = EquipmentTestSession.Item("armor", "body-armor", new ItemModifier(new(StatType.Armor), StatOperation.Flat, 100));
        session.State.RegisterOwnedItem(session.Owner, item);
        var service = Service(session);
        for (var index = 0; index < 3; index++)
        {
            var quote = service.Quote(session.Owner, item, ItemUpgradeOperation.Enhance);
            Assert.Equal(ItemUpgradeStatus.Applied, service.Execute(quote));
            item = quote.Result!;
        }

        var before = ItemBaseScaling.Modifiers(item).Single().Amount;
        var evolve = service.Quote(session.Owner, item, ItemUpgradeOperation.Evolve);
        Assert.Equal(ItemUpgradeStatus.Applied, service.Execute(evolve));
        Assert.Equal(ItemRarity.Uncommon, evolve.Result!.Rarity);
        Assert.Equal(0, evolve.Result.Progression.Enhancement);
        Assert.Equal(before, ItemBaseScaling.Modifiers(evolve.Result).Single().Amount);
        Assert.Equal(115, before);
        Assert.Same(item.Base, evolve.Result.Base);
        Assert.Equal(item.Id, evolve.Result.Id);
        Assert.Equal(0, session.State.ReadGoldWallet(session.Owner).Balance);
        Assert.Equal(ItemUpgradeStatus.AlreadyApplied, service.Execute(evolve));
    }

    /// <summary>Every evolution carries excess levels without discarding affix snapshots or weakening the base.</summary>
    /// <param name="rarity">The outgoing rarity index.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void EvolutionCarriesExcessAndPreservesAffixes(int rarity)
    {
        var session = Funded(1000);
        var kind = (ItemRarity)rarity;
        var item = Factory.Create("item", new("base", "Base", ["body-armor"]), kind,
            progression: new(10, ItemUpgradeRules.Default.RarityMultiplier(kind)));
        session.State.RegisterOwnedItem(session.Owner, item);
        var service = Service(session);
        var quote = service.Quote(session.Owner, item, ItemUpgradeOperation.Evolve);
        Assert.Equal(ItemUpgradeStatus.Applied, service.Execute(quote));
        Assert.Equal(10 - ItemUpgradeRules.Default.EvolutionGate(kind), quote.Result!.Progression.Enhancement);
        Assert.True(quote.Result.Progression.Multiplier >= item.Progression.Multiplier);
        Assert.Equal(item.Affixes, quote.Result.Affixes);
        Assert.Equal(item.GenerationLevel, quote.Result.GenerationLevel);
    }

    /// <summary>A rejected payment neither generates randomness nor changes ownership/version.</summary>
    [Fact]
    public void InsufficientFundsDoNotRollOrMutate()
    {
        var session = Funded(10);
        var item = Factory.Create("item", new("base", "Base", ["body-armor"]), ItemRarity.Rare);
        session.State.RegisterOwnedItem(session.Owner, item);
        var rolls = 0;
        var service = Service(session, () => { rolls++; return new RandomDiceRoller(); });
        var quote = service.Quote(session.Owner, item, ItemUpgradeOperation.AddAffix);
        var version = session.State.ReadGoldWallet(session.Owner).Version;
        Assert.Equal(ItemUpgradeStatus.InsufficientFunds, service.Execute(quote));
        Assert.Equal(0, rolls);
        Assert.Equal(version, session.State.ReadGoldWallet(session.Owner).Version);
        Assert.Same(item, session.State.ReadEquipment(session.Owner).OwnedItems[item.Id]);
    }

    /// <summary>Equipped enhancement recomposes stats and maxima without healing or spending mana.</summary>
    [Fact]
    public void EquippedEnhancementPublishesStatsWithoutRefill()
    {
        var session = Funded(100);
        var item = EquipmentTestSession.Item("armor", "body-armor", new ItemModifier(new(StatType.MaxHealth), StatOperation.Flat, 100));
        session.State.RegisterOwnedItem(session.Owner, item);
        session.Service.EquipItem(session.Owner, item.Id, EquipmentSlot.BodyArmor);
        var health = session.State.Read(session.Owner, session.Owner).Source.Health;
        var service = Service(session);
        var quote = service.Quote(session.Owner, item, ItemUpgradeOperation.Enhance);
        Assert.Equal(ItemUpgradeStatus.Applied, service.Execute(quote));
        Assert.Equal(205, session.State.GetStats(session.Owner).Get(new(StatType.MaxHealth)));
        Assert.Equal(health, session.State.Read(session.Owner, session.Owner).Source.Health);
        Assert.Same(quote.Result, session.State.ReadEquipment(session.Owner).Loadout.GetItem(EquipmentSlot.BodyArmor));
        Assert.Equal(86, session.State.ReadGoldWallet(session.Owner).Balance);
    }

    /// <summary>Changed item and Luck quotes reject payment; a defeated actor cannot craft.</summary>
    [Fact]
    public void StaleAndDefeatedRequestsCannotSpend()
    {
        var session = Funded(1000);
        var item = EquipmentTestSession.Item("item", "body-armor");
        session.State.RegisterOwnedItem(session.Owner, item);
        var service = Service(session);
        var quote = service.Quote(session.Owner, item, ItemUpgradeOperation.Enhance);
        session.State.UpdateNonEquipmentStats(session.Owner, EquipmentTestSession.Profile([new(new(StatType.Luck), StatOperation.Flat, 1, "luck")]));
        Assert.Equal(ItemUpgradeStatus.Stale, service.Execute(quote));
        quote = service.Quote(session.Owner, item, ItemUpgradeOperation.Enhance);
        session.State.TryEditOwnedItem(session.Owner, item, Factory.Create(item.Id, item.Base, item.Rarity));
        Assert.Equal(ItemUpgradeStatus.Stale, service.Execute(quote));
        item = session.State.ReadEquipment(session.Owner).OwnedItems[item.Id];
        quote = service.Quote(session.Owner, item, ItemUpgradeOperation.Enhance);
        var actor = session.State.Read(session.Owner, session.Owner).Source;
        session.State.UpdateCombatant(new(session.Owner, 0, actor.MaxHealth, actor.Armor, actor.Evasion, actor.Resistances, actor.Effects, actor.Offense));
        Assert.Equal(ItemUpgradeStatus.Defeated, service.Execute(quote));
        Assert.Equal(1000, session.State.ReadGoldWallet(session.Owner).Balance);
    }

    /// <summary>A version conflict retains the first random result and a retry pays exactly once.</summary>
    [Fact]
    public void ConflictRetryReusesRandomResultAndReceipt()
    {
        var session = Funded(1000);
        var item = Factory.Create("item", new("base", "Base", ["body-armor"]), ItemRarity.Rare);
        session.State.RegisterOwnedItem(session.Owner, item);
        var rolls = 0;
        var service = Service(session, () =>
        {
            rolls++;
            session.State.UpdateNonEquipmentStats(session.Owner, EquipmentTestSession.Profile());
            return new RandomDiceRoller(new Random(12));
        });
        var quote = service.Quote(session.Owner, item, ItemUpgradeOperation.AddAffix);
        Assert.Equal(ItemUpgradeStatus.Stale, service.Execute(quote));
        var captured = quote.Result;
        Assert.NotNull(captured);
        Assert.Equal(1000, session.State.ReadGoldWallet(session.Owner).Balance);
        Assert.Equal(ItemUpgradeStatus.Applied, service.Execute(quote));
        Assert.Equal(ItemUpgradeStatus.AlreadyApplied, service.Execute(quote));
        Assert.Same(captured, quote.Result);
        Assert.Equal(1, rolls);
        Assert.Equal(616, session.State.ReadGoldWallet(session.Owner).Balance);
    }

    /// <summary>Full reroll preserves locked snapshots and excludes their families; addition retains existing affixes.</summary>
    [Fact]
    public void LocksAndAdditionPreserveCapturedValues()
    {
        var session = Funded(5000);
        var source = new TestAffixes();
        var locked = new AffixRoller().Roll("locked", source.GetAffixes(null!)[0], "T7", new RandomDiceRoller());
        var other = new AffixRoller().Roll("other", source.GetAffixes(null!)[1], "T7", new RandomDiceRoller());
        var item = Factory.Create("item", new("base", "Base", ["body-armor"]), ItemRarity.Rare, [locked, other], 1, new(4));
        session.State.RegisterOwnedItem(session.Owner, item);
        var service = Service(session);
        var quote = service.Quote(session.Owner, item, ItemUpgradeOperation.Reroll, [locked.Id]);
        Assert.Equal(90, quote.Cost.Amount);
        Assert.Equal(ItemUpgradeStatus.Applied, service.Execute(quote));
        var rerolled = quote.Result!;
        Assert.Same(locked, rerolled.Affixes[0]);
        Assert.NotSame(other, rerolled.Affixes[1]);
        Assert.Equal(2, rerolled.Affixes.Select(affix => affix.FamilyId).Distinct().Count());
        Assert.Equal(4, rerolled.Progression.Enhancement);
        var add = service.Quote(session.Owner, rerolled, ItemUpgradeOperation.AddAffix);
        Assert.Equal(480, add.Cost.Amount);
        Assert.Equal(ItemUpgradeStatus.Applied, service.Execute(add));
        Assert.Equal(3, add.Result!.Affixes.Count);
        Assert.Same(rerolled.Affixes[0], add.Result.Affixes[0]);
        Assert.Same(rerolled.Affixes[1], add.Result.Affixes[1]);
        Assert.Equal(3, add.Result.Affixes.Select(affix => affix.FamilyId).Distinct().Count());
    }

    /// <summary>Unavailable operations and invalid lock requests fail before payment.</summary>
    [Fact]
    public void CapsGatesAndInvalidLocksReject()
    {
        var session = Funded(1000);
        var item = Factory.Create("item", new("base", "Base", ["body-armor"]), ItemRarity.Common);
        session.State.RegisterOwnedItem(session.Owner, item);
        var service = Service(session);
        foreach (var operation in new[] { ItemUpgradeOperation.Evolve, ItemUpgradeOperation.AddAffix, ItemUpgradeOperation.Reroll })
        {
            Assert.Equal(ItemUpgradeStatus.Unavailable, service.Quote(session.Owner, item, operation).Status);
        }
        Assert.Throws<ArgumentException>(() => service.Quote(session.Owner, item, ItemUpgradeOperation.Reroll, ["unknown"]));
        var max = Factory.Create(item.Id, item.Base, ItemRarity.Legendary, progression: new(10));
        session.State.TryEditOwnedItem(session.Owner, item, max);
        Assert.Equal(ItemUpgradeStatus.Unavailable, service.Quote(session.Owner, max, ItemUpgradeOperation.Enhance).Status);
        Assert.Equal(ItemUpgradeStatus.Unavailable, service.Quote(session.Owner, max, ItemUpgradeOperation.Evolve).Status);
        Assert.Equal(1000, session.State.ReadGoldWallet(session.Owner).Balance);
    }

    /// <summary>Scaled weapon rolls leave separately added dice untouched and preserve captured formulas.</summary>
    [Fact]
    public void WeaponScalingAppliesOnlyToItsBaseRoll()
    {
        var dice = new RandomDiceRoller(new MaximumRandom());
        var result = new RawDamageResolver().Resolve([new(new(1, 6), 2, DamageType.Fire, 1.15m), new(new(1, 4), 0, DamageType.Fire)], dice);
        Assert.Equal(14, result.Damage.Components.Sum(component => component.Amount));
        Assert.Equal(6, dice.Trace.Rolls[0].Results.Single());
        Assert.Single(result.Breakdown.Steps);
    }

    /// <summary>A failed equipped-stat projection leaves the item, wallet and actor unchanged.</summary>
    [Fact]
    public void ProjectionOverflowDoesNotChargeOrReplace()
    {
        var session = Funded(100);
        var item = EquipmentTestSession.Item("armor", "body-armor", new ItemModifier(new(StatType.MaxHealth), StatOperation.Flat, 2_100_000_000));
        session.State.RegisterOwnedItem(session.Owner, item);
        session.Service.EquipItem(session.Owner, item.Id, EquipmentSlot.BodyArmor);
        var service = Service(session);
        var quote = service.Quote(session.Owner, item, ItemUpgradeOperation.Enhance);
        var wallet = session.State.ReadGoldWallet(session.Owner);
        var actor = session.State.Read(session.Owner, session.Owner).Source;
        Assert.Throws<OverflowException>(() => service.Execute(quote));
        Assert.Equal(wallet, session.State.ReadGoldWallet(session.Owner));
        Assert.Same(actor, session.State.Read(session.Owner, session.Owner).Source);
        Assert.Same(item, session.State.ReadEquipment(session.Owner).Loadout.GetItem(EquipmentSlot.BodyArmor));
    }

    /// <summary>An empty eligible pool and a fully locked item cannot consume payment or randomness.</summary>
    [Fact]
    public void MissingPoolAndAllLockedRejectBeforeGeneration()
    {
        var session = Funded(1000);
        var affix = new AffixRoller().Roll("kept", new TestAffixes().GetAffixes(null!)[0], "T7", new RandomDiceRoller());
        var item = Factory.Create("item", new("base", "Base", ["body-armor"]), ItemRarity.Rare, [affix]);
        session.State.RegisterOwnedItem(session.Owner, item);
        var service = Service(session);
        Assert.Equal(ItemUpgradeStatus.Unavailable, service.Quote(session.Owner, item, ItemUpgradeOperation.Reroll, [affix.Id]).Status);
        var empty = new ItemUpgradeService(session.State, ItemUpgradeRules.Default, Factory, new EmptyAffixes(),
            () => throw new InvalidOperationException("Randomness must not be requested."));
        var quote = empty.Quote(session.Owner, item, ItemUpgradeOperation.AddAffix);
        Assert.Equal(ItemUpgradeStatus.NoEligibleAffixes, quote.Status);
        Assert.Equal(ItemUpgradeStatus.NoEligibleAffixes, empty.Execute(quote));
        Assert.Equal(1000, session.State.ReadGoldWallet(session.Owner).Balance);
    }

    /// <summary>Paid enhancement stops exactly at each rarity's gate, while evolution becomes available.</summary>
    /// <param name="rarity">The outgoing rarity index.</param>
    /// <param name="limit">The last permitted paid enhancement level.</param>
    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 5)]
    [InlineData(2, 7)]
    [InlineData(3, 10)]
    [InlineData(4, 10)]
    public void EnhancementStopsAtRarityLimit(int rarity, int limit)
    {
        var session = Funded(1000);
        var item = Factory.Create("item", new("base", "Base", ["body-armor"]), (ItemRarity)rarity,
            progression: new(limit - 1));
        session.State.RegisterOwnedItem(session.Owner, item);
        var service = Service(session);
        var lastStep = service.Quote(session.Owner, item, ItemUpgradeOperation.Enhance);
        Assert.Equal(ItemUpgradeStatus.Applied, service.Execute(lastStep));
        var wallet = session.State.ReadGoldWallet(session.Owner);
        var blocked = service.Quote(session.Owner, lastStep.Result!, ItemUpgradeOperation.Enhance);
        Assert.Equal(ItemUpgradeStatus.Unavailable, blocked.Status);
        Assert.Equal(ItemUpgradeStatus.Unavailable, service.Execute(blocked));
        Assert.Equal(wallet, session.State.ReadGoldWallet(session.Owner));
        Assert.Equal(rarity == 4 ? ItemUpgradeStatus.Unavailable : ItemUpgradeStatus.Ready,
            service.Quote(session.Owner, lastStep.Result!, ItemUpgradeOperation.Evolve).Status);
    }

    private static EquipmentTestSession Funded(long amount)
    {
        var session = new EquipmentTestSession();
        var source = new CombatantId(Guid.NewGuid());
        session.State.AddCombatant(new(source, 0, 1, 0, 0, [], []));
        session.State.CaptureGoldReward(source, amount);
        new GoldPickupService(session.State).Collect(session.Owner, source);
        return session;
    }

    private static ItemUpgradeService Service(EquipmentTestSession session, Func<RollForHonor.Domain.Dice.Services.IDiceRoller>? dice = null) =>
        new(session.State, ItemUpgradeRules.Default, Factory, new TestAffixes(), dice ?? (() => new RandomDiceRoller(new Random(15))));

    private sealed class TestAffixes : IItemUpgradeAffixSource
    {
        /// <inheritdoc />
        public IReadOnlyList<AffixDefinition> GetAffixes(ItemInstance item) => Enumerable.Range(0, 8)
            .Select(index => new AffixDefinition($"family{index}", $"family{index}", 1,
                [new("T7", 1, [new(new(StatType.MaxHealth), StatOperation.Flat, 1, 5)]),
                    new("T1", 20, [new(new(StatType.MaxHealth), StatOperation.Flat, 10, 20)])]))
            .ToArray();
    }

    private sealed class MaximumRandom : Random
    {
        /// <inheritdoc />
        public override long NextInt64(long minValue, long maxValue) => maxValue - 1;
    }

    private sealed class EmptyAffixes : IItemUpgradeAffixSource
    {
        /// <inheritdoc />
        public IReadOnlyList<AffixDefinition> GetAffixes(ItemInstance item) => [];
    }
}
