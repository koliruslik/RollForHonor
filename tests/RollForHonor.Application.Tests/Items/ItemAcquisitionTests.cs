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
