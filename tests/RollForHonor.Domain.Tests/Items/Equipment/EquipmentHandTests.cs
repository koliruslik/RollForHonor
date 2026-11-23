using RollForHonor.Domain.Items.Configuration;
using RollForHonor.Domain.Items.Equipment.Configuration;
using RollForHonor.Domain.Items.Equipment.Models;
using RollForHonor.Domain.Items.Equipment.Services;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Domain.Items.Services;

namespace RollForHonor.Domain.Tests.Items.Equipment;

/// <summary>Verifies configurable hand pairing and immutable dependent-slot removal.</summary>
public sealed class EquipmentHandTests
{
    /// <summary>Quivers require bows, shields permit one-handed weapons or an empty main hand.</summary>
    /// <param name="mainTag">The main-hand category, or null for an empty hand.</param>
    /// <param name="offTag">The offhand category.</param>
    /// <param name="expected">Whether the pair is compatible.</param>
    [Theory]
    [InlineData("bow", "quiver", true)]
    [InlineData("one-handed", "quiver", false)]
    [InlineData(null, "quiver", false)]
    [InlineData("bow", "shield", false)]
    [InlineData("two-handed", "shield", false)]
    [InlineData("one-handed", "shield", true)]
    [InlineData(null, "shield", true)]
    [InlineData("bow", "unknown", false)]
    public void OffHandChecksMainHand(string? mainTag, string offTag, bool expected)
    {
        var main = mainTag is null ? EquipmentLoadout.Empty : new EquipmentLoadout([new(EquipmentSlot.Weapon, Item("main", "weapon", mainTag))]);
        var offHand = Item("off", "offhand", offTag);

        Assert.Equal(expected, EquipmentRules.Default.CanEquip(main, offHand, EquipmentSlot.OffHand));
    }

    /// <summary>Changing main-hand categories removes only the now-incompatible offhand from the new loadout.</summary>
    [Fact]
    public void ReplacingMainHandReturnsIncompatibleOffHand()
    {
        var service = new EquipmentLoadoutService(EquipmentRules.Default);
        var bow = Item("bow", "weapon", "bow");
        var sword = Item("sword", "weapon", "one-handed");
        var quiver = Item("quiver", "offhand", "quiver");
        var shield = Item("shield", "offhand", "shield");
        var archer = service.Equip(service.Equip(EquipmentLoadout.Empty, bow, EquipmentSlot.Weapon), quiver, EquipmentSlot.OffHand);
        var swordsman = service.Equip(archer, sword, EquipmentSlot.Weapon);
        var defended = service.Equip(swordsman, shield, EquipmentSlot.OffHand);
        var archerAgain = service.Equip(defended, bow, EquipmentSlot.Weapon);

        Assert.Same(quiver, archer.GetItem(EquipmentSlot.OffHand));
        Assert.Null(swordsman.GetItem(EquipmentSlot.OffHand));
        Assert.Same(shield, defended.GetItem(EquipmentSlot.OffHand));
        Assert.Null(archerAgain.GetItem(EquipmentSlot.OffHand));
        Assert.Throws<ArgumentException>(() => service.Equip(archer, shield, EquipmentSlot.OffHand));
    }

    /// <summary>Removing a bow also removes its dependent quiver; a shield can remain without a weapon.</summary>
    [Fact]
    public void RemovingMainHandRetainsOnlyIndependentOffHands()
    {
        var service = new EquipmentLoadoutService(EquipmentRules.Default);
        var archer = new EquipmentLoadout([new(EquipmentSlot.Weapon, Item("bow", "weapon", "bow")), new(EquipmentSlot.OffHand, Item("quiver", "offhand", "quiver"))]);
        var swordsman = new EquipmentLoadout([new(EquipmentSlot.Weapon, Item("sword", "weapon", "one-handed")), new(EquipmentSlot.OffHand, Item("shield", "offhand", "shield"))]);

        Assert.Empty(service.Unequip(archer, EquipmentSlot.Weapon).Slots);
        Assert.Same(swordsman.GetItem(EquipmentSlot.OffHand), service.Unequip(swordsman, EquipmentSlot.Weapon).GetItem(EquipmentSlot.OffHand));
        Assert.Same(archer, service.Equip(archer, archer.GetItem(EquipmentSlot.Weapon)!, EquipmentSlot.Weapon));
    }

    /// <summary>Hand pairing configuration is copied and does not depend on hard-coded item identities.</summary>
    [Fact]
    public void CustomPairRulesAreCopied()
    {
        var pairs = new List<EquipmentPairRule> { new("focus", "wand") };
        var tags = Enum.GetValues<EquipmentSlot>()
            .Select(slot => new KeyValuePair<EquipmentSlot, string>(slot, slot == EquipmentSlot.OffHand ? "offhand" : "weapon"));
        var rules = new EquipmentRules(tags, pairs);
        var loadout = new EquipmentLoadout([new(EquipmentSlot.Weapon, Item("wand", "weapon", "wand"))]);
        pairs.Clear();

        Assert.True(rules.CanEquip(loadout, Item("focus", "offhand", "focus"), EquipmentSlot.OffHand));
        Assert.False(rules.CanEquip(EquipmentLoadout.Empty, Item("focus", "offhand", "focus"), EquipmentSlot.OffHand));
        Assert.Throws<ArgumentException>(() => new EquipmentPairRule(" ", "bow"));
        Assert.Throws<ArgumentException>(() => new EquipmentRules(tags, [null!]));
    }

    private static ItemInstance Item(string id, params string[] tags) => new ItemFactory(ItemRarityRules.Default, ItemStatRules.Default)
        .Create(id, new ItemDefinition(id, id, tags), ItemRarity.Common);
}
