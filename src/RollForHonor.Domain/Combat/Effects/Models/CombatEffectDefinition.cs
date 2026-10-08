namespace RollForHonor.Domain.Combat.Effects.Models;

/// <summary>
/// Defines the immutable rules shared by instances of a combat effect.
/// </summary>
public abstract record CombatEffectDefinition
{
    /// <summary>Gets the stable definition identifier.</summary>
    public EffectDefinitionId Id { get; }

    /// <summary>
    /// Initializes an effect definition with its stable identifier.
    /// </summary>
    /// <param name="id">The non-null identifier shared by instances of this definition.</param>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> is null.</exception>
    protected CombatEffectDefinition(EffectDefinitionId id)
    {
        ArgumentNullException.ThrowIfNull(id);

        Id = id;
    }
}
