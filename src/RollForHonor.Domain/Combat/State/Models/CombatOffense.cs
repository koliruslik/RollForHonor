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

    /// <summary>Gets the bonus on the independent critical d20.</summary>
    public int CriticalRollBonus { get; }

    /// <summary>Gets the independently resolved critical damage coefficient.</summary>
    public decimal CriticalMultiplier { get; }

    /// <summary>Gets copied, nonzero typed flat attack bonuses.</summary>
    public IReadOnlyList<DamageAmount> AddedDamage { get; }

    /// <summary>Gets copied extra dice rolled once per attack impact, excluded from periodic ticks.</summary>
    public IReadOnlyList<DamageComponent> AddedDice { get; }

    /// <summary>Gets fractional poison application chance, nonnegative at the configured reference attack speed.</summary>
    public decimal PoisonChance { get; }

    /// <summary>Gets the nonnegative dice count captured by each new poison stack.</summary>
    public int PoisonDice { get; }

    /// <summary>Gets captured production status inputs, including source talent and buff modifiers.</summary>
    public RollForHonor.Domain.Stats.StatBlock StatusStats { get; }

    private readonly ImmutableDictionary<DamageType, decimal> _multipliers;

    /// <summary>Captures resolved, nonnegative outgoing damage coefficients.</summary>
    /// <param name="attackModifier">The source's additional d20 modifier.</param>
    /// <param name="multipliers">Optional unique typed coefficients; omitted types use 1.</param>
    /// <param name="addedDamage">Optional nonnull typed flat attack bonuses; zero values are omitted.</param>
    /// <param name="addedDice">Optional nonnull extra attack components.</param>
    /// <param name="criticalRollBonus">The independent critical d20 bonus.</param>
    /// <param name="criticalMultiplier">The coefficient on a successful critical check.</param>
    /// <param name="poisonChance">Optional reference-speed poison chance; omitted reads statusStats.</param>
    /// <param name="poisonDice">Optional nonnegative poison dice count; omitted reads statusStats.</param>
    /// <param name="statusStats">Optional resolved status statistics, including per-status duration/damage/frequency/slow coefficients.</param>
    /// <exception cref="ArgumentException">Damage types are undefined/duplicated, coefficients are negative, or a flat bonus is null.</exception>
    public CombatOffense(int attackModifier = 0, IEnumerable<KeyValuePair<DamageType, decimal>>? multipliers = null,
        IEnumerable<DamageAmount>? addedDamage = null, IEnumerable<DamageComponent>? addedDice = null, int criticalRollBonus = 0, decimal criticalMultiplier = 2, decimal? poisonChance = null, int? poisonDice = null, RollForHonor.Domain.Stats.StatBlock? statusStats = null)
    {
        var values = multipliers?.ToImmutableDictionary() ?? ImmutableDictionary<DamageType, decimal>.Empty;

        if (values.Any(pair => !Enum.IsDefined(pair.Key) || pair.Value < 0))
        {
            throw new ArgumentException("Outgoing coefficients must be nonnegative and damage types defined.", nameof(multipliers));
        }

        var flat = addedDamage?.ToArray() ?? [];

        if (flat.Any(value => value is null))
        {
            throw new ArgumentException("Flat damage cannot contain null.", nameof(addedDamage));
        }

        AddedDamage = Array.AsReadOnly(flat
            .Where(value => value.Amount > 0)
            .ToArray());
        var extra = addedDice?.ToArray() ?? [];

        if (extra.Any(value => value is null))
        {
            throw new ArgumentException("Extra dice cannot contain null.", nameof(addedDice));
        }

        AddedDice = Array.AsReadOnly(extra);
        CriticalRollBonus = criticalRollBonus;
        CriticalMultiplier = Math.Max(1, criticalMultiplier);
        PoisonChance = Math.Max(0, poisonChance ?? statusStats?.Get(new(RollForHonor.Domain.Stats.StatType.PoisonChance)) ?? 0);
        PoisonDice = Math.Max(0, poisonDice ?? checked((int)decimal.Floor(statusStats?.Get(new(RollForHonor.Domain.Stats.StatType.PoisonDice)) ?? 0)));
        StatusStats = (statusStats ?? new())
            .With(new(RollForHonor.Domain.Stats.StatType.PoisonChance), PoisonChance)
            .With(new(RollForHonor.Domain.Stats.StatType.PoisonDice), PoisonDice);
        AttackModifier = attackModifier;
        _multipliers = values;
    }

    /// <summary>Reads one already-combined global and typed damage coefficient.</summary>
    /// <param name="type">The damage component's category.</param>
    /// <returns>The resolved multiplier, or 1 for an omitted type.</returns>
    public decimal DamageMultiplier(DamageType type) => _multipliers.GetValueOrDefault(type, 1);
}
