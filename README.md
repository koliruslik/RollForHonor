# Roll For Honor

[![CI](https://github.com/koliruslik/RollForHonor/actions/workflows/ci.yaml/badge.svg)](https://github.com/koliruslik/RollForHonor/actions/workflows/ci.yaml)

Roll For Honor is a 3D action game built with Godot and C#.

The project is in early development and currently focuses on building a
deterministic, testable combat foundation before expanding into full gameplay.

## Project Status

The current version is a technical foundation rather than a complete playable
game.

Combat calculation uses explicitly composed steps, including production weapon
damage and defense rules. An ordinary attack is evaluated against the
target's evasion and classified as:

| Attack margin | Outcome |
| ---: | --- |
| `≤ -10` | Evaded |
| `-9` to `-1` | Glancing Hit |
| `0` to `9` | Hit |
| `≥ 10` | Critical Hit |

Natural-roll thresholds can upgrade or downgrade the resulting outcome by one
step. The standard configuration uses a natural `1` for downgrades and a
natural `20` for upgrades.

Every typed weapon-damage component rolls through the supplied deterministic
dice source and adds its flat bonus. A component cannot fall below zero. The
configured multiplier for the resolved attack outcome is then applied to each
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
for players and enemies; equipment-driven stat aggregation is not implemented.
Snapshot construction rejects duplicate or above-one resistances before a request
can enter the pipeline. Defense calculation retains its defensive validation.
`HitMagnitudeArmorFormula` uses `A / (A + 5D)` as the reduction fraction,
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

## Implemented

- deterministic dice formulas, rolls, and trace data;
- immutable combat requests and combatant snapshots;
- combat request validation before dice are rolled;
- atomic resolution of one impact against one target;
- structured combat results with proposed state changes;
- composable, nested calculation steps that return immutable contexts;
- explicit effect-behavior groups and separate new-status application;
- captured effect-source snapshots and periodic damage formulas rerolled per tick;
- effect-tick requests without attack payloads or hit rolls, using current recipient state;
- once-only health collection, projection, and canonical result finalization;
- ordered step traces with nested executions and dice-trace index ranges;
- configurable d20 attack outcome rules;
- configurable natural-roll upgrades and downgrades;
- configurable outcome multipliers for weapon raw damage;
- typed weapon damage dice, flat bonuses, zero clamping, and per-component
  upward rounding;
- ordered raw-damage calculation breakdowns before target defenses;
- typed defense resolution with a replaceable armor-formula contract,
  resistances, weaknesses, and ordered before/after breakdowns;
- engine-independent Domain logic;
- deterministic resource-cost calculation with an explicitly replaceable additive reduction policy;
- an engine-independent Application impact use case with fresh per-impact state reads,
  separate snapshot construction, pipeline composition and version-checked commit ports;
- attack initiation with atomic resource deduction, cooldown start and command replay protection;
- configurable contact admission, once-per-target hits, obstacle/target termination and observed attack lifetime;
- automated Domain and Application tests;
- a standalone console host for smoke scenarios and opt-in performance measurements;
- optional intermediate-step capture and ILogger diagnostics with summary, steps, full and JSONL views;
- headless Godot runtime tests;
- continuous integration with GitHub Actions.

## Architecture

The project follows an inward dependency direction:

    Godot / Presentation → Application → Domain

### Domain

`RollForHonor.Domain` contains deterministic game rules, combat contracts,
value objects, validation, and resolution policies.

Combat code is organized by feature (`Attacks`, `Damage`, `Effects`,
`Resolution`, and `State`). Within each feature, immutable data belongs to
`Models`, while contracts and executable rule implementations belong to
`Services`. Tunable damage coefficients are kept in `Configuration`.

The Domain layer does not depend on Godot. It calculates combat results without
directly mutating live game state.

### Calculation pipeline

`CombatResolver` validates the request before any step or dice roll, runs the
calculation pipeline supplied by its caller, and always calls
`CombatResolutionBuilder` after successful calculation. An ordinary calculation
pipeline can be assembled as:

```text
AttackRollStep
AttackOutcomeStep
RawDamageStep
AttackQualityStep
ApplyEffectsStep (explicit pre-defense behaviors)
DefenseStep
ApplyEffectsStep (explicit post-defense behaviors)
ApplyNewEffectsStep
```

`RawDamageStep` rolls typed damage components and adds their flat bonuses without
requiring an attack roll or outcome. `AttackQualityStep` separately applies the
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
scheduler, production stat aggregation, or new reapplication policy is introduced.
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

Production lifesteal, healing bonuses, periodic effect behavior, and runtime
pipeline selection remain future work. Integration tests use example effects
to verify extension through the full resolver without changing its code.

Effect contract tests exercise every calculation boundary, ordered context
transformations, damage changes on either side of defenses, shared dice traces,
atomic health projection, failure propagation, and status replacement isolation.
A test-only consumer projects armor, evasion, and resistance from base stats plus
an applied status into a fresh snapshot for a later action, including removal
and repeated projection without accumulating bonuses. This illustrates the
consumer boundary; production stat aggregation and runtime state adapters
remain future work.

Regression tests also cover exact d20 margin boundaries and natural-roll shifts,
effect instance identity inputs, rejection of health-changing status application,
repeated calculation stages, and health sums beyond the 32-bit integer range.
Independent-damage tests verify damage rolls without attack rolls, omitted
quality scaling, effects at each boundary, per-component rounding, and expected
attack outcomes through defenses and final health projection.
Periodic integration tests cover all four production attack outcomes, sequential
health/version transitions, source capture and death, fresh tick rolls and target
defenses, self-target snapshots, removed effects, and rejection of proposals to
historical source state. State projection in tests is a test consumer, not a
production Application commit implementation.

### Multi-impact scenario tests

`tests/RollForHonor.Domain.Tests/Combat/Scenarios` exercises real Domain validators,
attack, damage, defense, status and finalization services through a small stateful
`TestCombatApplication`. The fake applies HP/status proposals, rebuilds fresh
snapshots, advances a fixture-wide version and records committed resolutions.
One test action groups several impacts; each impact has its own resolution ID.
Changes are staged before publishing, and stale or duplicate results are rejected.

The scenarios cover:

| Scenario | Expected behavior |
| --- | --- |
| One attack, three targets | Independent defenses yield 6, 3 and 3 damage; distinct resolutions share one test action |
| Three hits on one target | HP changes 15 -> 9 -> 3 -> 0; defeat occurs only on the final hit |
| Resistance applied by an earlier hit | Damage changes 6 -> 3 -> 3; replacement keeps one instance and does not accumulate bonuses |
| Status removal | After a protected hit, removal restores base defense for the next hit |
| Tick between hits | HP changes 50 -> 44 -> 40 -> 34 using committed state at each step |
| Source actually killed after application | A later tick uses its captured source and changes only the current recipient |
| Stale result | Neither HP nor statuses are applied; recalculation uses the new version |
| Duplicate result | Damage, effects and version advancement are not applied twice |
| Domain rejection | No dice, commit or version advancement; another valid action can still resolve |
| Pipeline exception after proposals | No partial HP/status changes are published |
| Changes to both participants | Source healing and target damage become visible at one commit version |

Run just these scenarios with:

```powershell
dotnet test tests/RollForHonor.Domain.Tests --filter FullyQualifiedName~MultiImpactScenarioTests
```

These scenarios use the existing opt-in `CombatDiagnosticTestBase` output for
every returned impact or tick outcome, including Domain rejections. To see
successful tests' detailed output in PowerShell:

```powershell
$env:RFH_COMBAT_DIAGNOSTICS = '1'
try {
    dotnet test tests/RollForHonor.Domain.Tests --filter FullyQualifiedName~MultiImpactScenarioTests --logger "console;verbosity=detailed"
} finally {
    Remove-Item Env:RFH_COMBAT_DIAGNOSTICS
}
```

The diagnostic contains calculated proposals, including ones the fake later
rejects as stale or duplicated; it is not a log of committed live state.
Diagnostics are disabled by default and are not required for test assertions.

The fake's action grouping, commit guards and single explicit resistance-buff
fixture are test infrastructure. These checks do not implement or verify production
attack acceptance, resource spending, cooldowns, concurrency, projectile physics,
or scheduling. Ordinary impacts in these fixtures use a fresh living source;
active attacks now read source parameters at impact time. Post-launch
source-lifetime rules remain a separate decision. These C10 fixtures remain
Domain scenarios; the Application integration tests below exercise the actual use case.

### Application

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
`CombatStateSnapshot`. Full equipment/stat aggregation is not implemented.
Pipeline composition is explicitly supplied and invoked anew per impact.

`CombatImpactResult` separates the Domain outcome from `Applied`,
`VersionConflict`, or `AlreadyApplied` commit status. Domain rejection has no
commit status and does not reach the adapter's commit method. Version conflicts
are returned without automatic retries or extra dice rolls. Exceptions propagate;
the adapter contract requires that failed commit leaves all proposed changes
unpublished. Snapshot version and participant identities are checked before
calculation, and each impact requires an empty dice trace.

The Application test project now uses real use-case orchestration and Domain
services with a stateful in-memory test adapter. Tests cover multiple targets,
successive HP/defeat transitions, source buffs added/removed after attack
construction, updated target resistance, status replacement, version conflict
and explicit retry, Domain/pipeline/adapter failure, replay protection, inconsistent
snapshots, self-targeting, changes to both participants and dice-trace isolation.
The existing opt-in combat diagnostic formatter is reused through linked test
sources, without a production logging dependency.

```powershell
dotnet test tests/RollForHonor.Application.Tests --filter FullyQualifiedName~CombatImpactServiceTests
```

`AttackInstance` is an impact context obtained from a newly accepted launch.
Its constructor is now internal, preventing ordinary external callers from
bypassing launch acceptance. Low-level impact tests have explicit friend-assembly
access to construct isolated contexts. Contact admission and observed lifetime rules
are implemented below; runtime state storage, Godot collision/animation wiring
and UI publication remain separate work.
The existing ordinary-attack validator still requires a living source;
post-launch source death is an open rule, not silently handled by a frozen
launch snapshot.

### Attack initiation and resource costs

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
the current in-memory implementation is test-only. It shares versions with the
impact test adapter so intervening combat changes can invalidate a launch read.

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
rules and the same game clock into the accepted attack instance. Runtime adapters,
production cost-effect definitions and Godot wiring remain future work; the console
host already supplies tool-local examples.

Tests cover additive and resource-specific reductions, policy replacement,
fractional/free costs, one charge across multiple impacts, miss behavior,
cooldown boundaries, insufficient resources, request replay, intervening state
changes, staged adapter failure and independent source/definition cooldowns.
Initiation results and cost breakdowns use the same opt-in test diagnostic switch;
impact calculations reuse the existing combat formatter.

```powershell
dotnet test tests/RollForHonor.Domain.Tests --filter FullyQualifiedName~ResourceCostResolverTests
dotnet test tests/RollForHonor.Application.Tests --filter FullyQualifiedName~AttackInitiationServiceTests
```

### Contact admission and observed lifetime (C10)

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
identifier or callback deduplication is invented. Rate-limited repeated contacts,
Godot runtime wiring and the post-launch source-death gameplay decision remain
separate work. Existing Domain validation of a defeated ordinary-attack source
is unchanged.

Twenty new Application cases check multi-target costs, per-target repeat blocking,
evaded contacts, first-contact termination, piercing/obstacle behavior, exact
expiry, no-contact expiry, animation completion, independent instances, conflict
and exception reservations, concurrent same-target callbacks, invalid rules and
clock-free status/diagnostic reads. The full solution
test totals are listed in the presentation-contract section below.

The console `contacts` command checks five fixed fixtures: piercing, first-contact
termination, repeatable damage, animation completion and an evaded once-only contact.
Each fixture validates its expected contact results, target HP and calculation count.
Ignored-contact summaries go to standard error; admitted launch/impact diagnostics
use the same views and JSONL sidecar as other modes.

```powershell
dotnet run --project tools/RollForHonor.Console -- contacts
dotnet run --project tools/RollForHonor.Console -- contacts --view steps
dotnet run --project tools/RollForHonor.Console -- contacts --view jsonl
dotnet run --project tools/RollForHonor.Console --launch-profile "Contacts - Summary + JSONL"
```

### Returned combat facts for presentation (I03)

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

I03 fills one demonstrated data gap: `DamageComponentRoll` retains the actual
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

Contract coverage includes independent d20 selection, component/bonus dice, all four
hit qualities, logging enabled/disabled, effects/identities, overkill, historical result
stability, rejection/conflict/replay/ignored contacts and periodic damage without d20.
Seven Domain cases validate component facts, formula consistency, overflow and immutable
collections; fifteen Application cases exercise the return boundary with real rules.
All 288 Domain and 88 Application tests pass locally. Existing console modes and their
17 launch profiles remain available; there is no new journal/GUI mode in this change.

### Godot

`RollForHonor.Godot` is the engine-facing layer responsible for scenes,
presentation, input, runtime composition, and future integration of combat
use cases.

### Detailed combat diagnostics (I02)

`RollForHonor.Infrastructure` implements Application's optional `ICombatDiagnostics`
port through `Microsoft.Extensions.Logging.ILogger`. Pass a `LoggerCombatDiagnostics`
instance to `CombatImpactService` and `AttackInitiationService` to observe both launch
and impact attempts. With no diagnostics dependency, or `NullLogger` and no JSONL
writer, intermediate capture is disabled. Domain has no logging dependency.

Four views use the same structured reports:

| View | Contents |
| --- | --- |
| `summary` (default) | One line per launch/impact, identities, attack outcome, final damage and actual commit status |
| `steps` | Ordered nested steps, before/after values, damage breakdowns, every die and final health projections/proposals |
| `full` | Complete labeled tree of inputs, snapshots, intermediate values, effects, modifier metadata, dice, proposals and results |
| `jsonl` | One complete JSON object per attempt for scripts and further inspection |

From the repository root:

```powershell
dotnet run --project tools/RollForHonor.Console -- diagnostics
dotnet run --project tools/RollForHonor.Console -- diagnostics --view steps
dotnet run --project tools/RollForHonor.Console -- diagnostics --view full
dotnet run --project tools/RollForHonor.Console -- diagnostics --view jsonl
dotnet run --project tools/RollForHonor.Console -- diagnostics --jsonl trace.jsonl
dotnet run --project tools/RollForHonor.Console -- diagnostics --errors-only
dotnet run --project tools/RollForHonor.Console -- diagnostics --target 00000000-0000-0000-0000-000000000002
```

The demo deliberately enables logging. It runs one accepted launch, four outcomes
(evaded/glancing/hit/critical), an impact version conflict and a rejected cooldown
attempt. Its fixture has `2d6 + 3` slashing and `1d4` fire damage, armor 30 and 50%
fire resistance. Two explicitly named demo cost reductions, 10% and 20%, change a
20-mana cost to 14 and project the balance from 100 to 86. These are demonstration
values, not gameplay content or equipment aggregation rules.

`--jsonl PATH` writes a full sidecar while retaining the chosen text view; the file
is overwritten at the start of a run. Filters `--attack GUID`, `--source GUID`,
`--target GUID` combine with AND. Target filters omit launch records, which have no
target. `--errors-only` selects exceptions, rejections and non-applied outcomes,
including version conflicts and replay receipts. A calculated damage value on a
`VersionConflict` line is a proposal, not applied HP loss. Failed commit reports
retain their calculated outcome separately from the absent successful result.

JSONL includes `schemaVersion: 1`, `kind` (`attackStart` or `impact`), correlation
identities and concrete `$type` labels for interface/abstract values. It is an
export format for diagnostics, not a save-game, replay or durable audit contract.
Step dice ranges are half-open `[start,end)` indexes into the report's dice list;
nested ranges can overlap. The report lists each die once, while step text also
shows the relevant rolls under their parent groups.

Domain capture records immutable values before/after steps and retains completed
steps plus the failing step when execution throws. Mandatory finalization records
health adjustments, clamped projections, canonical changes and the final result.
Attack-quality breakdowns report the actual configured multiplier. Modifier origins
and optional flat bonuses are supplied by calculating content; missing origins stay
null/unreported. Diagnostic output cannot infer an item/buff origin from a damage
difference. Launch reports include base/final costs, reductions, cooldown inputs and
resource-change projections; the enclosing status says whether they were applied.

Application keeps reporting mechanics outside its use-case bodies. Internal
`DiagnosticOperation<TResult>` wraps execution, safely checks diagnostic enablement,
reports success/failure and preserves the original operation exception even if output
also throws. `AttackStartDiagnostics` and `CombatImpactDiagnostics` retain the data
specific to one attempt and construct their respective reports. Each service enters
through one helper call; its core method contains business steps and explicit capture
checkpoints, without local publication functions or diagnostic try/catch blocks. Helper
instances are per attempt, so concurrent calls do not share mutable trace state.

Logging configuration, formatting and writer failures are isolated from gameplay;
no extra rolls, retries or state changes are introduced. An independent JSONL sink
can still succeed if the logger fails. Writes sharing a JSONL writer are serialized.
Hosts own and dispose their writers. The existing `RFH_COMBAT_DIAGNOSTICS=1`/`true`
switch still controls xUnit helper output (off by default in CI); it does not
configure production log providers or disable this explicit console demo.

Verification covers view/JSON fidelity, nested effects and bonus metadata, individual
dice, enabled/disabled equivalence, fresh state, rejection, version conflict, pipeline
and commit failures, missing read data, broken sinks, filters, costs and replay.
The solution builds without warnings; 288 Domain and 88 Application tests pass.
Console smoke, all four diagnostic views, JSONL sidecars, filters, invalid arguments
and a small opt-in benchmark run were checked locally. Godot runtime tests and remote
CI were not rerun for this local branch. UI publication and runtime Godot wiring remain
separate runtime/presentation work; I03 verifies the returned data contract and adds no combat feed.

### IDE launch profiles and the text duel

`tools/RollForHonor.Console/Properties/launchSettings.json` contains 17 shared
`Project` profiles. They launch the console project, with no Godot runtime:

| Profile | Purpose |
| --- | --- |
| `Smoke` | Assert six fixed Domain smoke scenarios; nonzero exit on failure |
| `Contacts - Summary + JSONL` | Five validated contact/lifetime fixtures plus `artifacts/contacts.jsonl` |
| `Contacts - Steps` | Same contact fixtures with intermediate calculation output |
| `Attacks - Summary + JSONL` | Four attack outcomes, conflict/cooldown rejection; short output plus `artifacts/attacks.jsonl` |
| `Attacks - Steps` | Fixed attacks with before/after step values and individual dice |
| `Attacks - Full` | Complete fixed-attack reports |
| `Attacks - JSONL` | Fixed-attack JSON objects on standard output |
| `Attacks - Errors` | Only non-applied/rejected fixed attempts |
| `Attacks - Target filter` | Fixed impacts against target `00000000-0000-0000-0000-000000000002`; omit launch records |
| `Battle - Summary + JSONL` | Complete seed-42 duel, up to 30 rounds, plus `artifacts/battle.jsonl` |
| `Battle - Steps` | First three battle rounds, with detailed step output |
| `Battle - Full` | First three battle rounds, with complete reports |
| `Battle - JSONL` | Complete seed-42 duel as JSONL on standard output |
| `Benchmark - All` | Attack, effect-tick and sequence performance workloads |
| `Benchmark - Attacks` | Attack workload only |
| `Benchmark - Effects` | Effect-tick workload only |
| `Benchmark - Sequence` | Four-impact sequence workload only |

In Rider, open `Properties/launchSettings.json` under the console project,
right-click it in Solution Explorer and choose **Generate Configurations** if
profiles are not already present. Then select a profile in the top Run/Debug
selector and run it. Gutter launch icons also work. Profile-based arguments are
edited in `launchSettings.json`; **Run > Edit Configurations** exposes Rider-specific
settings. These steps follow [JetBrains' launch-profile documentation](https://www.jetbrains.com/help/rider/Running_LaunchSettings.html).
IDE import itself was not exercised automatically; every profile was executed via
the .NET CLI. Existing personal Rider settings are preserved.

Use Debug for stepping through Domain/Application calculations. For benchmarks,
select the solution's **Release** build configuration and use **Run**, without the
debugger. A launch profile selects arguments, not the build configuration; Debug
benchmark runs show a warning. The equivalent explicit CLI commands are:

```powershell
dotnet run --project tools/RollForHonor.Console --launch-profile "Battle - Summary + JSONL"
dotnet run --project tools/RollForHonor.Console --launch-profile "Battle - Steps"
dotnet run --project tools/RollForHonor.Console -c Release --launch-profile "Benchmark - All"
dotnet run --project tools/RollForHonor.Console --no-launch-profile -- battle --seed 7 --rounds 40 --view summary --jsonl artifacts/custom-battle.jsonl
```

To add a configuration, copy one JSON profile, change its name and
`commandLineArgs`, save, then generate Rider configurations again. For example,
copy `Battle - Steps` and change `--seed 42 --rounds 3` to `--seed 7 --rounds 10`.
Visual Studio can also select the project's launch profiles. Automated xUnit tests
still run from the IDE test runner or `dotnet test`; console runs are scenarios,
not replacements for those tests.

The duel entry point is `TextCombatScenario.Run`, selected by `battle` in
`Program.cs`. It alternates Warrior/Mage turns, using real Application launch and
impact services, seeded dice and a tool-local state adapter. The Warrior starts
with 65 HP/24 stamina, the Mage with 55 HP/30 mana. Heavy slash and fire bolt cost
8 stamina/10 mana, each with a two-second cooldown. A round advances a simulated
clock by one second; cooldown/resource rejection falls back to a free basic strike.
These turn rules, sample content and adapter are console fixtures, not production
scheduling, balance or persistence. There is no interactive input or periodic-effect
scheduler in this duel.

Every applied hit updates current HP. Text narration shows attack choice, outcome,
damage, current HP/resources and the final winner or explicit round-limit draw.
Narration and the absolute sidecar path go to standard error, while technical reports
go to standard output, so JSONL output from the executable remains parseable. For
`dotnet run` JSONL redirection, use `--verbosity quiet` or consume the sidecar to
avoid CLI build/launch messages. The same seed reproduces dice/gameplay for the same
code/runtime; generated attack/resolution GUIDs differ between runs. Seed 42 ends
with the Warrior winning in round 28 after 55 committed impacts, at 26 HP; the Mage
has 0 HP and both starting resource pools are exhausted.

Output paths are relative to the run configuration's **Working directory**; the
absolute sidecar path is printed at startup. Parent folders are created automatically,
and an existing requested report is overwritten. Generated `artifacts` under the
repository/console project and all build folders are ignored by Git. Rider can use
a different working directory than the CLI; choose one explicitly in its configuration
if you want reports in a particular folder.

Local checks exercised all 17 launch profiles, the normal/short duel, multiple seeds,
view-equivalent gameplay, resource/cooldown rejection, valid JSONL, defeat and invalid
CLI arguments. Console build passed without warnings. Unit-test results for I02 are
288 Domain and 88 Application tests; the contact/lifetime extension also adds Application admission rules.

### Console scenarios

`tools/RollForHonor.Console` is a separate executable host outside Infrastructure
and Godot. Smoke/benchmark modes exercise Domain directly; diagnostics mode composes
Application with the Infrastructure logging adapter and tool-local state fixtures.
The automated text duel is a tool fixture; interactive console gameplay and
production runtime state adapters remain future work.

From the repository root:

```powershell
dotnet run --project tools/RollForHonor.Console -- smoke
dotnet run --project tools/RollForHonor.Console -c Release -- benchmark --scenario all --iterations 10000 --warmup 500 --samples 5 --seed 42
dotnet run --project tools/RollForHonor.Console -c Release -- benchmark --scenario attack --json benchmark.json
```

With no arguments it runs smoke checks. Smoke uses scripted dice and checks
explicit outcomes for evaded, glancing, normal and critical attacks, a sequence
ending in defeat, and effect application followed by ticks after source death.
It returns a nonzero exit code on failure and runs in CI. Invalid CLI arguments
return exit code 2.

Benchmark mode is opt-in and has no hardware-dependent pass threshold. Select
`attack`, `effect`, `sequence`, or `all`. Each sample uses the same seed, performs
an unmeasured warmup, then measures an exact operation count on one thread. A
sequence operation calculates four attacks with local recipient-state projection;
the other operations calculate one resolution each against fixed recipient
fixtures. The effect fixture is applied before measurement, so this scenario
measures ticks, not application or scheduling.

The console reports median/range of sample resolution throughput and allocated
bytes per operation. Optional JSON includes each sample's elapsed time,
operations and resolutions per second, allocation, GC counts and checksum,
plus runtime, OS, architecture, configuration and workload settings. Measurement
includes request construction, dice and step traces, finalization and result
consumption. It excludes fixture setup, Godot, rendering, production commits and
concurrency; throughput is not an estimate of the game's supported entity count.
Benchmark reports are machine-specific artifacts, not committed baselines.

## Godot runtime composition

`GameBootstrap` creates one session in `_EnterTree`, before child nodes enter
the tree. It exposes `CombatState` for explicit participant/resource registration
and `Attacks` for launching an `AttackDefinition` with request/source identities.
An accepted launch returns an `AttackInstance`; call its `ResolveContact` for
each eligible physical contact and inspect the returned commit result. Resources
and cooldown are committed at launch, not charged again on each impact.

`InMemoryCombatState` implements both Application state ports. It stores immutable
combatant views, resources, cooldowns and deduplication receipts. Reads and writes
are synchronized; each successful transaction publishes one replacement state.
A conservative session-wide version rejects stale proposals without partial
changes or automatic retries. State is session-local and is not persisted; this
is not a complete character/inventory model.

The baseline `BasicAttackPipelineFactory` composes attack rolls, hit quality,
typed damage, defense and new-status application. Runtime coefficients are
`0 / 0.5 / 1 / 2` for evaded/glancing/hit/critical. `NoResourceCostModifiers`
explicitly supplies no reductions. The factory also selects a single supported
`LifestealDefinition` from the payload's copied `Behaviors` collection, after
target damage/health effects. Behavior definitions are separate from `Effects`,
which still contains statuses to apply. Unknown behaviors and repeated lifesteal
definitions are rejected rather than silently stacked. Equipment/stat aggregation
and selection of arbitrary existing-status behaviors remain separate work.
`RandomDiceRoller` uses `Random.Shared` with a fresh trace per impact or tick;
an explicit `Random` can be supplied for reproducible non-engine tests.

Cooldowns and attack lifetime share accumulated Godot game time. `_Process`
consumes the supplied delta once, already scaled by Godot; a paused tree does
not advance the clock. Configure the bootstrap's `CombatDiagnosticsEnabled`
and `DiagnosticView` before tree entry to send optional reports to Godot Output.
Diagnostics default to off and are separate from the scene's result-based journal.

Normal Play starts the runtime without automatically running tests or quitting.
Bootstrap loads `Game.tscn` as a child so the session remains alive.

### Two-object combat scene

Build the solution, open `godot/project.godot` in Godot .NET and press **F5**
to run the project through `Bootstrap.tscn`. Open `Game.tscn` to edit its
objects visually; it depends on its parent bootstrap and cannot run standalone
with F6.

The scene includes a player, enemy, floor, camera, lighting and an editable
combat workbench. Press **Space** or **Player attacks** to launch a projectile;
press **E** or **Enemy attacks** to attack the player. Each side owns its mana
and cooldown. A successful
launch costs 10 of the initial 100 mana and starts a 0.8-second cooldown. Failed
launches create no projectile. Each arrow travels at 6 units per game second,
ends at its first target/obstacle contact, or expires after 3 game seconds.
**Restart session** reloads the bootstrap and resets both state and visuals.

`DemoCombatant` maps scene bodies to session identities. `DemoProjectile`
forwards real `Area3D.BodyEntered` callbacks to the accepted attack; moving the
visual alone does not calculate damage. Actors use collision layer 2 and
environment bodies layer 3; the projectile monitors both and ignores its source.
The sample speed/shape relies on discrete overlap detection; this is not a
high-speed projectile or swept-collision implementation.

The scene uses a 1920×1080 design viewport, opening in a 1280×720 window with
canvas scaling. Both stat panels expose current/maximum HP, armor, evasion,
attack modifier, damage dice/count/flat bonus/type and every supported typed
resistance. Resistance inputs are percentages; negative values are weaknesses.
**Apply draft stats** explicitly applies HP/defense edits, including any draft
current HP, while preserving live statuses, mana, cooldowns and receipts. Invalid
HP bounds leave state unchanged and produce a journal entry. Weapon/effect
selections affect the next launch; defense edits affect subsequent contacts,
including projectiles already in flight. No equipment/stat aggregation is implied.

The journal uses returned calculation data, independently of optional diagnostic
output. It shows source/target names, admission and commit status, attack roll
and modifier, input defenses, ordered typed damage stages and coefficients,
component dice/flat bonuses, complete dice trace, effect changes and actual
clamped HP changes. Damage types have distinct colors; proposed changes are
marked separately from applied changes. The separate d20 repeats only the
natural die value. Text can be selected and copied; Clear removes the journal
and the view retains at most 100 entries. Modifier tooltips are not implemented.

`DemoEffectCatalog` is a small fixture catalog outside Domain. Burning and
Bleeding selections attach existing `PeriodicDamageEffectDefinition` values to
the next attack payload (test formulas: 1d4 Fire and 1d4 Slashing). Active statuses
and their application/replacement appear in the live panel and journal, with a
captured source. The existing baseline applies selected statuses on processed
contact, including evaded contacts; this work does not introduce a new successful-
hit condition. Production bleeding-specific defense rules and status durations
remain unspecified; the fixture tick pipeline uses current armor/resistance rules.

Enable **Vampirism on attack** and set its percentage (0–100) on either side.
`LifestealEffect` heals from actual opponent HP lost after defenses and any prior
target health adjustments, excluding overkill. Healing rounds upward to whole
HP and finalization clamps the source to maximum HP. Target damage and source
healing publish atomically in the same resolution. Self-damage, ticks and zero
actual HP loss do not trigger vampirism. The behavior must be scheduled last
among damage/target-health effects; subsequent steps must not alter that basis.

**Next tick** advances one global workbench round: each periodic instance on
living participants is attempted once, as a separate transaction with fresh
recipient state, dice and resolution identity. Source state remains captured
at application, even after source death. Removed/replaced/uncaptured instances
and defeated recipients are rejected before dice. Ticks do not reroll d20,
charge mana, restart cooldowns or trigger vampirism. Conflicts return without
hidden retries; a later round is a new attempt, not a replay of the failed one.

Enable **Automatic ticks** to run those same rounds at the selected interval
(default 1 game second). Manual ticking is disabled while automatic mode is on.
Mode/interval changes restart the deadline; game-time pause holds it, and elapsed
intervals are processed in order. This is a scene-wide test clock, not individual
effect application-time scheduling. Statuses have no automatic expiry in this
slice and remain until replacement/reset; defeated recipients stop ticking and
can resume if explicitly restored through the editor. The journal identifies
each tick and its captured inputs while keeping the last attack's d20 display.
`PeriodicTickResult` returns these facts directly; it does not masquerade as an
attack diagnostic report. No weapon-property pipeline stage is introduced.

Run engine tests explicitly from the repository root with the Godot .NET
executable on PATH (or substitute its absolute path):

```powershell
dotnet build RollForHonor.sln
godot --headless --editor --path godot --quit
godot --headless --path godot --run-tests --quit-on-finish
```

Local I06 verification used Godot 4.7.2 .NET: all 17 engine tests and 425 .NET
tests passed. Runtime tests cover shared launch/impact state, once-only costs,
expiry, pause and supplied-delta handling. Adapter tests also cover conflicts,
concurrent launch attempts, duplicate receipts and rollback of invalid changes.
Seven scene integration cases additionally exercise actual projectile/target
and obstacle contacts, cooldown refusal, missed-projectile expiry, editing
defenses during flight, enemy attacks, status selections, journal facts and draft
validation, without directly invoking collision callbacks. Two adapter cases
verify explicit host edits, preserved bookkeeping and stale-proposal rejection.
Rendered Full HD workbench and exchanged-attack frames were inspected locally
with the Compatibility renderer.
I06 adds 14 Domain cases for healing basis, overkill, rounding, defenses, target
adjustments, self-damage and immutable content; 12 Application cases verify
atomic lifesteal composition and independent periodic attempts, captured/dead
sources, fresh defenses, invalid instances, conflicts and used-dice rejection.
Three new engine cases exercise real-impact vampirism, manual ticks after source
death, automatic rounds, pause and interval/mode changes. New rendered captures
show healing and both tick modes in the visual history.

Rendered milestone screenshots and their source context are preserved in the
[visual development history](docs/development-history/README.md).

## Technology

- Godot 4.7.2 with C#
- .NET 8
- Jolt Physics
- xUnit
- Chickensoft GoDotTest
- GitHub Actions

## Repository Structure

| Path | Purpose |
| --- | --- |
| `src/RollForHonor.Domain` | Engine-independent game and combat rules |
| `src/RollForHonor.Domain/Combat/*/Models` | Immutable combat data, results, snapshots, and value objects |
| `src/RollForHonor.Domain/Combat/*/Services` | Combat contracts and deterministic rule implementations |
| `src/RollForHonor.Domain/Combat/Damage/Configuration` | Tunable raw-damage coefficients |
| `src/RollForHonor.Infrastructure` | Diagnostic views, in-memory combat state and runtime dice |
| `src/RollForHonor.Application` | Application use cases and orchestration |
| `godot` | Godot project and engine integration |
| `tests/RollForHonor.Domain.Tests` | Domain unit tests |
| `tests/RollForHonor.Application.Tests` | Application tests |
| `tools/RollForHonor.Console` | Standalone smoke, diagnostic demo and benchmark scenarios |
| `.github/workflows` | Continuous integration configuration |

## Verification

Every pull request and push to `main` is verified by CI.

The pipeline:

1. restores .NET dependencies;
2. builds the complete solution;
3. runs Domain and Application tests;
4. runs the console smoke scenarios;
5. imports the Godot project in headless mode;
6. runs the Godot test suite.

## Roadmap

Near-term development remains combat-first and includes:

- defense balance tuning;
- production effect content and runtime resource adapters;
- persistent-effect scheduling and effect-specific gameplay policies;
- projectile impact resolution;
- runtime state adapters and committed-result presentation;
- Godot combat presentation and runtime integration.

Player movement, procedural maps, and saving and loading will follow the
combat foundation.

The roadmap describes the intended direction and does not imply that these
features are already implemented.

## Copyright and Usage

Copyright © 2026 Ruslan. All rights reserved.

Unless otherwise stated, the original source code and original project assets
in this repository are proprietary.

This repository is made publicly available for reviewing its architecture and
implementation. It is not open-source software.

Except for the limited rights granted by the GitHub Terms of Service and any
rights provided by applicable law, no permission is granted to use, copy,
modify, build, distribute, sublicense, sell, commercially exploit, or create
derivative works from this project, in whole or in part, without prior written
permission from the copyright holder.

Third-party software, libraries, tools, and assets remain subject to their
respective licenses and terms.
