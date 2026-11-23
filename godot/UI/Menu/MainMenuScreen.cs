using Godot;
using RollForHonor.Godot.UI.Settings;
using RollForHonor.Godot.UI.Common;
using RollForHonor.Godot.UI.Reference;

namespace RollForHonor.Godot.UI.Menu;

/// <summary>Connects title-screen navigation to fresh game sessions, settings and application exit.</summary>
public partial class MainMenuScreen : Control
{
    private MainMenuView _menu;
    private SettingsPanel _settings;
    private ReferencePanel _reference;
    private InspectionStack _inspector;

    /// <inheritdoc />
    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        PreferencesStore.Apply(new PreferencesStore().Load());
        _menu = new MainMenuView { Name = "Menu" };
        AddChild(_menu);
        _settings = new SettingsPanel { Name = "Settings" };
        AddChild(_settings);
        _menu.StartRequested += () => GetTree().ChangeSceneToFile("res://ArenaBootstrap.tscn");
        BuildReference();
        _menu.SettingsRequested += () =>
        {
            _inspector.Hide();
            _settings.Open();
        };
        _menu.ReferenceRequested += () => _reference.Open(ReferenceCatalog.Create());
        _menu.ExitRequested += () => GetTree().Quit();
        _settings.Closed += () =>
        {
            _menu.FocusStart();
            _inspector.Visible = _inspector.Count > 0;
        };
    }

    /// <inheritdoc />
    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false, PhysicalKeycode: Key.Escape })
        {
            return;
        }

        if (_settings.Visible)
        {
            _settings.Close();
        }
        else if (!_inspector.CloseLast())
        {
            if (!_reference.Visible)
            {
                return;
            }

            _reference.Close();
        }

        GetViewport().SetInputAsHandled();
    }

    private void BuildReference()
    {
        var root = new Control { Name = "ReferenceRoot", Theme = HonorTheme.Create() };
        AddChild(root);
        var design = UiLayout.Design(root);
        var shade = UiLayout.Place(design, new ColorRect { Name = "Shade", Color = new Color(0.01f, 0.02f, 0.025f, 0.85f), Visible = false }, new Rect2(0, 0, 1920, 1080));
        _reference = UiLayout.Place(design, new ReferencePanel { Name = "Reference" }, new Rect2(460, 90, 1000, 900));
        _reference.VisibilityChanged += () => shade.Visible = _reference.Visible;
        _inspector = new InspectionStack { Name = "Inspections" };
        design.AddChild(_inspector);
        _reference.InspectionRequested += topic => _inspector.Open(topic, design.GetLocalMousePosition() + new Vector2(18, 18));
        _reference.Closed += () =>
        {
            _inspector.Clear();
            _menu.FocusStart();
        };
    }
}
