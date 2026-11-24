using Godot;
using RollForHonor.Godot.UI.Common;

namespace RollForHonor.Godot.UI.Hud;

/// <summary>A noninteractive text tooltip following the pointer within the design surface.</summary>
public partial class FloatingTip : PanelContainer
{
    private Label _text;

    /// <inheritdoc />
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        AddThemeStyleboxOverride("panel", HonorTheme.Panel(new Color(HonorTheme.Ink, 0.86f), HonorTheme.Brass, 14));
        _text = UiLayout.Text("", 16);
        _text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _text.CustomMinimumSize = new Vector2(360, 0);
        AddChild(_text);
        Hide();
    }

    /// <inheritdoc />
    public override void _Process(double delta)
    {
        if (Visible)
        {
            var parent = GetParent<Control>();
            var cursor = parent.GetLocalMousePosition();
            Position = TooltipPlacement.Place(parent.Size, Size, cursor, TooltipPlacement.SourceBounds(parent, cursor));
        }
    }

    /// <summary>Shows the supplied text and lets the native container fit its content.</summary>
    /// <param name="text">The player-facing tooltip text.</param>
    public void ShowText(string text)
    {
        _text.Text = text;
        ResetSize();
        Show();
    }
}
