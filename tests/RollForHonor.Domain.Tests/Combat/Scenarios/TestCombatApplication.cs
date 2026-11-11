using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.Effects.Models;
using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Combat.Resolution.Services;
using RollForHonor.Domain.Combat.State.Changes;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Dice.Services;

namespace RollForHonor.Domain.Tests.Combat.Scenarios;

// Stateful test fake, not a production Application service or its future contract.
// Attack IDs group impacts only here. No resource spending, cooldowns, scheduling or physics.
internal sealed class TestCombatApplication(Action<ICombatResolutionOutcome> reportOutcome,
    params CombatantSnapshot[] characters)
{
    private Dictionary<CombatantId, CombatantSnapshot> _state = characters.ToDictionary(value => value.Id);
    private readonly HashSet<Guid> _applied = [];
    private readonly List<TestImpact> _committed = [];
    private readonly List<TestAttack> _attacks = [];

    public long Version { get; private set; }
    public IReadOnlyList<TestImpact> Committed => _committed.AsReadOnly();
    public IReadOnlyList<TestAttack> Attacks => _attacks.AsReadOnly();

    // Labels a test action. Real attack acceptance/resource policies remain Application work.
    public TestAttack BeginAttack(CombatantId sourceId, AttackPayload payload)
    {
        var attack = new TestAttack(Guid.NewGuid(), sourceId, payload);
        _attacks.Add(attack);
        return attack;
    }

    public CombatantSnapshot Snapshot(CombatantId id)
    {
        var state = _state[id];
        var resistances = state.Resistances.ToDictionary(value => value.Type, value => value);
        // One explicitly defined fixture status overrides a resistance. This is not a general
        // stacking/stat policy. Rebuild from base values so repeated snapshots never add bonuses.
        foreach (var effect in state.Effects.Select(value => value.Definition).OfType<TestResistanceDefinition>())
            resistances[effect.Type] = new ResistanceValue(effect.Type, effect.Value);
        return new CombatantSnapshot(state.Id, state.Health, state.MaxHealth, state.Armor, state.Evasion,
            resistances.Values.ToArray(), state.Effects);
    }

    // These scenarios use a fresh source for ordinary impacts; projectile source-lifetime
    // and capture policies are intentionally not established by this fake.
    public TestImpact CalculateImpact(TestAttack attack, CombatantId targetId, CombatResolver resolver, IDiceRoller dice)
    {
        var source = Snapshot(attack.SourceId);
        var target = targetId == source.Id ? source : Snapshot(targetId);
        var request = new CombatRequest(Guid.NewGuid(), source.Id, target.Id, attack.Payload,
            new CombatStateSnapshot(Version, source, target));
        var outcome = resolver.Resolve(request, dice);
        reportOutcome(outcome);
        return new TestImpact(attack.Id, request, outcome);
    }

    public TestImpact CalculateTick(CombatEffectSnapshot effect, CombatantId targetId,
        CombatResolver resolver, IDiceRoller dice)
    {
        var request = new CombatRequest(Guid.NewGuid(), effect, Snapshot(targetId), Version);
        var outcome = resolver.Resolve(request, dice);
        reportOutcome(outcome);
        return new TestImpact(null, request, outcome);
    }

    public bool TryCommit(TestImpact impact)
    {
        if (impact.Outcome is not ResolvedCombat resolved) return false;
        var resolution = resolved.Resolution;
        if (_applied.Contains(resolution.ResolutionId) || resolution.BasedOnStateVersion != Version) return false;

        // Stage the complete proposal before publishing anything, including source changes.
        var staged = new Dictionary<CombatantId, CombatantSnapshot>(_state);
        foreach (var change in resolution.Changes)
        {
            switch (change)
            {
                case HealthChanged health:
                    var character = staged[health.CombatantId];
                    if (character.Health != health.PreviousHealth)
                        throw new InvalidOperationException("Health proposal does not match current state.");
                    staged[character.Id] = Copy(character, health: health.CurrentHealth);
                    break;
                case EffectApplied applied:
                    var recipient = staged[applied.CombatantId];
                    if (recipient.Effects.Any(effect => effect.Id == applied.Effect.Id))
                        throw new InvalidOperationException("Effect instance was already applied.");
                    staged[recipient.Id] = Copy(recipient, effects: recipient.Effects.Append(applied.Effect).ToArray());
                    break;
                case EffectRemoved removed:
                    var previous = staged[removed.CombatantId];
                    if (!previous.Effects.Any(effect => effect.Id == removed.EffectId))
                        throw new InvalidOperationException("Removed effect does not exist.");
                    staged[previous.Id] = Copy(previous,
                        effects: previous.Effects.Where(effect => effect.Id != removed.EffectId).ToArray());
                    break;
                case CombatantDefeated defeated:
                    if (!staged[defeated.CombatantId].IsDefeated)
                        throw new InvalidOperationException("Defeat proposal does not match final health.");
                    break;
                default: throw new InvalidOperationException("Unsupported test proposal.");
            }
        }

        var nextVersion = checked(Version + 1);
        _state = staged;
        Version = nextVersion;
        _applied.Add(resolution.ResolutionId);
        _committed.Add(impact);
        return true;
    }

    private static CombatantSnapshot Copy(CombatantSnapshot previous, int? health = null,
        IReadOnlyList<CombatEffectSnapshot>? effects = null) =>
        new(previous.Id, health ?? previous.Health, previous.MaxHealth, previous.Armor, previous.Evasion,
            previous.Resistances, effects ?? previous.Effects);
}

internal sealed record TestAttack(Guid Id, CombatantId SourceId, AttackPayload Payload);
internal sealed record TestImpact(Guid? AttackId, CombatRequest Request, ICombatResolutionOutcome Outcome);

// Test content only: a status that supplies exactly one explicit resistance value.
internal sealed record TestResistanceDefinition : CombatEffectDefinition
{
    public DamageType Type { get; }
    public decimal Value { get; }

    public TestResistanceDefinition(DamageType type, decimal value,
        EffectRecipient recipient = EffectRecipient.Target) : base(new EffectDefinitionId(Guid.NewGuid()), recipient)
    {
        Type = type;
        Value = value;
    }
}
