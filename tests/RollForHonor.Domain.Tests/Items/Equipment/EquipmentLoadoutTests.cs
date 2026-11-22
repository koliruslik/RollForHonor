using RollForHonor.Domain.Items.Configuration;
using RollForHonor.Domain.Items.Equipment.Configuration;
using RollForHonor.Domain.Items.Equipment.Models;
using RollForHonor.Domain.Items.Equipment.Services;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Domain.Items.Services;
using RollForHonor.Domain.Stats;

namespace RollForHonor.Domain.Tests.Items.Equipment;

/// <summary>Verifies minimal slot rules, immutable replacements and complete modifier recomposition.</summary>
public sealed class EquipmentLoadoutTests
{
    /// <summary>Every agreed slot accepts its configured base tag.</summary>
    /// <param name="slot">The target equipment slot.</param>
    /// <param name="tag">The required ordinal item tag.</param>
    [Theory]
    [InlineData(EquipmentSlot.Weapon, "weapon")]
    [InlineData(EquipmentSlot.BodyArmor, "body-armor")]
    [InlineData(EquipmentSlot.Helmet, "helmet")]
    [InlineData(EquipmentSlot.Boots, "boots")]
    [InlineData(EquipmentSlot.Amulet, "amulet")]
    [InlineData(EquipmentSlot.RingLeft, "ring")]
    [InlineData(EquipmentSlot.RingRight, "ring")]
    public void DefaultSlotsAcceptTheirConfiguredTags(EquipmentSlot slot, string tag)
    {
        var item = CreateItem("owned", tag);

        var loadout = new EquipmentLoadoutService(EquipmentRules.Default).Equip(EquipmentLoadout.Empty, item, slot);

        Assert.Same(item, loadout.GetItem(slot));
        Assert.Empty(EquipmentLoadout.Empty.Slots);
    }

    /// <summary>Compatibility is explicit and does not infer torso armor from a generic armor tag.</summary>
    [Fact]
    public void IncompatibleItemsAndUndefinedSlotsAreRejected()
    {
        var service = new EquipmentLoadoutService(EquipmentRules.Default);
        var helmet = CreateItem("head", "helmet");
        var genericArmor = CreateItem("generic", "armor");

        Assert.Throws<ArgumentException>(() => service.Equip(EquipmentLoadout.Empty, helmet, EquipmentSlot.BodyArmor));
        Assert.Throws<ArgumentException>(() => service.Equip(EquipmentLoadout.Empty, genericArmor, EquipmentSlot.BodyArmor));
        Assert.Throws<ArgumentOutOfRangeException>(() => service.Unequip(EquipmentLoadout.Empty, (EquipmentSlot)99));
    }

    /// <summary>Two ring slots require distinct owned identities, even when the bases are identical.</summary>
    [Fact]
    public void OneInstanceCannotOccupyBothRingSlots()
    {
        var service = new EquipmentLoadoutService(EquipmentRules.Default);
        var first = CreateItem("one", "ring");
        var second = CreateItem("two", "ring");
        var left = service.Equip(EquipmentLoadout.Empty, first, EquipmentSlot.RingLeft);

        Assert.Throws<ArgumentException>(() => service.Equip(left, first, EquipmentSlot.RingRight));
        Assert.Throws<ArgumentException>(() => new EquipmentLoadout(
            [new(EquipmentSlot.RingLeft, first), new(EquipmentSlot.RingRight, first)]));
        var both = service.Equip(left, second, EquipmentSlot.RingRight);

        Assert.Equal(2, both.Slots.Count);
        Assert.Same(left, service.Equip(left, first, EquipmentSlot.RingLeft));
    }

    /// <summary>Replacement and removal preserve unrelated occupied slots and historical loadouts.</summary>
    [Fact]
    public void ReplacementAndRemovalAffectOnlyTheChosenSlot()
    {
        var service = new EquipmentLoadoutService(EquipmentRules.Default);
        var first = CreateItem("one", "weapon");
        var replacement = CreateItem("two", "weapon");
        var ring = CreateItem("ring", "ring");
        var original = new EquipmentLoadout([new(EquipmentSlot.Weapon, first), new(EquipmentSlot.RingLeft, ring)]);

        var changed = service.Equip(original, replacement, EquipmentSlot.Weapon);
        var removed = service.Unequip(changed, EquipmentSlot.Weapon);

        Assert.Same(first, original.GetItem(EquipmentSlot.Weapon));
        Assert.Same(replacement, changed.GetItem(EquipmentSlot.Weapon));
        Assert.Same(ring, removed.GetItem(EquipmentSlot.RingLeft));
        Assert.Null(removed.GetItem(EquipmentSlot.Weapon));
        Assert.Same(removed, service.Unequip(removed, EquipmentSlot.Weapon));
    }

    /// <summary>Fresh composition preserves all non-equipment sources and never appends previous equipment inputs.</summary>
    [Fact]
    public void CompositionRebuildsEquipmentWithoutFilteringOtherOrigins()
    {
        var armor = new StatKey(StatType.Armor);
        var other = new StatModifier(armor, StatOperation.Flat, 3, "Item:looks-like-equipment");
        var profile = new StatProfile(new StatBlock([new(armor, 2)]), [other]);
        var item = CreateItem("body", "body-armor", new ItemModifier(armor, StatOperation.Flat, 5));
        var loadout = new EquipmentLoadout([new(EquipmentSlot.BodyArmor, item)]);
        var composer = new EquipmentStatComposer();

        var first = composer.Compose(profile, loadout);
        var repeated = composer.Compose(profile, loadout);
        var removed = composer.Compose(profile, EquipmentLoadout.Empty);

        Assert.Equal(10, new StatCalculator().Calculate(first).Get(armor));
        Assert.Equal(first.Modifiers, repeated.Modifiers);
        Assert.Contains(other, repeated.Modifiers);
        Assert.Same(profile, removed);
        Assert.Single(profile.Modifiers);
    }

    /// <summary>Compatibility configuration and supplied slot collections are copied before use.</summary>
    [Fact]
    public void RulesAndLoadoutDoNotRetainMutableCollections()
    {
        var rulesData = Enum.GetValues<EquipmentSlot>()
            .Select(slot => new KeyValuePair<EquipmentSlot, string>(slot, "custom"))
            .ToArray();
        var rules = new EquipmentRules(rulesData);
        var item = CreateItem("item", "custom");
        var slots = new List<KeyValuePair<EquipmentSlot, ItemInstance>> { new(EquipmentSlot.Weapon, item) };
        var loadout = new EquipmentLoadout(slots);

        rulesData[0] = new(EquipmentSlot.Weapon, "different");
        slots.Clear();

        Assert.True(rules.CanEquip(item, EquipmentSlot.Weapon));
        Assert.Same(item, loadout.GetItem(EquipmentSlot.Weapon));
        Assert.Throws<ArgumentException>(() => new EquipmentRules(rulesData.Skip(1)));
    }

    private static ItemInstance CreateItem(string id, string tag, params ItemModifier[] modifiers) =>
        new ItemFactory(ItemRarityRules.Default, ItemStatRules.Default)
            .Create(id, new ItemDefinition("base", "Base", [tag], modifiers), ItemRarity.Common);
}
