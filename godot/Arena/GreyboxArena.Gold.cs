using System.Collections.Generic;
using System.Linq;
using Godot;
using RollForHonor.Application.Rewards;
using RollForHonor.Godot.Content.Loot;
using RollForHonor.Godot.Gameplay.Actors;
using RollForHonor.Infrastructure.Dice;
using RollForHonor.Domain.Stats;
using RollForHonor.Domain.Rewards;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Godot.Gameplay.Items;
using RollForHonor.Godot.UI.Hud;
using RollForHonor.Godot.UI.Localization;

namespace RollForHonor.Godot.Arena;

public partial class GreyboxArena
{
    /// <summary>Gets all uncollected gold representations, including rewards from previous rounds.</summary>
    public IReadOnlyList<WorldGoldDrop> GoldDrops => _goldDrops
        .Where(drop => GodotObject.IsInstanceValid(drop) && !drop.Collected)
        .ToArray();

    private readonly List<WorldGoldDrop> _goldDrops = [];
    private readonly HashSet<CombatantId> _representedGold = [];
    private readonly Queue<(GoldReward Reward, Vector3 Origin)> _pendingGold = new();
    private System.TimeSpan _nextGoldPlacement;

    /// <summary>Captures an already calculated defeat reward and places it on free nearby ground.</summary>
    /// <param name="sourceId">The registered defeated actor; duplicate calls retain its first reward.</param>
    /// <param name="amount">The already calculated positive gold quantity.</param>
    /// <param name="origin">The requested world-local drop origin.</param>
    /// <param name="rollContext">Optional matching captured generation context.</param>
    /// <exception cref="System.ArgumentException">Generation provenance does not match the amount.</exception>
    /// <returns>True when placed now; false when already represented, consumed or waiting for free ground.</returns>
    /// <exception cref="System.ArgumentNullException">The source is null.</exception>
    /// <exception cref="System.ArgumentOutOfRangeException">The amount is not positive.</exception>
    /// <exception cref="KeyNotFoundException">The source is not registered.</exception>
    /// <exception cref="System.InvalidOperationException">The source is still alive.</exception>
    /// <exception cref="System.OverflowException">The session version cannot advance.</exception>
    public bool PlaceGoldReward(CombatantId sourceId, long amount, Vector3 origin, LootGroupRoll rollContext = null)
    {
        var reward = _runtime.CombatState.CaptureGoldReward(sourceId, amount, rollContext);

        if (_runtime.CombatState.ReadGoldReward(sourceId) is null || !_representedGold.Add(sourceId))
        {
            return false;
        }

        return PlaceCapturedGold(reward, origin);
    }

    /// <summary>Lists captured gold within the living player's existing 2.5-unit pickup range.</summary>
    /// <returns>Reachable rewards in nearest-first order; empty after player defeat.</returns>
    public IReadOnlyList<WorldGoldDrop> NearbyGoldDrops() => PlayerIsAlive() ? GoldDrops
        .Where(drop => drop.GlobalPosition.DistanceSquaredTo(Player.GlobalPosition) <= 6.25f)
        .OrderBy(drop => drop.GlobalPosition.DistanceSquaredTo(Player.GlobalPosition))
        .ThenBy(drop => drop.LootId, System.StringComparer.Ordinal)
        .ToArray() : [];

    /// <summary>Collects one reachable gold reward without requiring any inventory cells.</summary>
    /// <param name="lootId">The selected gold-row identity.</param>
    /// <returns>True only when the wallet was newly credited.</returns>
    public bool PickUpGold(string lootId)
    {
        var drop = NearbyGoldDrops().FirstOrDefault(value => value.LootId == lootId);

        if (drop is null || GetTree().Paused)
        {
            return false;
        }

        return drop.TryCollect(sourceId =>
        {
            var status = new GoldPickupService(_runtime.CombatState).Collect(Player.CombatantId, sourceId);

            if (status == GoldPickupStatus.Collected)
            {
                var message = L.Format($"Picked up gold: {drop.Reward.Amount}");
                GetNode<ArenaHud>("Presentation").RecordAction(message);
                _pickupFeedback = message;
                _feedbackUntil = _runtime.GameTime + System.TimeSpan.FromSeconds(3);
            }

            return status;
        });
    }

    private void CreateMonsterLoot(Enemy enemy)
    {
        var loot = GameLootCatalog.Roll("sentinel", enemy.CombatantId, new RandomDiceRoller(_roundRandom),
            _runtime.CombatState.GetStats(Player.CombatantId).Get(new(StatType.Luck)));
        PlaceGoldReward(enemy.CombatantId, loot.Gold.GoldAmount, enemy.Position, loot.Gold);

        if (loot.Item is not null)
        {
            CreateDrop(loot.Item, enemy.Position);
        }
    }

    private bool PlaceCapturedGold(GoldReward reward, Vector3 origin)
    {
        if (!TryFindDropPosition(origin, out var position))
        {
            _pendingGold.Enqueue((reward, origin));
            return false;
        }

        var drop = new WorldGoldDrop { Name = $"Gold{reward.SourceId.Value:N}", Position = position };
        drop.Configure(reward);
        AddChild(drop);
        _goldDrops.Add(drop);

        return true;
    }

    private void PlacePendingGold()
    {
        _goldDrops.RemoveAll(drop => !GodotObject.IsInstanceValid(drop) || drop.Collected);

        if (_pendingGold.Count == 0 || _runtime.GameTime < _nextGoldPlacement)
        {
            return;
        }

        _nextGoldPlacement = _runtime.GameTime + System.TimeSpan.FromSeconds(1);
        var pending = _pendingGold.Dequeue();
        PlaceCapturedGold(pending.Reward, pending.Origin);
    }
}
