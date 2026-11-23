using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using RollForHonor.Application.Equipment.Models;
using RollForHonor.Domain.Items.Equipment.Models;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Domain.Items.Inventory.Models;
using RollForHonor.Godot.UI.Common;
using RollForHonor.Godot.Gameplay.Input;
using RollForHonor.Godot.UI.Localization;

namespace RollForHonor.Godot.UI.Inventory;

/// <summary>Shows a compact equipment paper doll and a fixed twelve-by-five bag of owned items.</summary>
public partial class InventoryPanel : Control
{
    /// <summary>Gets the current immutable bag layout used by capacity-checked pickup.</summary>
    public InventoryGridLayout CurrentLayout => _bag.Layout;

    /// <summary>Raised when an owned item is selected for inspection.</summary>
    public event Action<ItemInstance> ItemSelected;

    /// <summary>Raised when a cell is right-clicked to equip or unequip its item.</summary>
    public event Action<string> ItemActivated;

    /// <summary>Raised when a compatible owned item is dropped on a specific slot.</summary>
    public event Action<string, EquipmentSlot> EquipRequested;

    /// <summary>Raised when an equipped item is dropped into the inventory grid.</summary>
    public event Action<string> UnequipRequested;

    /// <summary>Raised when the inventory is closed independently of character stats.</summary>
    public event Action Closed;

    /// <summary>Raised when the pointer enters or leaves an owned item cell.</summary>
    public event Action<ItemInstance> ItemHovered;

    private readonly Dictionary<EquipmentSlot, ItemCell> _slots = new();
    private InventoryGridView _bag;
    private Label _count;
    private Label _hints;
    private Label _status;
    private EquipmentStateRead _state;

    /// <inheritdoc />
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        var panel = UiLayout.Panel(this, "Inventory", new Rect2(0, 0, 560, 1080));
        BuildHeader(panel);
        BuildPaperDoll(panel);
        BuildInventory(panel);
        Hide();
    }

    /// <inheritdoc />
    public override void _Process(double delta)
    {
        if (Visible)
        {
            RefreshHints();
        }
    }

    /// <summary>Rebuilds controls from a consistent authoritative equipment read.</summary>
    /// <param name="state">Owned items, current slots and active compatibility rules.</param>
    public void Refresh(EquipmentStateRead state)
    {
        RefreshHints();

        var unchanged = _state is not null && _state.OwnedItems.Count == state.OwnedItems.Count
            && _state.OwnedItems.All(pair => state.OwnedItems.TryGetValue(pair.Key, out var item) && ReferenceEquals(pair.Value, item))
            && _state.Loadout.Slots.Count == state.Loadout.Slots.Count
            && _state.Loadout.Slots.All(pair => state.Loadout.GetItem(pair.Key)?.Id == pair.Value.Id);
        _state = state;

        if (unchanged)
        {
            return;
        }

        foreach (var pair in _slots)
        {
            var item = state.Loadout.GetItem(pair.Key);
            pair.Value.ItemId = item?.Id ?? "";
            pair.Value.DragFootprint = item is null ? Vector2I.One : new Vector2I(item.Base.Footprint.Width, item.Base.Footprint.Height);

            var icon = item is null ? ItemPresentation.Icon(pair.Key) : ItemPresentation.Icon(item, pair.Key);
            pair.Value.SetAppearance(icon, item is null ? HonorTheme.Border : ItemPresentation.RarityColor(item.Rarity), item is not null);
        }

        var equipped = state.Loadout.Slots.Values
            .Select(item => item.Id)
            .ToHashSet(StringComparer.Ordinal);
        var items = state.OwnedItems.Values
            .Where(item => !equipped.Contains(item.Id))
            .OrderByDescending(item => item.Rarity)
            .ThenBy(item => item.Base.Name, StringComparer.Ordinal)
            .ToArray();
        L.SetText(_count, L.Format($"ITEMS   {items.Length}"));
        _bag.RefreshItems(items);
        L.SetText(_status, "");
    }

    /// <summary>Checks the fixed bag before publishing a proposed loadout, including dependent offhand returns.</summary>
    /// <param name="state">The consistent canonical ownership read.</param>
    /// <param name="loadout">The proposed equipped items.</param>
    /// <returns>True when all remaining owned items fit without expanding the bag.</returns>
    public bool CanFitLoadout(EquipmentStateRead state, EquipmentLoadout loadout)
    {
        var equipped = loadout.Slots.Values
            .Select(item => item.Id)
            .ToHashSet(StringComparer.Ordinal);
        var items = state.OwnedItems.Values
            .Where(item => !equipped.Contains(item.Id))
            .ToArray();

        return _bag.CanFitItems(items);
    }

    /// <summary>Shows feedback for a rejected item action above the inventory hints.</summary>
    /// <param name="message">The short action result.</param>
    public void ShowStatus(string message)
    {
        L.SetText(_status, message);
    }

    private void BuildHeader(Control panel)
    {
        UiLayout.Place(panel, UiLayout.Text(L.Text("Inventory"), 24, heading: true), new Rect2(24, 16, 400, 48));
        UiLayout.Place(panel, UiLayout.CloseButton(() => Closed?.Invoke()), new Rect2(496, 20, 40, 40));
        UiLayout.Divider(panel, new Rect2(24, 80, 512, 2));
        UiLayout.Place(panel, UiLayout.Text(L.Text("EQUIPMENT"), 14, HonorTheme.Brass), new Rect2(24, 96, 512, 32));
    }

    private void BuildPaperDoll(Control panel)
    {
        AddSlot(panel, EquipmentSlot.Helmet, new Vector2(240, 152));
        AddSlot(panel, EquipmentSlot.Amulet, new Vector2(352, 160), new Vector2(64, 64));
        AddSlot(panel, EquipmentSlot.Weapon, new Vector2(64, 264), new Vector2(96, 176));
        AddSlot(panel, EquipmentSlot.BodyArmor, new Vector2(232, 272), new Vector2(96, 120));
        AddSlot(panel, EquipmentSlot.OffHand, new Vector2(400, 264), new Vector2(96, 176));
        AddSlot(panel, EquipmentSlot.RingLeft, new Vector2(152, 456), new Vector2(56, 56));
        AddSlot(panel, EquipmentSlot.Boots, new Vector2(240, 424));
        AddSlot(panel, EquipmentSlot.RingRight, new Vector2(352, 456), new Vector2(56, 56));
        UiLayout.Divider(panel, new Rect2(24, 576, 512, 2));
    }

    private void BuildInventory(Control panel)
    {
        _count = UiLayout.Place(panel, UiLayout.Text(L.Text("ITEMS"), 14, HonorTheme.Brass), new Rect2(24, 600, 512, 32));
        _bag = new InventoryGridView
        {
            Name = "Grid",
            ResolveItem = id => _state is not null && _state.OwnedItems.TryGetValue(id, out var item) ? item : null,
            IsEquipped = IsEquipped,
            IconKey = item => ItemPresentation.Icon(item, ItemPresentation.Slot(item, _state.Rules))
        };
        _bag.ItemSelected += Select;
        _bag.ItemActivated += id => ItemActivated?.Invoke(id);
        _bag.ItemHovered += Hover;
        _bag.UnequipRequested += id => UnequipRequested?.Invoke(id);
        UiLayout.Place(panel, _bag, new Rect2(28, 648, InventoryGridView.Columns * InventoryGridView.CellSize, InventoryGridView.Rows * InventoryGridView.CellSize));
        _status = UiLayout.Place(panel, UiLayout.Text("", 13, HonorTheme.Brass), new Rect2(24, 872, 512, 24));
        _status.Name = "InventoryStatus";
        _hints = UiLayout.Place(panel, UiLayout.Text("", 12, new Color(HonorTheme.Muted, 0.55f)), new Rect2(24, 902, 512, 154));
        _hints.Name = "InventoryHints";
        _hints.VerticalAlignment = VerticalAlignment.Bottom;
    }

    private void RefreshHints()
    {
        _hints.Text = string.Join("\n",
        [
            L.Text("Hover  Inspect item"),
            L.Text("RMB  Equip / remove"),
            L.Text("LMB  Compare"),
            L.Text("Drag  Move / equip / remove"),
            L.Format($"{GameInputBindings.Name("rfh_details")}  Ranges / inspect tooltip"),
            L.Text("Underlined text  Details"),
            L.Format($"{GameInputBindings.Name("rfh_inventory")}  Close inventory")
        ]);
    }

    private void AddSlot(Control panel, EquipmentSlot slot, Vector2 position, Vector2? size = null)
    {
        var cell = new ItemCell
        {
            Name = slot.ToString(),
            CanReceive = id => _state is not null && _state.OwnedItems.TryGetValue(id, out var item) && _state.Rules.CanEquip(_state.Loadout, item, slot),
            Receive = id => EquipRequested?.Invoke(id, slot)
        };
        cell.Pressed += () => Select(cell.ItemId);
        cell.MouseEntered += () => Hover(cell.ItemId);
        cell.MouseExited += () => ItemHovered?.Invoke(null);
        cell.Activated += () => ItemActivated?.Invoke(cell.ItemId);
        var extent = size ?? new Vector2(80, 80);
        _slots.Add(slot, UiLayout.Place(panel, cell, new Rect2(position, extent)));
        var label = UiLayout.Text(ItemPresentation.SlotName(slot), 14, HonorTheme.Muted);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        UiLayout.Place(panel, label, new Rect2(position.X + (extent.X - 128) / 2, position.Y + extent.Y + 8, 128, 24));
    }

    private void Select(string id)
    {
        if (_state is not null && _state.OwnedItems.TryGetValue(id, out var item))
        {
            ItemSelected?.Invoke(item);
        }
    }

    private void Hover(string id)
    {
        ItemHovered?.Invoke(_state is not null && _state.OwnedItems.TryGetValue(id, out var item) ? item : null);
    }

    private bool IsEquipped(string id) => _state is not null && _state.Loadout.Slots.Values.Any(item => item.Id == id);
}
