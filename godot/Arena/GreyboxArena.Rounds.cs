using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using RollForHonor.Application.Items;
using RollForHonor.Application.Resources;
using RollForHonor.Domain.Stats;
using RollForHonor.Domain.Items.Inventory.Models;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Godot.Gameplay.Actors;
using RollForHonor.Godot.Gameplay.Items;
using RollForHonor.Godot.Gameplay.Projectiles;
using RollForHonor.Godot.Gameplay.Targeting;
using RollForHonor.Godot.UI.Hud;
using RollForHonor.Godot.UI.Localization;

namespace RollForHonor.Godot.Arena;

public partial class GreyboxArena
{
    /// <summary>The exact enemy count in each playable round.</summary>
    public const int EnemiesPerRound = 10;

    /// <summary>The maximum number of living enemies admitted together.</summary>
    public const int EnemiesPerPair = 2;

    /// <summary>Gets or sets whether the inherited arena hosts rounds instead of the single-enemy test fixture.</summary>
    [Export]
    public bool RoundsEnabled { get; set; }

    /// <summary>Gets or sets a reproducible spawn seed; zero chooses a fresh seed at scene startup.</summary>
    [Export]
    public int SpawnSeed { get; set; }

    /// <summary>Gets the one-based round number, or zero before its first physics-safe spawn.</summary>
    public int RoundNumber { get; private set; }

    /// <summary>Gets whether every round enemy was defeated while the player remained alive.</summary>
    public bool RoundComplete { get; private set; }

    /// <summary>Gets the distinct defeated enemies counted in this round.</summary>
    public int DefeatedCount => _defeated.Count;

    /// <summary>Gets the current round's actors, including defeated bodies.</summary>
    public IReadOnlyList<Enemy> Enemies => _enemies.AsReadOnly();

    /// <summary>Gets remaining uncollected drops, including those from previous rounds.</summary>
    public IReadOnlyList<WorldItemDrop> Drops => _drops
        .Where(drop => GodotObject.IsInstanceValid(drop) && !drop.Collected)
        .ToArray();

    /// <summary>Gets the current short pickup feedback in the active language.</summary>
    public string PickupFeedback => _runtime.GameTime < _feedbackUntil ? L.Refresh(_pickupFeedback) : "";

    private TimeSpan _lastManaRecovery;
    private decimal _healthRecoveryFraction;

    private readonly List<Enemy> _enemies = [];
    private readonly List<WorldItemDrop> _drops = [];
    private readonly HashSet<CombatantId> _defeated = [];
    private Random _roundRandom;
    private bool _pendingRound;
    private string _pickupFeedback = "";
    private TimeSpan _feedbackUntil;

    /// <inheritdoc />
    public override void _PhysicsProcess(double delta)
    {
        if (!RoundsEnabled)
        {
            return;
        }

        PlacePendingDrops();
        PlacePendingGold();
        StepAdminEnemies();
        StepRecovery();

        _drops.RemoveAll(drop => !GodotObject.IsInstanceValid(drop) || drop.Collected);

        if (_pendingRound && PlayerIsAlive())
        {
            SpawnRound();
        }

        foreach (var enemy in _enemies)
        {
            if (!_runtime.CombatState.Read(enemy.CombatantId, enemy.CombatantId).Source.IsDefeated || !_defeated.Add(enemy.CombatantId))
            {
                continue;
            }

            GetNode<ArenaHud>("Presentation").RecordAction(L.Format($"Defeated: {ActorName(enemy)}"));
            CreateMonsterLoot(enemy);
            enemy.GetRequiredComponent<ActorState>().PhysicsStep(0);
            enemy.SetPhysicsProcess(false);
            enemy.CollisionLayer = enemy.CollisionMask = 0;
            enemy.Hide();
        }

        if (RoundNumber > 0 && _enemies.Count < EnemiesPerRound && _defeated.Count == _enemies.Count && PlayerIsAlive())
        {
            SpawnPair();
        }

        if (!RoundComplete && RoundNumber > 0 && _defeated.Count == EnemiesPerRound && PlayerIsAlive())
        {
            RoundComplete = true;
            StopProjectiles();
            Player.GetRequiredComponent<CombatAwareness>().Selection.Reset();
        }

        StepAutoGoldPickup();
    }

    /// <summary>Queues the next round after unlimited free time, preserving equipment, resources and uncollected loot.</summary>
    /// <returns>True if a completed living session accepted the request; false during battle or after defeat.</returns>
    public bool StartNextRound()
    {
        if (!RoundsEnabled || !RoundComplete || _pendingRound || !PlayerIsAlive())
        {
            return false;
        }

        RoundComplete = false;
        _pendingRound = true;

        return true;
    }

    /// <summary>Lists uncollected items within 2.5 world units in a stable nearest-first order.</summary>
    /// <returns>The currently reachable items, or an empty list for defeated players.</returns>
    public IReadOnlyList<WorldItemDrop> NearbyDrops() => PlayerIsAlive() ? Drops
        .Where(drop => drop.GlobalPosition.DistanceSquaredTo(Player.GlobalPosition) <= 6.25f)
        .OrderBy(drop => drop.GlobalPosition.DistanceSquaredTo(Player.GlobalPosition))
        .ThenBy(drop => drop.Item.Id, StringComparer.Ordinal)
        .ToArray() : [];

    /// <summary>Finds the nearest uncollected item within pickup range of a living player.</summary>
    /// <returns>The nearby drop, or null when none is available.</returns>
    public WorldItemDrop NearestDrop() => NearbyDrops().FirstOrDefault();

    /// <summary>Picks the nearby-menu selection, falling back to a reachable item or gold reward.</summary>
    /// <returns>True when item ownership or gold credit was published.</returns>
    public bool PickUpNearest() => PickUpLoot(GetNode<ArenaHud>("Presentation").SelectedLootId ?? NearestDrop()?.Item.Id ?? NearbyGoldDrops().FirstOrDefault()?.LootId);

    /// <summary>Routes a nearby-menu selection to captured item or gold collection.</summary>
    /// <param name="lootId">The selected world-loot identity.</param>
    /// <returns>True only when a new item acquisition or gold collection succeeded.</returns>
    public bool PickUpLoot(string lootId) => GoldDrops.Any(drop => drop.LootId == lootId)
        ? PickUpGold(lootId)
        : PickUpItem(lootId);

    /// <summary>Attempts a specific nearby pickup without consuming or rerolling rejected drops.</summary>
    /// <param name="itemId">The selected world instance.</param>
    /// <param name="destination">Optional exact top-left bag cell for native dragging.</param>
    /// <returns>True only after successful ownership publication.</returns>
    public bool PickUpItem(string itemId, InventoryPosition? destination = null)
    {
        var drop = NearbyDrops().FirstOrDefault(value => value.Item.Id == itemId);

        if (drop is null)
        {
            return false;
        }

        return drop.TryCollect(item =>
        {
            var result = GetNode<ArenaHud>("Presentation").AcquireItem(item, destination);
            _pickupFeedback = result == ItemAcquisitionStatus.Acquired
                ? L.Format($"Picked up: {L.Text(item.Base.Name)}")
                : result == ItemAcquisitionStatus.InventoryFull ? L.Text("Inventory full. The item remains on the ground.")
                    : L.Text("Pickup could not be completed. Try again.");
            _feedbackUntil = _runtime.GameTime + TimeSpan.FromSeconds(3);

            return result == ItemAcquisitionStatus.Acquired;
        });
    }

    /// <summary>Releases an owned item onto free nearby ground, preserving its exact captured instance.</summary>
    /// <param name="itemId">The owned bag or equipped identity.</param>
    /// <returns>True when one item left ownership and entered the world; false if no safe position or valid release exists.</returns>
    public bool DiscardItem(string itemId)
    {
        if (!TryFindDropPosition(Player.Position, out var position))
        {
            return false;
        }

        var item = new ItemReleaseService(_runtime.CombatState).Release(Player.CombatantId, itemId, GetNode<ArenaHud>("Presentation").ReadBagLayout());

        if (item is null)
        {
            return false;
        }

        AddDropAt(item, position);
        GetNode<ArenaHud>("Presentation").RecordAction(L.Format($"Dropped: {L.Text(item.Base.Name)}"));

        return true;
    }

    /// <summary>Gets a localized actor label while leaving scene node identities unchanged.</summary>
    /// <param name="actor">The player, enemy or authored training target.</param>
    /// <returns>A localized name, including the current round enemy number.</returns>
    public string ActorName(Node3D actor)
    {
        var index = actor is Enemy enemy ? _enemies.IndexOf(enemy) : -1;

        return RoundsEnabled && index >= 0 ? L.Format($"Sentinel {index + 1}") : L.Text(actor.Name);
    }

    private void StepRecovery()
    {
        var elapsed = _runtime.GameTime - _lastManaRecovery;

        if (elapsed < TimeSpan.FromSeconds(1))
        {
            return;
        }

        var state = _runtime.CombatState;
        var stats = state.GetStats(Player.CombatantId);
        new ManaRecoveryService(state).Recover(Player.CombatantId, elapsed, stats.Get(new(StatType.ManaRecovery)));
        var actor = state.Read(Player.CombatantId, Player.CombatantId).Source;

        if (actor.IsDefeated || actor.Health == actor.MaxHealth)
        {
            _healthRecoveryFraction = 0;
        }
        else
        {
            _healthRecoveryFraction += stats.Get(new(StatType.HealthRecovery)) * (decimal)elapsed.Ticks / TimeSpan.TicksPerSecond;
            var whole = decimal.Truncate(_healthRecoveryFraction);
            _healthRecoveryFraction -= whole;
            state.RestoreHealth(Player.CombatantId, (int)Math.Min(int.MaxValue, whole));
        }

        _lastManaRecovery = _runtime.GameTime;
    }

    private bool PlayerIsAlive() => !_runtime.CombatState.Read(Player.CombatantId, Player.CombatantId).Source.IsDefeated;

    private void SpawnRound()
    {
        _pendingRound = false;
        StopProjectiles();
        var first = RoundNumber == 0 ? Opponent : null;

        if (first is null)
        {
            foreach (var previous in _enemies)
            {
                previous.CollisionLayer = previous.CollisionMask = 0;
                previous.SetPhysicsProcess(false);
                previous.QueueFree();
            }
        }

        _enemies.Clear();
        _defeated.Clear();
        RoundNumber++;
        RoundComplete = false;
        Player.GetRequiredComponent<CombatAwareness>().Selection.Reset();
        SpawnPair(first);
    }

    private void SpawnPair(Enemy first = null)
    {
        var positions = PlanSpawns();

        if (positions.Count != EnemiesPerPair)
        {
            throw new InvalidOperationException("The authored courtyard could not provide two clear enemy spawns.");
        }

        var scene = GD.Load<PackedScene>("res://Arena/Enemy.tscn");

        for (var index = 0; index < EnemiesPerPair; index++)
        {
            var enemy = index == 0 && first is not null ? first : scene.Instantiate<Enemy>();

            if (enemy != first)
            {
                enemy.Name = $"Round{RoundNumber}Enemy{_enemies.Count + 1}";
                enemy.Position = positions[index];
                AddChild(enemy);
                ConfigureOpponent(enemy);
            }

            enemy.Position = positions[index];
            _enemies.Add(enemy);
        }

        Opponent = _enemies.First(enemy => !_defeated.Contains(enemy.CombatantId));
        GetNode<ArenaHud>("Presentation").RefreshParticipants();
    }

    private List<Vector3> PlanSpawns()
    {
        var positions = new List<Vector3>();
        var exclusions = new global::Godot.Collections.Array<Rid>();
        using var shape = new CapsuleShape3D { Radius = 0.65f, Height = 1.8f };

        for (var attempt = 0; attempt < 512 && positions.Count < EnemiesPerPair; attempt++)
        {
            TrySpawnPoint(new Vector3(-29 + (float)_roundRandom.NextDouble() * 24, 0, 11 + (float)_roundRandom.NextDouble() * 23), positions, exclusions, shape);
        }

        for (var z = 12; z <= 34 && positions.Count < EnemiesPerPair; z += 3)
        {
            for (var x = -28; x <= -5 && positions.Count < EnemiesPerPair; x += 3)
            {
                TrySpawnPoint(new Vector3(x, 0, z), positions, exclusions, shape);
            }
        }

        return positions;
    }

    private void TrySpawnPoint(Vector3 point, List<Vector3> positions, global::Godot.Collections.Array<Rid> exclusions, CapsuleShape3D shape)
    {
        if (point.DistanceSquaredTo(Player.Position) < 49 || positions.Any(existing => existing.DistanceSquaredTo(point) < 10))
        {
            return;
        }

        var space = GetWorld3D().DirectSpaceState;
        using var floorQuery = PhysicsRayQueryParameters3D.Create(point + Vector3.Up * 8, point + Vector3.Down * 2, 16);
        var floor = space.IntersectRay(floorQuery);

        if (floor.Count == 0 || floor["position"].AsVector3().Y > 0.2f || floor["normal"].AsVector3().Y < 0.98f)
        {
            return;
        }

        point.Y = floor["position"].AsVector3().Y + 0.05f;
        using var clearance = new PhysicsShapeQueryParameters3D
        {
            Shape = shape,
            Transform = new Transform3D(Basis.Identity, point + Vector3.Up * 0.95f),
            CollisionMask = 22,
            Exclude = exclusions
        };

        if (space.IntersectShape(clearance, 1).Count == 0)
        {
            positions.Add(point);
        }
    }

    private void StopProjectiles()
    {
        foreach (var projectile in GetChildren().OfType<AirborneProjectile>())
        {
            projectile.Cancel();
        }
    }
}
