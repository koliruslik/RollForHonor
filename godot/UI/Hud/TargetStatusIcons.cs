using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using RollForHonor.Domain.Combat.Effects.Models;
using RollForHonor.Godot.CombatDemo;
using RollForHonor.Godot.Content.Combat;
using RollForHonor.Godot.UI.Common;
using RollForHonor.Godot.UI.Localization;

namespace RollForHonor.Godot.UI.Hud;

/// <summary>Displays clickable, stack-counted icons for the selected target's active production statuses.</summary>
public partial class TargetStatusIcons : HBoxContainer
{
    /// <summary>Raised when a status icon opens captured effect details.</summary>
    public event Action<InspectionTopic> InspectionRequested;

    /// <summary>Gets whether any production status icon is currently visible.</summary>
    public bool HasActiveStatuses => _buttons.Values.Any(button => button.Visible);

    private readonly Dictionary<EffectDefinitionId, Button> _buttons = [];
    private IReadOnlyList<CombatEffectSnapshot> _effects = [];

    /// <inheritdoc />
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        AddThemeConstantOverride("separation", 8);

        foreach (var rule in StatusEffectContent.Rules)
        {
            var button = new Button
            {
                Name = rule.Kind.ToString(),
                CustomMinimumSize = new Vector2(38, 38),
                MouseDefaultCursorShape = CursorShape.PointingHand,
                Visible = false
            };
            AddChild(button);
            var icon = new TextureRect
            {
                Texture = StatusEffectPresentation.Icon(rule),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = MouseFilterEnum.Ignore
            };
            UiLayout.Place(button, icon, new Rect2(3, 3, 32, 32));
            var count = UiLayout.Place(button, UiLayout.Text("", 12), new Rect2(14, 20, 22, 16));
            count.Name = "Count";
            count.HorizontalAlignment = HorizontalAlignment.Right;
            count.MouseFilter = MouseFilterEnum.Ignore;
            count.AddThemeConstantOverride("outline_size", 4);
            count.AddThemeColorOverride("font_outline_color", Colors.Black);
            button.Pressed += () => InspectionRequested?.Invoke(StatusEffectPresentation.ActiveTopic(rule, _effects));
            _buttons.Add(rule.Id, button);
        }
    }

    /// <summary>Updates icon visibility and stack counts from the current canonical target snapshot.</summary>
    /// <param name="effects">The selected target's current effects, or an empty list when selection clears.</param>
    public void Refresh(IReadOnlyList<CombatEffectSnapshot> effects)
    {
        _effects = effects;

        foreach (var rule in StatusEffectContent.Rules)
        {
            var count = effects.Count(effect => effect.Definition.Id == rule.Id);
            var button = _buttons[rule.Id];
            button.Visible = count > 0;
            button.GetNode<Label>("Count").Text = count > 1 ? count.ToString() : "";
            button.TooltipText = L.Format($"{DemoEffectCatalog.Name(rule.Id)} ×{count} — Details");
        }
    }
}
