using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Domain.Combat.Effects.Models;
using RollForHonor.Domain.Combat.Effects.Services;
using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Combat.State.Changes;
using RollForHonor.Domain.Dice.Services;

namespace RollForHonor.Domain.Combat.Resolution.Services.Steps;

/// <summary>Rolls configured cadence-normalized on-hit chances and proposes independent applications or duration-only refreshes.</summary>
public sealed class ApplyOnHitStatusesStep : ICombatStep
{
    private readonly OnHitStatusRule[] _rules;
    private readonly decimal? _attacksPerSecond;

    /// <summary>Creates on-hit resolution with explicit balance rules and accepted attack cadence.</summary>
    /// <param name="rules">Unique nonnull status definitions in stable roll order.</param>
    /// <param name="attacksPerSecond">Positive accepted attack frequency, or null for reference-speed fixtures.</param>
    /// <exception cref="ArgumentNullException">The rules are null.</exception>
    /// <exception cref="ArgumentException">Rules contain null or duplicate identities.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A supplied cadence is not positive.</exception>
    public ApplyOnHitStatusesStep(IEnumerable<OnHitStatusRule> rules, decimal? attacksPerSecond)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _rules = rules.ToArray();

        if (_rules.Any(rule => rule is null) || _rules.DistinctBy(rule => rule.Id).Count() != _rules.Length)
        {
            throw new ArgumentException("Status rules require unique nonnull definitions.", nameof(rules));
        }

        if (attacksPerSecond is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(attacksPerSecond));
        }

        _attacksPerSecond = attacksPerSecond;
    }

    /// <inheritdoc />
    public CombatContext Execute(CombatContext context, IDiceRoller dice)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(dice);
        var source = context.Request.StateSnapshot.Source;
        var target = context.Request.StateSnapshot.Target;

        if (context.Request.Attack is null || context.AttackOutcome is null or AttackOutcome.Evaded || target.IsDefeated)
        {
            return context;
        }

        var current = context;

        for (var index = 0; index < _rules.Length; index++)
        {
            var rule = _rules[index];
            var chance = rule.Chance(source.Offense, _attacksPerSecond);
            var definition = chance > 0 ? rule.Capture(source.Offense) : null;

            if (definition is null || dice.Roll(new(1, rule.ChanceRollSides)).Total > chance * rule.ChanceRollSides)
            {
                continue;
            }

            var existing = rule.RefreshDurationOnly
                ? target.Effects.FirstOrDefault(effect => effect.Definition.Id == rule.Id) : null;
            ICombatStateChange change;

            if (existing is not null)
            {
                change = new EffectDurationRefreshed(target.Id, existing.Id);
                definition = (PeriodicDamageEffectDefinition)existing.Definition;
            }
            else
            {
                var id = new ResolutionEffectInstanceIdFactory().Create(context.Request.ResolutionId,
                    target.Id, rule.Id, context.Request.Attack.Effects.Count + index);
                change = new EffectApplied(target.Id, new(id, definition, source));
            }

            current = current with
            {
                StateChanges = current.StateChanges.Add(change),
                EffectsApplied = current.EffectsApplied.Add(new(target.Id, definition))
            };
        }

        return current;
    }
}
