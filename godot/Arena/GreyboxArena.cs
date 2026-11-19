using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using RollForHonor.Application.Combat.Models;
using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Godot.CombatDemo;
using RollForHonor.Godot.Gameplay.Actors;
using RollForHonor.Godot.Gameplay.Combat;
using RollForHonor.Godot.Gameplay.Input;

namespace RollForHonor.Godot.Arena;

/// <summary>Composes a movement/facing playground around the existing engine-independent combat session.</summary>
public partial class GreyboxArena : Node3D
{
    /// <summary>Gets the controllable character after scene initialization.</summary>
    public Player Player { get; private set; }

    /// <summary>Gets the angled following camera.</summary>
    public ArenaCamera CameraRig { get; private set; }

    private GameBootstrap _runtime;

    private Label _status;
    private Label _die;

    private RichTextLabel _journal;
    private readonly Queue<string> _entries = new();

    /// <inheritdoc />
    public override void _Ready()
    {
        _runtime = GetParent<GameBootstrap>();
        Player = GetNode<Player>("Player");
        CameraRig = GetNode<ArenaCamera>("CameraRig");

        _runtime.CombatState.AddCombatant(new(Player.CombatantId, 100, 100, 2, 12, [], []), [new(new("mana"), 500)]);
        foreach (var target in GetTargets())
        {
            _runtime.CombatState.AddCombatant(new(target.CombatantId, 40, 40, 5, 10, [], []));
            target.GetNode<Label3D>("Name").Text = $"{target.Name}\n40 HP";
        }

        _status = GetNode<Label>("HUD/Info/Rows/State");
        _die = GetNode<Label>("HUD/Die");
        _journal = GetNode<RichTextLabel>("HUD/Journal/Rows/Text");

        Player.Controls.Configure(CameraRig.Camera);
        Player.GetRequiredComponent<MouseFacing>().Configure(Player, Player.GetNode<Node3D>("Visual"), GetNode<Node3D>("AimMarker"), CameraRig.Camera);
        Player.GetRequiredComponent<ActorState>().Configure(() => _runtime.CombatState.Read(Player.CombatantId, Player.CombatantId).Source.IsDefeated);

        var attack = new AttackDefinition(
            "arena-shot",
            new(new(1, 20), 8, NaturalOutcomeShiftRules.Standard,
                [new(new(1, 6), 3, DamageType.Piercing)], [], AttackTags.Projectile),
            TimeSpan.FromSeconds(0.25),
            [new(new("mana"), 10)],
            new(AttackRepeatPolicy.OncePerTarget,
                AttackStopCondition.CombatantContact | AttackStopCondition.EnvironmentContact,
                TimeSpan.FromSeconds(4)));

        Player.GetRequiredComponent<ProjectileAttack>().Configure(_runtime.Attacks, _runtime.CombatState, attack, GD.Load<PackedScene>("res://Gameplay/Projectiles/AirborneProjectile.tscn"));
        Player.CompleteConfiguration();

        Player.GetRequiredComponent<ProjectileAttack>().LaunchCompleted += result => Append($"Launch: {result.Status}\n");
        Player.GetRequiredComponent<ProjectileAttack>().ContactCompleted += OnContact;

        GetNode<Button>("HUD/Info/Rows/Workbench").Pressed += OpenWorkbench;
        GetNode<Button>("HUD/Info/Rows/Restart").Pressed += Restart;
    }

    /// <inheritdoc />
    public override void _Process(double delta)
    {
        var actor = _runtime.CombatState.Read(Player.CombatantId, Player.CombatantId).Source;
        var launch = _runtime.CombatState.ReadForStart(Player.CombatantId, "arena-shot", Guid.NewGuid());
        var mode = Player.GetRequiredComponent<ActorState>().IsDefeated ? "Defeated" : "Active";

        _status.Text = $"HP {actor.Health}/{actor.MaxHealth} | Mana {launch.Resources.Single().Amount}\n" +
            $"{mode} | Ground height {Player.GlobalPosition.Y:0.00}\n" +
            "WASD: move | Mouse: face | RMB: shoot\nWheel: zoom | R: restart | F2: combat lab";
    }

    /// <inheritdoc />
    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key)
        {
            return;
        }

        if (key.PhysicalKeycode == Key.F2)
        {
            OpenWorkbench();
        }
        else if (key.PhysicalKeycode == Key.R)
        {
            Restart();
        }
        else
        {
            return;
        }

        GetViewport().SetInputAsHandled();
    }

    private IEnumerable<DemoCombatant> GetTargets() => GetNode<Node3D>("Targets")
        .GetChildren()
        .OfType<DemoCombatant>();

    private void Append(string entry)
    {
        _entries.Enqueue(entry);
        while (_entries.Count > 20)
        {
            _entries.Dequeue();
        }

        _journal.Text = string.Concat(_entries);
    }

    private void OnContact(AttackContactResult contact, CombatStateRead input, string targetName)
    {
        Append(DemoCombatJournal.Format(contact, input, "Player", targetName));
        if (contact.Impact is not { IsCommitted: true, Outcome: ResolvedCombat result })
        {
            return;
        }

        _die.Text = $"d20\n{result.Resolution.Result.Target.AttackRoll?.Roll.Results.Single()}";
        foreach (var target in GetTargets())
        {
            var state = _runtime.CombatState.Read(Player.CombatantId, target.CombatantId).Target;
            target.GetNode<Label3D>("Name").Text = $"{target.Name}\n{state.Health}/{state.MaxHealth} HP";
        }
    }

    private void OpenWorkbench() => GetTree().ChangeSceneToFile("res://WorkbenchBootstrap.tscn");
    private void Restart() => GetTree().ReloadCurrentScene();

}
