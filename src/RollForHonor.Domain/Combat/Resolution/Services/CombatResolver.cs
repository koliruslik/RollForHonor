using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Domain.Combat.Attacks.Services;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.Damage.Services;
using RollForHonor.Domain.Combat.Effects.Models;
using RollForHonor.Domain.Combat.Effects.Services;
using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Combat.State.Services;
using RollForHonor.Domain.Combat.State.Changes;
using RollForHonor.Domain.Dice.Models;
using RollForHonor.Domain.Dice.Services;

namespace RollForHonor.Domain.Combat.Resolution.Services;

/// <summary>
/// Coordinates the complete atomic resolution of one attack impact.
/// </summary>
public sealed class CombatResolver
{
    private readonly ICombatRequestValidator _requestValidator;
    private readonly IAttackRollResolver _attackRollResolver;
    private readonly IAttackOutcomePolicy _attackOutcomePolicy;
    private readonly IAttackDamageResolver _attackDamageResolver;
    private readonly IDefenseResolver _defenseResolver;
    private readonly ICombatEffectResolver _effectResolver;
    private readonly IHealthStateProjector _healthStateProjector;

    /// <summary>
    /// Creates a resolver from the policies responsible for each combat stage.
    /// </summary>
    public CombatResolver(
        ICombatRequestValidator requestValidator,
        IAttackRollResolver attackRollResolver,
        IAttackOutcomePolicy attackOutcomePolicy,
        IAttackDamageResolver damageResolver,
        IDefenseResolver defenseResolver,
        ICombatEffectResolver effectResolver,
        IHealthStateProjector healthStateProjector)
    {
        ArgumentNullException.ThrowIfNull(requestValidator);
        ArgumentNullException.ThrowIfNull(attackRollResolver);
        ArgumentNullException.ThrowIfNull(attackOutcomePolicy);
        ArgumentNullException.ThrowIfNull(damageResolver);
        ArgumentNullException.ThrowIfNull(defenseResolver);
        ArgumentNullException.ThrowIfNull(effectResolver);
        ArgumentNullException.ThrowIfNull(healthStateProjector);

        _requestValidator = requestValidator;
        _attackRollResolver = attackRollResolver;
        _attackOutcomePolicy = attackOutcomePolicy;
        _attackDamageResolver = damageResolver;
        _defenseResolver = defenseResolver;
        _effectResolver = effectResolver;
        _healthStateProjector = healthStateProjector;
    }

    /// <summary>
    /// Resolves a request into either a rejection or a versioned set of state changes.
    /// </summary>
    public ICombatResolutionOutcome Resolve(CombatRequest request, IDiceRoller dice)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(dice);

        var combatRejection = _requestValidator.Validate(request);

        if (combatRejection is not null)
        {
            return new RejectedCombat(combatRejection);
        }

        var source = request.StateSnapshot.Source;
        var target = request.StateSnapshot.Target;

        var attackRoll = _attackRollResolver.Resolve(
            request.Attack,
            source,
            target,
            dice);

        var outcome = _attackOutcomePolicy.Determine(
            request.Attack,
            source,
            target,
            attackRoll);

        var damageResolution = ResolveDamage(
            request.Attack,
            source,
            target,
            attackRoll,
            outcome,
            dice);

        var effectResolution = ResolveEffects(
            request.Attack,
            source,
            target,
            attackRoll,
            outcome,
            damageResolution);

        var healthAdjustments = CollectHealthAdjustments(
            target,
            damageResolution.FinalDamage,
            effectResolution);

        var healthProjections = ProjectHealth(
            request.StateSnapshot,
            healthAdjustments);

        var stateChanges = BuildStateChanges(
            healthProjections,
            effectResolution.StateChanges);

        var combatResult = BuildCombatResult(
            source,
            target,
            attackRoll,
            outcome,
            damageResolution,
            effectResolution,
            healthProjections);

        return BuildResolution(
            request,
            combatResult,
            stateChanges,
            dice.Trace);
    }

    private DamageResolution ResolveDamage(
        AttackPayload attack,
        CombatantSnapshot source,
        CombatantSnapshot target,
        AttackRollResult attackRoll,
        AttackOutcome outcome,
        IDiceRoller dice)
    {
        var unmitigatedDamage = _attackDamageResolver.Resolve(
            attack,
            source,
            attackRoll,
            outcome,
            dice);

        var finalDamage = _defenseResolver.Resolve(
            unmitigatedDamage,
            target);

        return new DamageResolution(unmitigatedDamage, finalDamage);
    }

    private EffectResolution ResolveEffects(
        AttackPayload attack,
        CombatantSnapshot source,
        CombatantSnapshot target,
        AttackRollResult attackRoll,
        AttackOutcome outcome,
        DamageResolution damageResolution)
    {
        return _effectResolver.Resolve(
            attack,
            source,
            target,
            attackRoll,
            outcome,
            damageResolution.UnmitigatedDamage,
            damageResolution.FinalDamage);
    }

    private static IReadOnlyList<HealthAdjustment> CollectHealthAdjustments(
        CombatantSnapshot target,
        FinalDamage finalDamage,
        EffectResolution effectResolution)
    {
        var adjustments = new List<HealthAdjustment>();
        var totalDamage = finalDamage.Sum();

        if (totalDamage > 0)
        {
            adjustments.Add(new HealthAdjustment(target.Id, -totalDamage));
        }

        adjustments.AddRange(effectResolution.HealthAdjustments);
        return adjustments;
    }

    private IReadOnlyList<HealthProjection> ProjectHealth(
        CombatStateSnapshot snapshot,
        IReadOnlyList<HealthAdjustment> adjustments)
    {
        var combatants = snapshot.Source.Id == snapshot.Target.Id
            ? new[] { snapshot.Source }
            : new[] { snapshot.Source, snapshot.Target };

        var knownCombatantIds = combatants
            .Select(combatant => combatant.Id)
            .ToHashSet();

        if (adjustments.Any(adjustment => !knownCombatantIds.Contains(adjustment.CombatantId)))
        {
            throw new InvalidOperationException(
                "A health adjustment targets a combatant outside the combat snapshot.");
        }

        return combatants
            .Select(combatant => _healthStateProjector.Project(
                combatant,
                adjustments
                    .Where(adjustment => adjustment.CombatantId == combatant.Id)
                    .ToArray()))
            .ToArray();
    }

    private static IReadOnlyList<ICombatStateChange> BuildStateChanges(
        IReadOnlyList<HealthProjection> healthProjections,
        IReadOnlyList<ICombatStateChange> effectStateChanges)
    {
        var stateChanges = new List<ICombatStateChange>();

        stateChanges.AddRange(healthProjections
            .Where(projection => projection.HasChanged)
            .Select(projection => new HealthChanged(
                projection.CombatantId,
                projection.PreviousHealth,
                projection.CurrentHealth)));

        stateChanges.AddRange(effectStateChanges);

        stateChanges.AddRange(healthProjections
            .Where(projection => projection.BecameDefeated)
            .Select(projection => new CombatantDefeated(
                projection.CombatantId)));

        return stateChanges;
    }

    private static CombatResult BuildCombatResult(
        CombatantSnapshot source,
        CombatantSnapshot target,
        AttackRollResult attackRoll,
        AttackOutcome outcome,
        DamageResolution damageResolution,
        EffectResolution effectResolution,
        IReadOnlyList<HealthProjection> healthProjections)
    {
        var targetHealth = healthProjections.Single(
            projection => projection.CombatantId == target.Id);

        var targetResult = new TargetCombatResult(
            target.Id,
            attackRoll,
            outcome,
            damageResolution,
            effectResolution.Effects,
            targetHealth.CurrentHealth == 0);

        return new CombatResult(source.Id, targetResult);
    }

    private static ResolvedCombat BuildResolution(
        CombatRequest request,
        CombatResult combatResult,
        IReadOnlyList<ICombatStateChange> stateChanges,
        DiceTrace diceTrace)
    {
        var resolution = new CombatResolution(
            request.ResolutionId,
            request.StateSnapshot.Version,
            combatResult,
            stateChanges,
            diceTrace);

        return new ResolvedCombat(resolution);
    }
}
