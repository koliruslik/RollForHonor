namespace RollForHonor.Domain.Combat.Damage.Models;

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
    /// <param name="components">Typed amounts preserved in their supplied order.</param>
    /// <exception cref="ArgumentNullException"><paramref name="components"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="components"/> contains a null item.</exception>
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
    /// <returns>The checked sum of every component amount.</returns>
    /// <exception cref="OverflowException">The total exceeds the integer range.</exception>
    public int Sum()
    {
        return Components.Sum(component => component.Amount);
    }
}
