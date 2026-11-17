#nullable enable
using System;
using System.Linq;
using Godot;
using RollForHonor.Application.Combat.Models;
using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Resources.Models;

namespace RollForHonor.Godot.CombatDemo;

/// <summary>Hosts the bounded two-object combat demonstration using the bootstrap's existing services.</summary>
public partial class CombatDemoScene : Node3D
{
    private static readonly ResourceId Mana = new("mana");
    private static readonly PackedScene ProjectileScene = GD.Load<PackedScene>("res://CombatDemo/Projectile.tscn");
    private readonly AttackDefinition _definition = new("demo-arrow",
        new(new(1, 20), 5, NaturalOutcomeShiftRules.Standard,
            [new(new(1, 6), 3, DamageType.Piercing)], [], AttackTags.Projectile),
        TimeSpan.FromSeconds(0.8), [new(Mana, 10)],
        new(AttackRepeatPolicy.OncePerTarget,
            AttackStopCondition.CombatantContact | AttackStopCondition.EnvironmentContact, TimeSpan.FromSeconds(3)));
    private GameBootstrap _runtime = null!;
    private Label _state = null!;
    private Label _result = null!;
    private Label _die = null!;
    private Button _fire = null!;

    /// <summary>Gets the source body after the scene is ready.</summary>
    public DemoCombatant Source { get; private set; } = null!;
    /// <summary>Gets the target body after the scene is ready.</summary>
    public DemoCombatant Target { get; private set; } = null!;
    /// <summary>Gets the most recent physically reported contact, or null before one occurs.</summary>
    public AttackContactResult? LastContact { get; private set; }
    /// <summary>Gets the number of newly committed impacts observed by this scene.</summary>
    public int CommittedImpacts { get; private set; }

    /// <inheritdoc />
    public override void _Ready()
    {
        Node? ancestor = GetParent();
        while (ancestor is not null && ancestor is not GameBootstrap) ancestor = ancestor.GetParent();
        _runtime = ancestor as GameBootstrap ?? throw new InvalidOperationException("Run this scene through Bootstrap.tscn.");
        Source = GetNode<DemoCombatant>("Source");
        Target = GetNode<DemoCombatant>("Target");
        _state = GetNode<Label>("HUD/Panel/Rows/State");
        _result = GetNode<Label>("HUD/Panel/Rows/Result");
        _die = GetNode<Label>("HUD/Die");
        _fire = GetNode<Button>("HUD/Panel/Rows/Fire");
        _runtime.CombatState.AddCombatant(new(Source.CombatantId, 100, 100, 0, 10, [], []), [new(Mana, 100)]);
        _runtime.CombatState.AddCombatant(new(Target.CombatantId, 50, 50, 5, 12, [], []));
        _fire.Pressed += FireFromInput;
        GetNode<Button>("HUD/Panel/Rows/Restart").Pressed += Restart;
        RefreshState();
    }

    /// <summary>Requests one launch and spawns a projectile only when Application accepts it.</summary>
    /// <returns>The actual launch result, including cooldown/resource rejections.</returns>
    public AttackStartResult Fire()
    {
        var start = _runtime.Attacks.Start(Guid.NewGuid(), Source.CombatantId, _definition);
        if (start.Attack is { } attack)
        {
            var projectile = ProjectileScene.Instantiate<DemoProjectile>();
            var origin = Source.GlobalPosition + Vector3.Right * 0.9f;
            projectile.Initialize(attack, (Target.GlobalPosition - origin).Normalized() * 6, ShowContact);
            AddChild(projectile);
            projectile.GlobalPosition = origin;
            _result.Text = "Arrow launched";
        }
        else _result.Text = $"Launch: {start.Status}";
        RefreshState();
        return start;
    }

    /// <inheritdoc />
    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false, PhysicalKeycode: Key.Space })
        {
            FireFromInput();
            GetViewport().SetInputAsHandled();
        }
    }

    /// <inheritdoc />
    public override void _Process(double delta) => RefreshState();

    private void FireFromInput()
    {
        if (!_fire.Disabled) Fire();
    }
    private void Restart() => GetTree().ReloadCurrentScene();
    private void RefreshState()
    {
        var read = _runtime.CombatState.Read(Source.CombatantId, Target.CombatantId);
        var launch = _runtime.CombatState.ReadForStart(Source.CombatantId, _definition.Key, Guid.NewGuid());
        var mana = launch.Resources.Single(value => value.Resource == Mana).Amount;
        var remaining = Math.Max(0, (launch.ReadyAt - _runtime.GameTime).TotalSeconds);
        _state.Text = $"Archer: {read.Source.Health} HP | Mana: {mana}/100\nTraining target: {read.Target.Health}/50 HP | Cooldown: {remaining:0.0}s";
        _fire.Disabled = read.Target.IsDefeated || read.Source.IsDefeated || mana < 10 || remaining > 0;
        Target.GetNode<Label3D>("Name").Text = $"Training target\n{read.Target.Health}/50 HP";
    }

    private void ShowContact(AttackContactResult contact)
    {
        LastContact = contact;
        if (contact.Impact is { IsCommitted: true, Outcome: ResolvedCombat resolved })
        {
            CommittedImpacts++;
            var target = resolved.Resolution.Result.Target;
            _die.Text = $"d20\n{target.AttackRoll?.Roll.Results.Single()}";
            var damage = target.DamageResolution?.AfterDefenses.Damage.Components.Sum(value => value.Amount) ?? 0;
            _result.Text = $"{target.Outcome}: {damage} piercing damage (Training target)";
        }
        else _result.Text = $"Contact: {contact.Status} | Commit: {contact.Impact?.CommitStatus}";
        RefreshState();
    }
}
