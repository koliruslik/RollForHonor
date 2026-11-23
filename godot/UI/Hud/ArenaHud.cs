using System;
using System.Linq;
using Godot;
using RollForHonor.Application.Equipment.Models;
using RollForHonor.Application.Equipment.Services;
using RollForHonor.Domain.Items.Equipment.Models;
using RollForHonor.Domain.Items.Equipment.Services;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Domain.Stats;
using RollForHonor.Godot.Arena;
using RollForHonor.Godot.UI.Common;
using RollForHonor.Godot.UI.Inventory;
using RollForHonor.Godot.UI.Settings;
using RollForHonor.Godot.Gameplay.Targeting;
using RollForHonor.Godot.Gameplay.Input;
using RollForHonor.Godot.Gameplay.Combat;
using RollForHonor.Godot.Gameplay.Actors;
using RollForHonor.Godot.CombatDemo;
using System.Collections.Generic;
using RollForHonor.Godot.UI.Localization;

namespace RollForHonor.Godot.UI.Hud;

/// <summary>Coordinates styled HUD, live character/equipment windows and owned pause/settings navigation.</summary>
public partial class ArenaHud : CanvasLayer
{
    private GameBootstrap _runtime;
    private GreyboxArena _arena;
    private EquipmentService _equipment;
    private Control _design;
    private Control _inventoryGroup;
    private InventoryPanel _inventory;
    private CharacterPanel _character;
    private ItemDetailsPanel _details;
    private ItemDetailsPanel _comparison;
    private bool _comparisonDismissed;
    private Panel _pause;
    private ColorRect _pauseShade;
    private SettingsPanel _settings;
    private ActorBars _actorBars;
    private CombatHistoryPanel _history;
    private FloatingTip _textTip;
    private InspectionStack _inspector;
    private ItemDetailsPanel _itemTip;
    private ItemInstance _hoveredItem;
    private Label _hints;
    private Label _enemyName;
    private Label _enemyAmount;
    private readonly Dictionary<RollForHonor.Domain.Combat.State.Models.CombatantId, Node3D> _bodies = new();
    private ResourceOrb _health;
    private ResourceOrb _mana;
    private ArenaMinimap _map;
    private ProgressBar _enemyHealth;
    private Label _enemyState;
    private DiceRollControl _roll;
    private Label _objective;
    private Label _attack;
    private ItemInstance _selected;
    private long _inventoryVersion = -1;
    private bool _ownsPause;
    private bool _previousPause;

    /// <inheritdoc />
    public override void _Ready()
    {
        GameInputBindings.EnsureDefaults();
        ProcessMode = ProcessModeEnum.Always;
        var root = new Control { Name = "Root", Theme = HonorTheme.Create() };
        AddChild(root);
        _design = UiLayout.Design(root, true);
        BuildHud();
        BuildWindows();
        BuildPause();
        BuildSettings();
        BuildReference();
        BuildTooltips();
        _design.Resized += UpdateDockLayout;
        UpdateDockLayout();
    }

    /// <inheritdoc />
    public override void _Process(double delta)
    {
        if (_runtime is null)
        {
            return;
        }

        RefreshHud();
        RefreshWindows();
        RefreshHover();
        RefreshHints();
    }

    /// <inheritdoc />
    public override void _Input(InputEvent @event)
    {
        if (_runtime is null)
        {
            return;
        }

        if (@event.IsAction("rfh_details"))
        {
            RefreshHover();
            return;
        }

        if (@event is not InputEventKey { Pressed: true, Echo: false } key)
        {
            return;
        }

        if (GetViewport().GuiGetFocusOwner() is LineEdit && !key.IsActionPressed("rfh_menu"))
        {
            return;
        }

        if (key.IsActionPressed("rfh_reference") && !_settings.Visible)
        {
            ToggleReference();
        }
        else if (key.IsActionPressed("rfh_inventory") && !_pause.Visible && !_settings.Visible)
        {
            ToggleInventory();
        }
        else if (key.IsActionPressed("rfh_character") && !_pause.Visible && !_settings.Visible)
        {
            ToggleCharacter();
        }
        else if (key.IsActionPressed("rfh_menu"))
        {
            Escape();
        }
        else
        {
            return;
        }

        GetViewport().SetInputAsHandled();
    }

    /// <inheritdoc />
    public override void _UnhandledInput(InputEvent @event)
    {
        if (_runtime is null || GetTree().Paused || !@event.IsActionPressed("rfh_target"))
        {
            return;
        }

        var point = @event is InputEventMouseButton mouse ? mouse.Position : GetViewport().GetMousePosition();
        var camera = _arena.CameraRig.Camera;
        using var query = PhysicsRayQueryParameters3D.Create(camera.ProjectRayOrigin(point), camera.ProjectRayOrigin(point) + camera.ProjectRayNormal(point) * 200, 22);
        query.Exclude = [_arena.Player.GetRid()];
        var hit = _arena.GetWorld3D().DirectSpaceState.IntersectRay(query);
        var body = hit.Count > 0 ? hit["collider"].AsGodotObject() as ICombatBody : null;
        var selection = _arena.Player.GetRequiredComponent<CombatAwareness>().Selection;
        selection.Select(body?.CombatantId == selection.TargetId ? null : body?.CombatantId);
        GetViewport().SetInputAsHandled();
    }

    /// <inheritdoc />
    public override void _ExitTree()
    {
        if (_arena is not null && _history is not null)
        {
            _arena.ContactObserved -= _history.Record;
        }

        if (_ownsPause)
        {
            GetTree().Paused = _previousPause;
        }
    }

    /// <summary>Connects presentation to the already configured scene-local combat and equipment runtime.</summary>
    /// <param name="runtime">The owning session bootstrap.</param>
    /// <param name="arena">The configured world and actors.</param>
    public void Configure(GameBootstrap runtime, GreyboxArena arena)
    {
        _runtime = runtime;
        _arena = arena;
        _equipment = new EquipmentService(runtime.CombatState);
        RefreshParticipants();
        _actorBars.Preferences = new PreferencesStore().Load();
        _map.IsAlive = body => !runtime.CombatState.Read(arena.Player.CombatantId, ((ICombatBody)body).CombatantId).Target.IsDefeated;
        _map.IsSelected = body => arena.Player.GetRequiredComponent<CombatAwareness>().Selection.TargetId == ((ICombatBody)body).CombatantId;
        arena.ContactObserved += _history.Record;
        RefreshHud();
        RefreshHints();
    }

    /// <summary>Refreshes round actors in target selection, overhead bars and the minimap without recreating the HUD.</summary>
    public void RefreshParticipants()
    {
        _bodies.Clear();
        var bodies = new Node3D[] { _arena.Player }
            .Concat(_arena.Enemies)
            .Concat(_arena.GetNode<Node3D>("Targets").GetChildren()
                .OfType<DemoCombatant>());

        foreach (var body in bodies)
        {
            _bodies[((ICombatBody)body).CombatantId] = body;
        }

        _actorBars.Configure(_runtime, _arena, _bodies.Values);
        _map.Configure(_arena.GetNode<Node3D>("Map"), _arena.Player, _arena.Opponent);
        _map.Participants = _bodies.Values
            .Where(body => body != _arena.Player)
            .ToArray();
    }

    /// <summary>Opens or closes the inventory independently without pausing the world.</summary>
    public void ToggleInventory()
    {
        if (_pause.Visible || _settings.Visible || _reference.Visible)
        {
            return;
        }

        var open = !_inventory.Visible;
        _inventory.Visible = open;
        _details.Visible = open && _selected is not null;
        _comparison.Visible = false;
        UpdateOverlay();
    }

    /// <summary>Toggles the live character panel independently of the equipment window.</summary>
    public void ToggleCharacter()
    {
        if (_pause.Visible || _settings.Visible || _reference.Visible)
        {
            return;
        }

        _character.Visible = !_character.Visible;
        UpdateOverlay();
    }

    private void RefreshHud()
    {
        var state = _runtime.CombatState;
        var id = _arena.Player.CombatantId;
        var read = state.Read(id, _arena.Opponent.CombatantId);
        var stats = state.GetStats(id);
        var launch = state.ReadForStart(id, "arena-shot", Guid.NewGuid());
        var mana = launch.Resources.Single().Amount;
        _health.UpdateValue(read.Source.Health, read.Source.MaxHealth, L.Text("HEALTH"));
        _mana.UpdateValue(mana, stats.Get(new StatKey(StatType.MaxMana)), L.Text("MANA"));
        RefreshTarget();
        L.SetText(_objective, read.Source.IsDefeated ? L.Text("You have fallen. Open the menu to begin again.") : read.Target.IsDefeated ? L.Text("The courtyard is yours.") : L.Text("Defeat the Sentinel"));
        _roll.UpdateValue(_history.LastRoll);
        L.SetText(_attack, launch.ReadyAt > _runtime.GameTime ? L.Text("Recovering") : "");
        _character.Refresh(stats, state.GetStatProfile(id).BaseValues, read.Source, mana);
        RefreshRoundUi(!read.Source.IsDefeated);
    }







    private void RefreshWindows()
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

        if (_selected is not null)
        {
            SelectItem(_selected);
        }
    }

    private void Escape()
    {
        if (!_settings.Visible && !_pause.Visible && _inspector.CloseLast())
        {
            return;
        }

        if (_settings.Visible)
        {
            _settings.Close();
        }
        else if (_reference.Visible)
        {
            _reference.Close();
        }
        else if (_inventoryGroup.Visible)
        {
            CloseWindows();
        }
        else
        {
            _pause.Visible = !_pause.Visible;
            UpdatePause();
        }
    }

    private void UpdateOverlay()
    {
        _inventoryGroup.Visible = _inventory.Visible || _character.Visible;

        if (!_inventory.Visible)
        {
            CloseDetails();
        }

        _inventoryVersion = -1;
        UpdateDockLayout();
        UpdatePause();
        RefreshWindows();
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

    private void CloseWindows()
    {
        _inventory.Hide();
        _character.Hide();
        CloseDetails();
        UpdateOverlay();
    }

    private void UpdatePause()
    {
        RefreshHints();
        _pauseShade.Visible = _pause.Visible || _reference.Visible;
        var active = _pause.Visible || _settings.Visible || _reference.Visible;
        _inspector.Visible = _inspector.Count > 0 && !_pause.Visible && !_settings.Visible;

        if (active && !_ownsPause)
        {
            _previousPause = GetTree().Paused;
            _ownsPause = true;
            GetTree().Paused = true;
        }
        else if (!active && _ownsPause)
        {
            GetTree().Paused = _previousPause;
            _ownsPause = false;
        }
    }

    private void CloseInventory()
    {
        _inventory.Hide();
        CloseDetails();
        UpdateOverlay();
    }

    private void CloseDetails()
    {
        _inspector.Clear();
        _hoveredItem = null;
        _selected = null;
        _details.Hide();
        _comparison.Hide();
        _itemTip.Hide();
        _textTip.Hide();
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

    private void DropIntoInventory(string id)
    {
        var read = _runtime.CombatState.ReadEquipment(_arena.Player.CombatantId);
        var occupied = read.Loadout.Slots.FirstOrDefault(pair => pair.Value.Id == id);

        if (occupied.Value is not null)
        {
            ChangeEquipment(occupied.Value, occupied.Key, true);
        }
    }

    private void ChangeEquipment(ItemInstance item, EquipmentSlot slot, bool equipped)
    {
        var owner = _arena.Player.CombatantId;
        var read = _runtime.CombatState.ReadEquipment(owner);

        if (!CanChangeEquipment(read, item, slot, equipped))
        {
            return;
        }

        var result = equipped ? _equipment.UnequipItem(owner, slot) : _equipment.EquipItem(owner, item.Id, slot);
        RefreshWindows();

        if (_selected?.Id != item.Id)
        {
            return;
        }

        _details.SetStatus(result switch
        {
            EquipmentChangeStatus.Applied => equipped ? L.Text("Returned to inventory.") : L.Text("Equipped. Stats updated."),
            EquipmentChangeStatus.Unchanged => L.Text("Already equipped."),
            EquipmentChangeStatus.VersionConflict => L.Text("Your equipment changed. Select the item again."),
            _ => L.Text("This item cannot be equipped in that slot.")
        });
    }

    private bool CanChangeEquipment(EquipmentStateRead read, ItemInstance item, EquipmentSlot slot, bool equipped)
    {
        if (!equipped && !read.Rules.CanEquip(read.Loadout, item, slot))
        {
            _inventory.ShowStatus(L.Text("Requires a compatible main hand or equipment slot."));
            _details.SetStatus(L.Text("Incompatible hand or slot."));
            return false;
        }

        if (!equipped && read.Loadout.Slots.Any(pair => pair.Key != slot && pair.Value.Id == item.Id))
        {
            _inventory.ShowStatus(L.Text("Remove the item from its current slot first."));
            return false;
        }

        var service = new EquipmentLoadoutService(read.Rules);
        var proposed = equipped ? service.Unequip(read.Loadout, slot) : service.Equip(read.Loadout, item, slot);

        if (!_inventory.CanFitLoadout(read, proposed))
        {
            _inventory.ShowStatus(L.Text("Inventory full. Make room before changing equipment."));
            _details.SetStatus(L.Text("Inventory full. Make room before changing equipment."));
            return false;
        }

        return true;
    }
}
