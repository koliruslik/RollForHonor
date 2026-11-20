using System;
using System.Linq;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Godot;
using RollForHonor.Application.Combat.Models;
using RollForHonor.Godot.Arena;
using RollForHonor.Godot.CombatDemo;
using RollForHonor.Godot.Gameplay.Actors;
using RollForHonor.Godot.Gameplay.Combat;
using RollForHonor.Godot.Gameplay.Commands;
using RollForHonor.Godot.Gameplay.Input;
using RollForHonor.Godot.Gameplay.Movement;
using RollForHonor.Godot.Gameplay.Projectiles;
using Shouldly;

namespace RollForHonor.Godot.Tests;

/// <summary>Exercises native movement, camera aiming, straight airborne delivery and the existing combat services together.</summary>
public sealed class GodotArenaTest : TestClass
{
    private GameBootstrap Runtime => TestScene.GetTree().Root.GetChildren()
        .OfType<GameBootstrap>()
        .Single();

    /// <summary>Creates arena tests in the actual engine process.</summary>
    /// <param name="testScene">The host used to await physics and locate bootstrap.</param>
    public GodotArenaTest(Node testScene) : base(testScene)
    {
    }

    /// <summary>Camera-relative intent accelerates to the same speed diagonally and brakes to a stop.</summary>
    /// <returns>Completes after real movement and braking physics frames.</returns>
    [Test]
    public async Task MovementNormalizesDiagonalSpeedAndBrakes()
    {
        var arena = Create();

        try
        {
            await Frames(8);
            arena.Player.Controls.MovementIntent = Vector2.Right;
            await Frames(30);

            HorizontalSpeed(arena).ShouldBeInRange(5.9f, 6.1f);

            arena.Player.Controls.MovementIntent = Vector2.One;
            await Frames(20);

            HorizontalSpeed(arena).ShouldBeInRange(5.9f, 6.1f);

            arena.Player.Controls.MovementIntent = Vector2.Zero;
            await Frames(25);

            HorizontalSpeed(arena).ShouldBeLessThan(0.05f);
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>The native collider prevents movement through a solid courtyard wall.</summary>
    /// <returns>Completes after the actor has attempted to move beyond the wall.</returns>
    [Test]
    public async Task ObstaclesBlockMovement()
    {
        var arena = Create();

        try
        {
            arena.Player.GlobalPosition = new(-5, 0.05f, 18);
            arena.Player.Controls.MovementIntent = Intent(arena, Vector3.Right);
            await Frames(90);

            arena.Player.GlobalPosition.X.ShouldBeLessThan(-2.85f);
            arena.Player.GetSlideCollisionCount().ShouldBeGreaterThan(0);
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>The character walks a real ramp to the high plateau while retaining floor contact.</summary>
    /// <returns>Completes after reaching the top or exhausting the bounded physics frame budget.</returns>
    [Test]
    public async Task CharacterClimbsTheRamp()
    {
        var arena = Create();

        try
        {
            arena.Player.GlobalPosition = new(10, 0.05f, 23);
            arena.Player.Controls.MovementIntent = Intent(arena, Vector3.Forward);
            for (var frame = 0; frame < 210 && arena.Player.GlobalPosition.Y < 3.9f; frame++)
            {
                await Frames(1);
            }

            arena.Player.GlobalPosition.Y.ShouldBeGreaterThan(3.8f);
            arena.Player.IsOnFloor().ShouldBeTrue();
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>Screen ray aiming selects elevated terrain while visual facing remains yaw-only.</summary>
    /// <returns>Completes after scene physics is registered and the actual camera ray is queried.</returns>
    [Test]
    public async Task ElevatedAimDoesNotPitchTheCharacter()
    {
        var arena = Create();

        try
        {
            await Frames(3);
            arena.CameraRig.GlobalPosition = new(0, 0, 4);
            var screen = arena.CameraRig.Camera.UnprojectPosition(new(5, 4, 2));

            arena.Player.GetRequiredComponent<MouseFacing>().SampleCursor(screen).ShouldBeTrue();
            Math.Abs(arena.Player.GetRequiredComponent<MouseFacing>().Point.Y - 4).ShouldBeLessThan(0.02f);

            var visual = arena.Player.GetNode<Node3D>("Visual");

            Math.Abs(visual.Rotation.X).ShouldBeLessThan(0.001f);
            Math.Abs(visual.Rotation.Z).ShouldBeLessThan(0.001f);

            var direction = arena.Player.GetRequiredComponent<MouseFacing>().Point - arena.Player.GlobalPosition;
            direction.Y = 0;

            (-visual.GlobalBasis.Z).Dot(direction.Normalized()).ShouldBeGreaterThan(0.99f);
            arena.GetNode<Node3D>("AimMarker").GlobalPosition.Y.ShouldBeGreaterThan(4);
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>A horizontal projectile keeps its launch height when leaving the high terrace over a downward ramp.</summary>
    /// <returns>Completes after the shot has passed above the ramp entrance.</returns>
    [Test]
    public async Task ProjectileKeepsItsDirectionAboveDescendingTerrain()
    {
        var arena = Create();

        try
        {
            arena.Player.Position = new(10, 4.05f, 4);
            await Frames(6);
            var origin = arena.Player.Position + Vector3.Up * ProjectileAttack.ShotHeight;
            var started = arena.Player.GetRequiredComponent<ProjectileAttack>().RequestAttack(new(10, origin.Y, 30));
            var projectile = arena.GetChildren()
                .OfType<AirborneProjectile>()
                .Single();
            var direction = projectile.Direction;
            var contacts = 0;
            arena.Player.GetRequiredComponent<ProjectileAttack>().ContactCompleted += (_, _, _) => contacts++;
            for (var frame = 0; frame < 85 && GodotObject.IsInstanceValid(projectile) && projectile.Position.Z < 23; frame++)
            {
                await Frames(1);

                GodotObject.IsInstanceValid(projectile).ShouldBeTrue();
                Math.Abs(projectile.Position.Y - origin.Y).ShouldBeLessThan(0.01f);
                projectile.Direction.ShouldBe(direction);
            }

            projectile.Position.Z.ShouldBeGreaterThan(22);
            started.Attack!.IsActive.ShouldBeTrue();
            contacts.ShouldBe(0);
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>An upward trajectory blocked by the cliff face ends at that actual collider and reports the contact.</summary>
    /// <returns>Completes after the shot hits the solid terrace side.</returns>
    [Test]
    public async Task HighCliffPhysicallyBlocksAnOccludedUpwardShot()
    {
        var arena = Create();

        try
        {
            arena.Player.Position = new(-1, 0.05f, 0);
            await Frames(6);
            var body = "";
            arena.Player.GetRequiredComponent<ProjectileAttack>().ContactCompleted += (_, _, name) => body = name;
            var started = arena.Player.GetRequiredComponent<ProjectileAttack>().RequestAttack(new(12, 5, 0));
            var projectile = arena.GetChildren()
                .OfType<AirborneProjectile>()
                .Single();

            projectile.Direction.Y.ShouldBeGreaterThan(0);
            for (var frame = 0; frame < 60 && started.Attack!.IsActive; frame++)
            {
                await Frames(1);
            }

            started.Attack!.EndReason.ShouldBe(AttackEndReason.EnvironmentContact);
            body.ShouldBe("HighPlateau");
            arena.GetNode<Label>("HUD/Die").Text.ShouldContain("-");
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>A straight shot clears the low terrace edge and reaches the target's body center.</summary>
    /// <returns>Completes after a real elevated combatant contact.</returns>
    [Test]
    public async Task LowTerraceTargetIsHitWithoutFollowingTheGround()
    {
        var arena = Create();

        try
        {
            arena.Player.Position = new(-24, 0.05f, -10);
            await Frames(6);
            var target = arena.GetNode<DemoCombatant>("Targets/LowTarget");
            var committed = false;
            arena.Player.GetRequiredComponent<ProjectileAttack>().ContactCompleted += (contact, _, _) => committed |= contact.Impact?.IsCommitted == true;
            arena.Player.GetRequiredComponent<ProjectileAttack>().RequestAttack(target.Position + Vector3.Up * 0.9f);
            var projectile = arena.GetChildren()
                .OfType<AirborneProjectile>()
                .Single();
            var direction = projectile.Direction;
            for (var frame = 0; frame < 90 && !committed; frame++)
            {
                await Frames(1);
                if (GodotObject.IsInstanceValid(projectile))
                {
                    projectile.Direction.ShouldBe(direction);
                }
            }

            committed.ShouldBeTrue();
            Balance(arena).ShouldBe(490m);
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>A click command waits for physics, launches once and resolves a real target body contact through Application.</summary>
    /// <returns>Completes after the projectile reaches the courtyard target.</returns>
    [Test]
    public async Task ClickCommandResolvesActualCombatWithoutASecondCharge()
    {
        var arena = Create();

        try
        {
            await Frames(3);
            var target = arena.GetNode<DemoCombatant>("Targets/CourtyardTarget");
            var committed = false;
            arena.Player.GetRequiredComponent<ProjectileAttack>().ContactCompleted += (contact, _, _) => committed |= contact.Impact?.IsCommitted == true;
            arena.Player.Controls.HardwareInput = true;
            var screen = arena.CameraRig.Camera.UnprojectPosition(target.GlobalPosition + Vector3.Up * 0.9f);

            arena.Player.GetRequiredComponent<MouseFacing>().TryGetAttackPoint(screen, out var aim).ShouldBeTrue();
            aim.ShouldBe(target.GlobalPosition + Vector3.Up * 0.9f);

            arena.Player._UnhandledInput(new InputEventMouseButton { Pressed = true, ButtonIndex = MouseButton.Right, Position = screen });

            Balance(arena).ShouldBe(500m);
            for (var frame = 0; frame < 120 && !committed; frame++)
            {
                await Frames(1);
            }

            committed.ShouldBeTrue();
            Balance(arena).ShouldBe(490m);
            arena.GetNode<Label>("HUD/Die").Text.ShouldNotBe("d20\n-");
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>Defeated state blocks locomotion and Application refuses launches without spending resources.</summary>
    /// <returns>Completes after the motor observes the live defeated state.</returns>
    [Test]
    public async Task DefeatedCharacterCannotMoveOrLaunch()
    {
        var arena = Create();

        try
        {
            arena.Player.Position = new Vector3(0, 20, 0);
            arena.Player.Velocity = new Vector3(6, -2, 0);
            Runtime.CombatState.UpdateCombatant(new(arena.Player.CombatantId, 0, 100, 2, 12, [], []));
            arena.Player.Controls.MovementIntent = Vector2.Right;
            await Frames(4);

            arena.Player.GetRequiredComponent<ActorState>().IsDefeated.ShouldBeTrue();
            HorizontalSpeed(arena).ShouldBeLessThan(0.01f);
            arena.Player.Velocity.Y.ShouldBeLessThan(-2);
            arena.Player.GetRequiredComponent<ProjectileAttack>().RequestAttack(arena.Player.GlobalPosition + Vector3.Right * 10).Status.ShouldBe(AttackStartStatus.SourceDefeated);
            Balance(arena).ShouldBe(500m);

            arena.GetChildren()
                .OfType<AirborneProjectile>()

                .ShouldBeEmpty();
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>Physical WASD keys move relative to the camera, normalize diagonals and brake after release.</summary>
    /// <returns>Completes after actual keyboard input has driven the physics motor.</returns>
    [Test]
    public async Task WasdKeysDriveCameraRelativeMovementAndBrake()
    {
        var arena = Create();

        try
        {
            await Frames(6);
            arena.Player.Controls.HardwareInput = true;
            foreach (var (key, intent) in new[]
            {
                (Key.W, Vector2.Up),
                (Key.S, Vector2.Down),
                (Key.A, Vector2.Left),
                (Key.D, Vector2.Right)
            })
            {
                arena.Player.Position = new(-10, 0.05f, 24);
                var origin = arena.Player.Position;
                SetKey(key, true);
                await Frames(30);
                var displacement = arena.Player.Position - origin;
                displacement.Y = 0;
                var right = arena.CameraRig.Camera.GlobalBasis.X;
                var back = arena.CameraRig.Camera.GlobalBasis.Z;
                right.Y = back.Y = 0;
                var expected = right.Normalized() * intent.X + back.Normalized() * intent.Y;

                displacement.Normalized().Dot(expected).ShouldBeGreaterThan(0.99f);
                HorizontalSpeed(arena).ShouldBeInRange(5.9f, 6.1f);
                SetKey(key, false);
                await Frames(25);

                HorizontalSpeed(arena).ShouldBeLessThan(0.05f);
            }

            SetKey(Key.W, true);
            SetKey(Key.D, true);
            await Frames(30);

            HorizontalSpeed(arena).ShouldBeInRange(5.9f, 6.1f);
            Balance(arena).ShouldBe(500m);
        }
        finally
        {
            foreach (var key in new[]
            {
                Key.W,
                Key.A,
                Key.S,
                Key.D
            })
            {
                SetKey(key, false);
            }

            arena.Free();
        }
    }

    /// <summary>A left click no longer issues locomotion or launches a shot.</summary>
    /// <returns>Completes after physics has had time to consume any queued mouse command.</returns>
    [Test]
    public async Task LeftClickDoesNotMoveOrAttack()
    {
        var arena = Create();

        try
        {
            await Frames(6);
            arena.Player.Controls.HardwareInput = true;
            var origin = arena.Player.Position;
            var screen = arena.CameraRig.Camera.UnprojectPosition(origin + Vector3.Forward * 5);
            arena.Player._UnhandledInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = screen });
            await Frames(30);

            arena.Player.Position.DistanceTo(origin).ShouldBeLessThan(0.03f);
            arena.Player.GetRequiredComponent<ProjectileAttack>().LastAttack.ShouldBeNull();
            Balance(arena).ShouldBe(500m);
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>Swept projectile motion ends at an obstacle without resolving an impact against its hidden target.</summary>
    /// <returns>Completes when the projectile contacts the wall.</returns>
    [Test]
    public async Task WallStopsProjectileBeforeCombatant()
    {
        var arena = Create();

        try
        {
            arena.Player.Position = new(-5, 0.05f, 18);
            arena.GetNode<Node3D>("Targets/CourtyardTarget").Position = new(6, 0, 18);
            await Frames(3);
            var started = arena.Player.GetRequiredComponent<ProjectileAttack>().RequestAttack(new(6, 0, 18));
            for (var frame = 0; frame < 60 && started.Attack!.IsActive; frame++)
            {
                await Frames(1);
            }

            started.Attack!.EndReason.ShouldBe(AttackEndReason.EnvironmentContact);
            Balance(arena).ShouldBe(490m);
            arena.GetNode<Label>("HUD/Die").Text.ShouldContain("-");
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>A downward shot from the ramp edge must reach an actual physical contact instead of silently ending at a terrain-height transition.</summary>
    /// <returns>Completes after the bounded downward flight.</returns>
    [Test]
    public async Task DownwardRampEdgeShotReportsPhysicalContact()
    {
        var arena = Create();

        try
        {
            arena.Player.Position = new(8.2f, 3.3f, 12);
            await Frames(6);
            var contacts = 0;
            arena.Player.GetRequiredComponent<ProjectileAttack>().ContactCompleted += (_, _, _) => contacts++;
            var started = arena.Player.GetRequiredComponent<ProjectileAttack>().RequestAttack(new(0, 0, 12));
            var projectile = arena.GetChildren()
                .OfType<AirborneProjectile>()
                .Single();

            projectile.Direction.Y.ShouldBeLessThan(0);

            await Frames(6);

            started.Attack!.IsActive.ShouldBeTrue();
            for (var frame = 0; frame < 120 && started.Attack!.IsActive; frame++)
            {
                await Frames(1);
            }

            contacts.ShouldBe(1);
            started.Attack!.EndReason.ShouldBe(AttackEndReason.EnvironmentContact);
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>A visible elevated enemy is hit by an upward flight aimed at its body.</summary>
    /// <returns>Completes after the elevated target receives one committed impact.</returns>
    [Test]
    public Task UpwardShotReachesTheNearEdgeOfTheHighTerrace() => AssertElevationShot(new(10, 0.05f, 23), new(10, 4, 9), true);

    /// <summary>A shot from the high terrace crosses the downward ramp and hits the lower enemy.</summary>
    /// <returns>Completes after the lower target receives one committed impact.</returns>
    [Test]
    public Task DownwardShotReachesTheLowerEnemy() => AssertElevationShot(new(10, 4.05f, 9), new(10, 0, 25), false);

    /// <summary>Free terrain aiming adds launch height once and does not change the actor's horizontal facing contract.</summary>
    /// <returns>Completes after the camera ray observes the empty plateau surface.</returns>
    [Test]
    public async Task FreeAimUsesTheObservedTerrainHeightAtLaunch()
    {
        var arena = Create();

        try
        {
            await Frames(3);
            arena.CameraRig.GlobalPosition = new(0, 0, 4);
            var screen = arena.CameraRig.Camera.UnprojectPosition(new(5, 4, 2));

            arena.Player.GetRequiredComponent<MouseFacing>().TryGetAttackPoint(screen, out var point).ShouldBeTrue();
            Math.Abs(point.Y - 5).ShouldBeLessThan(0.02f);
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>A shot next to a wall starts at the source's muzzle instead of being offset through the collider.</summary>
    /// <returns>Completes after the near-wall physical contact.</returns>
    [Test]
    public async Task NearbyWallCannotBeSkippedByTheSpawnOffset()
    {
        var arena = Create();

        try
        {
            arena.Player.Position = new(-3, 0.05f, 18);
            await Frames(6);
            var body = "";
            arena.Player.GetRequiredComponent<ProjectileAttack>().ContactCompleted += (_, _, name) => body = name;
            var origin = arena.Player.Position + Vector3.Up * ProjectileAttack.ShotHeight;
            var started = arena.Player.GetRequiredComponent<ProjectileAttack>().RequestAttack(origin + Vector3.Right * 10);
            arena.GetChildren()
                .OfType<AirborneProjectile>()

                .Single().Position.ShouldBe(origin);
            for (var frame = 0; frame < 15 && started.Attack!.IsActive; frame++)
            {
                await Frames(1);
            }

            body.ShouldBe("CourtyardWall");
            started.Attack!.EndReason.ShouldBe(AttackEndReason.EnvironmentContact);
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>A current health update can return the actor from defeated mode to normal movement.</summary>
    /// <returns>Completes after defeat and recovery have both been observed by the actor FSM.</returns>
    [Test]
    public async Task ActorModeTracksDefeatAndRecovery()
    {
        var arena = Create();

        try
        {
            var modes = arena.Player.GetRequiredComponent<ActorState>();
            arena.Player.Controls.MovementIntent = Vector2.Right;
            Runtime.CombatState.UpdateCombatant(new(arena.Player.CombatantId, 0, 100, 2, 12, [], []));
            await Frames(4);

            modes.IsDefeated.ShouldBeTrue();
            modes.AllowsMovement.ShouldBeFalse();

            Runtime.CombatState.UpdateCombatant(new(arena.Player.CombatantId, 100, 100, 2, 12, [], []));
            await Frames(30);

            modes.IsDefeated.ShouldBeFalse();
            modes.AllowsMovement.ShouldBeTrue();
            HorizontalSpeed(arena).ShouldBeInRange(5.9f, 6.1f);
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>World-space commands from a non-player controller use the same launch path, consume refusals once and allow movement.</summary>
    /// <returns>Completes after the cooldown expires without a hidden retry.</returns>
    [Test]
    public async Task QueuedAttacksDoNotRetryAfterCooldownOrBlockMovement()
    {
        var arena = Create();

        try
        {
            var combat = arena.Player.GetRequiredComponent<ProjectileAttack>();
            var launches = 0;
            combat.LaunchCompleted += _ => launches++;
            var commands = arena.Player.GetRequiredComponent<CommandProcessor>();
            var point = arena.Player.GlobalPosition + Vector3.Forward * 20 + Vector3.Up;
            commands.Enqueue(new AttackAt(point));
            commands.Enqueue(new AttackAt(point));

            Balance(arena).ShouldBe(500m);

            arena.Player.Controls.MovementIntent = Vector2.Right;
            await Frames(40);

            launches.ShouldBe(2);
            Balance(arena).ShouldBe(490m);
            HorizontalSpeed(arena).ShouldBeInRange(5.9f, 6.1f);
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>The cursor marker sits on the target's visible face and rotates with its outward normal.</summary>
    /// <returns>Completes after the target's native collider is available to the camera ray.</returns>
    [Test]
    public async Task CursorMarkerLiesOnTargetSurface()
    {
        var arena = Create();

        try
        {
            await Frames(6);
            var target = arena.GetNode<DemoCombatant>("Targets/CourtyardTarget");
            var screen = arena.CameraRig.Camera.UnprojectPosition(target.Position + Vector3.Up * 0.9f);

            arena.Player.GetRequiredComponent<MouseFacing>().SampleCursor(screen).ShouldBeTrue();

            var point = arena.Player.GetRequiredComponent<MouseFacing>().Point;

            point.Y.ShouldBeGreaterThan(0.3f);
            point.Y.ShouldBeLessThan(1.8f);

            var marker = arena.GetNode<Node3D>("AimMarker");
            var normal = marker.GlobalBasis.Y;

            Mathf.Abs(normal.Y).ShouldBeLessThan(0.01f);
            Mathf.Max(Mathf.Abs(point.X - target.Position.X), Mathf.Abs(point.Z - target.Position.Z)).ShouldBeInRange(0.49f, 0.51f);
            (marker.GlobalPosition - point).Dot(normal).ShouldBeInRange(0.039f, 0.041f);
            marker.Visible.ShouldBeTrue();
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>The wall occludes floor aiming and the marker lies on its vertical face without pitching the actor.</summary>
    /// <returns>Completes after sampling the real courtyard wall.</returns>
    [Test]
    public async Task CursorMarkerLiesOnWallSurface()
    {
        var arena = Create();

        try
        {
            await Frames(6);
            var screen = arena.CameraRig.Camera.UnprojectPosition(new(-1.5f, 1.5f, 18));

            arena.Player.GetRequiredComponent<MouseFacing>().SampleCursor(screen).ShouldBeTrue();

            var point = arena.Player.GetRequiredComponent<MouseFacing>().Point;

            Math.Abs(point.X + 1.5f).ShouldBeLessThan(0.02f);
            point.Y.ShouldBeInRange(1.4f, 1.6f);

            var marker = arena.GetNode<Node3D>("AimMarker");

            marker.GlobalBasis.Y.Dot(Vector3.Right).ShouldBeGreaterThan(0.99f);
            marker.GlobalPosition.X.ShouldBeGreaterThan(point.X);

            var visual = arena.Player.GetNode<Node3D>("Visual");

            visual.Rotation.X.ShouldBe(0);
            visual.Rotation.Z.ShouldBe(0);
            arena.Player.GetRequiredComponent<MouseFacing>().TryGetAttackPoint(screen, out var attackPoint).ShouldBeTrue();
            attackPoint.DistanceTo(point).ShouldBeLessThan(0.001f);
        }
        finally
        {
            arena.Free();
        }
    }

    private GreyboxArena Create()
    {
        var scene = GD.Load<PackedScene>("res://Arena.tscn").Instantiate<GreyboxArena>();
        Runtime.AddChild(scene);
        scene.Player.Controls.HardwareInput = false;
        scene.Opponent.Controls.ThinkingEnabled = false;

        return scene;
    }

    private async Task Frames(int count)
    {
        for (var frame = 0; frame < count; frame++)
        {
            await TestScene.ToSignal(TestScene.GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
    }

    private static float HorizontalSpeed(GreyboxArena arena) => new Vector2(arena.Player.Velocity.X, arena.Player.Velocity.Z).Length();

    private static Vector2 Intent(GreyboxArena arena, Vector3 worldDirection)
    {
        var right = arena.CameraRig.Camera.GlobalBasis.X;
        var back = arena.CameraRig.Camera.GlobalBasis.Z;
        right.Y = back.Y = 0;

        return new(worldDirection.Dot(right.Normalized()), worldDirection.Dot(back.Normalized()));
    }

    private decimal Balance(GreyboxArena arena) => Runtime.CombatState.ReadForStart(arena.Player.CombatantId, "arena-shot", Guid.NewGuid()).Resources.Single().Amount;

    private static void SetKey(Key key, bool pressed) => Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Pressed = pressed });

    private async Task AssertElevationShot(Vector3 source, Vector3 destination, bool upward)
    {
        var arena = Create();

        try
        {
            arena.Player.Position = source;
            var target = arena.GetNode<DemoCombatant>("Targets/CourtyardTarget");
            target.Position = destination;
            await Frames(6);
            var committed = false;
            arena.Player.GetRequiredComponent<ProjectileAttack>().ContactCompleted += (contact, _, _) => committed |= contact.Impact?.IsCommitted == true;
            var started = arena.Player.GetRequiredComponent<ProjectileAttack>().RequestAttack(target.Position + Vector3.Up * 0.9f);
            var projectile = arena.GetChildren()
                .OfType<AirborneProjectile>()
                .Single();

            (projectile.Direction.Y > 0).ShouldBe(upward);
            for (var frame = 0; frame < 120 && started.Attack!.IsActive; frame++)
            {
                await Frames(1);
            }

            committed.ShouldBeTrue();
            Balance(arena).ShouldBe(490m);
            started.Attack!.EndReason.ShouldBe(AttackEndReason.CombatantContact);
        }
        finally
        {
            arena.Free();
        }
    }
}
