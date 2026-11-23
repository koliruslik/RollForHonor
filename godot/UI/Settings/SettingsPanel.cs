using System;
using Godot;
using RollForHonor.Godot.UI.Common;

namespace RollForHonor.Godot.UI.Settings;

/// <summary>Provides supported, persistent settings with explicit Apply and discard-on-close behavior.</summary>
public partial class SettingsPanel : Control
{
    /// <summary>Raised when the settings overlay is dismissed.</summary>
    public event Action Closed;

    private readonly PreferencesStore _store = new();
    private OptionButton _windowMode;
    private CheckButton _vSync;
    private HSlider _volume;
    private Label _volumeValue;
    private Label _message;
    private CheckButton _enemyBars;
    private CheckButton _playerHealth;
    private CheckButton _playerMana;

    /// <inheritdoc />
    public override void _Ready()
    {
        Theme = HonorTheme.Create();
        var design = UiLayout.Design(this);
        UiLayout.Place(design, new ColorRect { Color = new Color(0.01f, 0.02f, 0.025f, 0.9f) }, new Rect2(0, 0, 1920, 1080));
        var panel = UiLayout.Panel(design, "Settings", new Rect2(530, 110, 860, 860));
        BuildHeader(panel);
        BuildOptions(panel);
        BuildActions(panel);
        Visible = false;
    }

    /// <summary>Reloads saved values and displays the overlay without changing the runtime.</summary>
    public void Open()
    {
        SetValues(_store.Load());
        _message.Text = "";
        Show();
        _windowMode.GrabFocus();
    }

    /// <summary>Discards unapplied form edits and notifies the owner.</summary>
    public void Close()
    {
        Hide();
        Closed?.Invoke();
    }

    private void BuildHeader(Control panel)
    {
        UiLayout.Place(panel, UiLayout.Text("Settings", 28, heading: true), new Rect2(40, 32, 670, 56));
        UiLayout.Place(panel, UiLayout.CloseButton(Close), new Rect2(774, 34, 40, 40));
        UiLayout.Divider(panel, new Rect2(42, 145, 776, 1));
    }

    private void BuildOptions(Control panel)
    {
        UiLayout.Place(panel, UiLayout.Text("Window mode"), new Rect2(44, 185, 340, 50));
        _windowMode = UiLayout.Place(panel, new OptionButton { Name = "WindowMode" }, new Rect2(425, 185, 390, 50));
        _windowMode.AddItem("Windowed");
        _windowMode.AddItem("Fullscreen");
        UiLayout.Place(panel, UiLayout.Text("Vertical synchronization"), new Rect2(44, 267, 380, 50));
        _vSync = UiLayout.Place(panel, new CheckButton { Name = "VSync", Text = "Enabled" }, new Rect2(425, 267, 390, 50));
        UiLayout.Place(panel, UiLayout.Text("Master volume"), new Rect2(44, 349, 340, 50));
        _volume = UiLayout.Place(panel, new HSlider { Name = "Volume", MinValue = 0, MaxValue = 100, Step = 1 }, new Rect2(425, 357, 310, 34));
        _volumeValue = UiLayout.Place(panel, UiLayout.Text("80%", 20, HonorTheme.Brass), new Rect2(750, 348, 70, 50));
        _volume.ValueChanged += value => _volumeValue.Text = $"{value:0}%";
        _enemyBars = BarToggle(panel, "Enemy health bars", 430);
        _playerHealth = BarToggle(panel, "Player health bar", 492);
        _playerMana = BarToggle(panel, "Player mana bar", 554);
    }

    private void BuildActions(Control panel)
    {
        _message = UiLayout.Place(panel, UiLayout.Text("", 18, HonorTheme.Secondary), new Rect2(44, 628, 770, 40));
        UiLayout.Divider(panel, new Rect2(42, 698, 776, 2));
        UiLayout.Place(panel, UiLayout.Button("Reset defaults", () => SetValues(PlayerPreferences.Default)), new Rect2(44, 746, 238, 56));
        UiLayout.Place(panel, UiLayout.Button("Back", Close), new Rect2(425, 746, 160, 56));
        var apply = UiLayout.Button("Apply settings", Save, true);
        apply.Name = "Apply";
        UiLayout.Place(panel, apply, new Rect2(605, 746, 210, 56));
    }

    private static CheckButton BarToggle(Control panel, string title, int y)
    {
        UiLayout.Place(panel, UiLayout.Text(title), new Rect2(44, y, 380, 44));

        return UiLayout.Place(panel, new CheckButton { Text = "Show" }, new Rect2(425, y, 390, 44));
    }

    private void SetValues(PlayerPreferences preferences)
    {
        _windowMode.Select(preferences.Fullscreen ? 1 : 0);
        _vSync.ButtonPressed = preferences.VSync;
        _volume.Value = preferences.MasterVolume * 100;
        _enemyBars.ButtonPressed = preferences.EnemyHealthBars;
        _playerHealth.ButtonPressed = preferences.PlayerHealthBar;
        _playerMana.ButtonPressed = preferences.PlayerManaBar;
    }

    private void Save()
    {
        var values = new PlayerPreferences(_windowMode.Selected == 1, _vSync.ButtonPressed, (float)_volume.Value / 100, _enemyBars.ButtonPressed, _playerHealth.ButtonPressed, _playerMana.ButtonPressed);
        _message.Text = _store.SaveAndApply(values) == Error.Ok ? "Settings saved." : "Settings could not be saved. Please try again.";
    }
}
