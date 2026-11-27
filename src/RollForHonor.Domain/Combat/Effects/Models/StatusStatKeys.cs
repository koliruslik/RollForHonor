using RollForHonor.Domain.Stats;

namespace RollForHonor.Domain.Combat.Effects.Models;

/// <summary>Maps each production status to ordinary source statistics usable by equipment, buffs and future talents.</summary>
public sealed class StatusStatKeys
{
    /// <summary>Gets the reference-speed application chance.</summary>
    public StatType Chance { get; }

    /// <summary>Gets the stat whose positive value guarantees application independently of attack cadence.</summary>
    public StatType Guaranteed { get; }

    /// <summary>Gets the captured dice count.</summary>
    public StatType Dice { get; }

    /// <summary>Gets the coefficient multiplying configured duration.</summary>
    public StatType Duration { get; }

    /// <summary>Gets the coefficient multiplying captured tick damage.</summary>
    public StatType Damage { get; }

    /// <summary>Gets the coefficient multiplying configured tick frequency.</summary>
    public StatType TickRate { get; }

    /// <summary>Gets the coefficient multiplying configured movement slowing.</summary>
    public StatType Slow { get; }

    private static readonly IReadOnlyDictionary<StatusEffectKind, StatusStatKeys> Keys = Enum.GetValues<StatusEffectKind>()
        .ToDictionary(kind => kind, kind => new StatusStatKeys(kind));

    private static readonly HashSet<StatType> Multipliers = Keys.Values
        .SelectMany(keys => new[] { keys.Duration, keys.Damage, keys.TickRate, keys.Slow })
        .ToHashSet();

    private StatusStatKeys(StatusEffectKind kind)
    {
        var prefix = kind.ToString();
        Chance = Enum.Parse<StatType>($"{prefix}Chance");
        Guaranteed = Enum.Parse<StatType>($"{prefix}Guaranteed");
        Dice = Enum.Parse<StatType>($"{prefix}Dice");
        Duration = Enum.Parse<StatType>($"{prefix}DurationMultiplier");
        Damage = Enum.Parse<StatType>($"{prefix}DamageMultiplier");
        TickRate = Enum.Parse<StatType>($"{prefix}TickRateMultiplier");
        Slow = Enum.Parse<StatType>($"{prefix}SlowMultiplier");
    }

    /// <summary>Finds the stat categories associated with one defined status.</summary>
    /// <param name="kind">The status category.</param>
    /// <returns>Its source stat categories, including the optional guaranteed-application override.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The status category is undefined.</exception>
    public static StatusStatKeys For(StatusEffectKind kind)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        return Keys[kind];
    }

    /// <summary>Identifies neutral-one coefficients without treating application chances or dice as multipliers.</summary>
    /// <param name="type">A source stat category.</param>
    /// <returns>True for a production status duration, damage, frequency or slow coefficient.</returns>
    public static bool IsMultiplier(StatType type) => Multipliers.Contains(type);
}
