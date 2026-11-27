using RollForHonor.Domain.Combat.Damage.Models;

namespace RollForHonor.Domain.Combat.Effects.Models;

/// <summary>Stores immutable, captured damage formulas to reroll for each independently scheduled tick.</summary>
/// <remarks>Optional schedule metadata is consumed by Application; defense and reapplication policies remain explicit.</remarks>
public sealed record PeriodicDamageEffectDefinition : CombatEffectDefinition
{
    /// <summary>Gets typed dice and flat damage components captured when the effect is applied.</summary>
    public IReadOnlyList<DamageComponent> Damage { get; }

    /// <summary>Gets the automatic tick interval, or null for manually scheduled fixtures.</summary>
    public TimeSpan? TickInterval { get; }

    /// <summary>Gets complete tick intervals in the initial lifetime; a manual fixture has zero.</summary>
    public int TickCount { get; }

    /// <summary>Gets the captured automatic lifetime; null denotes a manual fixture.</summary>
    public TimeSpan? Duration { get; }

    /// <summary>Gets whether ordinary typed/global source bonuses affect ticks.</summary>
    public bool ScaleWithSource { get; }

    /// <summary>Gets the captured dedicated status-damage coefficient.</summary>
    public decimal DamageMultiplier { get; }

    /// <summary>Gets the captured movement-speed reduction fraction.</summary>
    public decimal MovementSlow { get; }

    /// <summary>Creates a periodic damage definition with explicitly supplied formulas.</summary>
    /// <param name="id">The stable definition identifier.</param>
    /// <param name="damage">Non-empty damage components, including any captured source bonuses.</param>
    /// <param name="recipient">The recipient role at application time.</param>
    /// <param name="tickInterval">Positive automatic interval, or null for manual scheduling.</param>
    /// <param name="tickCount">Positive automatic tick count, or zero for manual scheduling.</param>
    /// <param name="duration">Optional positive automatic lifetime; omitted uses interval times tick count.</param>
    /// <param name="scaleWithSource">Whether captured ordinary source bonuses scale ticks.</param>
    /// <param name="damageMultiplier">Nonnegative dedicated status-damage coefficient.</param>
    /// <param name="movementSlow">Captured movement reduction from zero through one.</param>
    /// <exception cref="ArgumentOutOfRangeException">Automatic interval/count are invalid or only one is supplied.</exception>
    /// <exception cref="ArgumentNullException">The identifier or collection is null.</exception>
    /// <exception cref="ArgumentException">Damage is empty or contains null.</exception>
    public PeriodicDamageEffectDefinition(EffectDefinitionId id, IReadOnlyList<DamageComponent> damage,
        EffectRecipient recipient = EffectRecipient.Target, TimeSpan? tickInterval = null, int tickCount = 0, TimeSpan? duration = null,
        bool scaleWithSource = true, decimal damageMultiplier = 1, decimal movementSlow = 0) : base(id, recipient)
    {
        ArgumentNullException.ThrowIfNull(damage);
        var components = damage.ToArray();
        if (components.Length == 0 || components.Any(component => component is null))
        {
            throw new ArgumentException("Periodic damage requires non-null damage components.", nameof(damage));
        }

        if (tickCount < 0 || tickInterval is { } interval && interval <= TimeSpan.Zero
            || tickInterval.HasValue != (tickCount > 0) || duration is { } value && (value <= TimeSpan.Zero || tickInterval is null)
            || damageMultiplier < 0 || movementSlow is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(tickCount));
        }

        Duration = duration ?? (tickInterval is { } spacing ? TimeSpan.FromTicks(checked(spacing.Ticks * tickCount)) : null);
        ScaleWithSource = scaleWithSource;
        DamageMultiplier = damageMultiplier;
        MovementSlow = movementSlow;
        TickInterval = tickInterval;
        TickCount = duration is { } lifetime && tickInterval is { } spacingValue
            ? checked((int)(lifetime.Ticks / spacingValue.Ticks)) : tickCount;
        Damage = Array.AsReadOnly(components);
    }
}
