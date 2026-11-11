using RollForHonor.Domain.Combat.Damage.Models;

namespace RollForHonor.Domain.Combat.Effects.Models;

/// <summary>Stores immutable, captured damage formulas to reroll for each independently scheduled tick.</summary>
/// <remarks>Does not choose duration, scheduling, reapplication policy, or applicable defenses.</remarks>
public sealed record PeriodicDamageEffectDefinition : CombatEffectDefinition
{
    /// <summary>Gets typed dice and flat damage components captured when the effect is applied.</summary>
    public IReadOnlyList<DamageComponent> Damage { get; }

    /// <summary>Creates a periodic damage definition with explicitly supplied formulas.</summary>
    /// <param name="id">The stable definition identifier.</param>
    /// <param name="damage">Non-empty damage components, including any captured source bonuses.</param>
    /// <param name="recipient">The recipient role at application time.</param>
    /// <exception cref="ArgumentNullException">The identifier or collection is null.</exception>
    /// <exception cref="ArgumentException">Damage is empty or contains null.</exception>
    public PeriodicDamageEffectDefinition(EffectDefinitionId id, IReadOnlyList<DamageComponent> damage,
        EffectRecipient recipient = EffectRecipient.Target) : base(id, recipient)
    {
        ArgumentNullException.ThrowIfNull(damage);
        var components = damage.ToArray();
        if (components.Length == 0 || components.Any(component => component is null))
            throw new ArgumentException("Periodic damage requires non-null damage components.", nameof(damage));
        Damage = Array.AsReadOnly(components);
    }
}
