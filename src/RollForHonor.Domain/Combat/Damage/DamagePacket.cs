namespace RollForHonor.Domain.Combat.Damage;

/// <summary>
/// Groups damage amounts while preserving their individual types.
/// </summary>
public sealed record DamagePacket
{
    /// <summary>Gets the typed damage amounts in the packet.</summary>
    public IReadOnlyList<DamageAmount> Components { get; }

    /// <summary>
    /// Creates an immutable packet from typed damage amounts.
    /// </summary>
    public DamagePacket(IReadOnlyList<DamageAmount> components)
    {
        ArgumentNullException.ThrowIfNull(components);

        if (components.Any(component => component is null))
        {
            throw new ArgumentException(
                "A damage packet cannot contain null components.",
                nameof(components));
        }

        Components = components.ToArray();
    }

    /// <summary>
    /// Returns the total damage across all packet components.
    /// </summary>
    public int Sum()
    {
        return Components.Sum(component => component.Amount);
    }
}
