using System.Linq;
using Chickensoft.GoDotTest;
using Godot;
using RollForHonor.Application.Equipment.Services;
using RollForHonor.Domain.Items.Configuration;
using RollForHonor.Domain.Items.Equipment.Models;
using RollForHonor.Domain.Items.Inventory.Models;
using RollForHonor.Domain.Items.Inventory.Services;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Domain.Items.Services;
using RollForHonor.Godot.Arena;
using RollForHonor.Godot.UI.Hud;
using RollForHonor.Godot.UI.Inventory;
using Shouldly;

namespace RollForHonor.Godot.Tests;

/// <summary>Verifies native spatial item geometry, safe drops and session-local layout retention.</summary>
public sealed class GodotSpatialInventoryTest : TestClass
{
    /// <summary>Creates spatial-inventory checks.</summary>
    /// <param name="testScene">The engine test host.</param>
    public GodotSpatialInventoryTest(Node testScene) : base(testScene)
    {
    }

    /// <summary>The starter kit occupies complete rectangles without overlap or icon-cell gaps, with five fixed rows.</summary>
    [Test]
    public void StarterItemsUseTheirAuthoredFootprints()
    {
        var arena = CreateArena();

        try
        {
            var hud = arena.GetNode<ArenaHud>("Presentation");
            hud.ToggleInventory();
            var grid = (InventoryGridView)hud.FindChild("Grid", true, false);
            grid.Layout.Columns.ShouldBe(12);
            grid.Layout.Rows.ShouldBe(5);
            grid.GetParent().ShouldNotBeOfType<ScrollContainer>();
            grid.Layout.Placements.Count.ShouldBe(12);
            grid.Layout.Placements.Values.Sum(entry => entry.Footprint.Width * entry.Footprint.Height).ShouldBe(45);
            var bow = (ItemCell)hud.FindChild("ashen-longbow", true, false);
            bow.Size.ShouldBe(new Vector2(2, 3) * InventoryGridView.CellSize);
            ((ItemCell)hud.FindChild("cinder-band", true, false)).Size.ShouldBe(Vector2.One * InventoryGridView.CellSize);
            var placement = new InventoryGridService();

            foreach (var entry in grid.Layout.Placements.Values)
            {
                placement.CanPlace(grid.Layout, entry.ItemId, entry.Footprint, entry.Position).ShouldBeTrue();
            }
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>Manual positions survive refresh and reopening; invalid drops preserve layout and canonical ownership.</summary>
    [Test]
    public void ReorderingSurvivesWindowsWithoutChangingEquipmentState()
    {
        var arena = CreateArena();

        try
        {
            var hud = arena.GetNode<ArenaHud>("Presentation");
            hud.ToggleInventory();
            var grid = (InventoryGridView)hud.FindChild("Grid", true, false);
            var ring = (ItemCell)hud.FindChild("oathkeeper-ring", true, false);
            var id = ring.ItemId;
            var runtime = arena.GetParent<GameBootstrap>();
            var before = runtime.CombatState.ReadEquipment(arena.Player.CombatantId);
            var payload = new global::Godot.Collections.Dictionary { ["rfh-item-id"] = id };
            var point = new Vector2(9, 3) * InventoryGridView.CellSize + Vector2.One * 4;
            grid._CanDropData(point, payload).ShouldBeTrue();
            grid._DropData(point, payload);
            grid.Layout.Placements[id].Position.ShouldBe(new InventoryPosition(9, 3));
            runtime.CombatState.ReadEquipment(arena.Player.CombatantId).Version.ShouldBe(before.Version);
            hud.ToggleInventory();
            hud.ToggleInventory();
            grid.Layout.Placements[id].Position.ShouldBe(new InventoryPosition(9, 3));
            var bow = (ItemCell)hud.FindChild("ashen-longbow", true, false);
            var blocked = grid.Layout;
            grid._CanDropData(bow.Position + Vector2.One * 4, payload).ShouldBeFalse();
            grid._DropData(bow.Position + Vector2.One * 4, payload);
            grid._CanDropData(new Vector2(-1, 0), payload).ShouldBeFalse();
            grid._CanDropData(Vector2.Zero, new global::Godot.Collections.Dictionary { ["rfh-item-id"] = "foreign" }).ShouldBeFalse();
            grid.Layout.ShouldBeSameAs(blocked);
            runtime.CombatState.ReadEquipment(arena.Player.CombatantId).OwnedItems.Count.ShouldBe(12);
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>A full or fragmented bag rejects extra rectangles atomically without adding rows.</summary>
    [Test]
    public void CapacityDoesNotCreateHiddenRows()
    {
        var arena = CreateArena();

        try
        {
            var hud = arena.GetNode<ArenaHud>("Presentation");
            hud.ToggleInventory();
            var runtime = arena.GetParent<GameBootstrap>();
            var grid = (InventoryGridView)hud.FindChild("Grid", true, false);
            var before = grid.Layout;
            var extra = new ItemFactory(ItemRarityRules.Default, ItemStatRules.Default).Create("extra-bow",
                new ItemDefinition("extra-bow", "Extra Bow", ["weapon"], footprint: new ItemFootprint(2, 3)), ItemRarity.Common);
            var items = runtime.CombatState.ReadEquipment(arena.Player.CombatantId).OwnedItems.Values
                .Append(extra)
                .ToArray();
            grid.CanFitItems(items).ShouldBeFalse();
            Should.Throw<System.InvalidOperationException>(() => grid.RefreshItems(items));
            grid.Layout.ShouldBeSameAs(before);
            grid.Layout.Rows.ShouldBe(5);
            grid.Size.ShouldBe(new Vector2(12, 5) * InventoryGridView.CellSize);
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>A grabbed bottom-right cell lands on the corresponding destination cell within the fixed bag.</summary>
    [Test]
    public void GrabOffsetsRetainTheHeldCell()
    {
        var arena = CreateArena();

        try
        {
            var hud = arena.GetNode<ArenaHud>("Presentation");
            hud.ToggleInventory();
            var runtime = arena.GetParent<GameBootstrap>();
            var armor = runtime.CombatState.ReadEquipment(arena.Player.CombatantId).OwnedItems.Values.Single(item => item.Base.Id == "embercoat");
            new EquipmentService(runtime.CombatState).EquipItem(arena.Player.CombatantId, armor.Id, EquipmentSlot.BodyArmor);
            hud._Process(0);
            var grid = (InventoryGridView)hud.FindChild("Grid", true, false);
            var bow = (ItemCell)hud.FindChild("ashen-longbow", true, false);
            var payload = new global::Godot.Collections.Dictionary
            {
                ["rfh-item-id"] = bow.ItemId,
                ["rfh-grab-cell"] = new Vector2I(1, 2)
            };
            var id = bow.ItemId;
            var point = new Vector2(3, 2) * InventoryGridView.CellSize + Vector2.One * 4;
            grid._CanDropData(point, payload).ShouldBeTrue();
            grid._DropData(point, payload);
            grid.Layout.Placements[id].Position.ShouldBe(new InventoryPosition(2, 0));
            grid.Layout.Rows.ShouldBe(5);
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>Right-click removal is rejected before publication when free cells cannot contain the weapon.</summary>
    [Test]
    public void EquipmentCannotReturnIntoAFragmentedFullBag()
    {
        var arena = CreateArena();

        try
        {
            var hud = arena.GetNode<ArenaHud>("Presentation");
            hud.ToggleInventory();
            var runtime = arena.GetParent<GameBootstrap>();
            var owner = arena.Player.CombatantId;
            var bow = runtime.CombatState.ReadEquipment(owner).OwnedItems.Values.Single(item => item.Base.Id == "ashen-longbow");
            new EquipmentService(runtime.CombatState).EquipItem(owner, bow.Id, EquipmentSlot.Weapon);
            hud._Process(0);
            var factory = new ItemFactory(ItemRarityRules.Default, ItemStatRules.Default);

            foreach (var index in Enumerable.Range(0, 6))
            {
                runtime.CombatState.RegisterOwnedItem(owner, factory.Create($"filler-{index}", new ItemDefinition($"filler-{index}", "Small item", ["ring"]), ItemRarity.Common));
            }

            hud._Process(0);
            var before = runtime.CombatState.ReadEquipment(owner);
            var hand = (ItemCell)hud.FindChild("Weapon", true, false);
            hand._GuiInput(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = true });
            var after = runtime.CombatState.ReadEquipment(owner);
            after.Version.ShouldBe(before.Version);
            after.Loadout.GetItem(EquipmentSlot.Weapon).ShouldBeSameAs(bow);
            after.OwnedItems.Count.ShouldBe(18);
            ((Label)hud.FindChild("InventoryStatus", true, false)).Text.ShouldContain("Inventory full");
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>The second hand validates native drops against the main weapon and returns dependent items to the bag.</summary>
    [Test]
    public void TwoHandPanelsUseAuthoritativeCompatibility()
    {
        var arena = CreateArena();

        try
        {
            var hud = arena.GetNode<ArenaHud>("Presentation");
            hud.ToggleInventory();
            var runtime = arena.GetParent<GameBootstrap>();
            var service = new EquipmentService(runtime.CombatState);
            var owner = arena.Player.CombatantId;
            var quiver = (ItemCell)hud.FindChild("ranger-quiver", true, false);
            var bow = (ItemCell)hud.FindChild("ashen-longbow", true, false);
            var offHand = (ItemCell)hud.FindChild("OffHand", true, false);
            var mainHand = (ItemCell)hud.FindChild("Weapon", true, false);
            var payload = new global::Godot.Collections.Dictionary { ["rfh-item-id"] = quiver.ItemId };
            offHand._CanDropData(Vector2.One, payload).ShouldBeFalse();
            service.EquipItem(owner, bow.ItemId, EquipmentSlot.Weapon);
            hud._Process(0);
            offHand._CanDropData(Vector2.One, payload).ShouldBeTrue();
            offHand._DropData(Vector2.One, payload);
            runtime.CombatState.ReadEquipment(owner).Loadout.GetItem(EquipmentSlot.OffHand).Base.Id.ShouldBe("ranger-quiver");
            mainHand.GetRect().Intersects(offHand.GetRect()).ShouldBeFalse();
            mainHand.Size.ShouldBe(offHand.Size);

            service.UnequipItem(owner, EquipmentSlot.Weapon);
            hud._Process(0);
            offHand.ItemId.ShouldBeEmpty();
            var grid = (InventoryGridView)hud.FindChild("Grid", true, false);
            grid.Layout.Placements.Count.ShouldBe(12);
            grid.Layout.Placements.ContainsKey(payload["rfh-item-id"].AsString()).ShouldBeTrue();
        }
        finally
        {
            arena.Free();
        }
    }

    private GreyboxArena CreateArena()
    {
        var runtime = TestScene.GetTree().Root.GetChildren()
            .OfType<GameBootstrap>()
            .Single();
        var arena = GD.Load<PackedScene>("res://Arena.tscn").Instantiate<GreyboxArena>();
        runtime.AddChild(arena);
        arena.Player.Controls.HardwareInput = false;
        arena.Opponent.Controls.ThinkingEnabled = false;

        return arena;
    }
}
