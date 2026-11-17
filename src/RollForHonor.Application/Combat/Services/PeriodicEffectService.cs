using RollForHonor.Application.Combat.Models;
using RollForHonor.Application.Combat.Ports;
using RollForHonor.Domain.Combat.Effects.Models;
using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Combat.Resolution.Services;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Combat.State.Services;
using RollForHonor.Domain.Dice.Services;

namespace RollForHonor.Application.Combat.Services;

/// <summary>Reads a live recipient and executes one captured-source effect tick without launch costs, d20 or implicit retries.</summary>
/// <remarks>Host scheduling chooses when to call. Each call is a new attempt; this service owns no timer or duration policy.</remarks>
public sealed class PeriodicEffectService
{
    private readonly ICombatStateAdapter _state;
    private readonly ICombatStep _pipeline;
    private readonly Func<IDiceRoller> _createDice;

    /// <summary>Creates the use case with explicit state, defense/pipeline policy and fresh dice.</summary>
    /// <param name="state">Consistent recipient reads and atomic publication.</param>
    /// <param name="pipeline">Tick calculation steps; ordinary attack and lifesteal stages are not appropriate here.</param>
    /// <param name="createDice">Creates an independent empty dice trace for every admitted tick.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    public PeriodicEffectService(ICombatStateAdapter state, ICombatStep pipeline, Func<IDiceRoller> createDice)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _createDice = createDice ?? throw new ArgumentNullException(nameof(createDice));
    }

    /// <summary>Attempts one tick of the specified effect against fresh recipient state.</summary>
    /// <param name="targetId">The current recipient identity; the original source need not exist or be alive.</param>
    /// <param name="effectId">The active instance to tick; a replaced or removed instance is rejected before dice.</param>
    /// <returns>The original calculation inputs, result and separate commit status.</returns>
    /// <exception cref="ArgumentNullException">An identity is null.</exception>
    /// <exception cref="InvalidOperationException">A collaborator returns inconsistent data or a reused dice trace.</exception>
    public PeriodicTickResult Tick(CombatantId targetId, EffectInstanceId effectId)
    {
        ArgumentNullException.ThrowIfNull(targetId);
        ArgumentNullException.ThrowIfNull(effectId);
        var resolutionId = Guid.NewGuid();
        var read = _state.Read(targetId, targetId) ?? throw new InvalidOperationException("The state adapter returned null.");
        if (read.Source.Id != targetId || read.Target.Id != targetId)
            throw new InvalidOperationException("The recipient read changed the requested identity.");
        var effect = read.Target.Effects.SingleOrDefault(value => value.Id == effectId);
        if (read.Target.IsDefeated || effect?.Definition is not PeriodicDamageEffectDefinition || effect.SourceSnapshot is null)
            return new(resolutionId, targetId, effectId, null,
                new RejectedCombat(new(CombatRejectionReason.InvalidState,
                    "A tick requires a living recipient and an active periodic instance with a captured source.")), null);
        var request = new CombatRequest(resolutionId, effect, read.Target, read.Version);
        var dice = _createDice() ?? throw new InvalidOperationException("The dice factory returned null.");
        if (dice.Trace.Rolls.Count != 0) throw new InvalidOperationException("Each tick requires a fresh dice trace.");
        var outcome = new CombatResolver(new CombatRequestValidator(), _pipeline,
            new CombatResolutionBuilder(new HealthStateProjector())).Resolve(request, dice);
        var commit = outcome is ResolvedCombat resolved ? _state.TryCommit(resolved.Resolution) : (CombatCommitStatus?)null;
        return new(resolutionId, targetId, effectId, request, outcome, commit);
    }
}
