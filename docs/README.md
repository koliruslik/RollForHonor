# Technical documentation

[Back to project overview](../README.md)

| Guide | Read it for |
| --- | --- |
| [Character stats](stats.md) | Base definitions, modifier stacking, runtime ownership and combat projection |
| [Items and affixes](items.md) | Rarity rules, immutable items, weighted procedural generation and stat inputs |
| [Equipment](equipment.md) | Slot rules, ownership, atomic equip/unequip and non-equipment stat inputs |
| [Game interface](interface.md) | Screens, HUD, inventory, searchable reference, shared explanations and localization readiness |
| [Combat](combat.md) | Layer responsibilities, calculation order, damage/defense rules, launch/contact contracts and returned facts |
| [Godot](godot.md) | Runtime composition, arena controls, physical delivery, editable laboratory, vampirism and periodic rounds |
| [Console](console.md) | Smoke scenarios, diagnostic views/JSONL, IDE launch profiles, text duel and benchmark interpretation |

These guides describe current behavior and fixture limitations. Planned features
are identified explicitly; project history is not used as setup instructions.

## Verification

Run commands from the repository root. Install .NET 8 SDK; engine checks also
require Godot 4.7.2 .NET. In the commands below, `godot` means that executable
on PATH; substitute its actual path/name if needed.

```powershell
dotnet restore RollForHonor.sln
dotnet build RollForHonor.sln --configuration Debug --no-restore
dotnet test RollForHonor.sln --configuration Debug --no-build --no-restore
dotnet run --project tools/RollForHonor.Console --configuration Debug --no-build --no-restore -- smoke
godot --headless --editor --path godot --quit
godot --headless --path godot --run-tests --quit-on-finish
```

The .NET test command runs the Domain and Application suites. It does not start
Godot: engine tests require the final command above. Build Debug before using
the runtime test flags. Normal game startup does not run tests automatically.
The [CI workflow](../.github/workflows/ci.yaml) performs these same stages.

| Coverage | Sources |
| --- | --- |
| Calculation rules and immutable contracts | [Domain tests](../tests/RollForHonor.Domain.Tests) |
| Initiation, contacts, adapter commits and ticks | [Application tests](../tests/RollForHonor.Application.Tests) |
| Engine composition, workbench, melee and arena physics | [Godot tests](../godot/tests) |

For focused .NET cases, use `--filter FullyQualifiedName~AttackContactTests` or
the relevant test-class name. Console scenarios supplement automated tests.

## Diagnostic test output

Detailed xUnit combat output is opt-in through `RFH_COMBAT_DIAGNOSTICS=1` or
`true`; it is off by default in CI. This switch does not enable runtime Godot
logging or configure console demo providers. See [diagnostics](console.md#detailed-combat-diagnostics)
for runtime views, filters and JSONL.
