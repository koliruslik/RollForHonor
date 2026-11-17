# Console scenarios and diagnostics

[Project overview](../README.md) · [Documentation index](README.md)

## Console scenarios

`tools/RollForHonor.Console` is a separate executable host outside Infrastructure
and Godot. Smoke/benchmark modes exercise Domain directly; diagnostics mode composes
Application with the Infrastructure logging adapter and tool-local state fixtures.
The automated text duel continues to use tool-local state fixtures. Interactive
console gameplay is not implemented.

From the repository root:

```powershell
dotnet run --project tools/RollForHonor.Console -- smoke
dotnet run --project tools/RollForHonor.Console -c Release -- benchmark --scenario all --iterations 10000 --warmup 500 --samples 5 --seed 42
dotnet run --project tools/RollForHonor.Console -c Release -- benchmark --scenario attack --json artifacts/benchmark.json
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

## Detailed combat diagnostics

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
dotnet run --project tools/RollForHonor.Console -- diagnostics --jsonl artifacts/trace.jsonl
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

## IDE launch profiles and the text duel

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
code/runtime; generated attack/resolution GUIDs differ between runs.

Output paths are relative to the run configuration's **Working directory**; the
absolute sidecar path is printed at startup. Parent folders are created automatically,
and an existing requested report is overwritten. Generated `artifacts` under the
repository/console project and all build folders are ignored by Git. Rider can use
a different working directory than the CLI; choose one explicitly in its configuration
if you want reports in a particular folder.
