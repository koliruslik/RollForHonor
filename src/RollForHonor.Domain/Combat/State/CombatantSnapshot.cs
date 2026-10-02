using RollForHonor.Domain.Combat.Effects;

namespace RollForHonor.Domain.Combat.State;

public sealed record CombatantSnapshot
{
    public CombatantId Id { get; }

    public int Health { get; }

    public int MaxHealth { get; }

    public int Armor { get; }

    public int Evasion { get; }

    public IReadOnlyList<ResistanceValue> Resistances { get; }

    public IReadOnlyList<CombatEffectSnapshot> Effects { get; }

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
