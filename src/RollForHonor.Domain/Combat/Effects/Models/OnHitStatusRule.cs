using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Stats;

namespace RollForHonor.Domain.Combat.Effects.Models;

/// <summary>Stores content-authored periodic balance and captures source modifiers when a status is first applied.</summary>
public sealed class OnHitStatusRule
{
    /// <summary>Gets the status category and corresponding source stat keys.</summary>
    public StatusEffectKind Kind { get; }

    /// <summary>Gets the stable status definition identity.</summary>
    public EffectDefinitionId Id { get; }

    /// <summary>Gets the source stat categories used by this rule.</summary>
    public StatusStatKeys Stats { get; }

    /// <summary>Gets the number of sides in each captured damage die.</summary>
    public int DiceSides { get; }

    /// <summary>Gets signed flat damage added for each captured die, before scaling.</summary>
    public int FlatDamagePerDie { get; }

    /// <summary>Gets the configured damage category; Pure bypasses armor and resistances.</summary>
    public DamageType DamageType { get; }

    /// <summary>Gets unmodified status duration.</summary>
    public TimeSpan Duration { get; }

    /// <summary>Gets unmodified time between ticks, including the delay before the first tick.</summary>
    public TimeSpan TickInterval { get; }

    /// <summary>Gets the attack frequency at which authored chance values apply.</summary>
    public decimal ReferenceAttacksPerSecond { get; }

    /// <summary>Gets the configured probability die precision.</summary>
    public int ChanceRollSides { get; }

    /// <summary>Gets whether a successful reapplication refreshes an existing instance without replacing its captured parameters.</summary>
    public bool RefreshDurationOnly { get; }

    /// <summary>Gets whether outgoing typed/global source damage bonuses affect ticks.</summary>
    public bool ScaleWithSource { get; }

    /// <summary>Gets the configured movement-speed reduction fraction.</summary>
    public decimal MovementSlow { get; }

    /// <summary>Creates a validated rule without embedding gameplay balance defaults.</summary>
    /// <param name="kind">A defined production status.</param>
    /// <param name="id">Its stable definition identity.</param>
    /// <param name="diceSides">At least two sides per damage die.</param>
    /// <param name="flatDamagePerDie">Signed flat damage per die.</param>
    /// <param name="damageType">A defined damage category.</param>
    /// <param name="duration">Positive unmodified duration.</param>
    /// <param name="tickInterval">Positive unmodified tick interval.</param>
    /// <param name="referenceAttacksPerSecond">Positive reference attack frequency.</param>
    /// <param name="chanceRollSides">At least two faces for probability resolution.</param>
    /// <param name="refreshDurationOnly">Whether repeated applications refresh one existing instance.</param>
    /// <param name="scaleWithSource">Whether source hit-damage bonuses affect ticks.</param>
    /// <param name="movementSlow">Unmodified slow fraction from zero through one.</param>
    /// <exception cref="ArgumentNullException">The identity is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A category or numeric bound is invalid.</exception>
    public OnHitStatusRule(StatusEffectKind kind, EffectDefinitionId id, int diceSides, int flatDamagePerDie,
        DamageType damageType, TimeSpan duration, TimeSpan tickInterval, decimal referenceAttacksPerSecond,
        int chanceRollSides, bool refreshDurationOnly, bool scaleWithSource, decimal movementSlow)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (!Enum.IsDefined(kind) || !Enum.IsDefined(damageType) || diceSides < 2 || chanceRollSides < 2
            || duration <= TimeSpan.Zero || tickInterval <= TimeSpan.Zero || referenceAttacksPerSecond <= 0
            || movementSlow is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(kind), "Status content contains invalid bounds.");
        }

        Kind = kind;
        Id = id;
        Stats = StatusStatKeys.For(kind);
        DiceSides = diceSides;
        FlatDamagePerDie = flatDamagePerDie;
        DamageType = damageType;
        Duration = duration;
        TickInterval = tickInterval;
        ReferenceAttacksPerSecond = referenceAttacksPerSecond;
        ChanceRollSides = chanceRollSides;
        RefreshDurationOnly = refreshDurationOnly;
        ScaleWithSource = scaleWithSource;
        MovementSlow = movementSlow;
    }

    /// <summary>Normalizes a source's reference chance by the accepted attack's actual cadence.</summary>
    /// <param name="source">The current immutable offensive snapshot.</param>
    /// <param name="attacksPerSecond">Positive accepted frequency, or null for cadence-free fixtures at reference speed.</param>
    /// <returns>One for a positive guaranteed-application override; otherwise the probability capped after normalization.</returns>
    /// <exception cref="ArgumentNullException">The source is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A supplied frequency is not positive.</exception>
    public decimal Chance(CombatOffense source, decimal? attacksPerSecond)
    {
        ArgumentNullException.ThrowIfNull(source);
        var speed = attacksPerSecond ?? ReferenceAttacksPerSecond;

        if (speed <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(attacksPerSecond));
        }

        return source.StatusStats.Get(new(Stats.Guaranteed)) > 0 ? 1
            : Math.Clamp(source.StatusStats.Get(new(Stats.Chance)) / speed * ReferenceAttacksPerSecond, 0, 1);
    }

    /// <summary>Captures this application using current source stats while keeping content and future modifications independent.</summary>
    /// <param name="source">The current offensive state, including ordinary talent/buff stat modifiers.</param>
    /// <returns>A captured periodic definition, or null when dice, duration or tick frequency disable application.</returns>
    /// <exception cref="ArgumentNullException">The source is null.</exception>
    /// <exception cref="OverflowException">Modified timing or dice exceed supported bounds.</exception>
    public PeriodicDamageEffectDefinition? Capture(CombatOffense source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var stats = source.StatusStats;
        var count = checked((int)decimal.Floor(stats.Get(new(Stats.Dice))));
        var duration = Duration.Ticks * stats.Get(new(Stats.Duration));
        var rate = stats.Get(new(Stats.TickRate));

        if (count <= 0 || duration < 1 || rate <= 0)
        {
            return null;
        }

        var interval = TimeSpan.FromTicks(checked((long)Math.Max(1, TickInterval.Ticks / rate)));
        var lifetime = TimeSpan.FromTicks(checked((long)duration));
        var ticks = checked((int)Math.Max(1, decimal.Ceiling((decimal)lifetime.Ticks / interval.Ticks)));
        return new(Id, [new(new(count, DiceSides), checked(FlatDamagePerDie * count), DamageType)],
            tickInterval: interval, tickCount: ticks, duration: lifetime, scaleWithSource: ScaleWithSource,
            damageMultiplier: stats.Get(new(Stats.Damage)), movementSlow: Math.Clamp(MovementSlow * stats.Get(new(Stats.Slow)), 0, 1));
    }
}
