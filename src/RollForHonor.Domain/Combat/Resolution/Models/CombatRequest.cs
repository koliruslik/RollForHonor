using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.Effects.Models;
using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Domain.Combat.Resolution.Models;

/// <summary>
/// Requests one atomic attack or effect tick against one current target snapshot.
/// </summary>
public sealed record CombatRequest
{
    /// <summary>Gets the identifier shared with the resulting resolution.</summary>
    public Guid ResolutionId { get; }

    /// <summary>Gets the original attack or effect source identifier.</summary>
    public CombatantId SourceId { get; }

    /// <summary>Gets the target combatant identifier.</summary>
    public CombatantId TargetId { get; }

    /// <summary>Gets the attack payload, or null for an effect tick.</summary>
    public AttackPayload? Attack { get; }

    /// <summary>Gets the applied effect being ticked, or null for an attack.</summary>
    public CombatEffectSnapshot? TickEffect { get; }

    /// <summary>Gets unscaled damage formulas without requiring an attack roll.</summary>
    public IReadOnlyList<DamageComponent> Damage => Attack?.Damage
        ?? ((PeriodicDamageEffectDefinition)TickEffect!.Definition).Damage;

    /// <summary>Gets the state snapshot used for calculation.</summary>
    public CombatStateSnapshot StateSnapshot { get; }

    /// <summary>
    /// Creates a validated combat resolution request.
    /// </summary>
    /// <param name="resolutionId">The non-empty identifier copied into the result.</param>
    /// <param name="sourceId">The expected source combatant identifier.</param>
    /// <param name="targetId">The expected target combatant identifier.</param>
    /// <param name="attack">The immutable attack payload to resolve.</param>
    /// <param name="state">The versioned source and target snapshot.</param>
    /// <exception cref="ArgumentException"><paramref name="resolutionId"/> is empty.</exception>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
    public CombatRequest(
        Guid resolutionId,
        CombatantId sourceId,
        CombatantId targetId,
        AttackPayload attack,
        CombatStateSnapshot state)
    {
        if (resolutionId == Guid.Empty)
        {
            throw new ArgumentException("A resolution identifier cannot be empty.", nameof(resolutionId));
        }

        ArgumentNullException.ThrowIfNull(sourceId);
        ArgumentNullException.ThrowIfNull(targetId);
        ArgumentNullException.ThrowIfNull(attack);
        ArgumentNullException.ThrowIfNull(state);

        ResolutionId = resolutionId;
        SourceId = sourceId;
        TargetId = targetId;
        Attack = attack;
        StateSnapshot = state;
    }

    /// <summary>Creates a tick from a captured source and the recipient's current state.</summary>
    /// <param name="resolutionId">The non-empty transaction identifier.</param>
    /// <param name="effect">The applied periodic effect containing its source snapshot and damage formulas.</param>
    /// <param name="target">The current recipient snapshot, including the active effect.</param>
    /// <param name="version">The current state version used for commit validation.</param>
    /// <exception cref="ArgumentNullException">The effect or target is null.</exception>
    /// <exception cref="ArgumentException">The identifier is empty, the effect is not periodic, or its source was not captured.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The version is negative.</exception>
    /// <remarks>The captured source is calculation data only; ticks cannot propose changes to its historical state.</remarks>
    public CombatRequest(Guid resolutionId, CombatEffectSnapshot effect, CombatantSnapshot target, long version)
    {
        if (resolutionId == Guid.Empty)
            throw new ArgumentException("A resolution identifier cannot be empty.", nameof(resolutionId));
        ArgumentNullException.ThrowIfNull(effect);
        ArgumentNullException.ThrowIfNull(target);
        if (effect.Definition is not PeriodicDamageEffectDefinition || effect.SourceSnapshot is null)
            throw new ArgumentException("A damage tick requires a periodic definition and a captured source.", nameof(effect));

        ResolutionId = resolutionId;
        SourceId = effect.SourceSnapshot.Id;
        TargetId = target.Id;
        TickEffect = effect;
        StateSnapshot = new CombatStateSnapshot(version, effect.SourceSnapshot, target);
    }
}
