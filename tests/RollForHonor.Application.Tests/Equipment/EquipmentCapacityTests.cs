using RollForHonor.Application.Equipment.Models;
using RollForHonor.Domain.Items.Configuration;
using RollForHonor.Domain.Items.Equipment.Models;
using RollForHonor.Domain.Items.Inventory.Models;
using RollForHonor.Domain.Items.Inventory.Services;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Domain.Items.Services;
using RollForHonor.Domain.Stats;

namespace RollForHonor.Application.Tests.Equipment;

/// <summary>Verifies bag capacity at the equipment use-case boundary without any game screen.</summary>
public sealed class EquipmentCapacityTests
{
    /// <summary>A full bag rejects removal with or without a caller-supplied spatial layout.</summary>
    /// <param name="supplyLayout">Whether the caller supplies the current bag positions.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FullBagRejectsUnequipWithoutChangingEquipmentOrStats(bool supplyLayout)
    {
        var session = new EquipmentTestSession();
        var weapon = Item("bow", ["weapon", "bow"], new ItemFootprint(2, 3));
        session.State.RegisterOwnedItem(session.Owner, weapon);
        Assert.Equal(EquipmentChangeStatus.Applied, session.Service.EquipItem(session.Owner, weapon.Id, EquipmentSlot.Weapon));
        FillBag(session, 60);
        var before = session.State.ReadEquipment(session.Owner);
        var stats = session.State.GetStats(session.Owner);
        var layout = supplyLayout ? Bag(session) : null;

        var result = session.Service.UnequipItem(session.Owner, EquipmentSlot.Weapon, layout);

        Assert.Equal(EquipmentChangeStatus.InventoryFull, result);
        var after = session.State.ReadEquipment(session.Owner);
        Assert.Equal(before.Version, after.Version);
        Assert.Same(weapon, after.Loadout.GetItem(EquipmentSlot.Weapon));
        Assert.Same(stats, session.State.GetStats(session.Owner));
        Assert.Equal(before.OwnedItems.Count, after.OwnedItems.Count);
    }

    /// <summary>Replacing a bow also accounts for its dependent quiver before publishing any state.</summary>
    [Fact]
    public void ReplacementChecksAllReturnedItemsAtomically()
    {
        var session = new EquipmentTestSession();
        var bow = Item("bow", ["weapon", "bow"], new ItemFootprint(2, 3));
        var quiver = Item("quiver", ["offhand", "quiver"], new ItemFootprint(1, 1));
        var sword = Item("sword", ["weapon", "one-handed"], new ItemFootprint(1, 1));

        foreach (var item in new[] { bow, quiver, sword })
        {
            session.State.RegisterOwnedItem(session.Owner, item);
        }

        session.Service.EquipItem(session.Owner, bow.Id, EquipmentSlot.Weapon);
        session.Service.EquipItem(session.Owner, quiver.Id, EquipmentSlot.OffHand);
        FillBag(session, 59);
        var before = session.State.ReadEquipment(session.Owner);
        var stats = session.State.GetStats(session.Owner);

        var result = session.Service.EquipItem(session.Owner, sword.Id, EquipmentSlot.Weapon, Bag(session));

        Assert.Equal(EquipmentChangeStatus.InventoryFull, result);
        var after = session.State.ReadEquipment(session.Owner);
        Assert.Equal(before.Version, after.Version);
        Assert.Same(bow, after.Loadout.GetItem(EquipmentSlot.Weapon));
        Assert.Same(quiver, after.Loadout.GetItem(EquipmentSlot.OffHand));
        Assert.Same(stats, session.State.GetStats(session.Owner));
    }

    /// <summary>Freeing an incoming weapon's rectangle permits both replaced hands to return.</summary>
    [Fact]
    public void ReplacementUsesSpaceFreedByTheIncomingItem()
    {
        var session = new EquipmentTestSession();
        var bow = Item("bow", ["weapon", "bow"], new ItemFootprint(1, 1));
        var quiver = Item("quiver", ["offhand", "quiver"], new ItemFootprint(1, 1));
        var sword = Item("sword", ["weapon", "one-handed"], new ItemFootprint(2, 1));

        foreach (var item in new[] { bow, quiver, sword })
        {
            session.State.RegisterOwnedItem(session.Owner, item);
        }

        session.Service.EquipItem(session.Owner, bow.Id, EquipmentSlot.Weapon);
        session.Service.EquipItem(session.Owner, quiver.Id, EquipmentSlot.OffHand);
        FillBag(session, 58);

        var result = session.Service.EquipItem(session.Owner, sword.Id, EquipmentSlot.Weapon, Bag(session));

        Assert.Equal(EquipmentChangeStatus.Applied, result);
        var after = session.State.ReadEquipment(session.Owner);
        Assert.Same(sword, after.Loadout.GetItem(EquipmentSlot.Weapon));
        Assert.Null(after.Loadout.GetItem(EquipmentSlot.OffHand));
        Assert.Equal(60, Bag(session).Placements.Count);
    }

    /// <summary>A caller cannot omit owned bag items to bypass capacity validation.</summary>
    [Fact]
    public void IncompleteLayoutCannotBypassCapacity()
    {
        var session = new EquipmentTestSession();
        var bow = Item("bow", ["weapon", "bow"], new ItemFootprint(2, 3));
        session.State.RegisterOwnedItem(session.Owner, bow);
        session.Service.EquipItem(session.Owner, bow.Id, EquipmentSlot.Weapon);
        FillBag(session, 60);
        var before = session.State.ReadEquipment(session.Owner);

        Assert.Throws<InvalidOperationException>(() => session.Service.UnequipItem(session.Owner, EquipmentSlot.Weapon, new InventoryGridLayout(12, 5)));
        Assert.Equal(before.Version, session.State.ReadEquipment(session.Owner).Version);
    }

    /// <summary>Equipment larger than the fixed bag is rejected as a capacity result when removed.</summary>
    [Fact]
    public void OversizedEquipmentReturnsCapacityFailureWithoutPublication()
    {
        var session = new EquipmentTestSession();
        var weapon = Item("oversized", ["weapon"], new ItemFootprint(13, 1));
        session.State.RegisterOwnedItem(session.Owner, weapon);
        Assert.Equal(EquipmentChangeStatus.Applied, session.Service.EquipItem(session.Owner, weapon.Id, EquipmentSlot.Weapon));
        var before = session.State.ReadEquipment(session.Owner);

        Assert.Equal(EquipmentChangeStatus.InventoryFull, session.Service.UnequipItem(session.Owner, EquipmentSlot.Weapon));
        Assert.Equal(before.Version, session.State.ReadEquipment(session.Owner).Version);
        Assert.Same(weapon, session.State.ReadEquipment(session.Owner).Loadout.GetItem(EquipmentSlot.Weapon));
    }

    private static ItemInstance Item(string id, string[] tags, ItemFootprint footprint)
        => new ItemFactory(ItemRarityRules.Default, ItemStatRules.Default).Create(id,
            new ItemDefinition(id, id, tags, [new ItemModifier(new(StatType.Armor), StatOperation.Flat, 1)], footprint: footprint), ItemRarity.Common);

    private static void FillBag(EquipmentTestSession session, int count)
    {
        foreach (var index in Enumerable.Range(0, count))
        {
            session.State.RegisterOwnedItem(session.Owner, Item($"filler-{index}", ["ring"], new ItemFootprint(1, 1)));
        }
    }

    private static InventoryGridLayout Bag(EquipmentTestSession session)
    {
        var read = session.State.ReadEquipment(session.Owner);
        var equipped = read.Loadout.Slots.Values
            .Select(item => item.Id)
            .ToHashSet();
        var items = read.OwnedItems.Values
            .Where(item => !equipped.Contains(item.Id))
            .ToArray();
        Assert.True(new InventoryLayoutService().TryReconcile(new InventoryGridLayout(12, 5), items, out var layout));

        return layout;
    }
}
