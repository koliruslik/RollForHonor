using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using RollForHonor.Godot.Arena;
using RollForHonor.Godot.Gameplay.Combat;
using RollForHonor.Godot.Gameplay.Targeting;
using RollForHonor.Godot.UI.Common;
using RollForHonor.Godot.UI.Settings;

namespace RollForHonor.Godot.UI.Hud;

/// <summary>Draws numeric-free overhead resource bars and a simple selected-body frame.</summary>
public partial class ActorBars : Control
{
    /// <summary>Gets or sets the current persisted visibility choices.</summary>
    public PlayerPreferences Preferences { get; set; } = PlayerPreferences.Default;

    private GameBootstrap _runtime;
    private GreyboxArena _arena;
    private Node3D[] _bodies = [];

    /// <inheritdoc />
    public override void _Process(double delta) => QueueRedraw();

    /// <inheritdoc />
    public override void _Draw()
    {
        if (_arena is null)
        {
            return;
        }

        var selected = _arena.Player.GetRequiredComponent<CombatAwareness>().Selection.TargetId;

        foreach (var body in _bodies)
        {
            if (!GodotObject.IsInstanceValid(body) || _arena.CameraRig.Camera.IsPositionBehind(body.GlobalPosition))
            {
                continue;
            }

            var combat = (ICombatBody)body;
            var state = _runtime.CombatState.Read(_arena.Player.CombatantId, combat.CombatantId).Target;
            var point = GetGlobalTransformWithCanvas().AffineInverse() * _arena.CameraRig.Camera.UnprojectPosition(body.GlobalPosition + Vector3.Up * 2.3f);
            var player = body == _arena.Player;

            if (state.IsDefeated)
            {
                continue;
            }

            if (player ? Preferences.PlayerHealthBar : Preferences.EnemyHealthBars)
            {
                Bar(point, state.Health, state.MaxHealth, player ? new Color("7f9c80") : new Color("b46d61"));
            }

            if (player && Preferences.PlayerManaBar)
            {
                var mana = _runtime.CombatState.ReadForStart(combat.CombatantId, "arena-shot", Guid.NewGuid()).Resources.Single().Amount;
                Bar(point + new Vector2(0, 9), mana, _runtime.CombatState.GetStats(combat.CombatantId).Get(new(RollForHonor.Domain.Stats.StatType.MaxMana)), new Color("6c9db7"));
            }

            if (selected == combat.CombatantId)
            {
                var feet = GetGlobalTransformWithCanvas().AffineInverse() * _arena.CameraRig.Camera.UnprojectPosition(body.GlobalPosition);
                DrawRect(new Rect2(point.X - 24, point.Y + 10, 48, Math.Max(16, feet.Y - point.Y - 6)), HonorTheme.Brass, false, 2, true);
            }
        }
    }

    /// <summary>Connects world participants after the arena has registered their stats.</summary>
    /// <param name="runtime">The owning combat session.</param>
    /// <param name="arena">The physical scene and camera.</param>
    /// <param name="bodies">Player, enemies and training targets to project.</param>
    public void Configure(GameBootstrap runtime, GreyboxArena arena, IEnumerable<Node3D> bodies)
    {
        _runtime = runtime;
        _arena = arena;
        _bodies = bodies.ToArray();
        MouseFilter = MouseFilterEnum.Ignore;

        foreach (var label in _bodies
            .SelectMany(body => body.GetChildren()
                .OfType<Label3D>()))
        {
            label.Hide();
        }
    }

    private void Bar(Vector2 point, decimal current, decimal maximum, Color tint)
    {
        DrawRect(new Rect2(point.X - 31, point.Y, 62, 7), HonorTheme.Ink);
        DrawRect(new Rect2(point.X - 30, point.Y + 1, 60 * (maximum > 0 ? Mathf.Clamp((float)(current / maximum), 0, 1) : 0), 5), tint);
    }
}
