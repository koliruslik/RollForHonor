using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Stats;
using RollForHonor.Godot.Arena;
using RollForHonor.Godot.CombatDemo;
using RollForHonor.Godot.Gameplay.Combat;
using RollForHonor.Godot.Gameplay.Targeting;
using RollForHonor.Godot.UI.Inventory;
using RollForHonor.Godot.UI.Localization;

namespace RollForHonor.Godot.UI.Hud;

internal sealed class CombatHudPresenter
{
    private readonly GameBootstrap _runtime;
    private readonly GreyboxArena _arena;
    private readonly Control _design;
    private readonly CharacterPanel _character;
    private readonly ActionJournalPanel _history;
    private readonly ActorBars _actorBars;
    private readonly ArenaMinimap _map;
    private readonly ResourceOrb _health;
    private readonly ResourceOrb _mana;
    private readonly DiceRollControl _roll;
    private readonly Label _objective;
    private readonly Label _attack;
    private readonly Label _enemyName;
    private readonly Label _enemyAmount;
    private readonly Label _enemyState;
    private readonly ProgressBar _enemyHealth;
    private readonly Dictionary<CombatantId, Node3D> _bodies = new();

    internal CombatHudPresenter(GameBootstrap runtime, GreyboxArena arena, Control design,
        CharacterPanel character, ActionJournalPanel history)
    {
        _runtime = runtime;
        _arena = arena;
        _design = design;
        _character = character;
        _history = history;
        _actorBars = design.GetNode<ActorBars>("ActorBars");
        _map = design.GetNode<ArenaMinimap>("Minimap");
        _health = design.GetNode<ResourceOrb>("Health");
        _mana = design.GetNode<ResourceOrb>("Mana");
        _roll = design.GetNode<DiceRollControl>("ActionBar/RollValue");
        _objective = design.GetNode<Label>("Objective/ObjectiveText");
        _attack = design.GetNode<Label>("ActionBar/Recovery");
        _enemyName = design.GetNode<Label>("Enemy/TargetName");
        _enemyAmount = design.GetNode<Label>("Enemy/TargetAmount");
        _enemyState = design.GetNode<Label>("Enemy/TargetState");
        _enemyHealth = design.GetNode<ProgressBar>("Enemy/TargetHealth");
        _map.IsAlive = body => !runtime.CombatState.Read(arena.Player.CombatantId, ((ICombatBody)body).CombatantId).Target.IsDefeated;
        _map.IsSelected = body => arena.Player.GetRequiredComponent<CombatAwareness>().Selection.TargetId == ((ICombatBody)body).CombatantId;
        arena.ContactObserved += history.Record;
    }

    internal void RefreshParticipants()
    {
        _bodies.Clear();
        var bodies = new Node3D[] { _arena.Player }
            .Concat(_arena.Enemies)
            .Concat(_arena.AdminEnemies)
            .Concat(_arena.GetNode<Node3D>("Targets").GetChildren()
                .OfType<DemoCombatant>());

        foreach (var body in bodies)
        {
            _bodies[((ICombatBody)body).CombatantId] = body;
        }

        _actorBars.Configure(_runtime, _arena, _bodies.Values);
        _map.Configure(_arena.GetNode<Node3D>("Map"), _arena.Player, _arena.Opponent);
        _map.Participants = _bodies.Values
            .Where(body => body != _arena.Player)
            .ToArray();
    }

    internal bool Refresh()
    {
        var state = _runtime.CombatState;
        var id = _arena.Player.CombatantId;
        var read = state.Read(id, _arena.Opponent.CombatantId);
        var stats = state.GetStats(id);
        var launch = state.ReadForStart(id, "arena-shot", Guid.NewGuid());
        var mana = launch.Resources.Single().Amount;
        _health.UpdateValue(read.Source.Health, read.Source.MaxHealth, L.Text("HEALTH"));
        _mana.UpdateValue(mana, stats.Get(new StatKey(StatType.MaxMana)), L.Text("MANA"));
        RefreshTarget();
        L.SetText(_objective, read.Source.IsDefeated ? L.Text("You have fallen. Open the menu to begin again.") : read.Target.IsDefeated ? L.Text("The courtyard is yours.") : L.Text("Defeat the Sentinel"));
        _roll.UpdateValue(_history.LastRoll);
        L.SetText(_attack, launch.ReadyAt > _runtime.GameTime ? L.Text("Recovering") : "");
        _character.Refresh(stats, state.GetStatProfile(id).BaseValues, read.Source, mana);

        return !read.Source.IsDefeated;
    }

    internal void SelectTarget(InputEvent @event)
    {
        var point = @event is InputEventMouseButton mouse ? mouse.Position : _design.GetViewport().GetMousePosition();
        var camera = _arena.CameraRig.Camera;
        using var query = PhysicsRayQueryParameters3D.Create(camera.ProjectRayOrigin(point), camera.ProjectRayOrigin(point) + camera.ProjectRayNormal(point) * 200, 22);
        query.Exclude = [_arena.Player.GetRid()];
        var hit = _arena.GetWorld3D().DirectSpaceState.IntersectRay(query);
        var body = hit.Count > 0 ? hit["collider"].AsGodotObject() as ICombatBody : null;
        var selection = _arena.Player.GetRequiredComponent<CombatAwareness>().Selection;
        selection.Select(body?.CombatantId == selection.TargetId ? null : body?.CombatantId);
    }

    internal void LaunchPrimary()
    {
        var id = _arena.Player.GetRequiredComponent<CombatAwareness>().Selection.TargetId;
        var point = id is not null && _bodies.TryGetValue(id, out var body) ? ((ICombatBody)body).AimPoint
            : _arena.Player.GlobalPosition - _arena.Player.GetNode<Node3D>("Visual").GlobalBasis.Z * 10 + Vector3.Up;
        _arena.Player.GetRequiredComponent<ProjectileAttack>().RequestAttack(point);
    }

    internal void Dispose() => _arena.ContactObserved -= _history.Record;

    private void RefreshTarget()
    {
        var selection = _arena.Player.GetRequiredComponent<CombatAwareness>();
        var id = selection.Selection.TargetId;
        var panel = _design.GetNode<Panel>("Enemy");
        panel.Visible = id is not null && _bodies.ContainsKey(id) && !_runtime.CombatState.Read(_arena.Player.CombatantId, id).Target.IsDefeated;

        if (!panel.Visible)
        {
            return;
        }

        var target = _runtime.CombatState.Read(_arena.Player.CombatantId, id).Target;
        L.SetText(_enemyName, _arena.ActorName(_bodies[id]));
        _enemyHealth.MaxValue = target.MaxHealth;
        _enemyHealth.Value = target.Health;
        _enemyAmount.Text = $"{target.Health} / {target.MaxHealth}";
        L.SetText(_enemyState, selection.IsInCombat ? L.Text("IN COMBAT") : L.Text("SELECTED"));
    }
}
