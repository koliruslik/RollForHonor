# Roll For Honor

[![CI](https://github.com/koliruslik/RollForHonor/actions/workflows/ci.yaml/badge.svg)](https://github.com/koliruslik/RollForHonor/actions/workflows/ci.yaml)

Roll For Honor is a 3D action game built with Godot and C#.

The project is in early development and currently focuses on building a
deterministic, testable combat foundation before expanding into full gameplay.

## Project Status

The current version is a technical foundation rather than a complete playable
game.

The latest combat milestone adds production weapon raw-damage resolution to
the configurable d20 attack outcome rules. An attack is evaluated against the
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

## Implemented

- deterministic dice formulas, rolls, and trace data;
- immutable combat requests and combatant snapshots;
- combat request validation before dice are rolled;
- atomic resolution of one impact against one target;
- structured combat results with proposed state changes;
- configurable d20 attack outcome rules;
- configurable natural-roll upgrades and downgrades;
- configurable outcome multipliers for weapon raw damage;
- typed weapon damage dice, flat bonuses, zero clamping, and per-component
  upward rounding;
- ordered raw-damage calculation breakdowns before target defenses;
- engine-independent Domain logic;
- automated Domain and Application tests;
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

### Application

`RollForHonor.Application` is reserved for use cases, transaction coordination,
state commits, and ports to external systems.

This layer is still at an early stage and does not yet contain the complete
combat application flow.

### Godot

`RollForHonor.Godot` is the engine-facing layer responsible for scenes,
presentation, input, runtime composition, and future integration of combat
use cases.

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
| `.github/workflows` | Continuous integration configuration |

## Verification

Every pull request and push to `main` is verified by CI.

The pipeline:

1. restores .NET dependencies;
2. builds the complete solution;
3. runs Domain and Application tests;
4. imports the Godot project in headless mode;
5. runs the Godot test suite.

## Roadmap

Near-term development remains combat-first and includes:

- armor, resistance, and defense rules;
- combat effects and resource costs;
- persistent-effect ticks;
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
