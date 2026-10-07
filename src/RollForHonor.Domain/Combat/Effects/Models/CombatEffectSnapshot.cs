namespace RollForHonor.Domain.Combat.Effects.Models;

/// <summary>
/// Captures an effect instance present in a combat snapshot.
/// </summary>
public sealed record CombatEffectSnapshot
{
    /// <summary>Gets the runtime effect instance identifier.</summary>
    public EffectInstanceId Id { get; }

    /// <summary>Gets the rules shared by this effect instance.</summary>
    public CombatEffectDefinition Definition { get; }

    /// <summary>
    /// Creates a snapshot for an applied effect instance.
    /// </summary>
    /// <param name="id">The runtime instance identifier.</param>
    /// <param name="definition">The immutable rules used by the instance.</param>
    /// <exception cref="ArgumentNullException">Either argument is null.</exception>
    public CombatEffectSnapshot(
        EffectInstanceId id,
        CombatEffectDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(definition);

        Id = id;
        Definition = definition;
    }
}
