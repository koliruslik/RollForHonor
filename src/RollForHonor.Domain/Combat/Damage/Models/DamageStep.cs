namespace RollForHonor.Domain.Combat.Damage.Models;

/// <summary>
/// Records the input and output of one damage-calculation stage.
/// </summary>
public sealed record DamageStep
{
    /// <summary>Gets the kind of damage-calculation stage.</summary>
    public DamageStepType Type { get; }

    /// <summary>Gets the packet entering the stage.</summary>
    public DamagePacket Input { get; }

    /// <summary>Gets the packet produced by the stage.</summary>
    public DamagePacket Output { get; }

    /// <summary>
    /// Creates a validated damage-calculation step.
    /// </summary>
    /// <param name="type">A defined calculation-stage type.</param>
    /// <param name="input">The packet entering the stage.</param>
    /// <param name="output">The packet produced by the stage.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> is undefined.</exception>
    /// <exception cref="ArgumentNullException">The input or output packet is null.</exception>
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
