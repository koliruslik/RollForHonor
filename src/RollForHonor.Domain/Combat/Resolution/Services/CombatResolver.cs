using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Dice.Services;

namespace RollForHonor.Domain.Combat.Resolution.Services;

/// <summary>Validates one action, executes its composed pipeline, and returns an atomic proposal.</summary>
public sealed class CombatResolver
{
    private readonly ICombatRequestValidator _requestValidator;
    private readonly ICombatStep _pipeline;
    private readonly CombatResolutionBuilder _resolutionBuilder;

    /// <summary>Creates a resolver without choosing calculation or effect stages.</summary>
    /// <param name="requestValidator">Validation performed before any step or dice roll.</param>
    /// <param name="pipeline">The configurable calculation steps, excluding mandatory finalization.</param>
    /// <param name="resolutionBuilder">The mandatory finalization shared by all actions.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    public CombatResolver(ICombatRequestValidator requestValidator, ICombatStep pipeline,
        CombatResolutionBuilder resolutionBuilder)
    {
        _requestValidator = requestValidator ?? throw new ArgumentNullException(nameof(requestValidator));
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _resolutionBuilder = resolutionBuilder ?? throw new ArgumentNullException(nameof(resolutionBuilder));
    }

    /// <summary>Calculates one action without mutating snapshots or committing live state.</summary>
    /// <param name="request">The action, participants and expected state version.</param>
    /// <param name="dice">The shared deterministic dice source and trace.</param>
    /// <param name="traceCapture">Optional intermediate capture, without changing calculation or dice.</param>
    /// <returns>A rejection, or a complete versioned proposal with dice and step traces.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="InvalidOperationException">The pipeline changes the request or returns invalid proposals.</exception>
    /// <remarks>
    /// Finalization always runs once after successful calculation. Step and builder exceptions
    /// propagate; partial calculations never become successful proposals.
    /// </remarks>
    public ICombatResolutionOutcome Resolve(CombatRequest request, IDiceRoller dice,
        CombatTraceCapture? traceCapture = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(dice);
        var rejection = _requestValidator.Validate(request);
        if (rejection is not null)
            return new RejectedCombat(rejection);

        var initial = new CombatContext(request) { TraceCapture = traceCapture };
        var directStep = _pipeline is CombatPipeline ? null : traceCapture?.Begin(_pipeline.Name, initial, dice.Trace.Rolls.Count);
        CombatContext context;
        try
        {
            context = _pipeline.Execute(initial, dice)
                ?? throw new InvalidOperationException("The combat pipeline returned null.");
            if (directStep is int index) traceCapture!.Complete(index, context, dice.Trace.Rolls.Count);
        }
        catch (Exception error)
        {
            if (directStep is int index) traceCapture!.Complete(index, null, dice.Trace.Rolls.Count, error);
            throw;
        }
        if (!ReferenceEquals(context.TraceCapture, traceCapture))
            context = context with { TraceCapture = traceCapture };
        if (!ReferenceEquals(context.Request, request))
            throw new InvalidOperationException("The combat pipeline replaced the original request.");
        return new ResolvedCombat(_resolutionBuilder.Build(context, dice.Trace));
    }
}
