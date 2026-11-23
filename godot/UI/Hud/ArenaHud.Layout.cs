using Godot;
using RollForHonor.Godot.UI.Common;
using RollForHonor.Godot.UI.Inventory;
using RollForHonor.Godot.UI.Settings;
using RollForHonor.Godot.UI.Localization;

namespace RollForHonor.Godot.UI.Hud;

public partial class ArenaHud
{
    private void BuildHud()
    {
        UiLayout.Place(_design, UiLayout.Text("ROLL FOR HONOR", 20, HonorTheme.Brass, true), new Rect2(32, 24, 330, 40));

        BuildEnemyBar();
        BuildMap();
        BuildActionBar();
        _actorBars = UiLayout.Place(_design, new ActorBars { Name = "ActorBars" }, new Rect2(0, 0, 1920, 1080));
        _history = UiLayout.Place(_design, new CombatHistoryPanel { Name = "History" }, new Rect2(24, 680, 400, 230));
        _history.InspectionRequested += OpenInspection;
    }

    private void BuildWindows()
    {
        _inventoryGroup = UiLayout.Place(_design, new Control { Name = "InventoryOverlay", Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore }, new Rect2(0, 0, 1920, 1080));
        _character = UiLayout.Place(_inventoryGroup, new CharacterPanel { Name = "Character" }, new Rect2(0, 0, 360, 1080));
        _inventory = UiLayout.Place(_inventoryGroup, new InventoryPanel { Name = "Inventory" }, new Rect2(1360, 0, 560, 1080));
        _comparison = UiLayout.Place(_inventoryGroup, new ItemDetailsPanel { Name = "Comparison" }, new Rect2(696, 16, 320, 620));
        _details = UiLayout.Place(_inventoryGroup, new ItemDetailsPanel { Name = "ItemDetails" }, new Rect2(1028, 16, 320, 620));
        _inventory.ItemSelected += SelectItem;
        _inventory.ItemHovered += HoverItem;
        _inventory.ItemActivated += ActivateItem;
        _inventory.EquipRequested += DropIntoSlot;
        _inventory.UnequipRequested += DropIntoInventory;
        _inventory.Closed += CloseInventory;
        _character.Closed += CloseCharacter;
        _details.Closed += CloseDetails;
        _details.AffixHovered += ShowAffixTip;
        _comparison.AffixHovered += ShowAffixTip;
        _details.InspectionRequested += OpenInspection;
        _comparison.InspectionRequested += OpenInspection;
        _character.InspectionRequested += OpenInspection;
        _comparison.Closed += () =>
        {
            _comparisonDismissed = true;
            _comparison.Hide();
        };
    }

    private void BuildPause()
    {
        _pauseShade = UiLayout.Place(_design, new ColorRect { Color = new Color(0.01f, 0.02f, 0.025f, 0.8f), Visible = false }, new Rect2(0, 0, 1920, 1080));
        _pause = UiLayout.Panel(_design, "Pause", new Rect2(650, 238, 620, 604));
        var title = UiLayout.Place(_pause, UiLayout.Text(L.Text("SESSION"), 22, HonorTheme.Brass, true), new Rect2(54, 32, 512, 48));
        title.HorizontalAlignment = HorizontalAlignment.Center;
        UiLayout.Divider(_pause, new Rect2(54, 112, 512, 2));
        UiLayout.Place(_pause, UiLayout.Button(L.Text("Resume"), Resume, true), new Rect2(54, 160, 512, 60));
        UiLayout.Place(_pause, UiLayout.Button(L.Text("Restart"), () => Navigate("res://ArenaBootstrap.tscn")), new Rect2(54, 244, 512, 60));
        UiLayout.Place(_pause, UiLayout.Button(L.Text("Reference"), ToggleReference), new Rect2(54, 328, 512, 60));
        UiLayout.Place(_pause, UiLayout.Button(L.Text("Settings"), OpenSettings), new Rect2(54, 412, 512, 60));
        UiLayout.Place(_pause, UiLayout.Button(L.Text("Main menu"), () => Navigate("res://Bootstrap.tscn")), new Rect2(54, 496, 512, 60));
        _pause.Hide();
    }

    private void BuildSettings()
    {
        _settings = new SettingsPanel { Name = "Settings" };
        _design.AddChild(_settings);
        _settings.Closed += ReturnToPause;
    }

    private void BuildEnemyBar()
    {
        var panel = UiLayout.Panel(_design, "Enemy", new Rect2(670, 25, 580, 91));
        _enemyName = UiLayout.Place(panel, UiLayout.Text("", 24, heading: true), new Rect2(23, 10, 350, 38));
        _enemyState = UiLayout.Place(panel, UiLayout.Text(L.Text("COURTYARD GUARD"), 13, HonorTheme.Muted), new Rect2(355, 12, 210, 34));
        _enemyAmount = UiLayout.Place(panel, UiLayout.Text("", 16), new Rect2(23, 39, 300, 20));
        _enemyHealth = UiLayout.Place(panel, new ProgressBar { ShowPercentage = false }, new Rect2(23, 60, 534, 8));
        _enemyHealth.AddThemeStyleboxOverride("background", HonorTheme.Panel(new Color("29322e"), HonorTheme.Border, 0));
        _enemyHealth.AddThemeStyleboxOverride("fill", HonorTheme.Panel(new Color("a85e52"), new Color("a85e52"), 0));
    }

    private void BuildMap()
    {
        _map = UiLayout.Place(_design, new ArenaMinimap { ClipContents = true, MouseFilter = Control.MouseFilterEnum.Ignore }, new Rect2(1651, 28, 232, 200));
        var north = UiLayout.Place(_design, UiLayout.Text("N", 14, HonorTheme.Brass), new Rect2(1758, 8, 30, 20));
        north.Name = "North";
        var objective = UiLayout.Panel(_design, "Objective", new Rect2(1550, 248, 333, 110));
        objective.MouseFilter = Control.MouseFilterEnum.Ignore;
        objective.AddThemeStyleboxOverride("panel", HonorTheme.Panel(new Color(HonorTheme.Ink, 0.65f), new Color(HonorTheme.Border, 0.65f)));
        UiLayout.Place(objective, UiLayout.Text(L.Text("THE PROVING GROUNDS"), 15, HonorTheme.Brass), new Rect2(18, 14, 300, 29));
        _objective = UiLayout.Place(objective, UiLayout.Text(L.Text("Defeat the Sentinel"), 18), new Rect2(18, 49, 296, 50));
        _objective.AutowrapMode = TextServer.AutowrapMode.WordSmart;
    }

    private void BuildActionBar()
    {
        var bar = UiLayout.Panel(_design, "ActionBar", new Rect2(560, 951, 800, 106));
        _health = UiLayout.Place(_design, new ResourceOrb { Tint = new Color("c6655d") }, new Rect2(576, 934, 140, 140));
        _mana = UiLayout.Place(_design, new ResourceOrb { Tint = new Color("6c9db7") }, new Rect2(1204, 934, 140, 140));
        _roll = UiLayout.Place(bar, new DiceRollControl(), new Rect2(168, 4, 96, 102));
        _roll.Name = "RollValue";
        _roll.Requested += () => OpenInspection(_history.CreateDiceHistoryTopic());
        _attack = UiLayout.Place(bar, UiLayout.Text("", 12, HonorTheme.Muted), new Rect2(280, 82, 286, 18));
        UiLayout.Place(_design, UiLayout.Button(L.Text("Inventory"), ToggleInventory), new Rect2(16, 974, 193, 51));
        var reference = UiLayout.Place(_design, UiLayout.Button(L.Text("Reference"), ToggleReference), new Rect2(225, 974, 193, 51));
        reference.Name = "ReferenceButton";
        BuildAbilities(bar);
        UiLayout.Place(_design, UiLayout.Button(L.Text("Character"), ToggleCharacter), new Rect2(16, 914, 193, 51));
        var menu = UiLayout.Place(_design, UiLayout.Button(L.Text("Menu"), Escape), new Rect2(1693, 974, 190, 51));
        menu.Name = "MenuButton";
        var hints = UiLayout.Panel(_design, "Hints", new Rect2(1637, 766, 250, 184));
        hints.MouseFilter = Control.MouseFilterEnum.Ignore;
        hints.AddThemeStyleboxOverride("panel", HonorTheme.Panel(new Color(HonorTheme.Ink, 0.25f), new Color(HonorTheme.Border, 0.15f)));
        _hints = UiLayout.Place(hints, UiLayout.Text("", 12, new Color(HonorTheme.Secondary, 0.55f)), new Rect2(14, 14, 222, 156));
        _hints.VerticalAlignment = VerticalAlignment.Bottom;
    }

    private void BuildAbilities(Control bar)
    {
        for (var index = 0; index < 4; index++)
        {
            var slot = index;
            var button = UiLayout.Button("", () =>
            {
                if (slot == 0)
                {
                    LaunchPrimary();
                }
            });
            var tooltip = index == 0 ? "Basic attack" : "Locked skill slot";
            button.TooltipText = L.Text(tooltip);
            L.OnChanged(button, () => button.TooltipText = L.Text(tooltip));
            button.Disabled = index > 0;
            UiLayout.Place(bar, button, new Rect2(280 + index * 72, 16, 64, 64));
            UiLayout.Place(button, UiLayout.Icon(index == 0 ? "weapon" : "lock"), new Rect2(12, 12, 40, 40));
        }
    }

    private void BuildTooltips()
    {
        _textTip = new FloatingTip { Name = "TextTooltip" };
        _design.AddChild(_textTip);
        _itemTip = UiLayout.Place(_design, new ItemDetailsPanel { Name = "ItemTooltip" }, new Rect2(0, 0, 320, 620));
        _itemTip.UseAsHover();
        _itemTip.InspectionRequested += OpenInspection;
        _inspector = new InspectionStack { Name = "Inspections" };
        _design.AddChild(_inspector);
    }

    private void OpenInspection(InspectionTopic topic)
    {
        _textTip.Hide();
        _inspector.Open(topic, _design.GetLocalMousePosition() + new Vector2(18, 18));
    }

    private void ShowAffixTip(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            _textTip.Hide();
        }
        else
        {
            _textTip.ShowText(text);
        }
    }

    private void UpdateDockLayout()
    {
        var width = _design.Size.X;
        _inventoryGroup.Size = _design.Size;
        _inventory.Position = new Vector2(width - 560, 0);
        _details.Position = new Vector2(width - 892, 16);
        _comparison.Position = new Vector2(width - 1224, 16);
        _pause.Position = new Vector2((width - 620) / 2, 238);
        _reference.Position = new Vector2((width - 1000) / 2, 90);
        _pauseShade.Size = _design.Size;
        var center = width / 2;
        _design.GetNode<Panel>("Enemy").Position = new Vector2(center - 290, 25);
        _design.GetNode<Panel>("ActionBar").Position = new Vector2(center - 400, 951);
        _health.Position = new Vector2(center - 400 + 16, 934);
        _mana.Position = new Vector2(center + 400 - 156, 934);
        _actorBars.Size = _design.Size;
        _design.GetNode<Panel>("Hints").Position = new Vector2(width - 283, 766);
        _map.Position = new Vector2(width - 269, 28);
        _design.GetNode<Label>("North").Position = new Vector2(width - 162, 8);
        _design.GetNode<Button>("MenuButton").Position = new Vector2(width - 227, 974);
        _design.GetNode<Panel>("Objective").Position = new Vector2(width - 370, 248);
    }

    private void CloseCharacter()
    {
        _inspector.Clear();
        _character.Hide();
        UpdateOverlay();
    }

    private void Resume()
    {
        _pause.Hide();
        UpdatePause();
    }

    private void OpenSettings()
    {
        _pause.Hide();
        _settings.Open();
        UpdatePause();
    }

    private void Navigate(string path)
    {
        GetTree().Paused = false;
        _ownsPause = false;
        GetTree().ChangeSceneToFile(path);
    }

    private void ReturnToPause()
    {
        _pause.Show();
        _actorBars.Preferences = new PreferencesStore().Load();
        UpdatePause();
    }
}
