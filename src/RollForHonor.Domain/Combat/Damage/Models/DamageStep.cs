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
    /// <summary>Gets the actual configured scaling coefficient, when this stage has one.</summary>
    public decimal? Multiplier { get; }
    /// <summary>Gets a content-supplied modifier origin, or null when unreported.</summary>
    public string? Origin { get; }
    /// <summary>Gets a reported flat modifier, without inferring it from packet differences.</summary>
    public int? FlatBonus { get; }
    /// <summary>Gets explicitly reported per-component rolls, copied independently of optional diagnostic capture.</summary>
    public IReadOnlyList<DamageComponentRoll> ComponentRolls { get; }

    /// <summary>
    /// Creates a validated damage-calculation step.
    /// </summary>
    /// <param name="type">A defined calculation-stage type.</param>
    /// <param name="input">The packet entering the stage.</param>
    /// <param name="output">The packet produced by the stage.</param>
    /// <param name="multiplier">Actual configured scaling, if reported by the calculating stage.</param>
    /// <param name="origin">Optional modifier origin; null means unreported.</param>
    /// <param name="flatBonus">Actual supplied flat modifier, if reported.</param>
    /// <param name="componentRolls">Optional actual component dice/results reported by this stage; null means none.</param>
    /// <exception cref="ArgumentException">A reported component roll is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> is undefined.</exception>
    /// <exception cref="ArgumentNullException">The input or output packet is null.</exception>
    public DamageStep(
        DamageStepType type,
        DamagePacket input,
        DamagePacket output, decimal? multiplier = null, string? origin = null, int? flatBonus = null,
        IReadOnlyList<DamageComponentRoll>? componentRolls = null)
    {
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);

        var rolls = componentRolls?.ToArray() ?? [];
        if (rolls.Any(roll => roll is null))
            throw new ArgumentException("Component rolls cannot contain null.", nameof(componentRolls));
        ComponentRolls = Array.AsReadOnly(rolls);
        Type = type;
        Input = input;
        Output = output;
        Multiplier = multiplier;
        Origin = origin;
        FlatBonus = flatBonus;
    }
}
