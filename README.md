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
- automated Domain and Application tests;
- a standalone console host for smoke scenarios and opt-in performance measurements;
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
Application integration will select active effects, assemble pipelines, schedule
ticks and commit state. The existing replacement-by-definition behavior remains.

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
consumer boundary; production stat aggregation and Application state commits
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

### Application

`RollForHonor.Application` is reserved for use cases, transaction coordination,
state commits, and ports to external systems.

This layer is still at an early stage and does not yet contain the complete
combat application flow.

### Godot

`RollForHonor.Godot` is the engine-facing layer responsible for scenes,
presentation, input, runtime composition, and future integration of combat
use cases.

### Console scenarios

`tools/RollForHonor.Console` is a separate executable host referencing Domain,
outside Infrastructure and Godot. It currently runs combat scenarios; full
console gameplay and Application integration remain future work.

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
| `src/RollForHonor.Application` | Application use cases and orchestration |
| `godot` | Godot project and engine integration |
| `tests/RollForHonor.Domain.Tests` | Domain unit tests |
| `tests/RollForHonor.Application.Tests` | Application tests |
| `tools/RollForHonor.Console` | Standalone smoke and benchmark scenarios |
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
- combat effects and resource costs;
- persistent-effect scheduling and effect-specific gameplay policies;
- projectile impact resolution;
- application-level state commits;
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
