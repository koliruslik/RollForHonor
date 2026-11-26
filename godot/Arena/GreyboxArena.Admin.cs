using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Godot.Gameplay.Actors;
using RollForHonor.Godot.Gameplay.Targeting;
using RollForHonor.Godot.UI.Hud;
using RollForHonor.Godot.UI.Localization;

namespace RollForHonor.Godot.Arena;

public partial class GreyboxArena
{
    /// <summary>Gets sandbox enemies, excluded from the ten-enemy round counter.</summary>
    public IReadOnlyList<Enemy> AdminEnemies => _adminEnemies.AsReadOnly();

    private readonly List<Enemy> _adminEnemies = [];
    private readonly HashSet<RollForHonor.Domain.Combat.State.Models.CombatantId> _adminDefeated = [];
    private int _pendingAdminEnemies;

    /// <summary>Queues a currently catalogued Sentinel for physics-safe sandbox spawning.</summary>
    /// <returns>True if the living round session accepted the request.</returns>
    public bool QueueAdminEnemy()
    {
        if (!RoundsEnabled || !PlayerIsAlive())
        {
            return false;
        }

        _pendingAdminEnemies++;

        return true;
    }

    /// <summary>Places or queues a captured admin-created item on free ground near the player.</summary>
    /// <param name="item">The generated immutable instance.</param>
    /// <returns>True when placed; false when retained in the pending placement queue.</returns>
    public bool PlaceAdminItem(ItemInstance item) => CreateDrop(item, Player.Position);

    private void StepAdminEnemies()
    {
        if (_pendingAdminEnemies > 0 && PlayerIsAlive())
        {
            _pendingAdminEnemies--;
            var positions = PlanSpawns();

            if (positions.Count > 0)
            {
                var enemy = GD.Load<PackedScene>("res://Arena/Enemy.tscn").Instantiate<Enemy>();
                enemy.Name = $"AdminSentinel{_adminEnemies.Count + 1}";
                enemy.Position = positions[0];
                AddChild(enemy);
                ConfigureOpponent(enemy);
                _adminEnemies.Add(enemy);
                var hud = GetNode<ArenaHud>("Presentation");
                hud.RefreshParticipants();
                hud.RecordAction(L.Text("Admin: spawned Sentinel."));
            }
        }

        foreach (var enemy in _adminEnemies)
        {
            if (_runtime.CombatState.Read(enemy.CombatantId, enemy.CombatantId).Source.IsDefeated && _adminDefeated.Add(enemy.CombatantId))
            {
                CreateMonsterLoot(enemy);
                enemy.GetRequiredComponent<ActorState>().PhysicsStep(0);
                enemy.SetPhysicsProcess(false);
                enemy.CollisionLayer = enemy.CollisionMask = 0;
                enemy.Hide();
                GetNode<ArenaHud>("Presentation").RecordAction(L.Text("Defeated: sandbox Sentinel"));
            }
        }
    }
}
