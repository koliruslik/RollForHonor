namespace RollForHonor.Domain.Combat.Effects.Models;

/// <summary>
/// Defines the immutable rules shared by instances of a combat effect.
/// </summary>
public abstract record CombatEffectDefinition
{
    /// <summary>Gets the stable definition identifier.</summary>
    public EffectDefinitionId Id { get; }

    /// <summary>Gets whether the status is applied to the source or target.</summary>
    public EffectRecipient Recipient { get; }

    /// <summary>
    /// Initializes an effect definition with its stable identifier.
    /// </summary>
    /// <param name="id">The non-null identifier shared by instances of this definition.</param>
    /// <param name="recipient">The recipient role; defaults to the target.</param>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The recipient role is unknown.</exception>
    protected CombatEffectDefinition(EffectDefinitionId id, EffectRecipient recipient = EffectRecipient.Target)
    {
        ArgumentNullException.ThrowIfNull(id);
        if (!Enum.IsDefined(recipient))
            throw new ArgumentOutOfRangeException(nameof(recipient));

        Id = id;
        Recipient = recipient;
    }
}
