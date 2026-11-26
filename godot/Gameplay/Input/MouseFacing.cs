using Godot;
using RollForHonor.Godot.Gameplay.Actors;
using RollForHonor.Godot.Gameplay.Combat;

namespace RollForHonor.Godot.Gameplay.Input;

/// <summary>Prioritizes combatants beneath the cursor through camera occluders and rotates only the actor visual yaw.</summary>
public partial class MouseFacing : EntityComponent
{
    /// <inheritdoc />
    public override ActorPhysicsStage PhysicsStage => ActorPhysicsStage.Presentation;

    /// <summary>Gets the last valid actor-priority cursor surface position.</summary>
    public Vector3 Point { get; private set; }

    private const uint TerrainMask = 16;

    private Node3D _actor;
    private Node3D _visual;

    private Node3D _marker;
    private Camera3D _camera;

    /// <summary>Connects scene-owned components after the complete scene is ready.</summary>
    /// <param name="actor">The moving actor root.</param>
    /// <param name="visual">The visual pivot carrying the mesh and directional arrow.</param>
    /// <param name="marker">The scene-owned cursor marker.</param>
    /// <param name="camera">The active top-down camera.</param>
    public void Configure(Node3D actor, Node3D visual, Node3D marker, Camera3D camera)
    {
        _actor = actor;
        _visual = visual;
        _marker = marker;
        _camera = camera;
        Point = actor.GlobalPosition + Vector3.Forward * 5;
    }

    /// <summary>Queries the cursor during physics, preferring a combatant over camera-occluding scenery.</summary>
    /// <param name="screen">Viewport-space cursor position.</param>
    /// <returns>True when a surface was found; false hides the marker and preserves the previous facing.</returns>
    public bool SampleCursor(Vector2 screen)
    {
        var hit = CastCursor(screen);
        _marker.Visible = hit.Count != 0;
        if (hit.Count == 0)
        {
            return false;
        }

        AimAt(hit["position"].AsVector3(), hit["normal"].AsVector3());

        return true;
    }

    /// <summary>Converts the same cursor surface query into a body-center or height-adjusted attack aim point.</summary>
    /// <param name="screen">Viewport-space cursor position.</param>
    /// <param name="point">A target's body center, terrain plus launch height, or the observed obstacle surface.</param>
    /// <returns>True when the cursor ray hits an actor, terrain or obstacle; false leaves point at its default value.</returns>
    public bool TryGetAttackPoint(Vector2 screen, out Vector3 point)
    {
        var hit = CastCursor(screen);
        point = default;
        if (hit.Count == 0)
        {
            return false;
        }

        if (hit["collider"].AsGodotObject() is ICombatBody target)
        {
            point = target.AimPoint;
        }
        else
        {
            point = hit["position"].AsVector3();
            if (hit["collider"].AsGodotObject() is CollisionObject3D body && (body.CollisionLayer & TerrainMask) != 0)
            {
                point += Vector3.Up * ProjectileAttack.ShotHeight;
            }
        }

        return true;
    }

    /// <summary>Faces a world-space surface point without pitching or rolling the actor.</summary>
    /// <param name="point">The observed surface position.</param>
    /// <param name="normal">The unit outward surface normal used to orient and offset the marker.</param>
    public void AimAt(Vector3 point, Vector3 normal)
    {
        Point = point;
        _marker.GlobalPosition = point + normal * 0.04f;
        var reference = Mathf.Abs(normal.Dot(Vector3.Right)) < 0.95f ? Vector3.Right : Vector3.Forward;
        var tangent = reference.Slide(normal).Normalized();
        _marker.GlobalBasis = new(tangent, normal, tangent.Cross(normal));
        var direction = point - _actor.GlobalPosition;
        direction.Y = 0;
        if (!direction.IsZeroApprox())
        {
            _visual.Rotation = new(0, Mathf.Atan2(-direction.X, -direction.Z), 0);
        }
    }

    private global::Godot.Collections.Dictionary CastCursor(Vector2 screen)
    {
        var origin = _camera.ProjectRayOrigin(screen);
        var query = PhysicsRayQueryParameters3D.Create(origin, origin + _camera.ProjectRayNormal(screen) * 500, 22);
        if (_actor is PhysicsBody3D source)
        {
            query.Exclude = [source.GetRid()];
        }

        var space = _actor.GetWorld3D().DirectSpaceState;
        var sourceExclusions = query.Exclude.Duplicate();
        query.CollisionMask = 6;

        while (true)
        {
            var actorHit = space.IntersectRay(query);

            if (actorHit.Count == 0)
            {
                break;
            }

            if (actorHit["collider"].AsGodotObject() is ICombatBody)
            {
                return actorHit;
            }

            if (actorHit["collider"].AsGodotObject() is not CollisionObject3D occluder)
            {
                break;
            }

            var exclusions = query.Exclude;
            exclusions.Add(occluder.GetRid());
            query.Exclude = exclusions;
        }

        query.Exclude = sourceExclusions;
        query.CollisionMask = 22;
        return space.IntersectRay(query);
    }
}
