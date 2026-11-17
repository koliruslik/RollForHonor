#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using RollForHonor.Application.Combat.Models;
using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Combat.Effects.Models;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Resources.Models;

namespace RollForHonor.Godot.CombatDemo;

/// <summary>Hosts an editable two-sided combat workbench using the existing Application runtime.</summary>
public partial class CombatDemoScene : Node3D
{
    private static readonly ResourceId Mana = new("mana");
    private readonly Queue<string> _journalEntries = new();
    private GameBootstrap _runtime = null!;
    private Label _state = null!;
    private Label _result = null!;
    private Label _die = null!;
    private RichTextLabel _journal = null!;
    private Button _fire = null!;
    private Button _enemyFire = null!;
    private CheckButton _autoTicks = null!;
    private SpinBox _tickInterval = null!;
    private Button _nextTick = null!;
    private TimeSpan _nextTickAt;
    private OptionButton _deliveryChoice = null!;
    private IAttackDelivery[] _deliveries = null!;

    /// <summary>Gets how many scene-wide tick rounds the host has requested.</summary>
    public int TickRounds { get; private set; }

    /// <summary>Gets the player body after scene initialization.</summary>
    public DemoCombatant Source { get; private set; } = null!;
    /// <summary>Gets the enemy body after scene initialization.</summary>
    public DemoCombatant Target { get; private set; } = null!;
    /// <summary>Gets the editable player draft and next-attack configuration.</summary>
    public DemoStatsPanel SourceEditor { get; private set; } = null!;
    /// <summary>Gets the editable enemy draft and next-attack configuration.</summary>
    public DemoStatsPanel TargetEditor { get; private set; } = null!;
    /// <summary>Gets the latest physical contact, or null before a contact occurs.</summary>
    public AttackContactResult? LastContact { get; private set; }
    /// <summary>Gets the number of newly committed impacts observed by this scene.</summary>
    public int CommittedImpacts { get; private set; }

    /// <inheritdoc />
    public override void _Ready()
    {
        Node? ancestor = GetParent();
        while (ancestor is not null && ancestor is not GameBootstrap) ancestor = ancestor.GetParent();
        _runtime = ancestor as GameBootstrap ?? throw new InvalidOperationException("Run this scene through Bootstrap.tscn.");
        _deliveries = [new ProjectileDelivery(), new MeleeDelivery(() => _runtime.GameTime)];
        Source = GetNode<DemoCombatant>("Source");
        Target = GetNode<DemoCombatant>("Target");
        SourceEditor = GetNode<DemoStatsPanel>("HUD/PlayerPanel/Rows/Scroll/Stats");
        TargetEditor = GetNode<DemoStatsPanel>("HUD/EnemyPanel/Rows/Scroll/Stats");
        _state = GetNode<Label>("HUD/Panel/Rows/State");
        _result = GetNode<Label>("HUD/Panel/Rows/Result");
        _die = GetNode<Label>("HUD/Die");
        _journal = GetNode<RichTextLabel>("HUD/JournalPanel/Rows/Journal");
        _fire = GetNode<Button>("HUD/Panel/Rows/Fire");
        _enemyFire = GetNode<Button>("HUD/Panel/Rows/EnemyFire");
        _deliveryChoice = GetNode<OptionButton>("HUD/Panel/Rows/DeliveryControls/Delivery");
        _deliveryChoice.AddItem("Projectile", 0);
        _deliveryChoice.AddItem("Melee", 1);
        GetNode<CheckButton>("HUD/Panel/Rows/DeliveryControls/NearTarget").Toggled += near =>
            Target.GlobalPosition = near ? Source.GlobalPosition + Vector3.Right * 1.5f : new Vector3(4, 0.9f, 0);
        _autoTicks = GetNode<CheckButton>("HUD/Panel/Rows/TickControls/Auto");
        _tickInterval = GetNode<SpinBox>("HUD/Panel/Rows/TickControls/Interval");
        _nextTick = GetNode<Button>("HUD/Panel/Rows/NextTick");
        _nextTick.Pressed += () => NextTick();
        _autoTicks.Toggled += enabled => { _nextTick.Disabled = enabled; ResetTickDeadline(); };
        _tickInterval.ValueChanged += _ => ResetTickDeadline();
        var player = new CombatantSnapshot(Source.CombatantId, 100, 100, 0, 10, [], []);
        var enemy = new CombatantSnapshot(Target.CombatantId, 50, 50, 5, 12, [], []);
        _runtime.CombatState.AddCombatant(player, [new(Mana, 100)]);
        _runtime.CombatState.AddCombatant(enemy, [new(Mana, 100)]);
        SourceEditor.Configure("PLAYER", player, 5, 3);
        TargetEditor.Configure("ENEMY", enemy, 5, 3);
        SourceEditor.ApplyRequested += () => ApplyDraft();
        TargetEditor.ApplyRequested += () => ApplyDraft(true);
        _fire.Pressed += () => FireFromInput(false);
        _enemyFire.Pressed += () => FireFromInput(true);
        GetNode<Button>("HUD/Arena").Pressed += () => GetTree().ChangeSceneToFile("res://Bootstrap.tscn");
        GetNode<Button>("HUD/Panel/Rows/Restart").Pressed += () => GetTree().ReloadCurrentScene();
        GetNode<Button>("HUD/JournalPanel/Rows/Clear").Pressed += () => { _journalEntries.Clear(); _journal.Text = ""; };
        AppendJournal("Combat workbench ready. Draft state applies with Apply; weapon/effect selections affect the next launch.\nVampirism heals from actual opponent HP loss, rounded up. Next tick advances all living recipients; Auto uses game time.\n");
        RefreshState();
    }

    /// <summary>Applies explicit draft health/defense edits, preserving live resources, effects and cooldown receipts.</summary>
    /// <param name="enemy">True to edit the enemy; false for the player.</param>
    /// <returns>True on success; false when Domain validation rejects the draft without changing state.</returns>
    public bool ApplyDraft(bool enemy = false)
    {
        var actor = enemy ? Target : Source;
        try
        {
            var current = _runtime.CombatState.Read(actor.CombatantId, actor.CombatantId).Source;
            var edited = (enemy ? TargetEditor : SourceEditor).BuildState(current);
            _runtime.CombatState.UpdateCombatant(edited);
            AppendJournal($"[b]Edit {(enemy ? "Enemy" : "Player")}[/b]: HP {current.Health}/{current.MaxHealth} → {edited.Health}/{edited.MaxHealth}; armor {edited.Armor}; evasion {edited.Evasion}. Resources, statuses and cooldown retained.\n");
            RefreshState();
            return true;
        }
        catch (ArgumentException error)
        {
            AppendJournal($"[color=#ff7777]Draft rejected: {Escape(error.Message)}[/color]\n");
            return false;
        }
    }

    /// <summary>Requests a launch and spawns a visual only for an accepted attack.</summary>
    /// <param name="enemy">True for an enemy attack against the player; false for a player attack.</param>
    /// <returns>The actual launch status, including resource/cooldown refusals.</returns>
    public AttackStartResult Fire(bool enemy = false)
    {
        var source = enemy ? Target : Source;
        var target = enemy ? Source : Target;
        var editor = enemy ? TargetEditor : SourceEditor;
        var delivery = _deliveries[_deliveryChoice.GetSelectedId()];
        var definition = new AttackDefinition(delivery.Key, editor.BuildAttack(delivery.Tags),
            TimeSpan.FromSeconds(0.8), [new(Mana, 10)], delivery.Rules);
        var start = _runtime.Attacks.Start(Guid.NewGuid(), source.CombatantId, definition);
        AppendJournal($"[b]{(enemy ? "Enemy" : "Player")} launch[/b]: {start.Status}" +
            (start.Attack is not null ? $" | cost 10 mana | damage {definition.Attack.Damage.Single().Dice} + {definition.Attack.Damage.Single().FlatDamage}" : " | no new cost or attack visual") + "\n");
        if (start.Attack is { } attack)
        {
            var direction = (target.GlobalPosition - source.GlobalPosition).Normalized();
            var input = _runtime.CombatState.Read(source.CombatantId, target.CombatantId);
            delivery.Spawn(source, direction, attack,
                contact => ShowContact(contact, input, enemy, attack),
                contacted => input = _runtime.CombatState.Read(source.CombatantId, contacted.CombatantId));
            _result.Text = $"{(enemy ? "Enemy" : "Player")}: {(_deliveryChoice.GetSelectedId() == 0 ? "arrow" : "melee window")}";
        }
        else _result.Text = $"Launch: {start.Status}";
        RefreshState();
        return start;
    }

    /// <inheritdoc />
    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false, PhysicalKeycode: Key.F2 })
        {
            GetTree().ChangeSceneToFile("res://Bootstrap.tscn");
            GetViewport().SetInputAsHandled();
            return;
        }
        if (@event is InputEventKey { Pressed: true, Echo: false } key && key.PhysicalKeycode is Key.Space or Key.E)
        {
            FireFromInput(key.PhysicalKeycode == Key.E);
            GetViewport().SetInputAsHandled();
        }
    }

    /// <inheritdoc />
    public override void _Process(double delta)
    {
        if (GetTree().Paused) return;
        if (_autoTicks.ButtonPressed)
        {
            var now = _runtime.GameTime;
            var interval = TimeSpan.FromSeconds(_tickInterval.Value);
            while (now >= _nextTickAt)
            {
                _nextTickAt += interval;
                NextTick();
            }
        }
        RefreshState();
    }

    /// <summary>Ticks each periodic instance present on living scene participants at round start, one transaction per effect.</summary>
    /// <returns>The independent results; an empty list means no eligible statuses existed.</returns>
    /// <remarks>Replacement/removal or defeat before an individual tick is rechecked by Application. No launch cost or d20 runs.</remarks>
    public IReadOnlyList<PeriodicTickResult> NextTick()
    {
        TickRounds++;
        var pending = new[] { Source, Target }.Select(actor =>
            _runtime.CombatState.Read(actor.CombatantId, actor.CombatantId).Target)
            .Where(actor => !actor.IsDefeated)
            .SelectMany(actor => actor.Effects.Where(effect => effect.Definition is PeriodicDamageEffectDefinition)
                .Select(effect => (actor.Id, effect.Id))).ToArray();
        var results = new List<PeriodicTickResult>();
        AppendJournal($"\n[b]Tick round {TickRounds}[/b] | {pending.Length} periodic instances\n");
        foreach (var (targetId, effectId) in pending)
        {
            var result = _runtime.Ticks.Tick(targetId, effectId);
            results.Add(result);
            var sourceName = result.Request?.SourceId == Source.CombatantId ? "Player (captured)" : "Enemy (captured)";
            AppendJournal(DemoCombatJournal.FormatTick(result, sourceName, targetId == Source.CombatantId ? "Player" : "Enemy"));
        }
        RefreshState();
        return results;
    }

    private void ResetTickDeadline() => _nextTickAt = _runtime.GameTime + TimeSpan.FromSeconds(_tickInterval.Value);

    private void FireFromInput(bool enemy)
    {
        if (!(enemy ? _enemyFire : _fire).Disabled) Fire(enemy);
    }
    private void RefreshState()
    {
        var read = _runtime.CombatState.Read(Source.CombatantId, Target.CombatantId);
        var key = _deliveries[_deliveryChoice.GetSelectedId()].Key;
        var player = _runtime.CombatState.ReadForStart(Source.CombatantId, key, Guid.NewGuid());
        var enemy = _runtime.CombatState.ReadForStart(Target.CombatantId, key, Guid.NewGuid());
        var playerMana = player.Resources.Single(value => value.Resource == Mana).Amount;
        var enemyMana = enemy.Resources.Single(value => value.Resource == Mana).Amount;
        var playerCooldown = Math.Max(0, (player.ReadyAt - _runtime.GameTime).TotalSeconds);
        var enemyCooldown = Math.Max(0, (enemy.ReadyAt - _runtime.GameTime).TotalSeconds);
        _state.Text = $"Player: {read.Source.Health}/{read.Source.MaxHealth} HP | Mana {playerMana} | CD {playerCooldown:0.0}s\nEnemy: {read.Target.Health}/{read.Target.MaxHealth} HP | Mana {enemyMana} | CD {enemyCooldown:0.0}s";
        _fire.Disabled = read.Source.IsDefeated || read.Target.IsDefeated || playerMana < 10 || playerCooldown > 0;
        _enemyFire.Disabled = read.Source.IsDefeated || read.Target.IsDefeated || enemyMana < 10 || enemyCooldown > 0;
        Source.GetNode<Label3D>("Name").Text = $"Player\n{read.Source.Health}/{read.Source.MaxHealth} HP";
        Target.GetNode<Label3D>("Name").Text = $"Enemy\n{read.Target.Health}/{read.Target.MaxHealth} HP";
        GetNode<Label>("HUD/PlayerPanel/Rows/Live").Text = Live(read.Source);
        GetNode<Label>("HUD/EnemyPanel/Rows/Live").Text = Live(read.Target);
    }
    private static string Live(CombatantSnapshot actor) => $"Live HP {actor.Health}/{actor.MaxHealth} | Statuses: " +
        (actor.Effects.Count == 0 ? "none" : string.Join(", ", actor.Effects.Select(effect => DemoEffectCatalog.Name(effect.Definition.Id))));
    private void ShowContact(AttackContactResult contact, CombatStateRead input, bool enemy, AttackInstance attack)
    {
        LastContact = contact;
        AppendJournal(DemoCombatJournal.Format(contact, input, enemy ? "Enemy" : "Player", enemy ? "Player" : "Enemy", attack.Attack));
        if (contact.Impact is { IsCommitted: true, Outcome: ResolvedCombat resolved })
        {
            CommittedImpacts++;
            var target = resolved.Resolution.Result.Target;
            _die.Text = $"d20\n{target.AttackRoll?.Roll.Results.Single()}";
            _result.Text = $"{target.Outcome} ({(enemy ? "Player" : "Enemy")}) - see calculation below";
        }
        else _result.Text = $"Contact: {contact.Status} | Commit: {contact.Impact?.CommitStatus}";
        RefreshState();
    }
    private void AppendJournal(string text)
    {
        _journalEntries.Enqueue(text);
        while (_journalEntries.Count > 100) _journalEntries.Dequeue();
        _journal.Text = string.Concat(_journalEntries);
    }
    private static string Escape(string text) => text.Replace("[", "[lb]");
}
