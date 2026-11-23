using System.Linq;
using System;
using Godot;
using RollForHonor.Godot.UI.Common;

namespace RollForHonor.Godot.UI.Hud;

/// <summary>Draws actual arena floor footprints and actor positions in a compact north-up map.</summary>
public partial class ArenaMinimap : Control
{
    /// <summary>Gets or sets all nonplayer combat bodies shown on the map.</summary>
    public Node3D[] Participants { get; set; } = [];

    /// <summary>Gets or sets the current authoritative alive-state query.</summary>
    public Func<Node3D, bool> IsAlive { get; set; } = _ => true;

    /// <summary>Gets or sets the selected-target query.</summary>
    public Func<Node3D, bool> IsSelected { get; set; } = _ => false;

    private Node3D _player;
    private Node3D _opponent;
    private StaticBody3D[] _surfaces = [];

    /// <inheritdoc />
    public override void _Process(double delta) => QueueRedraw();

    /// <inheritdoc />
    public override void _Draw()
    {
        DrawRect(new Rect2(0, 0, 232, 200), new Color("0d171b"));
        DrawRect(new Rect2(0, 0, 232, 200), HonorTheme.Border, false);

        foreach (var surface in _surfaces)
        {
            if (surface.GetNodeOrNull<MeshInstance3D>("Mesh")?.Mesh is not BoxMesh box)
            {
                continue;
            }

            var center = Project(surface.GlobalPosition);
            var size = new Vector2(box.Size.X, box.Size.Z) * 2.4f;
            DrawRect(new Rect2(center - size / 2, size), new Color("3a4945"), false, 1);
        }

        if (GodotObject.IsInstanceValid(_player))
        {
            DrawCircle(Project(_player.GlobalPosition), 4, HonorTheme.Brass);
            foreach (var body in Participants)
            {
                var point = Project(body.GlobalPosition);

                if (IsAlive(body))
                {
                    DrawCircle(point, body == _opponent ? 3 : 2.5f, new Color("be6861"));
                }
                else
                {
                    DrawLine(point - new Vector2(3, 3), point + new Vector2(3, 3), HonorTheme.Muted, 1.5f, true);
                    DrawLine(point + new Vector2(-3, 3), point + new Vector2(3, -3), HonorTheme.Muted, 1.5f, true);
                }

                if (IsSelected(body))
                {
                    DrawArc(point, 6, 0, Mathf.Tau, 24, HonorTheme.Brass, 1.5f, true);
                }
            }
        }
    }

    /// <summary>Supplies the map and actors whose real positions should be displayed.</summary>
    /// <param name="map">The scene's authored map root.</param>
    /// <param name="player">The current player actor.</param>
    /// <param name="opponent">The active opponent.</param>
    public void Configure(Node3D map, Node3D player, Node3D opponent)
    {
        _player = player;
        _opponent = opponent;
        _surfaces = map.GetChildren()
            .OfType<StaticBody3D>()
            .Where(body => body.Name != "Base" && body.GetNodeOrNull<MeshInstance3D>("Mesh")?.Mesh is BoxMesh box && box.Size.Y < 5)
            .ToArray();
    }

    private static Vector2 Project(Vector3 point) => new(116 + point.X * 2.4f, 87 + point.Z * 2.4f);
}
