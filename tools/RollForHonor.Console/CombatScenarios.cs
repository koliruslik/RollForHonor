using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Domain.Combat.Attacks.Services;
using RollForHonor.Domain.Combat.Damage.Configuration;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.Damage.Services;
using RollForHonor.Domain.Combat.Effects.Models;
using RollForHonor.Domain.Combat.Effects.Services;
using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Combat.Resolution.Services;
using RollForHonor.Domain.Combat.Resolution.Services.Steps;
using RollForHonor.Domain.Combat.State.Changes;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Combat.State.Services;
using RollForHonor.Domain.Dice.Models;
using RollForHonor.Domain.Dice.Services;

namespace RollForHonor.ConsoleHost;

internal sealed class CombatScenarios(int seed)
{
    public static readonly string[] Names = ["attack", "effect", "sequence"];
    private readonly Random _random = new(seed);
    private readonly CombatantSnapshot _source = Character(100);
    private readonly CombatantSnapshot _target = Character(100, armor: 30, evasion: 15);
    private readonly AttackPayload _attack = new(new DiceFormula(1, 20), 0, NaturalOutcomeShiftRules.Standard,
        [new DamageComponent(new DiceFormula(1, 6), 3, DamageType.Slashing)], [], AttackTags.Melee);
    private readonly CombatResolver _attackResolver = Resolver([
        new AttackRollStep(new AttackRollResolver()), new AttackOutcomeStep(new D20AttackOutcomePolicy()),
        new RawDamageStep(new RawDamageResolver()),
        new AttackQualityStep(new AttackQualityDamageResolver(new DamageMultipliers(0, 0.5m, 1, 2))),
        new DefenseStep(new DefenseResolver(new HitMagnitudeArmorFormula()))
    ]);
    // Explicit scenario choice, not a universal defense rule for every periodic effect.
    private readonly CombatResolver _tickResolver = Resolver([
        new RawDamageStep(new RawDamageResolver()), new DefenseStep(new DefenseResolver(new HitMagnitudeArmorFormula()))
    ]);

    public static void Smoke()
    {
        var scenarios = new CombatScenarios(42);
        var rolls = new[] { 1, 6, 15, 20 };
        var outcomes = new[] { AttackOutcome.Evaded, AttackOutcome.GlancingHit, AttackOutcome.Hit, AttackOutcome.CriticalHit };
        var damages = new[] { 0, 1, 3, 8 };
        for (var i = 0; i < rolls.Length; i++)
        {
            var result = scenarios.Attack(scenarios._target, i, ScenarioDice.Scripted(rolls[i], 3));
            Require(result.Result.Target.Outcome == outcomes[i], "Unexpected attack outcome.");
            Require(result.Result.Target.DamageResolution!.AfterDefenses.Sum() == damages[i], "Unexpected mitigated damage.");
            Require(result.DiceTrace.Rolls.Count == 2, "Attack dice trace is incomplete.");
            Console.WriteLine($"PASS attack/{outcomes[i]}: damage={damages[i]}");
        }

        var target = Character(10, 30, 15);
        for (var i = 0; i < 4; i++)
        {
            var result = scenarios.Attack(target, i, ScenarioDice.Scripted(15, 3));
            var expected = new[] { 7, 4, 1, 0 }[i];
            Require(result.BasedOnStateVersion == i, "Incorrect state version.");
            target = ProjectRecipient(target, result);
            Require(target.Health == expected, "Consecutive attacks did not use updated health.");
            Require(result.Changes.OfType<CombatantDefeated>().Any() == (i == 3), "Incorrect defeat transition.");
        }
        Console.WriteLine("PASS sequence: health=10 -> 7 -> 4 -> 1 -> 0");

        var (instance, recipient) = scenarios.ApplyPeriodicEffect();
        var deadSource = new CombatantSnapshot(scenarios._source.Id, 0, 100, 999, 999, [], []);
        var first = scenarios.Tick(instance, recipient, 1, ScenarioDice.Scripted(1));
        recipient = ProjectRecipient(recipient, first);
        Require(recipient.Health == 98, "First tick should deal 2 damage.");
        var second = scenarios.Tick(instance, recipient, 2, ScenarioDice.Scripted(6));
        recipient = ProjectRecipient(recipient, second);
        Require(recipient.Health == 93, "Second tick should reroll and deal 5 damage.");
        Require(instance.SourceSnapshot!.Health == 100 && deadSource.IsDefeated, "Captured source changed.");
        Require(second.Result.Target.AttackRoll is null && second.Result.Target.Outcome is null, "Tick fabricated an attack.");
        Require(second.DiceTrace.Rolls.Count == 1 && second.DiceTrace.Rolls[0].Formula.Sides == 6, "Tick rolled an attack die.");
        Console.WriteLine("PASS effect: captured source survives live-source death; fresh rolls; health=100 -> 98 -> 93");
    }

    // Each invocation includes request creation, per-resolution dice/trace allocation and result consumption.
    // Fixed fixtures are constructed outside measurement. Sequence additionally projects recipient state.
    public (Func<long> Run, int ResolutionsPerOperation) Prepare(string name)
    {
        if (name == "attack")
            return (() => Attack(_target, 0, ScenarioDice.Seeded(_random)).Result.Target.DamageResolution!.AfterDefenses.Sum(), 1);
        if (name == "effect")
        {
            var (effect, recipient) = ApplyPeriodicEffect();
            return (() => Tick(effect, recipient, 0, ScenarioDice.Seeded(_random)).Result.Target.DamageResolution!.AfterDefenses.Sum(), 1);
        }
        if (name == "sequence")
            return (() =>
            {
                var recipient = _target;
                long checksum = 0;
                for (var i = 0; i < 4; i++)
                {
                    var result = Attack(recipient, i, ScenarioDice.Seeded(_random));
                    recipient = ProjectRecipient(recipient, result);
                    checksum += recipient.Health;
                }
                return checksum;
            }, 4);
        throw new ArgumentException($"Unknown scenario '{name}'.");
    }

    private CombatResolution Attack(CombatantSnapshot target, long version, IDiceRoller dice) =>
        Resolve(_attackResolver, new CombatRequest(Guid.NewGuid(), _source.Id, target.Id, _attack,
            new CombatStateSnapshot(version, _source, target)), dice);

    private CombatResolution Tick(CombatEffectSnapshot effect, CombatantSnapshot target, long version, IDiceRoller dice) =>
        Resolve(_tickResolver, new CombatRequest(Guid.NewGuid(), effect, target, version), dice);

    private (CombatEffectSnapshot, CombatantSnapshot) ApplyPeriodicEffect()
    {
        var definition = new PeriodicDamageEffectDefinition(new EffectDefinitionId(Guid.NewGuid()),
            [new DamageComponent(new DiceFormula(1, 6), 3, DamageType.Fire)]);
        var attack = new AttackPayload(new DiceFormula(1, 20), 0, NaturalOutcomeShiftRules.Standard, [], [definition], AttackTags.Spell);
        var recipient = Character(100, resistances: [new ResistanceValue(DamageType.Fire, 0.5m)]);
        var result = Resolve(Resolver([new ApplyNewEffectsStep(new CombatEffectResolver(new ResolutionEffectInstanceIdFactory()))]),
            new CombatRequest(Guid.NewGuid(), _source.Id, recipient.Id, attack,
                new CombatStateSnapshot(0, _source, recipient)), ScenarioDice.Scripted());
        var effect = result.Changes.OfType<EffectApplied>().Single().Effect;
        return (effect, ProjectRecipient(recipient, result));
    }

    // Deliberately tool-local projection, not the future atomic Application commit implementation.
    private static CombatantSnapshot ProjectRecipient(CombatantSnapshot previous, CombatResolution resolution)
    {
        var health = previous.Health;
        var effects = previous.Effects.ToList();
        foreach (var change in resolution.Changes)
        {
            switch (change)
            {
                case HealthChanged value when value.CombatantId == previous.Id:
                    Require(value.PreviousHealth == health, "Stale health proposal.");
                    health = value.CurrentHealth;
                    break;
                case EffectApplied value when value.CombatantId == previous.Id:
                    effects.Add(value.Effect);
                    break;
                case EffectRemoved value when value.CombatantId == previous.Id:
                    effects.RemoveAll(effect => effect.Id == value.EffectId);
                    break;
                case CombatantDefeated value when value.CombatantId == previous.Id:
                    break;
                default: throw new InvalidOperationException("Scenario cannot project this proposal.");
            }
        }
        return new CombatantSnapshot(previous.Id, health, previous.MaxHealth, previous.Armor, previous.Evasion,
            previous.Resistances, effects);
    }

    private static CombatResolution Resolve(CombatResolver resolver, CombatRequest request, IDiceRoller dice) =>
        resolver.Resolve(request, dice) is ResolvedCombat resolved ? resolved.Resolution
            : throw new InvalidOperationException("Scenario request was rejected.");

    private static CombatResolver Resolver(IEnumerable<ICombatStep> steps) =>
        new(new CombatRequestValidator(), new CombatPipeline(steps), new CombatResolutionBuilder(new HealthStateProjector()));

    private static CombatantSnapshot Character(int health, int armor = 0, int evasion = 0,
        IReadOnlyList<ResistanceValue>? resistances = null) =>
        new(new CombatantId(Guid.NewGuid()), health, 100, armor, evasion, resistances ?? [], []);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
