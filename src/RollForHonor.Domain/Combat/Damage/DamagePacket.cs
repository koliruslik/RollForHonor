namespace RollForHonor.Domain.Combat.Damage;

public sealed record DamagePacket
{
    public IReadOnlyList<DamageAmount> Components { get; }

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

    public int Sum()
    {
        return Components.Sum(component => component.Amount);
    }
}
