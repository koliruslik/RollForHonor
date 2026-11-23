using System.Collections.Immutable;
using RollForHonor.Application.Combat.Services;
using RollForHonor.Application.Equipment.Models;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Items.Equipment.Configuration;
using RollForHonor.Domain.Items.Equipment.Models;
using RollForHonor.Domain.Items.Equipment.Services;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Domain.Resources.Models;
using RollForHonor.Domain.Stats;

namespace RollForHonor.Infrastructure.Combat;

public sealed partial class InMemoryCombatState
{
    private readonly EquipmentRules _equipmentRules;

    /// <summary>Creates an isolated session with optional immutable equipment compatibility rules.</summary>
    /// <param name="equipmentRules">Optional slot and hand pairing rules; absent uses the default equipment rules.</param>
    public InMemoryCombatState(EquipmentRules? equipmentRules = null)
    {
        _equipmentRules = equipmentRules ?? EquipmentRules.Default;
    }

    /// <summary>Registers canonical ownership for a stat-managed actor without equipping or applying item bonuses.</summary>
    /// <param name="ownerId">The registered stat-managed owner.</param>
    /// <param name="item">The immutable owned instance, unique across this session.</param>
    /// <exception cref="ArgumentNullException">An input is null.</exception>
    /// <exception cref="KeyNotFoundException">The actor is not stat-managed.</exception>
    /// <exception cref="ArgumentException">The instance ID is already owned anywhere in this session.</exception>
    /// <exception cref="OverflowException">The session version cannot advance.</exception>
    public void RegisterOwnedItem(CombatantId ownerId, ItemInstance item)
    {
        ArgumentNullException.ThrowIfNull(ownerId);
        ArgumentNullException.ThrowIfNull(item);

        lock (_gate)
        {
            var equipment = _data.Equipment[ownerId];
            EnsureUnowned(item.Id);
            var updated = equipment with { OwnedItems = equipment.OwnedItems.Add(item.Id, item) };
            _data = _data with
            {
                Version = checked(_data.Version + 1),
                Equipment = _data.Equipment.SetItem(ownerId, updated)
            };
        }
    }

    /// <inheritdoc />
    public EquipmentStateRead ReadEquipment(CombatantId ownerId)
    {
        ArgumentNullException.ThrowIfNull(ownerId);

        lock (_gate)
        {
            var equipment = _data.Equipment[ownerId];

            return new EquipmentStateRead(_data.Version, ownerId, equipment.OwnedItems.Values,
                equipment.Loadout, equipment.NonEquipmentProfile, _equipmentRules);
        }
    }

    /// <inheritdoc />
    public EquipmentChangeStatus TryCommitEquipment(EquipmentChangeProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);

        lock (_gate)
        {
            if (proposal.ExpectedVersion != _data.Version)
            {
                return EquipmentChangeStatus.VersionConflict;
            }

            var equipment = _data.Equipment[proposal.OwnerId];
            ValidateLoadout(equipment, proposal.Loadout);

            if (SameLoadout(equipment.Loadout, proposal.Loadout))
            {
                return EquipmentChangeStatus.Unchanged;
            }

            _data = ApplyEquipmentStats(_data, proposal.OwnerId, equipment.NonEquipmentProfile, proposal.Loadout);

            return EquipmentChangeStatus.Applied;
        }
    }

    /// <summary>Replaces only non-equipment inputs and atomically recomposes all currently equipped item contributions.</summary>
    /// <param name="ownerId">The registered stat-managed actor.</param>
    /// <param name="profile">Base values and other-source modifiers, excluding equipment contributions.</param>
    /// <remarks>Read these inputs through ReadEquipment.NonEquipmentProfile, not the composed GetStatProfile result. Current HP/mana clamp downward only.</remarks>
    /// <exception cref="ArgumentNullException">An input is null.</exception>
    /// <exception cref="KeyNotFoundException">The actor is not stat-managed.</exception>
    /// <exception cref="OverflowException">Stat arithmetic, projection or the session version overflows.</exception>
    public void UpdateNonEquipmentStats(CombatantId ownerId, StatProfile profile)
    {
        ArgumentNullException.ThrowIfNull(ownerId);
        ArgumentNullException.ThrowIfNull(profile);

        lock (_gate)
        {
            _data = ApplyEquipmentStats(_data, ownerId, profile, _data.Equipment[ownerId].Loadout);
        }
    }

    private void EnsureUnowned(string itemId)
    {
        if (_data.Equipment.Values.Any(equipment => equipment.OwnedItems.ContainsKey(itemId)))
        {
            throw new ArgumentException("The item instance is already owned in this session.", nameof(itemId));
        }
    }

    private void ValidateLoadout(EquipmentInventory equipment, EquipmentLoadout loadout)
    {
        if (loadout.Slots.Any(pair => !equipment.OwnedItems.TryGetValue(pair.Value.Id, out var canonical)
            || !ReferenceEquals(pair.Value, canonical) || !_equipmentRules.CanEquip(loadout, pair.Value, pair.Key)))
        {
            throw new InvalidOperationException("Equipment must contain canonical owned items in compatible slots.");
        }
    }

    private static bool SameLoadout(EquipmentLoadout first, EquipmentLoadout second) => first.Slots
        .OrderBy(pair => pair.Key)
        .SequenceEqual(second.Slots.OrderBy(pair => pair.Key));

    private static SessionData ApplyEquipmentStats(SessionData data, CombatantId ownerId,
        StatProfile nonEquipmentProfile, EquipmentLoadout loadout)
    {
        var profile = new EquipmentStatComposer().Compose(nonEquipmentProfile, loadout);
        var stats = new StatCalculator().Calculate(profile);
        var actor = new CombatStatProjector().Project(data.Actors[ownerId], stats);
        var resources = ClampMana(data.Resources[ownerId], stats);
        var equipment = data.Equipment[ownerId] with { NonEquipmentProfile = nonEquipmentProfile, Loadout = loadout };

        return data with
        {
            Version = checked(data.Version + 1),
            Actors = data.Actors.SetItem(ownerId, actor),
            Resources = data.Resources.SetItem(ownerId, resources),
            Profiles = data.Profiles.SetItem(ownerId, profile),
            Stats = data.Stats.SetItem(ownerId, stats),
            Equipment = data.Equipment.SetItem(ownerId, equipment)
        };
    }

    private static ImmutableDictionary<ResourceId, decimal> ClampMana(ImmutableDictionary<ResourceId, decimal> resources, StatBlock stats)
    {
        var mana = new ResourceId("mana");
        var current = resources.GetValueOrDefault(mana);
        var maximum = stats.Get(new StatKey(StatType.MaxMana));

        return resources.SetItem(mana, Math.Min(current, maximum));
    }

    private sealed record EquipmentInventory(StatProfile NonEquipmentProfile, EquipmentLoadout Loadout,
        ImmutableDictionary<string, ItemInstance> OwnedItems);
}
