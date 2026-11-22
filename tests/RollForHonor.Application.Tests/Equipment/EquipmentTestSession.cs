using RollForHonor.Application.Combat.Models;
using RollForHonor.Application.Combat.Services;
using RollForHonor.Application.Equipment.Services;
using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Domain.Combat.Damage.Configuration;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Items.Configuration;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Domain.Items.Services;
using RollForHonor.Domain.Resources.Models;
using RollForHonor.Domain.Resources.Services;
using RollForHonor.Domain.Stats;
using RollForHonor.Infrastructure.Combat;
using RollForHonor.Infrastructure.Dice;

namespace RollForHonor.Application.Tests.Equipment;

internal sealed class EquipmentTestSession
{
    internal InMemoryCombatState State { get; } = new();
    internal CombatantId Owner { get; } = new(Guid.NewGuid());
    internal CombatantId Other { get; } = new(Guid.NewGuid());
    internal EquipmentService Service { get; }

    internal EquipmentTestSession(IEnumerable<StatModifier>? modifiers = null)
    {
        State.AddStatCombatant(Owner, Profile(modifiers));
        State.AddStatCombatant(Other, Profile());
        Service = new EquipmentService(State);
    }

    internal static StatProfile Profile(IEnumerable<StatModifier>? modifiers = null) => new(new StatBlock(
    [
        new(new(StatType.MaxHealth), 100),
        new(new(StatType.MaxMana), 100),
        new(new(StatType.Armor), 2),
        new(new(StatType.Evasion), 10),
        new(new(StatType.MovementSpeed), 6)
    ]), modifiers);

    internal static ItemInstance Item(string id, string tag, params ItemModifier[] modifiers) =>
        new ItemFactory(ItemRarityRules.Default, ItemStatRules.Default)
            .Create(id, new ItemDefinition("base", "Base", [tag], modifiers), ItemRarity.Common);

    internal AttackInitiationService CreateStarts()
    {
        var impacts = new CombatImpactService(State, new CombatSnapshotBuilder(),
            new BasicAttackPipelineFactory(new DamageMultipliers(1, 1, 1, 1)), () => new RandomDiceRoller(new MaximumRandom()));

        return new AttackInitiationService(State, new ResourceCostResolver(new AdditiveResourceCostPolicy()),
            new NoResourceCostModifiers(), impacts, () => TimeSpan.Zero);
    }

    internal static AttackDefinition Shot() => new("shot", new(new(1, 20), 0, NaturalOutcomeShiftRules.Standard,
        [new(new(1, 2), 8, DamageType.Fire)], [], AttackTags.Projectile), TimeSpan.FromSeconds(2),
        [new ResourceAmount(new ResourceId("mana"), 20)]);

    private sealed class MaximumRandom : Random
    {
        /// <inheritdoc />
        public override long NextInt64(long minValue, long maxValue) => maxValue - 1;
    }
}
