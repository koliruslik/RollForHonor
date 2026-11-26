using RollForHonor.Domain.Dice.Models;
using RollForHonor.Domain.Dice.Services;

namespace RollForHonor.Domain.Rewards;

/// <summary>Rolls each configured group independently and captures gold quantities only for gold outcomes.</summary>
public sealed class LootGroupRoller
{
    /// <summary>Produces one retained result per group with no retries or hidden item-generation rolls.</summary>
    /// <param name="groups">The unique, nonnull groups in their configured execution order.</param>
    /// <param name="luckBonus">The already calculated nonnegative group-selection bonus, independent of amount Luck.</param>
    /// <param name="dice">The injected deterministic/random roll source.</param>
    /// <param name="luck">Captured Luck for gold amounts only; it does not define the selection bonus.</param>
    /// <returns>Immutable group results; one group never suppresses another group's reward.</returns>
    /// <exception cref="ArgumentNullException">Groups or dice are null.</exception>
    /// <exception cref="ArgumentException">A group is null or identities repeat.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The Luck bonus is negative.</exception>
    /// <exception cref="OverflowException">A modified selection total cannot be represented.</exception>
    public IReadOnlyList<LootGroupRoll> Roll(IEnumerable<LootGroupDefinition> groups, int luckBonus, IDiceRoller dice, decimal luck = 0)
    {
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentNullException.ThrowIfNull(dice);
        ArgumentOutOfRangeException.ThrowIfNegative(luckBonus);
        var definitions = groups.ToArray();
        var context = new GenerationLuck(luck);

        if (definitions.Any(group => group is null)
            || definitions
                .Select(group => group.Id)
                .Distinct(StringComparer.Ordinal)
                .Count() != definitions.Length)
        {
            throw new ArgumentException("Loot group identities must be unique and nonnull.", nameof(groups));
        }

        return Array.AsReadOnly(definitions
            .Select(group => RollGroup(group, luckBonus, dice, context))
            .ToArray());
    }

    private static LootGroupRoll RollGroup(LootGroupDefinition group, int luckBonus, IDiceRoller dice, GenerationLuck context)
    {
        var selection = dice.Roll(group.Dice);
        var outcome = group.Select(checked(selection.Total + luckBonus));
        var quantity = outcome.Gold is null ? null : dice.Roll(new DiceFormula(1, 100));
        var amount = quantity is null ? 0 : outcome.Gold!.AmountAt(context.BoostD100(quantity.Total));

        return new LootGroupRoll(group.Id, selection, luckBonus, outcome, quantity, amount, context);
    }
}
