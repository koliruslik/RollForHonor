using System.Collections.Generic;
using System.Linq;
using System;
using Godot;
using RollForHonor.Application.Combat.Models;
using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Items.Equipment.Models;
using RollForHonor.Domain.Stats;
using RollForHonor.Godot.CombatDemo;
using RollForHonor.Godot.Content.Loot;
using RollForHonor.Godot.Content.Items;
using RollForHonor.Godot.Gameplay.Actors;
using RollForHonor.Godot.Gameplay.Combat;
using RollForHonor.Godot.Gameplay.Input;
using RollForHonor.Godot.Gameplay.Stats;
using RollForHonor.Godot.Gameplay.Targeting;
using RollForHonor.Godot.UI.Hud;
using RollForHonor.Godot.UI.Localization;

namespace RollForHonor.Godot.Arena;

/// <summary>Composes a movement/facing playground around the existing engine-independent combat session.</summary>
public partial class GreyboxArena : Node3D
{
    /// <summary>Gets the controllable character after scene initialization.</summary>
    public Player Player { get; private set; }

    /// <summary>Gets the angled following camera.</summary>
    public ArenaCamera CameraRig { get; private set; }

    /// <summary>Gets the active projectile opponent after scene initialization.</summary>
    public Enemy Opponent { get; private set; }

    /// <summary>Raised for observed attack contacts so presentation can retain actual results.</summary>
    public event Action<string, AttackContactResult, CombatStateRead, string> ContactObserved;

    /// <summary>Raised once for each committed hit or periodic resolution available to world presentation.</summary>
    public event Action<CombatResolution> CombatResolved;

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
        Opponent = GetNode<Enemy>("Sentinel");
        CameraRig = GetNode<ArenaCamera>("CameraRig");

        RegisterActorStats(Player);
        foreach (var target in GetTargets())
        {
            _runtime.CombatState.AddCombatant(new(target.CombatantId, 40, 40, 5, 10, [], []));
            target.GetNode<Label3D>("Name").Text = L.Format($"{target.Name}\n40 HP");
        }

        ConfigureOpponent(Opponent);
        _enemies.Add(Opponent);
        _roundRandom = new Random(SpawnSeed == 0 ? Random.Shared.Next() : SpawnSeed);
        _pendingRound = RoundsEnabled;

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
        Player.GetRequiredComponent<ProjectileAttack>().AttackProvider = CreatePlayerAttack;
        Player.CompleteConfiguration();

        Player.GetRequiredComponent<ProjectileAttack>().LaunchCompleted += result => Append($"Launch: {result.Status}\n");
        Player.GetRequiredComponent<ProjectileAttack>().ContactCompleted += (contact, input, name) => OnContact(Player.Name, contact, input, name);

        GetNode<Button>("HUD/Info/Rows/Workbench").Pressed += OpenWorkbench;
        GetNode<Button>("HUD/Info/Rows/Restart").Pressed += Restart;
        ConfigurePresentation();
    }

    /// <inheritdoc />
    public override void _Process(double delta)
    {
        var actor = _runtime.CombatState.Read(Player.CombatantId, Player.CombatantId).Source;
        var launch = _runtime.CombatState.ReadForStart(Player.CombatantId, "arena-shot", Guid.NewGuid());
        var mode = Player.GetRequiredComponent<ActorState>().IsDefeated ? "Defeated" : "Active";
        var enemy = _runtime.CombatState.Read(Opponent.CombatantId, Opponent.CombatantId).Source;
        var enemyMode = enemy.IsDefeated ? "Defeated" : Opponent.Controls.HasVisibleTarget ? "Engaged" : "Idle";
        Opponent.GetNode<Label3D>("Status").Text = $"Sentinel | {enemyMode}\nHP {enemy.Health}/{enemy.MaxHealth}";

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
        else if (@event.IsActionPressed("rfh_pickup", false, true))
        {
            PickUpNearest();
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

    private void RegisterActorStats(DynamicEntity actor)
    {
        var stats = actor.GetRequiredComponent<ActorStats>();
        _runtime.CombatState.AddStatCombatant(actor.CombatantId, stats.Definition.CreateProfile());
        stats.Configure(() => _runtime.CombatState.GetStats(actor.CombatantId));
        actor.GetRequiredComponent<CombatAwareness>().Configure(_runtime.CombatState, () => _runtime.GameTime);
    }

    private IEnumerable<DemoCombatant> GetTargets() => GetNode<Node3D>("Targets")
        .GetChildren()
        .OfType<DemoCombatant>();

    private void ConfigureOpponent(Enemy enemy)
    {
        RegisterActorStats(enemy);
        enemy.GetRequiredComponent<ActorState>().Configure(() =>
            _runtime.CombatState.Read(enemy.CombatantId, enemy.CombatantId).Source.IsDefeated);

        var attack = SentinelAttackContent.Create();

        var combat = enemy.GetRequiredComponent<ProjectileAttack>();
        combat.Configure(_runtime.Attacks, _runtime.CombatState, attack,
            GD.Load<PackedScene>("res://Arena/EnemyProjectile.tscn"));
        combat.ContactCompleted += (contact, input, name) => OnContact(enemy.Name, contact, input, name);

        enemy.Controls.Configure(Player, _runtime.CombatState, () =>
            _runtime.CombatState.ReadForStart(enemy.CombatantId, "sentinel-shot", Guid.NewGuid()).ReadyAt <= _runtime.GameTime);
        enemy.CompleteConfiguration();
    }

    private void Append(string entry)
    {
        _entries.Enqueue(entry);
        while (_entries.Count > 20)
        {
            _entries.Dequeue();
        }

        _journal.Text = string.Concat(_entries);
    }

    private void OnContact(string sourceName, AttackContactResult contact, CombatStateRead input, string targetName)
    {
        Append(DemoCombatJournal.Format(contact, input, sourceName, targetName));
        var sourceBody = _enemies
            .Concat(_adminEnemies)
            .FirstOrDefault(enemy => enemy.CombatantId == input.Source.Id);
        var targetBody = _enemies
            .Concat(_adminEnemies)
            .FirstOrDefault(enemy => enemy.CombatantId == input.Target.Id);
        ContactObserved?.Invoke(sourceBody is null ? sourceName : ActorName(sourceBody), contact, input, targetBody is null ? targetName : ActorName(targetBody));
        if (contact.Impact is not { IsCommitted: true, Outcome: ResolvedCombat result })
        {
            return;
        }

        CombatResolved?.Invoke(result.Resolution);
        _runtime.PeriodicEffects.Observe(result.Resolution, _runtime.GameTime);
        foreach (var effect in result.Resolution.Result.Target.Effects
            .Where(effect => Content.Combat.StatusEffectContent.Rules.Any(rule => rule.Id == effect.Definition.Id)))
        {
            var status = DemoEffectCatalog.Name(effect.Definition.Id);
            GetNode<ArenaHud>("Presentation").RecordAction(L.Format($"{status} applied or refreshed on {targetName}"));
        }

        foreach (var actor in new DynamicEntity[] { Player }
            .Concat(_enemies)
            .Concat(_adminEnemies))
        {
            if (actor.CombatantId == input.Source.Id || actor.CombatantId == input.Target.Id)
            {
                var other = actor.CombatantId == input.Source.Id ? input.Target.Id : input.Source.Id;
                actor.GetRequiredComponent<CombatAwareness>().Selection.Record(other, _runtime.GameTime, TimeSpan.FromSeconds(5));
            }
        }

        _die.Text = $"d20\n{result.Resolution.Result.Target.AttackRoll?.Roll.Results.Single()}";
        foreach (var target in GetTargets())
        {
            var state = _runtime.CombatState.Read(Player.CombatantId, target.CombatantId).Target;
            target.GetNode<Label3D>("Name").Text = L.Format($"{target.Name}\n{state.Health}/{state.MaxHealth} HP");
        }
    }

    private AttackDefinition CreatePlayerAttack()
    {
        var weapon = _runtime.CombatState.ReadEquipment(Player.CombatantId).Loadout.GetItem(EquipmentSlot.Weapon)?.Base.Weapon;

        var speed = _runtime.CombatState.GetStats(Player.CombatantId).Get(new(StatType.AttackSpeed));

        return new AttackDefinition("arena-shot",
            new(new(1, 20), 8, NaturalOutcomeShiftRules.Standard, weapon?.Damage ?? [new(new(1, 6), 3, DamageType.Piercing)], [], AttackTags.Projectile),
            TimeSpan.FromSeconds((double)(1 / Math.Max(0.01m, (weapon?.AttacksPerSecond ?? 4) * speed))),
            [new(new("mana"), weapon?.ManaCost ?? 10)],
            new(AttackRepeatPolicy.OncePerTarget, AttackStopCondition.CombatantContact | AttackStopCondition.EnvironmentContact, TimeSpan.FromSeconds(4)));
    }

    private void OpenWorkbench() => GetTree().ChangeSceneToFile("res://WorkbenchBootstrap.tscn");
    private void Restart() => GetTree().ReloadCurrentScene();

    private void ConfigurePresentation()
    {
        foreach (var item in RoundsEnabled ? System.Array.Empty<RollForHonor.Domain.Items.Models.ItemInstance>() : StarterItemCatalog.Create(Player.CombatantId))
        {
            _runtime.CombatState.RegisterOwnedItem(Player.CombatantId, item);
        }

        GetNode<CanvasLayer>("HUD").Visible = false;
        var presentation = new ArenaHud { Name = "Presentation" };
        AddChild(presentation);
        presentation.Configure(_runtime, this);
        Player.Controls.CanShoot = () => !RoundsEnabled || _runtime.CombatState.ReadEquipment(Player.CombatantId).Loadout.GetItem(EquipmentSlot.Weapon) is not null;
        ShowStarterSelection();
    }
}
