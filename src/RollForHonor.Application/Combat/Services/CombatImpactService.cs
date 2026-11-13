using RollForHonor.Application.Combat.Models;
using RollForHonor.Application.Combat.Ports;
using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Combat.Resolution.Services;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Combat.State.Services;
using RollForHonor.Domain.Dice.Services;

namespace RollForHonor.Application.Combat.Services;

/// <summary>Reads, snapshots, calculates and attempts one atomic impact commit.</summary>
public sealed class CombatImpactService
{
    private readonly ICombatStateAdapter _state;
    private readonly ICombatSnapshotBuilder _snapshots;
    private readonly ICombatPipelineFactory _pipelines;
    private readonly Func<IDiceRoller> _createDice;

    /// <summary>Creates the engine-independent impact use case with explicit external boundaries.</summary>
    /// <param name="state">Consistent reads and atomic commits.</param>
    /// <param name="snapshots">The per-impact snapshot builder.</param>
    /// <param name="pipelines">Application-owned effect selection and pipeline composition.</param>
    /// <param name="createDice">Creates a fresh, empty-trace dice source for each impact.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    public CombatImpactService(ICombatStateAdapter state, ICombatSnapshotBuilder snapshots,
        ICombatPipelineFactory pipelines, Func<IDiceRoller> createDice)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _snapshots = snapshots ?? throw new ArgumentNullException(nameof(snapshots));
        _pipelines = pipelines ?? throw new ArgumentNullException(nameof(pipelines));
        _createDice = createDice ?? throw new ArgumentNullException(nameof(createDice));
    }

    /// <summary>Executes one eligible contact using the current source and target state.</summary>
    /// <param name="attack">The source-bound attack context.</param>
    /// <param name="targetId">The current target identity.</param>
    /// <returns>The Domain outcome and separate adapter commit status.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="InvalidOperationException">A dependency returns inconsistent inputs or a used dice trace.</exception>
    /// <remarks>
    /// Rejections never reach commit. Exceptions propagate; adapters must preserve atomicity on
    /// commit failure. Version conflicts are returned without hidden retries or rerolls. This
    /// path does not implement launch costs, cooldowns, collision policies or UI publication.
    /// </remarks>
    public CombatImpactResult Resolve(AttackInstance attack, CombatantId targetId)
    {
        ArgumentNullException.ThrowIfNull(attack);
        ArgumentNullException.ThrowIfNull(targetId);
        var read = _state.Read(attack.SourceId, targetId)
            ?? throw new InvalidOperationException("The state adapter returned null.");
        var snapshot = _snapshots.Build(read)
            ?? throw new InvalidOperationException("The snapshot builder returned null.");
        if (read.Source.Id != attack.SourceId || read.Target.Id != targetId
            || snapshot.Source.Id != attack.SourceId || snapshot.Target.Id != targetId
            || snapshot.Version != read.Version)
            throw new InvalidOperationException("The read and snapshot must retain participant identities and the expected version.");

        var resolutionId = Guid.NewGuid();
        var request = new CombatRequest(resolutionId, attack.SourceId, targetId, attack.Attack, snapshot);
        var pipeline = _pipelines.Create(attack, snapshot)
            ?? throw new InvalidOperationException("The pipeline factory returned null.");
        var dice = _createDice() ?? throw new InvalidOperationException("The dice factory returned null.");
        if (dice.Trace.Rolls.Count != 0)
            throw new InvalidOperationException("Each impact requires a fresh dice trace.");
        var resolver = new CombatResolver(new CombatRequestValidator(), pipeline,
            new CombatResolutionBuilder(new HealthStateProjector()));
        var outcome = resolver.Resolve(request, dice);
        var commit = outcome is ResolvedCombat resolved ? _state.TryCommit(resolved.Resolution) : (CombatCommitStatus?)null;
        return new CombatImpactResult(attack.Id, resolutionId, outcome, commit);
    }
}
