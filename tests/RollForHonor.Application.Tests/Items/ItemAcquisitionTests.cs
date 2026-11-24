using RollForHonor.Application.Items;
using RollForHonor.Application.Equipment.Models;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Items.Configuration;
using RollForHonor.Domain.Items.Equipment.Models;
using RollForHonor.Domain.Items.Inventory.Models;
using RollForHonor.Domain.Items.Inventory.Services;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Domain.Items.Services;
using RollForHonor.Domain.Stats;
using RollForHonor.Infrastructure.Combat;

namespace RollForHonor.Application.Tests.Items;

/// <summary>Verifies spatial capacity, ownership uniqueness and atomic rejection of pickup requests.</summary>
public sealed class ItemAcquisitionTests
{
    /// <summary>A successful pickup owns the exact captured instance; a duplicate does not advance the version.</summary>
    [Fact]
    public void AcquisitionRetainsTheInstanceAndRejectsDuplicates()
    {
        var state = new InMemoryCombatState();
        var owner = new CombatantId(Guid.NewGuid());
        state.AddStatCombatant(owner, new StatProfile(new StatBlock()));
        var item = Item("loot", 2, 3);
        var service = new ItemAcquisitionService(state);

        Assert.Equal(ItemAcquisitionStatus.Acquired, service.Acquire(owner, item, new(12, 5)));
        var read = state.ReadEquipment(owner);
        Assert.Same(item, read.OwnedItems[item.Id]);
        Assert.Equal(ItemAcquisitionStatus.AlreadyOwned, service.Acquire(owner, item, new(12, 5)));
        Assert.Equal(read.Version, state.ReadEquipment(owner).Version);
    }

    /// <summary>Free cell area is insufficient when it contains no rectangle of the requested shape.</summary>
    [Fact]
    public void FragmentedBagRejectsPickupWithoutChangingOwnership()
    {
        var state = new InMemoryCombatState();
        var owner = new CombatantId(Guid.NewGuid());
        state.AddStatCombatant(owner, new StatProfile(new StatBlock()));
        var layout = new InventoryGridLayout(12, 5);
        var grid = new InventoryGridService();

        for (var column = 0; column < 12; column += 2)
        {
            var blocker = Item($"block-{column}", 1, 5);
            state.RegisterOwnedItem(owner, blocker);
            Assert.True(grid.TryPlace(layout, blocker.Id, blocker.Base.Footprint, new(column, 0), out layout));
        }

        var before = state.ReadEquipment(owner);
        var loot = Item("wide", 2, 1);
        Assert.Equal(ItemAcquisitionStatus.InventoryFull, new ItemAcquisitionService(state).Acquire(owner, loot, layout));
        Assert.Equal(before.Version, state.ReadEquipment(owner).Version);
        Assert.False(state.ReadEquipment(owner).OwnedItems.ContainsKey(loot.Id));
    }

    /// <summary>Omitting existing items or changing the grid size cannot bypass capacity checks.</summary>
    [Fact]
    public void StaleOrInventedLayoutsCannotHideOwnedItems()
    {
        var state = new InMemoryCombatState();
        var owner = new CombatantId(Guid.NewGuid());
        state.AddStatCombatant(owner, new StatProfile(new StatBlock()));
        state.RegisterOwnedItem(owner, Item("existing", 1, 1));
        var service = new ItemAcquisitionService(state);
        var loot = Item("loot", 1, 1);

        Assert.Throws<InvalidOperationException>(() => service.Acquire(owner, loot, new(12, 5)));
        Assert.Throws<InvalidOperationException>(() => service.Acquire(owner, loot, new(13, 5)));
        Assert.Single(state.ReadEquipment(owner).OwnedItems);
    }

    /// <summary>A concurrent session mutation rejects the grant without an implicit retry or a second instance.</summary>
    [Fact]
    public void VersionConflictDoesNotGrantLoot()
    {
        var state = new InMemoryCombatState();
        var owner = new CombatantId(Guid.NewGuid());
        var other = new CombatantId(Guid.NewGuid());
        state.AddStatCombatant(owner, new StatProfile(new StatBlock()));
        state.AddStatCombatant(other, new StatProfile(new StatBlock()));
        var service = new ItemAcquisitionService(new ConflictingPort(state, other));

        Assert.Equal(ItemAcquisitionStatus.VersionConflict, service.Acquire(owner, Item("loot", 1, 1), new(12, 5)));
        Assert.Empty(state.ReadEquipment(owner).OwnedItems);
    }

    /// <summary>Defeated actors cannot gain ownership, and a foreign owner cannot acquire an existing instance.</summary>
    [Fact]
    public void DefeatAndForeignOwnershipRejectWithoutPublication()
    {
        var state = new InMemoryCombatState();
        var owner = new CombatantId(Guid.NewGuid());
        var other = new CombatantId(Guid.NewGuid());
        state.AddStatCombatant(owner, new StatProfile(new StatBlock()));
        state.AddStatCombatant(other, new StatProfile(new StatBlock()));
        var item = Item("owned", 1, 1);
        state.RegisterOwnedItem(other, item);
        var service = new ItemAcquisitionService(state);

        Assert.Equal(ItemAcquisitionStatus.AlreadyOwned, service.Acquire(owner, item, new(12, 5)));
        var current = state.Read(owner, owner).Source;
        state.UpdateCombatant(new(owner, 0, current.MaxHealth, current.Armor, current.Evasion, current.Resistances, current.Effects, current.Offense));
        var version = state.ReadEquipment(owner).Version;
        Assert.Equal(ItemAcquisitionStatus.OwnerDefeated, service.Acquire(owner, Item("fresh", 1, 1), new(12, 5)));
        Assert.Equal(version, state.ReadEquipment(owner).Version);
    }

    /// <summary>An occupied requested rectangle rejects even when automatic placement elsewhere would succeed.</summary>
    [Fact]
    public void ExactPickupChecksTheChosenRectangle()
    {
        var state = new InMemoryCombatState();
        var owner = new CombatantId(Guid.NewGuid());
        state.AddStatCombatant(owner, new StatProfile(new StatBlock()));
        var blocker = Item("blocker", 2, 2);
        state.RegisterOwnedItem(owner, blocker);
        var grid = new InventoryGridService();
        grid.TryPlace(new(12, 5), blocker.Id, blocker.Base.Footprint, new(0, 0), out var layout);
        var service = new ItemAcquisitionService(state);
        var loot = Item("exact", 2, 3);
        var version = state.ReadEquipment(owner).Version;

        Assert.Equal(ItemAcquisitionStatus.InventoryFull, service.Acquire(owner, loot, layout, new(1, 1)));
        Assert.Equal(ItemAcquisitionStatus.InventoryFull, service.Acquire(owner, loot, layout, new(11, 4)));
        Assert.Equal(version, state.ReadEquipment(owner).Version);
        Assert.Equal(ItemAcquisitionStatus.Acquired, service.Acquire(owner, loot, layout, new(10, 2)));
        Assert.Same(loot, state.ReadEquipment(owner).OwnedItems[loot.Id]);
    }

    /// <summary>Release and reacquisition preserve the original immutable item without duplication.</summary>
    [Fact]
    public void ReleaseRetainsCapturedValuesAndRejectsStaleOrDuplicateRequests()
    {
        var state = new InMemoryCombatState();
        var owner = new CombatantId(Guid.NewGuid());
        state.AddStatCombatant(owner, new StatProfile(new StatBlock()));
        var item = Item("release", 1, 1);
        state.RegisterOwnedItem(owner, item);
        var version = state.ReadEquipment(owner).Version;
        Assert.Null(state.TryReleaseItem(owner, item.Id, version - 1));
        Assert.Same(item, new ItemReleaseService(state).Release(owner, item.Id));
        var after = state.ReadEquipment(owner).Version;
        Assert.Null(new ItemReleaseService(state).Release(owner, item.Id));
        Assert.Equal(after, state.ReadEquipment(owner).Version);
        Assert.Equal(ItemAcquisitionStatus.Acquired, new ItemAcquisitionService(state).Acquire(owner, item, new(12, 5)));
        Assert.Same(item, state.ReadEquipment(owner).OwnedItems[item.Id]);
    }

    /// <summary>Foreign and defeated-owner releases preserve canonical ownership and the session version.</summary>
    [Fact]
    public void ReleaseRejectsForeignAndDefeatedOwners()
    {
        var state = new InMemoryCombatState();
        var owner = new CombatantId(Guid.NewGuid());
        state.AddStatCombatant(owner, new StatProfile(new StatBlock()));
        var item = Item("retained", 1, 1);
        state.RegisterOwnedItem(owner, item);
        var version = state.ReadEquipment(owner).Version;
        Assert.Null(new ItemReleaseService(state).Release(owner, "foreign"));
        Assert.Equal(version, state.ReadEquipment(owner).Version);
        var actor = state.Read(owner, owner).Source;
        state.UpdateCombatant(new(owner, 0, actor.MaxHealth, actor.Armor, actor.Evasion, actor.Resistances, actor.Effects, actor.Offense));
        version = state.ReadEquipment(owner).Version;
        Assert.Null(new ItemReleaseService(state).Release(owner, item.Id));
        Assert.Equal(version, state.ReadEquipment(owner).Version);
        Assert.Same(item, state.ReadEquipment(owner).OwnedItems[item.Id]);
    }

    /// <summary>Releasing equipped items removes their bonuses and ownership together, clamping current HP once.</summary>
    [Fact]
    public void EquippedReleaseRecomposesStatsWithOnePublication()
    {
        var state = new InMemoryCombatState();
        var owner = new CombatantId(Guid.NewGuid());
        state.AddStatCombatant(owner, new(new StatBlock([new(new(StatType.MaxHealth), 100)])));
        var ring = new ItemFactory(ItemRarityRules.Default, ItemStatRules.Default)
            .Create("equipped", new ItemDefinition("ring", "Ring", ["ring"],
                [new(new(StatType.MaxHealth), StatOperation.Flat, 50), new(new(StatType.Armor), StatOperation.Flat, 3)]), ItemRarity.Common);
        state.RegisterOwnedItem(owner, ring);
        new RollForHonor.Application.Equipment.Services.EquipmentService(state).EquipItem(owner, ring.Id, EquipmentSlot.RingLeft);
        var actor = state.Read(owner, owner).Source;
        state.UpdateCombatant(new(owner, 140, actor.MaxHealth, actor.Armor, actor.Evasion, actor.Resistances, actor.Effects, actor.Offense));
        var version = state.ReadEquipment(owner).Version;
        Assert.Null(state.TryReleaseItem(owner, ring.Id, version - 1));
        Assert.Equal(150, state.Read(owner, owner).Source.MaxHealth);
        Assert.Same(ring, new ItemReleaseService(state).Release(owner, ring.Id));
        var after = state.ReadEquipment(owner);
        Assert.Equal(version + 1, after.Version);
        Assert.Null(after.Loadout.GetItem(EquipmentSlot.RingLeft));
        Assert.False(after.OwnedItems.ContainsKey(ring.Id));
        Assert.Equal(100, state.Read(owner, owner).Source.MaxHealth);
        Assert.Equal(100, state.Read(owner, owner).Source.Health);
        Assert.Equal(0, state.GetStats(owner).Get(new(StatType.Armor)));
    }

    /// <summary>A dependent quiver needs a real bag rectangle before its main weapon can be released.</summary>
    [Fact]
    public void EquippedReleaseChecksDependentReturnsBeforeSpendingOwnership()
    {
        var state = new InMemoryCombatState();
        var owner = new CombatantId(Guid.NewGuid());
        state.AddStatCombatant(owner, new StatProfile(new StatBlock()));
        var factory = new ItemFactory(ItemRarityRules.Default, ItemStatRules.Default);
        var bow = factory.Create("bow", new ItemDefinition("bow", "Bow", ["weapon", "bow"]), ItemRarity.Common);
        var quiver = factory.Create("quiver", new ItemDefinition("quiver", "Quiver", ["offhand", "quiver"], footprint: new ItemFootprint(1, 2)), ItemRarity.Common);
        var full = Item("full", 12, 5);

        foreach (var item in new[] { bow, quiver })
        {
            state.RegisterOwnedItem(owner, item);
        }

        var equipment = new RollForHonor.Application.Equipment.Services.EquipmentService(state);
        Assert.Equal(EquipmentChangeStatus.Applied, equipment.EquipItem(owner, bow.Id, EquipmentSlot.Weapon));
        Assert.Equal(EquipmentChangeStatus.Applied, equipment.EquipItem(owner, quiver.Id, EquipmentSlot.OffHand));
        state.RegisterOwnedItem(owner, full);
        new InventoryGridService().TryPlace(new(12, 5), full.Id, full.Base.Footprint, new(0, 0), out var layout);
        var version = state.ReadEquipment(owner).Version;
        var release = new ItemReleaseService(state);
        Assert.Null(release.Release(owner, bow.Id, layout));
        Assert.Null(release.Release(owner, bow.Id));
        Assert.Throws<InvalidOperationException>(() => release.Release(owner, bow.Id, new(12, 5)));
        Assert.Equal(version, state.ReadEquipment(owner).Version);
        Assert.Same(bow, state.ReadEquipment(owner).Loadout.GetItem(EquipmentSlot.Weapon));
        Assert.Same(quiver, state.ReadEquipment(owner).Loadout.GetItem(EquipmentSlot.OffHand));
        Assert.Same(full, release.Release(owner, full.Id));
        Assert.Same(bow, release.Release(owner, bow.Id, new(12, 5)));
        var after = state.ReadEquipment(owner);
        Assert.Null(after.Loadout.GetItem(EquipmentSlot.Weapon));
        Assert.Null(after.Loadout.GetItem(EquipmentSlot.OffHand));
        Assert.Same(quiver, Assert.Single(after.OwnedItems).Value);
    }

    private static ItemInstance Item(string id, int width, int height) => new ItemFactory(ItemRarityRules.Default, ItemStatRules.Default)
        .Create(id, new ItemDefinition("test", "Test item", footprint: new ItemFootprint(width, height)), ItemRarity.Common);

    private sealed class ConflictingPort(InMemoryCombatState state, CombatantId other) : IItemAcquisitionStateAdapter
    {
        /// <inheritdoc />
        public EquipmentStateRead ReadEquipment(CombatantId ownerId) => state.ReadEquipment(ownerId);

        /// <inheritdoc />
        public EquipmentChangeStatus TryCommitEquipment(EquipmentChangeProposal proposal) => state.TryCommitEquipment(proposal);

        /// <inheritdoc />
        public ItemAcquisitionStatus TryGrantItem(CombatantId owner, ItemInstance item, long expectedVersion)
        {
            state.RegisterOwnedItem(other, Item("concurrent", 1, 1));

            return state.TryGrantItem(owner, item, expectedVersion);
        }
    }
}
