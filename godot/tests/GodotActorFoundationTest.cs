using System;
using System.Collections.Generic;
using Chickensoft.GoDotTest;
using Godot;
using RollForHonor.Godot.Gameplay.Actors;
using RollForHonor.Godot.Gameplay.Commands;
using RollForHonor.Godot.Gameplay.Movement;
using RollForHonor.Godot.Gameplay.StateMachines;
using Shouldly;

namespace RollForHonor.Godot.Tests;

/// <summary>Verifies actor lifecycle, ordered command execution and finite-state transitions.</summary>
public sealed class GodotActorFoundationTest : TestClass
{
    /// <summary>Creates foundation checks inside the actual engine.</summary>
    /// <param name="testScene">The engine-owned test host.</param>
    public GodotActorFoundationTest(Node testScene) : base(testScene)
    {
    }

    /// <summary>All hosts are assigned before dependency hooks, and physics follows stages rather than child order.</summary>
    [Test]
    public void ComponentsInitializeBeforeHooksAndRunInStageOrder()
    {
        var calls = new List<string>();
        var actor = new FoundationActor();
        var later = new FoundationProbe
        {
            Calls = calls,
            Stage = ActorPhysicsStage.Movement
        };
        var earlier = new FoundationProbe
        {
            Calls = calls,
            Stage = ActorPhysicsStage.Input
        };
        later.Peer = earlier;
        earlier.Peer = later;
        actor.AddChild(later);
        actor.AddChild(earlier);
        TestScene.AddChild(actor);

        try
        {
            calls.ShouldBe(new[] { "Input:initialized", "Movement:initialized" });

            actor._PhysicsProcess(0.01);

            calls.Count.ShouldBe(2);

            actor.Enable();
            actor._PhysicsProcess(0.01);

            calls.ShouldBe(new[] { "Input:initialized", "Movement:initialized", "Input:physics", "Movement:physics" });
            Should.Throw<InvalidOperationException>(() => actor.GetRequiredComponent<CommandProcessor>());
            Should.Throw<InvalidOperationException>(() => actor.GetRequiredComponent<FoundationProbe>());
        }
        finally
        {
            actor.Free();
        }
    }

    /// <summary>Commands wait for physics, execute in FIFO order once and are cleared when their actor leaves the tree.</summary>
    [Test]
    public void CommandsAreConsumedOnceAndClearedOnExit()
    {
        var calls = new List<string>();
        var actor = new FoundationActor();
        var commands = new CommandProcessor();
        actor.AddChild(commands);
        TestScene.AddChild(actor);

        try
        {
            commands.Enqueue(new RecordCommand("first", calls));
            commands.Enqueue(new RecordCommand("second", calls));

            calls.ShouldBeEmpty();

            actor.Enable();
            actor._PhysicsProcess(0.01);
            actor._PhysicsProcess(0.01);

            calls.ShouldBe(new[] { "first", "second" });

            commands.Enqueue(new RecordCommand("stale", calls));
            TestScene.RemoveChild(actor);
            actor._PhysicsProcess(0.01);

            calls.Count.ShouldBe(2);
        }
        finally
        {
            actor.Free();
        }
    }

    /// <summary>A transition exits the previous state before entering the next; selecting the active type is a no-op.</summary>
    [Test]
    public void StateTransitionsRespectLifecycleAndIgnoreSameState()
    {
        var calls = new List<string>();
        var machine = new StateMachine();
        machine.Register(new FirstState(calls));
        machine.Register(new SecondState(calls));
        machine.CurrentState.ShouldBeNull();
        machine.ChangeState<FirstState>();
        machine.ChangeState<FirstState>();
        machine.ChangeState<SecondState>();
        calls.ShouldBe(new[] { "first:enter", "first:exit", "second:enter" });
        machine.CurrentState.ShouldBeOfType<SecondState>();
    }

    /// <summary>Invalid registrations and destinations fail before the active state exits.</summary>
    [Test]
    public void InvalidStateRequestsPreserveCurrentState()
    {
        var calls = new List<string>();
        var machine = new StateMachine();
        machine.Register(new FirstState(calls));
        machine.ChangeState<FirstState>();
        Should.Throw<ArgumentNullException>(() => machine.Register(null!));
        Should.Throw<ArgumentException>(() => machine.Register(new FirstState(calls)));
        Should.Throw<InvalidOperationException>(() => machine.ChangeState<SecondState>());
        calls.ShouldBe(new[] { "first:enter" });
        machine.CurrentState.ShouldBeOfType<FirstState>();
    }

    /// <summary>Lifecycle recursion is rejected, and a later explicit transition still works after the callback throws.</summary>
    [Test]
    public void ReentrantTransitionsFailWithoutLockingTheMachine()
    {
        var calls = new List<string>();
        var machine = new StateMachine();
        machine.Register(new ReentrantState(machine));
        machine.Register(new SecondState(calls));
        Should.Throw<InvalidOperationException>(() => machine.ChangeState<ReentrantState>());
        machine.ChangeState<SecondState>();
        machine.CurrentState.ShouldBeOfType<SecondState>();
        calls.ShouldBe(new[] { "second:enter" });
    }

    /// <summary>Zero intent brakes while an explicit stop removes horizontal motion without touching vertical velocity.</summary>
    [Test]
    public void MotorSeparatesBrakingFromForcedStoppingWithoutAnActorFsm()
    {
        var actor = new FoundationActor();
        var motor = new MovementMotor();
        actor.AddChild(motor);
        TestScene.AddChild(actor);

        try
        {
            actor.Position = new Vector3(0, 50, 0);
            actor.Velocity = new Vector3(6, -3, 0);
            actor.Enable();

            actor._PhysicsProcess(0.01);

            motor.HorizontalSpeed.ShouldBeInRange(5.7f, 5.8f);
            motor.Velocity.Y.ShouldBeLessThan(-3);
            motor.IsGrounded.ShouldBeFalse();

            var fallingSpeed = motor.Velocity.Y;
            motor.MovementIntent = Vector3.Right;
            motor.StopHorizontalMotion();

            motor.HorizontalSpeed.ShouldBe(0);
            motor.MovementIntent.ShouldBe(Vector3.Zero);
            motor.Velocity.Y.ShouldBe(fallingSpeed);

            actor._PhysicsProcess(0.01);

            motor.HorizontalSpeed.ShouldBe(0);
            motor.Velocity.Y.ShouldBeLessThan(fallingSpeed);
        }
        finally
        {
            actor.Free();
        }
    }

    /// <summary>A motor accepts world intent from any controller and normalizes diagonal movement without a camera or FSM.</summary>
    [Test]
    public void MotorRunsFromWorldIntentWithoutPlayerComponents()
    {
        var actor = new FoundationActor();
        var motor = new MovementMotor();
        actor.AddChild(motor);
        TestScene.AddChild(actor);

        try
        {
            actor.Position = new Vector3(0, 50, 0);
            actor.Enable();
            motor.MovementIntent = new Vector3(1, 0, 1);

            for (var frame = 0; frame < 40; frame++)
            {
                actor._PhysicsProcess(1.0 / 60);
            }

            motor.HorizontalSpeed.ShouldBeInRange(5.99f, 6.01f);
            motor.Velocity.X.ShouldBeGreaterThan(0);
            motor.Velocity.Z.ShouldBeGreaterThan(0);
        }
        finally
        {
            actor.Free();
        }
    }

    private sealed record RecordCommand(string Value, List<string> Calls) : IEntityCommand
    {
        /// <inheritdoc />
        public void Execute(DynamicEntity actor) => Calls.Add(Value);
    }

    private sealed class FirstState(List<string> calls) : IState
    {
        /// <inheritdoc />
        public void Enter() => calls.Add("first:enter");

        /// <inheritdoc />
        public void Exit() => calls.Add("first:exit");
    }

    private sealed class SecondState(List<string> calls) : IState
    {
        /// <inheritdoc />
        public void Enter() => calls.Add("second:enter");

        /// <inheritdoc />
        public void Exit()
        {
        }
    }

    private sealed class ReentrantState(StateMachine machine) : IState
    {
        /// <inheritdoc />
        public void Enter() => machine.ChangeState<SecondState>();

        /// <inheritdoc />
        public void Exit()
        {
        }
    }
}

internal partial class FoundationActor : DynamicEntity
{
    internal void Enable() => IsConfigured = true;
}

internal partial class FoundationProbe : EntityComponent
{
    /// <inheritdoc />
    public override ActorPhysicsStage PhysicsStage => Stage;
    internal List<string> Calls { get; set; } = null!;
    internal FoundationProbe Peer { get; set; } = null!;
    internal ActorPhysicsStage Stage { get; set; }

    /// <inheritdoc />
    public override void OnComponentsInitialized()
    {
        Peer.Host.ShouldBe(Host);
        Calls.Add($"{Stage}:initialized");
    }

    /// <inheritdoc />
    public override void PhysicsStep(double delta) => Calls.Add($"{Stage}:physics");
}
