using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Godot.Gameplay.Items;

namespace RollForHonor.Godot.Arena;

public partial class GreyboxArena
{
    private readonly Queue<(ItemInstance Item, Vector3 Origin)> _pendingDrops = new();
    private TimeSpan _nextDropPlacement;

    private bool CreateDrop(ItemInstance item, Vector3 origin)
    {
        if (TryFindDropPosition(origin, out var position))
        {
            AddDropAt(item, position);
            return true;
        }

        _pendingDrops.Enqueue((item, origin));

        return false;
    }

    private bool TryFindDropPosition(Vector3 origin, out Vector3 position)
    {
        var occupied = Drops
            .Select(drop => drop.Position)
            .Concat(GoldDrops.Select(drop => drop.Position))
            .ToArray();
        using var shape = new SphereShape3D { Radius = 0.4f };

        for (var ring = 0; ring <= 8; ring++)
        {
            for (var z = -ring; z <= ring; z++)
            {
                for (var x = -ring; x <= ring; x++)
                {
                    if (Math.Max(Math.Abs(x), Math.Abs(z)) != ring)
                    {
                        continue;
                    }

                    var candidate = origin + new Vector3(x * 0.9f, 0, z * 0.9f);

                    if (TryDropSurface(candidate, occupied, shape, out position))
                    {
                        return true;
                    }
                }
            }
        }

        position = default;

        return false;
    }

    private bool TryDropSurface(Vector3 candidate, Vector3[] occupied, SphereShape3D shape, out Vector3 position)
    {
        position = default;
        var space = GetWorld3D().DirectSpaceState;
        using var floorQuery = PhysicsRayQueryParameters3D.Create(candidate + Vector3.Up * 1.5f, candidate + Vector3.Down * 2, 16);
        var floor = space.IntersectRay(floorQuery);

        if (floor.Count == 0 || floor["normal"].AsVector3().Y < 0.7f)
        {
            return false;
        }

        var point = floor["position"].AsVector3() + Vector3.Up * 0.04f;

        if (occupied.Any(other => Math.Abs(other.Y - point.Y) < 0.75f
            && new Vector2(other.X - point.X, other.Z - point.Z).LengthSquared() < 0.64f))
        {
            return false;
        }

        using var clearance = new PhysicsShapeQueryParameters3D
        {
            Shape = shape,
            Transform = new Transform3D(Basis.Identity, point + Vector3.Up * 0.4f),
            CollisionMask = 20
        };

        if (space.IntersectShape(clearance, 1).Count > 0)
        {
            return false;
        }

        position = point;

        return true;
    }

    private void AddDropAt(ItemInstance item, Vector3 position)
    {
        var drop = new WorldItemDrop { Name = $"Loot{Guid.NewGuid():N}", Position = position };
        drop.Configure(item);
        AddChild(drop);
        _drops.Add(drop);
    }

    private void PlacePendingDrops()
    {
        if (_pendingDrops.Count == 0 || _runtime.GameTime < _nextDropPlacement)
        {
            return;
        }

        _nextDropPlacement = _runtime.GameTime + TimeSpan.FromSeconds(1);
        var pending = _pendingDrops.Dequeue();
        CreateDrop(pending.Item, pending.Origin);
    }
}
