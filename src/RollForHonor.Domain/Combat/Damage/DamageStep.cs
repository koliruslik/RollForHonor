namespace RollForHonor.Domain.Combat.Damage;

public sealed record DamageStep
{
    public DamageStepType Type { get; }

    public DamagePacket Input { get; }

    public DamagePacket Output { get; }

    public DamageStep(
        DamageStepType type,
        DamagePacket input,
        DamagePacket output)
    {
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);

        Type = type;
        Input = input;
        Output = output;
    }
}
