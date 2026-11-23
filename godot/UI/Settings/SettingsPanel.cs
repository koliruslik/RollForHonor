using System;
using Godot;
using RollForHonor.Godot.UI.Common;
using RollForHonor.Godot.UI.Localization;

namespace RollForHonor.Godot.UI.Settings;

/// <summary>Provides supported, persistent settings with explicit Apply and discard-on-close behavior.</summary>
public partial class SettingsPanel : Control
{
    /// <summary>Raised when the settings overlay is dismissed.</summary>
    public event Action Closed;

    /// <summary>Gets or sets the isolated preferences path; configure before opening the panel.</summary>
    public string PreferencesPath { get; set; } = "user://presentation.cfg";

    private OptionButton _windowMode;
    private CheckButton _vSync;
    private HSlider _volume;
    private Label _volumeValue;
    private Label _message;
    private CheckButton _enemyBars;
    private CheckButton _playerHealth;
    private CheckButton _playerMana;
    private OptionButton _language;

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
        L.OnChanged(this, RefreshOptions);
        Visible = false;
    }

    /// <summary>Reloads saved values and displays the overlay without changing the runtime.</summary>
    public void Open()
    {
        SetValues(new PreferencesStore(PreferencesPath).Load());
        _language.Select(TranslationServer.GetLocale().StartsWith("uk", StringComparison.Ordinal) ? 0 : 1);
        L.SetText(_message, "");
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
        UiLayout.Place(panel, UiLayout.Text(L.Text("Settings"), 28, heading: true), new Rect2(40, 32, 670, 56));
        UiLayout.Place(panel, UiLayout.CloseButton(Close), new Rect2(774, 34, 40, 40));
        UiLayout.Divider(panel, new Rect2(42, 145, 776, 1));
    }

    private void BuildOptions(Control panel)
    {
        UiLayout.Place(panel, UiLayout.Text(L.Text("Window mode")), new Rect2(44, 185, 340, 50));
        _windowMode = UiLayout.Place(panel, new OptionButton { Name = "WindowMode" }, new Rect2(425, 185, 390, 50));
        _windowMode.AddItem(L.Text("Windowed"));
        _windowMode.AddItem(L.Text("Fullscreen"));
        UiLayout.Place(panel, UiLayout.Text(L.Text("Vertical synchronization")), new Rect2(44, 267, 380, 50));
        _vSync = UiLayout.Place(panel, new CheckButton { Name = "VSync", Text = L.Text("Enabled") }, new Rect2(425, 267, 390, 50));
        L.SetText(_vSync, L.Text("Enabled"));
        UiLayout.Place(panel, UiLayout.Text(L.Text("Master volume")), new Rect2(44, 349, 340, 50));
        _volume = UiLayout.Place(panel, new HSlider { Name = "Volume", MinValue = 0, MaxValue = 100, Step = 1 }, new Rect2(425, 357, 310, 34));
        _volumeValue = UiLayout.Place(panel, UiLayout.Text("80%", 20, HonorTheme.Brass), new Rect2(750, 348, 70, 50));
        _volume.ValueChanged += value => _volumeValue.Text = $"{value:0}%";
        _enemyBars = BarToggle(panel, L.Text("Enemy health bars"), 430);
        _playerHealth = BarToggle(panel, L.Text("Player health bar"), 492);
        _playerMana = BarToggle(panel, L.Text("Player mana bar"), 554);
        UiLayout.Place(panel, UiLayout.Text(L.Text("Language")), new Rect2(44, 614, 340, 44));
        _language = UiLayout.Place(panel, new OptionButton { Name = "Language" }, new Rect2(425, 614, 390, 44));
        _language.AddItem("Українська");
        _language.AddItem("English");
    }

    private void BuildActions(Control panel)
    {
        _message = UiLayout.Place(panel, UiLayout.Text("", 18, HonorTheme.Secondary), new Rect2(44, 660, 770, 32));
        UiLayout.Divider(panel, new Rect2(42, 698, 776, 2));
        UiLayout.Place(panel, UiLayout.Button(L.Text("Reset defaults"), () => SetValues(PlayerPreferences.Default)), new Rect2(44, 746, 238, 56));
        UiLayout.Place(panel, UiLayout.Button(L.Text("Back"), Close), new Rect2(425, 746, 160, 56));
        var apply = UiLayout.Button(L.Text("Apply settings"), Save, true);
        apply.Name = "Apply";
        UiLayout.Place(panel, apply, new Rect2(605, 746, 210, 56));
    }

    private static CheckButton BarToggle(Control panel, string title, int y)
    {
        UiLayout.Place(panel, UiLayout.Text(title), new Rect2(44, y, 380, 44));

        var toggle = UiLayout.Place(panel, new CheckButton(), new Rect2(425, y, 390, 44));
        L.SetText(toggle, L.Text("Show"));

        return toggle;
    }

    private void SetValues(PlayerPreferences preferences)
    {
        _windowMode.Select(preferences.Fullscreen ? 1 : 0);
        _vSync.ButtonPressed = preferences.VSync;
        _volume.Value = preferences.MasterVolume * 100;
        _enemyBars.ButtonPressed = preferences.EnemyHealthBars;
        _playerHealth.ButtonPressed = preferences.PlayerHealthBar;
        _playerMana.ButtonPressed = preferences.PlayerManaBar;
        _language.Select(preferences.Language == "uk" ? 0 : 1);
    }

    private void Save()
    {
        var values = new PlayerPreferences(_windowMode.Selected == 1, _vSync.ButtonPressed, (float)_volume.Value / 100, _enemyBars.ButtonPressed, _playerHealth.ButtonPressed, _playerMana.ButtonPressed,
            _language.Selected == 0 ? "uk" : "en");
        L.SetText(_message, new PreferencesStore(PreferencesPath).SaveAndApply(values) == Error.Ok ? L.Text("Settings saved.") : L.Text("Settings could not be saved. Please try again."));
    }

    private void RefreshOptions()
    {
        _windowMode.SetItemText(0, L.Text("Windowed"));
        _windowMode.SetItemText(1, L.Text("Fullscreen"));
    }
}
