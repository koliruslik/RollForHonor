# Combat architecture and rules

[Project overview](../README.md) · [Documentation index](README.md)

## Layer responsibilities

| Project | Responsibility |
| --- | --- |
| Domain | Deterministic rules, immutable inputs/results and proposed changes |
| Application | Launch/impact/tick use cases, pipeline selection and commit orchestration |
| Infrastructure | In-memory state adapter, runtime dice and diagnostic formatting |
| Godot | Input, physics, scene composition and presentation |

Dependencies point toward Application/Domain. Domain and Application have no
engine dependency. See [runtime composition](godot.md#runtime-composition) for
the concrete session setup.

## Domain

`RollForHonor.Domain` contains deterministic game rules, combat contracts,
value objects, validation, and resolution policies.

Combat code is organized by feature (`Attacks`, `Damage`, `Effects`,
`Resolution`, and `State`). Within each feature, immutable data belongs to
`Models`, while contracts and executable rule implementations belong to
`Services`. Tunable damage coefficients are kept in `Configuration`.

The Domain layer does not depend on Godot. It calculates combat results without
directly mutating live game state.

## Damage rules

The game explicitly enables rating rules in `BasicAttackPipelineFactory`.
Only natural 1 is a full miss. Natural 20 is full quality. On natural 2–19,
`d20 + accuracy < 6 + EvasionRating / 10` gives glancing quality ×0.5;
otherwise the quality is full (×1). Fractional evasion is preserved.

After quality, a separate `d20 + CriticalRollBonus >= 20` produces a critical
with base multiplier ×2. A glancing crit still applies glancing quality first.
Misses do not roll a critical die. The actual die, bonus and coefficient are
retained in the breakdown, and all performed dice are in the trace.
The old margin-based policy and coefficient-5 armor remain explicit laboratory
policies; they are not the game's selected composition.

Every typed weapon-damage component rolls through the supplied deterministic
dice source and adds its flat bonus. A component cannot fall below zero. The
basic pipeline then applies resolved source damage coefficients from the
[character stat model](stats.md), followed by the
configured multiplier for the resolved attack outcome applied to each
component independently, with fractional results rounded upward before target
defenses are evaluated. The result preserves damage types and an ordered
calculation breakdown.

Defense resolution now applies a replaceable armor formula once to the combined
piercing, bludgeoning, and slashing damage of a hit. The remaining physical
damage is distributed proportionally across its original typed components,
rounding each upward. Typed resistances then reduce the matching components;
negative resistance values represent weaknesses. Both defense stages preserve
the existing calculation history and append their input and output packets.
Resistance values use fractions (`0.25` means 25% reduction), must not exceed
`1`, and must be unique per type in a combatant snapshot. This is a shared rule
for players and enemies; equipped contributions are aggregated before projection.
Snapshot construction rejects duplicate or above-one resistances before a request
can enter the pipeline. Defense calculation retains its defensive validation.
The game selects `RatingArmorFormula`: `A / (A + 30D)` as the reduction fraction,
where `A` is armor and `D` is combined physical hit damage. Remaining damage
rounds upward; non-positive armor provides no reduction. There is no percentage
reduction cap. After resistances, a hit with positive physical input retains at
least one physical damage in total. If needed, a finalization step restores one
point to the first originally positive physical component, preserving types and
order. Zero physical input remains zero; non-physical damage has no such floor.
Existing effect behaviors can be scheduled explicitly before or after calculation
stages. New status application is separate from executing existing behavior.
Status definitions choose the source or target as recipient; a new instance
replaces existing instances with the same definition ID on that recipient.
Instance identities are derived deterministically through an injected factory.

## Calculation pipeline

`CombatResolver` validates the request before any step or dice roll, runs the
calculation pipeline supplied by its caller, and always calls
`CombatResolutionBuilder` after successful calculation. An ordinary calculation
pipeline can be assembled as:

```text
AttackRollStep
AttackOutcomeStep
RawDamageStep
SourceDamageStep (typed flat attack bonuses, then resolved damage coefficients)
AttackQualityStep
ApplyEffectsStep (explicit pre-defense behaviors)
DefenseStep
ApplyEffectsStep (explicit post-defense behaviors)
ApplyNewEffectsStep
```

`RawDamageStep` rolls typed damage components and adds their flat bonuses without
requiring an attack roll or outcome. The basic attack factory includes
`SourceDamageStep` to add typed flat attack bonuses and then consume resolved
source coefficients once, without extra dice. Flat bonuses are omitted for
periodic ticks; neutral offense preserves the existing packets. `AttackQualityStep` separately applies the
configured outcome multiplier before defenses, rounding each component upward.
Omitting the quality step leaves damage unscaled (effectively a multiplier of
one), even when an outcome is present. A direct-damage calculation can therefore
use `RawDamageStep` followed by the applicable defenses without rolling to hit.
Effects may also run between raw damage and outcome scaling. The quality step
rejects missing prerequisites, repeated scaling, and execution after defenses.

`CombatRequest` has separate constructor overloads for an ordinary attack and
an applied periodic-damage effect. Only attacks require a living source and a
`1d20` formula. Tick requests contain no `AttackPayload`; they use the source
snapshot captured by `CombatEffectResolver` on application and a fresh recipient
snapshot. Validation rejects a tick if its effect is no longer active on that
recipient. Attack-specific steps reject tick inputs before rolling dice.

`PeriodicDamageEffectDefinition` retains copied typed formulas (for example,
`1d6 + 3`), including source-dependent bonuses supplied by its caller at
application time. Every tick rolls these formulas anew. Source changes or death
do not change an existing instance. Older manually constructed effect snapshots
without a captured source remain valid statuses but cannot produce damage ticks.
For ticks, finalization projects only the current recipient; the historical
source is never a state-change recipient. When source and recipient IDs match,
current recipient health still takes precedence over the captured health.

Ticks reuse `CombatResolver`, `RawDamageStep`, optional explicitly chosen defense
steps, and mandatory finalization. No universal periodic-defense rule, duration,
scheduler, automatic equipment-modifier collection, or new reapplication policy is introduced.
Application owns active-effect selection and pipeline assembly. Its impact use
case now invokes a supplied pipeline factory against current participant data
and attempts commit through an adapter. `PeriodicEffectService` now reads a live
recipient, calculates one tick with its captured source and attempts atomic commit.
The workbench supplies manual/global-interval scheduling; full production effect
aggregation and per-effect lifetime policies remain future work. The existing
replacement-by-definition behavior remains.

Concrete steps live under `Combat/Resolution/Services/Steps`. The step contract,
pipeline executor, resolver, and mandatory result builder remain directly under
`Services`.

Finalization is not part of the configurable list. `CombatResolutionBuilder`
always collects health adjustments, projects health, builds state changes and
constructs the result, in that order. These operations are private methods,
so composition cannot omit, repeat or reorder them. The calculation context
contains only inputs and intermediate proposals, without finalization flags.

Each `ICombatStep` takes the current `CombatContext` and the shared `IDiceRoller`,
and returns a context retaining prior results. Constructors receive calculation
dependencies. `CombatPipeline` copies its step sequence, preserves order, and
stops on an exception or invalid result. It rejects steps that replace the
original request or overwrite preceding execution traces. Calculation steps
reject missing prerequisites and repeated execution where applicable.

Domain models defensively copy collection inputs and expose immutable collections
or read-only wrappers, including damage packets, participant snapshots, dice data
and final proposals. Casting a public read-only list to a mutable interface cannot
change its contents. Attack-roll result construction validates the mathematical
sum in a wider integer type and rejects overflow-wrapped totals.

`ApplyEffectsStep` accepts a copied list of `ICombatEffect` implementations.
Its caller decides which behaviors run and where; an empty group is valid.
Effects may read available rolls/damage, transform calculations, and append
addressed health or status proposals. They do not commit state. Newly proposed
statuses are not discovered and executed automatically during the same action.
The status application resolver currently has no immunity or hit-condition
policy: composition must choose when to invoke it.

Damage may omit defenses deliberately; finalization then collects the
pre-defense damage. Non-damaging assemblies may omit damage and defense stages
entirely, with absent results represented explicitly rather than fake damage.
All health proposals are combined before one clamped projection per distinct
participant, including self-target actions. Effects finish inside the calculation
pipeline before mandatory finalization begins. Rebuilding the same context does
not mutate it or collect damage a second time into its proposals.

`CombatResolution.StepTrace` records completed outer and nested steps, including
effect names and half-open ranges into the shared dice trace. The mandatory
result builder appends its own finalization record with four ordered children.
The trace contains no
wall-clock time and is diagnostic data, not a persistence or replay format.
`DamageBreakdown` remains the numerical input/output history for damage
arithmetic. Failed executions propagate exceptions and do not return a partial
successful resolution.

## Application

`RollForHonor.Application` coordinates one target impact through
`CombatImpactService`. An `AttackInstance` retains its source identity and base
`AttackPayload`; `Resolve(targetId)` performs a new read and calculation for
each eligible contact. Source combat effects and target defenses are observed
at impact time, rather than being frozen at attack construction. This differs
from applied periodic effects, which retain their captured source parameters.

The use case:

1. reads current source and target data at one consistent version through
   `ICombatStateAdapter`;
2. invokes `ICombatSnapshotBuilder` to construct a per-impact snapshot;
3. asks `ICombatPipelineFactory` to select applicable behaviors and compose steps;
4. creates a unique resolution identity and uses a fresh, empty-trace dice source;
5. executes the existing Domain resolver and mandatory finalization;
6. attempts atomic commit only for a resolved proposal.

`CombatStateRead` reuses existing immutable `CombatantSnapshot` views as adapter
read data; it does not prescribe a live entity model or database. The default
`CombatSnapshotBuilder` assembles those views and their version into
`CombatStateSnapshot`. Stat-managed actors are projected atomically from the [character stat model](stats.md); equipment-modifier collection is applied by the shared equipment composer.
Pipeline composition is explicitly supplied and invoked anew per impact.

`CombatImpactResult` separates the Domain outcome from `Applied`,
`VersionConflict`, or `AlreadyApplied` commit status. Domain rejection has no
commit status and does not reach the adapter's commit method. Version conflicts
are returned without automatic retries or extra dice rolls. Exceptions propagate;
the adapter contract requires that failed commit leaves all proposed changes
unpublished. Snapshot version and participant identities are checked before
calculation, and each impact requires an empty dice trace.

Attack instances are obtained from accepted launches. Their constructor is
internal; friend test assemblies can create isolated contexts. The ordinary
attack validator requires a living source at impact. Post-launch source death
remains an unresolved gameplay rule.


## Attack initiation and resource costs

`AttackInitiationService.Start(requestId, sourceId, definition)` accepts one
launch without resolving damage. `AttackDefinition` supplies the base impact
description, unique base resource costs and a source-local cooldown key/duration.
The initiation adapter reads the current source, balances, cooldown and any
receipt for the request at one consistent version. Current source cost effects
are selected through `IResourceCostModifierProvider` and passed to Domain.

`ResourceCostResolver` calculates each resource independently. The default
`AdditiveResourceCostPolicy` implements the confirmed additive rule: reductions
of 10% and 20% produce a 30% reduction. It is injected through
`IResourceCostPolicy`, so changing reduction combination does not require
rewriting initiation or impact orchestration. The initial policy floors costs
at zero for total reductions of 100% or more. Costs use exact decimal quantities
without implicit integer rounding: a cost of 11 reduced by 30% is 7.7. Individual
reduction inputs are fractions from zero through one. Flat modifiers, cost
increases, resource regeneration and integer rounding rules are not introduced.

Resource identifiers are extensible, case-sensitive keys, allowing mana,
stamina or ammunition without coupling the calculator to items or Godot.
The resolution retains copied base costs, reductions and final costs.
Duplicate base costs for the same resource are rejected rather than charged twice.
No resource balances are mutated by Domain.

After alive-source, cooldown and affordability checks, Application proposes
all deductions, the cooldown deadline and the command receipt together through
`IAttackInitiationAdapter`. Only a successful atomic commit returns a new
`AttackInstance`. An unaffordable resource, cooldown rejection, version conflict
or failed adapter commit publishes none of this attempt's resource/cooldown changes.
Adapters must recheck receipt/version/affordability and provide that atomicity;
the runtime `InMemoryCombatState` implements both ports with one session version,
so intervening combat changes can invalidate a launch read.

Use a new nonempty request ID for each intended launch; retries of the same
command retain it. `AlreadyStarted` does not expose another spawnable instance
or charge again. A receipt found on the initial read also reports the original
attack ID. Version conflicts are returned without automatic retries.
Cooldown deadlines use an injected monotonic game timeline; game pause and
time scaling belong to the host. A new launch is allowed exactly at its deadline.
Cooldowns are keyed by source plus attack-definition key.

Once launched, zero, one or many later impacts use the existing impact path.
Hits and misses neither charge the launch again nor refund it. The service does
not create projectiles or execute animations. It passes the definition's execution
rules and the same game clock into the accepted attack instance. Godot supplies
runtime delivery and state through its bootstrap; the console uses tool-local
fixtures. Production cost-effect selection and automatic equipment-modifier collection remain separate work.

## Contact admission and observed lifetime

`AttackDefinition.ExecutionRules` configures engine-independent execution behavior
on each accepted `AttackInstance`. Omitting rules preserves the previous behavior:
every forwarded contact may calculate another impact, contacts do not terminate the
instance and there is no automatic expiry deadline.

| Setting | Behavior |
| --- | --- |
| `AttackRepeatPolicy.EveryContact` | Each host-forwarded contact may calculate an independent impact |
| `AttackRepeatPolicy.OncePerTarget` | First admitted contact consumes the target for this instance, including `Evaded` |
| `AttackStopCondition.CombatantContact` | End on the first admitted combatant contact |
| `AttackStopCondition.EnvironmentContact` | End on a non-combatant obstacle contact |
| Both stop flags | End on the first admitted contact of either kind |
| `MaxLifetime` | Optional positive duration measured from the original launch time |

For example, a piercing arrow can use these rules:

```csharp
var rules = new AttackExecutionRules(
    AttackRepeatPolicy.OncePerTarget,
    AttackStopCondition.EnvironmentContact,
    TimeSpan.FromSeconds(3));
var definition = new AttackDefinition("piercing-arrow", payload, cooldown, costs, rules);
var launch = starts.Start(requestId, sourceId, definition);
```

After an accepted launch, the host owns the returned instance and reports events:

```csharp
var contact = attack.ResolveContact(targetId);
var obstacle = attack.ContactEnvironment();
var stillActive = attack.UpdateLifetime();
attack.End(AttackEndReason.AnimationFinished);
```

These are separate event examples, not an unconditional sequence for every hit.
`ResolveContact` returns `AttackContactResult`: admission status, optional
`CombatImpactResult` and the observed end reason. Ignored contacts return
`AttackEnded` or `TargetAlreadyContacted`, create no resolution, perform no state
read and consume no dice. Obstacle contacts never calculate combat damage.
The existing `Resolve(targetId)` convenience method observes the same rules but
throws when admission fails; hosts that expect ignored contacts should use
`ResolveContact` to inspect the status instead.

Once-per-target admission is reserved before calculation. It stays consumed even
if Domain rejects, commit conflicts or calculation throws; no contact-triggered
retry/reroll is hidden in this path. Ending blocks later admissions without
cancelling impacts already admitted. The first end reason is retained. Admission
is synchronized per instance; this does not establish production live-state
adapter concurrency or a deterministic order for simultaneous different targets.

No background timer runs. Contacts check expiry at the exact deadline; the host
calls `UpdateLifetime` to expire an attack with no contacts and remove its visual
object. `IsActive` and `EndReason` are pure reads of the last observed status, so
logging/serialization cannot consult the game clock or advance lifetime. An
animation event can call `End(AnimationFinished)`. Neither ending nor ignored
contacts refund or charge launch resources or reset the cooldown.

The host still owns collision detection, mapping physical objects to combatants
or obstacles, selecting eligible contacts, animation events, movement and object
removal. `EveryContact` treats each call as a separate contact; no physical-event
identifier or callback deduplication is invented. Rate-limited repeated contacts
and the post-launch source-death gameplay decision remain separate work. Godot
hosts currently provide real projectile and melee collision delivery. Existing Domain validation of a defeated ordinary-attack source
is unchanged.

## Returned combat facts for presentation

Godot consumes the existing return values and its own display context. No Application
journal builder, UI model, localization/color/font policy or log parser is added.
The ordinary contact boundary remains:

```text
AttackInstance.ResolveContact(targetId)
  -> AttackContactResult
       -> optional CombatImpactResult
            -> ResolvedCombat.Resolution or RejectedCombat
```

Use `CombatImpactResult.IsCommitted` to distinguish a newly applied transition.
An ignored contact has no impact. Domain rejection has no commit status. A version
conflict retains a calculated proposal without applying it; an `AlreadyApplied`
receipt does not describe a new publication. An evaded attack can still be a newly
committed result with zero damage. Presentation owns how these facts are shown.

For `resolution = ((ResolvedCombat)impact.Outcome).Resolution`, the verified fields are:

| Consumer need | Existing return path |
| --- | --- |
| Separate natural d20 | `resolution.Result.Target.AttackRoll?.Roll.Results` (ordinary attacks use one die) |
| Attack modifier and total for details | `AttackRoll.Modifier` and `AttackRoll.Total` on the target result |
| Hit-quality styling | `resolution.Result.Target.Outcome` |
| Damage-type colors and remaining damage | `resolution.Result.Target.DamageResolution?.AfterDefenses.Damage.Components` |
| Original component formula, flat bonus and actual dice | `DamageResolution.BeforeDefenses.Breakdown.Steps`, then each step's `ComponentRolls` |
| Transformations, reported bonuses/origins and scaling | Each `DamageStep` has `Type`, `Input`, `Output`, `FlatBonus`, `Origin` and `Multiplier` |
| Effects and actual recipients | `resolution.Result.Target.Effects`; canonical applied/removed changes are in `resolution.Changes` |
| Participants for Godot display-name lookup | `resolution.Result.SourceId` and `resolution.Result.Target.TargetId` |
| Actual HP change and defeat | Typed `HealthChanged` / `CombatantDefeated` entries in `resolution.Changes`, plus `Target.IsDefeated` |
| Complete optional dice inspection | `resolution.DiceTrace.Rolls` |

`DamageComponentRoll` retains the actual
`DamageComponent`, its `DiceRoll` and the unscaled zero-clamped `Amount`.
`RawDamageResolver` records these immutable facts on the `WeaponRoll` stage through
`DamageStep.ComponentRolls`. Other rolling modifiers can explicitly report their own
component facts on their calculation stage. Later quality/defense steps preserve the
breakdown, without rerolling or copying a live participant into the output contract.

An unrelated effect may roll a d20 before the attack, and modifiers may roll damage
dice of their own. Do not take the first global d20 as the attack roll or zip the
complete dice trace with weapon components. Use the explicit attack-roll field and
reported component-roll links. Detailed views work with diagnostic capture disabled;
step display names and technical logger text are not presentation contracts.

The fixture demonstrates `2d6 [2,5] + 3 = 10` slashing, plus a reported rune modifier
`1d8 [6] + 2 = 8`, and `1d4 [3] + 1 = 4` fire reduced to 2 by resistance: final typed
damage is 18 slashing and 2 fire. These are retained calculation facts, not a UI template.
`FlatBonus` and a component's flat value can describe the same supplied modifier;
do not sum diagnostic metadata or recalculate damage from it. Use authoritative stage
outputs. Final types can differ from the original formulas when effects transform them.

Damage and actual HP loss are separate: 20 remaining damage against 3 HP produces a
3-to-0 `HealthChanged`, preserving both facts. Identify health/effect recipients by ID,
not by assuming all changes target the contacted actor. A periodic Domain resolution
can contain damage facts with no attack roll or outcome; do not invent a d20 for it.
Runtime ticks use the separate `PeriodicEffectService`; no fictitious attack is created.

Godot owns names, entry assembly, the separate d20 widget, damage-type colors,
hit-quality typography and hover/click details. Only reported modifier provenance is
available; null `Origin` is unreported. The attack modifier is currently an aggregate,
and a defense stage's input/output does not encode an unreported armor or resistance
coefficient. Do not infer missing values from rounded damage differences. Full equipment
aggregation and a named breakdown of every stat source remain separate work.
