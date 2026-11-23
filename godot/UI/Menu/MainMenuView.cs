using System;
using Godot;
using RollForHonor.Godot.UI.Common;
using RollForHonor.Godot.UI.Localization;

namespace RollForHonor.Godot.UI.Menu;

/// <summary>Draws the styled title screen and exposes navigation intent without changing scenes itself.</summary>
public partial class MainMenuView : Control
{
    /// <summary>Raised when the player starts a fresh arena session.</summary>
    public event Action StartRequested;

    /// <summary>Raised when presentation settings are requested.</summary>
    public event Action SettingsRequested;

    /// <summary>Raised when the shared game reference is requested.</summary>
    public event Action ReferenceRequested;

    /// <summary>Raised when the player requests application exit.</summary>
    public event Action ExitRequested;

    /// <inheritdoc />
    public override void _Ready()
    {
        Theme = HonorTheme.Create();
        var design = UiLayout.Design(this);
        var backdrop = new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://UI/Assets/menu-backdrop.svg"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore
        };
        UiLayout.Place(design, backdrop, new Rect2(0, 0, 1920, 1080));
        BuildIdentity(design);
        BuildNavigation(design);
        BuildFooter(design);
    }

    /// <summary>Restores keyboard focus after a child overlay closes.</summary>
    public void FocusStart() => GetNode<Button>("Design/Start").GrabFocus();

    private static void BuildIdentity(Control design)
    {
        UiLayout.Place(design, UiLayout.Text("ROLL FOR HONOR", 16, HonorTheme.Muted), new Rect2(128, 96, 560, 32));

        UiLayout.Divider(design, new Rect2(128, 160, 496, 1));
        UiLayout.Place(design, UiLayout.Text("ROLL", 88, heading: true), new Rect2(122, 264, 650, 112));
        UiLayout.Place(design, UiLayout.Text("FOR HONOR", 64, heading: true), new Rect2(126, 376, 800, 96));
    }

    private void BuildNavigation(Control design)
    {
        var start = UiLayout.Button(L.Text("Start game"), () => StartRequested?.Invoke(), true);
        start.Name = "Start";
        UiLayout.Place(design, start, new Rect2(128, 576, 496, 64));
        var settings = UiLayout.Button(L.Text("Settings"), () => SettingsRequested?.Invoke());
        settings.Name = "Settings";
        UiLayout.Place(design, settings, new Rect2(128, 656, 496, 64));
        var reference = UiLayout.Button(L.Text("Reference"), () => ReferenceRequested?.Invoke());
        reference.Name = "Reference";
        UiLayout.Place(design, reference, new Rect2(128, 736, 496, 64));
        var exit = UiLayout.Button(L.Text("Exit"), () => ExitRequested?.Invoke());
        exit.Name = "Exit";
        UiLayout.Place(design, exit, new Rect2(128, 816, 496, 64));
        start.GrabFocus();
    }

    private static void BuildFooter(Control design)
    {
        UiLayout.Divider(design, new Rect2(128, 944, 496, 1));
        UiLayout.Place(design, UiLayout.Text(L.Text("THE ASHEN COURT"), 16, HonorTheme.Muted), new Rect2(128, 960, 496, 32));
    }
}
