using Godot;
using RollForHonor.Godot.UI.Common;

namespace RollForHonor.Godot.UI.Hud;

/// <summary>Renders a round, framed resource reservoir from current and maximum values.</summary>
public partial class ResourceOrb : Control
{
    /// <summary>Gets or sets the reservoir color.</summary>
    public Color Tint { get; set; } = new("a14c4a");

    private float _ratio = 1;
    private Label _amount;
    private Label _caption;

    /// <inheritdoc />
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _amount = UiLayout.Place(this, UiLayout.Text("", 24), new Rect2(0, 50, 140, 40));
        _amount.HorizontalAlignment = HorizontalAlignment.Center;
        _caption = UiLayout.Place(this, UiLayout.Text("", 10, new Color(HonorTheme.Muted, 0.65f)), new Rect2(0, 94, 140, 22));
        _caption.HorizontalAlignment = HorizontalAlignment.Center;
    }

    /// <inheritdoc />
    public override void _Draw()
    {
        var center = new Vector2(70, 70);
        DrawCircle(center, 67, new Color("0a1115"));
        DrawArc(center, 64, 0, Mathf.Tau, 96, HonorTheme.Border, 1, true);
        DrawArc(center, 58, -Mathf.Pi / 2, Mathf.Tau - Mathf.Pi / 2, 96, new Color("283131"), 8, true);

        if (_ratio > 0)
        {
            DrawArc(center, 58, -Mathf.Pi / 2, _ratio * Mathf.Tau - Mathf.Pi / 2, 96, Tint, 8, true);
        }

        DrawCircle(center, 48, HonorTheme.Ink);
    }

    /// <summary>Updates the ring and its textual quantity.</summary>
    /// <param name="current">The actual current balance.</param>
    /// <param name="maximum">The resolved maximum.</param>
    /// <param name="caption">The resource name.</param>
    public void UpdateValue(decimal current, decimal maximum, string caption)
    {
        _ratio = maximum > 0 ? Mathf.Clamp((float)(current / maximum), 0, 1) : 0;
        _amount.Text = $"{current:0}/{maximum:0}";
        _caption.Text = caption;
        QueueRedraw();
    }
}
