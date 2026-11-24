using RollForHonor.Godot.UI.Admin;
using System.Linq;
using Godot;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Godot.Arena;
using RollForHonor.Godot.UI.Common;
using RollForHonor.Godot.UI.Inventory;
using RollForHonor.Godot.UI.Settings;
using RollForHonor.Godot.Gameplay.Input;

namespace RollForHonor.Godot.UI.Hud;

/// <summary>Coordinates styled HUD, live character/equipment windows and owned pause/settings navigation.</summary>
public partial class ArenaHud : CanvasLayer
{
    private ArenaRoundPresenter _rounds;
    private CombatHudPresenter _combat;
    private HudNavigationController _navigation;
    private InventoryWindowController _items;
    private WorldDropTarget _worldDropTarget;
    private AdminPanel _admin;
    private GameBootstrap _runtime;
    private GreyboxArena _arena;
    private Control _design;
    private Control _inventoryGroup;
    private InventoryPanel _inventory;
    private CharacterPanel _character;
    private ItemDetailsPanel _details;
    private ItemDetailsPanel _comparison;
    private Panel _pause;
    private ColorRect _pauseShade;
    private SettingsPanel _settings;
    private ActorBars _actorBars;
    private ActionJournalPanel _history;
    private FloatingTip _textTip;
    private InspectionStack _inspector;
    private ItemDetailsPanel _itemTip;
    private ItemInstance _hoveredItem;
    private Label _hints;
    private Label _enemyName;
    private Label _enemyAmount;
    private ResourceOrb _health;
    private ResourceOrb _mana;
    private ArenaMinimap _map;
    private ProgressBar _enemyHealth;
    private Label _enemyState;
    private DiceRollControl _roll;
    private Label _objective;
    private Label _attack;

    /// <inheritdoc />
    public override void _Ready()
    {
        GameInputBindings.EnsureDefaults();
        ProcessMode = ProcessModeEnum.Always;
        var root = new Control { Name = "Root", Theme = HonorTheme.Create(), MouseFilter = Control.MouseFilterEnum.Ignore };
        AddChild(root);
        _design = UiLayout.Design(root, true);
        BuildHud();
        BuildWindows();
        BuildPause();
        BuildSettings();
        BuildReference();
        BuildTooltips();
        _navigation = new HudNavigationController(GetTree(), _pause, _pauseShade, _settings,
            _reference, _inspector, ClearItemInspection, CaptureReference, RefreshHints, ReloadPreferences);
        _admin = UiLayout.Place(_design, new AdminPanel { Name = "Admin" }, new Rect2(670, 200, 580, 440));
        _admin.Closed += () => _admin.Hide();
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

        var data = GetViewport().GuiGetDragData();
        _worldDropTarget.Size = _design.Size;
        _worldDropTarget.Visible = GetViewport().GuiIsDragging() && ItemCell.TryReadItem(data, out var dragged) && _items.CanDiscardItem(dragged);
        RefreshHud();
        _items.Refresh();
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

        if (GetViewport().GuiGetFocusOwner() is LineEdit && !key.IsActionPressed("rfh_menu") && key.PhysicalKeycode != Key.F3)
        {
            return;
        }

        if (key.PhysicalKeycode == Key.F3)
        {
            if (_admin.Visible)
            {
                _admin.Hide();
            }
            else
            {
                _admin.Position = new Vector2((_design.Size.X - 580) / 2, 200);
                _admin.Open();
                _admin.MoveToFront();
            }
        }
        else if (key.IsActionPressed("rfh_reference") && !_settings.Visible)
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

        _combat.SelectTarget(@event);
        GetViewport().SetInputAsHandled();
    }

    /// <inheritdoc />
    public override void _ExitTree()
    {
        _combat?.Dispose();
        _navigation?.Dispose();
    }

    /// <summary>Connects presentation to the already configured scene-local combat and equipment runtime.</summary>
    /// <param name="runtime">The owning session bootstrap.</param>
    /// <param name="arena">The configured world and actors.</param>
    public void Configure(GameBootstrap runtime, GreyboxArena arena)
    {
        _runtime = runtime;
        _arena = arena;
        arena.CameraRig.IsZoomBlocked = IsPointerOverInterface;
        _rounds.Configure(runtime, arena);
        _items.Configure(runtime, arena);
        _admin.Configure(runtime, arena);
        _combat?.Dispose();
        _combat = new CombatHudPresenter(runtime, arena, _design, _character, _history);
        RefreshParticipants();
        ReloadPreferences();
        RefreshHud();
        RefreshHints();
    }

    /// <summary>Refreshes round actors in target selection, overhead bars and the minimap without recreating the HUD.</summary>
    public void RefreshParticipants() => _combat.RefreshParticipants();

    /// <summary>Opens or closes the inventory independently without pausing the world.</summary>
    public void ToggleInventory()
    {
        if (_pause.Visible || _settings.Visible || _reference.Visible)
        {
            return;
        }

        _items.ToggleInventory();
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

    private void RefreshHud() => _rounds.Refresh(_combat.Refresh());

    private bool IsPointerOverInterface(Vector2 point) => _design.GetChildren()
        .OfType<Control>()
        .Any(control => IsInteractiveAt(control, point));

    private static bool IsInteractiveAt(Control control, Vector2 point)
    {
        if (!control.IsVisibleInTree())
        {
            return false;
        }

        var local = control.GetGlobalTransformWithCanvas().AffineInverse() * point;

        return control.MouseFilter != Control.MouseFilterEnum.Ignore && new Rect2(Vector2.Zero, control.Size).HasPoint(local)
            || control.GetChildren()
                .OfType<Control>()
                .Any(child => IsInteractiveAt(child, point));
    }

    private void Escape()
    {
        if (_admin.Visible)
        {
            _admin.Hide();
            return;
        }

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
            _items.CloseWindows();
        }
        else
        {
            _navigation.TogglePause();
        }
    }

    private void UpdateOverlay()
    {
        _inventoryGroup.Visible = _inventory.Visible || _character.Visible;
        _items.Invalidate();
        UpdateDockLayout();
        _navigation.Refresh();
        _items.Refresh();
    }

    private void ClearItemInspection()
    {
        _inspector.Clear();
        _hoveredItem = null;
        _itemTip.Hide();
        _textTip.Hide();
    }
}
