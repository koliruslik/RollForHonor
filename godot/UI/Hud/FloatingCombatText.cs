using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Godot.UI.Common;
using RollForHonor.Godot.UI.Localization;

namespace RollForHonor.Godot.UI.Hud;

/// <summary>Projects committed typed damage and misses beside world targets, then floats and fades the labels.</summary>
public partial class FloatingCombatText : Control
{
    /// <summary>Gets or sets the active world camera used to project captured impact positions.</summary>
    public Camera3D Camera { get; set; }

    private const float Lifetime = 1.2f;
    private readonly List<Popup> _active = [];
    private readonly SystemFont _regular = new() { FontNames = ["Segoe UI", "Noto Sans", "DejaVu Sans"], FontWeight = 400 };
    private readonly SystemFont _bold = new() { FontNames = ["Segoe UI", "Noto Sans", "DejaVu Sans"], FontWeight = 700 };
    private int _sequence;

    /// <inheritdoc />
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ProcessMode = ProcessModeEnum.Pausable;
    }

    /// <summary>Displays post-defense amounts grouped by damage type, or a localized gray miss for an evaded attack.</summary>
    /// <param name="position">The captured world position, retained even if the target is subsequently removed.</param>
    /// <param name="result">A target result from an already committed resolution.</param>
    public void ShowResult(Vector3 position, TargetCombatResult result)
    {
        if (result.Outcome == AttackOutcome.Evaded)
        {
            Add(position, L.Text("Miss"), new Color("969791"), false, 0);
            return;
        }

        if (result.DamageResolution is not { } damage)
        {
            return;
        }

        var critical = result.Outcome == AttackOutcome.CriticalHit || damage.BeforeDefenses.Breakdown.Steps
            .Any(step => step.Type == DamageStepType.CriticalCheck && step.DecisionRoll is { } roll
                && (long)roll.Total + (step.FlatBonus ?? 0) >= 20);
        var row = 0;

        foreach (var group in damage.AfterDefenses.Damage.Components
            .GroupBy(component => component.Type))
        {
            var amount = group.Sum(component => component.Amount);

            if (amount <= 0)
            {
                continue;
            }

            var color = CombatGlossary.DamageNumberColor(group.Key);

            if (result.Outcome == AttackOutcome.GlancingHit)
            {
                color.A = 0.65f;
            }

            Add(position, amount.ToString(CultureInfo.InvariantCulture), color, critical, row++);
        }
    }

    /// <inheritdoc />
    public override void _Process(double delta)
    {
        for (var index = _active.Count - 1; index >= 0; index--)
        {
            var popup = _active[index];
            popup.Age += (float)delta;

            if (popup.Age >= Lifetime)
            {
                RemoveChild(popup.Label);
                popup.Label.QueueFree();
                _active.RemoveAt(index);
                continue;
            }

            popup.Label.Visible = GodotObject.IsInstanceValid(Camera) && !Camera.IsPositionBehind(popup.Position);

            if (!popup.Label.Visible)
            {
                continue;
            }

            var screen = GetGlobalTransformWithCanvas().AffineInverse() * Camera.UnprojectPosition(popup.Position);
            popup.Label.Position = screen + popup.Offset - new Vector2(60, popup.Age * 56);
            popup.Label.Modulate = new Color(1, 1, 1, Mathf.Clamp((Lifetime - popup.Age) / 0.5f, 0, 1));
        }
    }

    private void Add(Vector3 position, string text, Color color, bool critical, int row)
    {
        var label = new Label
        {
            Text = text,
            Size = new Vector2(120, 40),
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        label.AddThemeFontOverride("font", critical ? _bold : _regular);
        label.AddThemeFontSizeOverride("font_size", critical ? 28 : 22);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
        label.AddThemeConstantOverride("outline_size", 5);
        AddChild(label);
        _active.Add(new Popup(label, position, new Vector2((_sequence++ % 5 - 2) * 30, -24 - row * 34)));
        _Process(0);
    }

    private sealed class Popup(Label label, Vector3 position, Vector2 offset)
    {
        internal Label Label { get; } = label;
        internal Vector3 Position { get; } = position;
        internal Vector2 Offset { get; } = offset;
        internal float Age { get; set; }
    }
}
