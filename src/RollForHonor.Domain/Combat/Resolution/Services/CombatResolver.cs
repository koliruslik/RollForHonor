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
    /// <returns>A rejection, or a complete versioned proposal with dice and step traces.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="InvalidOperationException">The pipeline changes the request or returns invalid proposals.</exception>
    /// <remarks>
    /// Finalization always runs once after successful calculation. Step and builder exceptions
    /// propagate; partial calculations never become successful proposals.
    /// </remarks>
    public ICombatResolutionOutcome Resolve(CombatRequest request, IDiceRoller dice)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(dice);
        var rejection = _requestValidator.Validate(request);
        if (rejection is not null)
            return new RejectedCombat(rejection);

        var context = _pipeline.Execute(new CombatContext(request), dice)
            ?? throw new InvalidOperationException("The combat pipeline returned null.");
        if (!ReferenceEquals(context.Request, request))
            throw new InvalidOperationException("The combat pipeline replaced the original request.");
        return new ResolvedCombat(_resolutionBuilder.Build(context, dice.Trace));
    }
}
