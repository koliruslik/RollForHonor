using RollForHonor.Domain.Combat.Effects.Models;

namespace RollForHonor.Domain.Combat.State.Models;

/// <summary>
/// Captures the immutable combat-relevant state of one combatant.
/// </summary>
public sealed record CombatantSnapshot
{
    /// <summary>Gets the combatant identifier.</summary>
    public CombatantId Id { get; }

    /// <summary>Gets current health at snapshot time.</summary>
    public int Health { get; }

    /// <summary>Gets the maximum health value.</summary>
    public int MaxHealth { get; }

    /// <summary>Gets the armor value used by defense rules.</summary>
    public int Armor { get; }

    /// <summary>Gets the evasion value used by attack rules.</summary>
    public int Evasion { get; }

    /// <summary>Gets typed resistance values.</summary>
    public IReadOnlyList<ResistanceValue> Resistances { get; }

    /// <summary>Gets active effect instances.</summary>
    public IReadOnlyList<CombatEffectSnapshot> Effects { get; }

    /// <summary>Gets whether the combatant has no remaining health.</summary>
    public bool IsDefeated => Health == 0;

    /// <summary>
    /// Creates a validated combatant snapshot.
    /// </summary>
    public CombatantSnapshot(
        CombatantId id,
        int health,
        int maxHealth,
        int armor,
        int evasion,
        IReadOnlyList<ResistanceValue> resistances,
        IReadOnlyList<CombatEffectSnapshot> effects)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxHealth, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(health);

        if (health > maxHealth)
        {
            throw new ArgumentOutOfRangeException(
                nameof(health),
                "Health cannot exceed maximum health.");
        }

        ArgumentNullException.ThrowIfNull(resistances);
        ArgumentNullException.ThrowIfNull(effects);

        if (resistances.Any(resistance => resistance is null))
        {
            throw new ArgumentException(
                "Combatant resistances cannot contain null values.",
                nameof(resistances));
        }

        if (effects.Any(effect => effect is null))
        {
            throw new ArgumentException(
                "Combatant effects cannot contain null values.",
                nameof(effects));
        }

        Id = id;
        Health = health;
        MaxHealth = maxHealth;
        Armor = armor;
        Evasion = evasion;
        Resistances = resistances.ToArray();
        Effects = effects.ToArray();
    }
}
