using System.Collections.Immutable;
using RollForHonor.Domain.Combat.Damage.Models;

namespace RollForHonor.Domain.Combat.State.Models;

/// <summary>Captures only resolved offensive inputs consumed by combat, without character definitions or item references.</summary>
public sealed class CombatOffense
{
    /// <summary>Gets a neutral offensive view used by existing fixtures.</summary>
    public static CombatOffense Neutral { get; } = new();

    /// <summary>Gets the additive d20 modifier, separate from attack-specific modifiers.</summary>
    public int AttackModifier { get; }

    private readonly ImmutableDictionary<DamageType, decimal> _multipliers;

    /// <summary>Captures resolved, nonnegative outgoing damage coefficients.</summary>
    /// <param name="attackModifier">The source's additional d20 modifier.</param>
    /// <param name="multipliers">Optional unique typed coefficients; omitted types use 1.</param>
    /// <exception cref="ArgumentException">Damage types are undefined/duplicated or coefficients are negative.</exception>
    public CombatOffense(int attackModifier = 0, IEnumerable<KeyValuePair<DamageType, decimal>>? multipliers = null)
    {
        var values = multipliers?.ToImmutableDictionary() ?? ImmutableDictionary<DamageType, decimal>.Empty;

        if (values.Any(pair => !Enum.IsDefined(pair.Key) || pair.Value < 0))
        {
            throw new ArgumentException("Outgoing coefficients must be nonnegative and damage types defined.", nameof(multipliers));
        }

        AttackModifier = attackModifier;
        _multipliers = values;
    }

    /// <summary>Reads one already-combined global and typed damage coefficient.</summary>
    /// <param name="type">The damage component's category.</param>
    /// <returns>The resolved multiplier, or 1 for an omitted type.</returns>
    public decimal DamageMultiplier(DamageType type) => _multipliers.GetValueOrDefault(type, 1);
}
