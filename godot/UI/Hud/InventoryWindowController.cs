using System;
using System.Linq;
using System.Collections.Generic;
using Godot;
using RollForHonor.Application.Equipment.Models;
using RollForHonor.Application.Equipment.Services;
using RollForHonor.Application.Items;
using RollForHonor.Domain.Items.Equipment.Models;
using RollForHonor.Domain.Items.Inventory.Models;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Godot.Arena;
using RollForHonor.Godot.UI.Inventory;
using RollForHonor.Godot.UI.Localization;

namespace RollForHonor.Godot.UI.Hud;

internal sealed class InventoryWindowController
{
    private readonly InventoryPanel _inventory;
    private readonly CharacterPanel _character;
    private readonly ItemDetailsPanel _details;
    private readonly ItemDetailsPanel _comparison;
    private readonly Action _overlayChanged;
    private readonly Action _clearInspection;
    private readonly Action _capacityWarning;
    private readonly Action _refreshCharacter;
    private readonly Action<string> _recordAction;
    private GameBootstrap _runtime;
    private GreyboxArena _arena;
    private EquipmentService _equipment;
    private ItemInstance _selected;
    private bool _comparisonDismissed;
    private long _inventoryVersion = -1;

    internal InventoryWindowController(InventoryPanel inventory, CharacterPanel character,
        ItemDetailsPanel details, ItemDetailsPanel comparison, Action overlayChanged,
        Action clearInspection, Action capacityWarning, Action refreshCharacter, Action<string> recordAction)
    {
        _inventory = inventory;
        _character = character;
        _details = details;
        _comparison = comparison;
        _overlayChanged = overlayChanged;
        _clearInspection = clearInspection;
        _capacityWarning = capacityWarning;
        _refreshCharacter = refreshCharacter;
        _recordAction = recordAction;
        _details.DropRequested += DiscardSelected;
        _details.Closed += CloseDetails;
        _inventory.ItemSelected += SelectItem;
        _inventory.ItemActivated += ActivateItem;
        _inventory.EquipRequested += DropIntoSlot;
        _inventory.UnequipRequested += DropIntoInventory;
        _inventory.Closed += CloseInventory;
        _comparison.Closed += DismissComparison;
    }

    internal void Configure(GameBootstrap runtime, GreyboxArena arena)
    {
        _runtime = runtime;
        _arena = arena;
        _equipment = new EquipmentService(runtime.CombatState);
        _inventory.ResolveWorldItem = id => arena.NearbyDrops().FirstOrDefault(drop => drop.Item.Id == id)?.Item;
        _inventory.ReceiveWorldItem = (id, position) => arena.PickUpItem(id, position);
    }

    internal void ToggleInventory()
    {
        var open = !_inventory.Visible;
        _inventory.Visible = open;
        _details.Visible = open && _selected is not null;
        _comparison.Hide();

        if (!open)
        {
            CloseDetails();
        }

        _overlayChanged();
    }

    internal void Invalidate() => _inventoryVersion = -1;

    internal InventoryGridLayout ReadBagLayout()
    {
        _inventory.Refresh(_runtime.CombatState.ReadEquipment(_arena.Player.CombatantId));

        return _inventory.CurrentLayout;
    }

    internal ItemAcquisitionStatus AcquireItem(ItemInstance item, InventoryPosition? destination, bool recordAction)
    {
        var layout = ReadBagLayout();
        var result = new ItemAcquisitionService(_runtime.CombatState).Acquire(_arena.Player.CombatantId, item, layout, destination);

        if (result == ItemAcquisitionStatus.InventoryFull)
        {
            _capacityWarning();
        }
        else if (result == ItemAcquisitionStatus.Acquired)
        {
            if (recordAction)
            {
                _recordAction(L.Format($"Picked up: {ItemPresentation.Name(item)}"));
            }

            Invalidate();
            Refresh();
        }

        return result;
    }

    internal void Refresh()
    {
        if (!_inventory.Visible)
        {
            return;
        }

        var read = _runtime.CombatState.ReadEquipment(_arena.Player.CombatantId);

        if (read.Version == _inventoryVersion)
        {
            return;
        }

        _inventoryVersion = read.Version;
        _inventory.Refresh(read);
        _inventory.RefreshGold(_runtime.CombatState.ReadGoldWallet(_arena.Player.CombatantId).Balance);

        if (_selected is not null)
        {
            if (read.OwnedItems.TryGetValue(_selected.Id, out var current))
            {
                SelectItem(current);
            }
            else
            {
                CloseDetails();
            }
        }
    }

    internal bool CanDiscardItem(string id)
    {
        if (_inventory.GetTree().Paused || !_inventory.Visible)
        {
            return false;
        }

        var owner = _arena.Player.CombatantId;
        var read = _runtime.CombatState.ReadEquipment(owner);

        return !_runtime.CombatState.Read(owner, owner).Source.IsDefeated
            && read.OwnedItems.ContainsKey(id);
    }

    internal bool DiscardOwnedItem(string id)
    {
        if (!CanDiscardItem(id) || !_arena.DiscardItem(id))
        {
            _inventory.ShowStatus(L.Text("Could not drop the item. Check nearby ground and inventory space."));
            return false;
        }

        if (_selected?.Id == id)
        {
            CloseDetails();
        }

        _inventoryVersion = -1;
        Refresh();

        return true;
    }

    internal void CloseWindows()
    {
        _inventory.Hide();
        _character.Hide();
        CloseDetails();
        _overlayChanged();
    }

    internal void CloseDetails()
    {
        _clearInspection();
        _selected = null;
        _details.Hide();
        _comparison.Hide();
    }

    private void DiscardSelected()
    {
        if (_selected is not null)
        {
            DiscardOwnedItem(_selected.Id);
        }
    }

    private void SelectItem(ItemInstance item)
    {
        _comparisonDismissed = _selected?.Id == item.Id && _comparisonDismissed;
        _selected = item;
        var read = _runtime.CombatState.ReadEquipment(_arena.Player.CombatantId);
        var occupied = read.Loadout.Slots.FirstOrDefault(pair => pair.Value.Id == item.Id);
        var equipped = occupied.Value is not null;
        var slot = equipped ? occupied.Key : ItemPresentation.Slot(item, read.Rules);

        if (slot is null)
        {
            _details.ShowItem(item, null, false, null);
            _comparison.Hide();
            return;
        }

        if (!equipped && slot == EquipmentSlot.RingLeft && read.Loadout.GetItem(slot.Value) is not null
            && read.Loadout.GetItem(EquipmentSlot.RingRight) is null)
        {
            slot = EquipmentSlot.RingRight;
        }

        _details.ShowItem(item, slot, equipped, () => ChangeEquipment(item, slot.Value, equipped));
        var current = read.Loadout.GetItem(slot.Value);

        if (!equipped && current is not null && !_comparisonDismissed)
        {
            _comparison.ShowComparison(current, slot.Value);
        }
        else
        {
            _comparison.Hide();
        }
    }

    private void ActivateItem(string id)
    {
        var read = _runtime.CombatState.ReadEquipment(_arena.Player.CombatantId);

        if (!read.OwnedItems.TryGetValue(id, out var item))
        {
            return;
        }

        var occupied = read.Loadout.Slots.FirstOrDefault(pair => pair.Value.Id == id);

        if (occupied.Value is not null)
        {
            ChangeEquipment(item, occupied.Key, true);
            return;
        }

        var slot = ItemPresentation.Slot(item, read.Rules);

        if (slot == EquipmentSlot.RingLeft && read.Loadout.GetItem(EquipmentSlot.RingLeft) is not null
            && read.Loadout.GetItem(EquipmentSlot.RingRight) is null)
        {
            slot = EquipmentSlot.RingRight;
        }

        if (slot is { } destination)
        {
            ChangeEquipment(item, destination, false);
        }
    }

    private void DropIntoSlot(string id, EquipmentSlot slot)
    {
        var read = _runtime.CombatState.ReadEquipment(_arena.Player.CombatantId);

        if (read.OwnedItems.TryGetValue(id, out var item) && read.Rules.CanEquip(read.Loadout, item, slot))
        {
            ChangeEquipment(item, slot, false);
        }
    }

    private void DropIntoInventory(string id, InventoryPosition position)
    {
        var read = _runtime.CombatState.ReadEquipment(_arena.Player.CombatantId);
        var occupied = read.Loadout.Slots.FirstOrDefault(pair => pair.Value.Id == id);

        if (occupied.Value is not null)
        {
            ChangeEquipment(occupied.Value, occupied.Key, true, position);
        }
    }

    private void ChangeEquipment(ItemInstance item, EquipmentSlot slot, bool equipped, InventoryPosition? destination = null)
    {
        var owner = _arena.Player.CombatantId;
        var layout = ReadBagLayout();
        var preferred = new Dictionary<string, InventoryPosition>(_inventory.PreferredPositions);

        if (destination is { } position)
        {
            preferred[item.Id] = position;
        }

        var result = equipped
            ? _equipment.UnequipItem(owner, slot, layout, preferred)
            : _equipment.EquipItem(owner, item.Id, slot, layout, preferred);

        if (result == EquipmentChangeStatus.Applied)
        {
            _recordAction(equipped ? L.Format($"Removed: {ItemPresentation.Name(item)}") : L.Format($"Equipped: {ItemPresentation.Name(item)}"));
        }

        Refresh();

        if (result == EquipmentChangeStatus.Applied)
        {
            _refreshCharacter();
        }

        ShowEquipmentResult(item, equipped, result);
    }

    private void ShowEquipmentResult(ItemInstance item, bool equipped, EquipmentChangeStatus result)
    {
        var status = result switch
        {
            EquipmentChangeStatus.Applied => equipped ? L.Text("Returned to inventory.") : L.Text("Equipped. Stats updated."),
            EquipmentChangeStatus.Unchanged => L.Text("Already equipped."),
            EquipmentChangeStatus.InventoryFull => L.Text("Inventory full. Make room before changing equipment."),
            EquipmentChangeStatus.AlreadyEquippedElsewhere => L.Text("Remove the item from its current slot first."),
            EquipmentChangeStatus.IncompatibleSlot => L.Text("Requires a compatible main hand or equipment slot."),
            EquipmentChangeStatus.RequirementsNotMet => L.Text("This item requires a higher level or more allocated attributes."),
            EquipmentChangeStatus.VersionConflict => L.Text("Your equipment changed. Select the item again."),
            _ => L.Text("This item cannot be equipped in that slot.")
        };

        if (result == EquipmentChangeStatus.InventoryFull)
        {
            _capacityWarning();
        }

        if (result is not (EquipmentChangeStatus.Applied or EquipmentChangeStatus.Unchanged))
        {
            _inventory.ShowStatus(status);
        }

        if (_selected?.Id == item.Id)
        {
            _details.SetStatus(status);
        }
    }

    private void CloseInventory()
    {
        _inventory.Hide();
        CloseDetails();
        _overlayChanged();
    }

    private void DismissComparison()
    {
        _comparisonDismissed = true;
        _comparison.Hide();
    }
}
