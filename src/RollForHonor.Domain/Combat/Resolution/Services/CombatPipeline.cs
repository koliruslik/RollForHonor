using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Dice.Services;

namespace RollForHonor.Domain.Combat.Resolution.Services;

/// <summary>
/// Executes a copied sequence of calculation steps and nested pipelines in order,
/// passing each returned context to the next step and recording an execution tree.
/// </summary>
/// <remarks>
/// This class runs the configurable calculation only. Request validation and mandatory
/// result construction belong to CombatResolver. An empty sequence returns the input
/// context unchanged. It does not choose effects, reorder steps, or commit live state.
/// </remarks>
public sealed class CombatPipeline : ICombatStep
{
    private readonly ICombatStep[] _steps;

    /// <summary>Gets the diagnostic name used when this pipeline is nested in another pipeline.</summary>
    public string Name { get; }

    /// <summary>Copies an ordered step sequence. An empty pipeline is an identity operation.</summary>
    /// <param name="steps">Steps in execution order.</param>
    /// <param name="name">Optional diagnostic name for a nested group.</param>
    /// <exception cref="ArgumentNullException">Steps or the name are null.</exception>
    /// <exception cref="ArgumentException">A step is null or the supplied name is blank.</exception>
    public CombatPipeline(IEnumerable<ICombatStep> steps, string name = nameof(CombatPipeline))
    {
        ArgumentNullException.ThrowIfNull(steps);

        _steps = steps.ToArray();

        if (_steps.Any(step => step is null))
            throw new ArgumentException("Steps cannot contain null.", nameof(steps));
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }
    /// <summary>
    /// Runs steps sequentially and appends one trace record per completed step,
    /// retaining nested child records and half-open dice index ranges.
    /// </summary>
    /// <param name="context">The initial immutable calculation state and original request.</param>
    /// <param name="dice">The action's shared dice source, whose trace grows as steps roll.</param>
    /// <returns>The final step's context with the accumulated trace, or the original context when empty.</returns>
    /// <exception cref="ArgumentNullException">The context or dice source is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// A step returns null, replaces the original request, or removes or overwrites
    /// previously completed trace records.
    /// </exception>
    /// <remarks>
    /// Snapshots and previous contexts are not modified by the pipeline. Exceptions from
    /// steps propagate immediately; later steps do not run and no partial context is returned.
    /// Trace names are diagnostic labels rather than stable serialization identifiers.
    /// </remarks>
    public CombatContext Execute(CombatContext context, IDiceRoller dice)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(dice);

        var current = context;

        foreach (var step in _steps)
        {
            var previousTrace = current.StepTrace;
            var diceStart = dice.Trace.Rolls.Count;
            var next = step.Execute(current, dice)
                       ?? throw new InvalidOperationException("A combat step returned null.");
            if (!ReferenceEquals(next.Request, context.Request))
                throw new InvalidOperationException("A combat step replaced the original request.");
            if (next.StepTrace.Length < previousTrace.Length
                || !next.StepTrace.Take(previousTrace.Length).SequenceEqual(previousTrace))
                throw new InvalidOperationException("A combat step changed the preceding execution trace.");

            var children = next.StepTrace.Skip(previousTrace.Length);
            var trace = new CombatStepTrace(step.Name, diceStart, dice.Trace.Rolls.Count, children);
            current = next with { StepTrace = previousTrace.Add(trace) };
        }

        return current;
    }
}
