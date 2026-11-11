using RollForHonor.Domain.Combat.State.Models;

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

    /// <summary>Gets the immutable source state captured on application, if available.</summary>
    /// <remarks>Older/manual snapshots may omit it; they cannot be used for periodic damage requests.</remarks>
    public CombatantSnapshot? SourceSnapshot { get; }

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

    /// <summary>Creates an applied instance retaining its source state for later calculations.</summary>
    /// <param name="id">The runtime instance identifier.</param>
    /// <param name="definition">The immutable rules and captured formulas.</param>
    /// <param name="sourceSnapshot">The source at application time; never refreshed by later ticks.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public CombatEffectSnapshot(EffectInstanceId id, CombatEffectDefinition definition,
        CombatantSnapshot sourceSnapshot) : this(id, definition)
    {
        ArgumentNullException.ThrowIfNull(sourceSnapshot);
        SourceSnapshot = sourceSnapshot;
    }
}
