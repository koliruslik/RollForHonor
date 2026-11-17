# Visual development history

Real screenshots of the running Godot project, preserved alongside development
milestones. These are development evidence, not concept images or a substitute
for automated tests. Earlier captures remain unchanged as the scene evolves.

## 2026-11-17 — I05 combat scene and editable workbench

Scene: `godot/Game.tscn`, hosted by `Bootstrap.tscn`.
Engine: Godot 4.7.2 .NET, Compatibility renderer, NVIDIA GeForce RTX 3060.
Screenshots were captured from the running viewport and visually inspected.

| Capture | Stage | Resolution | Source state |
| --- | --- | --- | --- |
| [Initial scene](2026-11-17/01-initial-scene.png) | Two combat objects, camera, lighting and basic HUD | 1152×648 | Initial I05 slice, subsequently published in PR #21; reference `4701fa4` |
| [Projectile flight](2026-11-17/02-projectile-flight.png) | Accepted launch and visible projectile | 1152×648 | Initial I05 slice |
| [Initial impact](2026-11-17/03-initial-impact.png) | Changed target HP, mana and separate d20 | 1152×648 | Initial I05 slice |
| [Editable workbench](2026-11-17/04-editable-workbench.png) | Both stat editors and detailed journal layout | 1920×1080 | Local expansion after `4701fa4`; uncommitted when captured |
| [Workbench combat journal](2026-11-17/05-workbench-combat-journal.png) | Exchanged attacks, applied Burning/Bleeding statuses and calculation details | 1920×1080 | Local expansion after `4701fa4`; uncommitted when captured |

The expanded workbench exposes status application only; these frames do not
demonstrate automatic periodic ticks or vampirism. Dice results vary per launch.

### Initial combat slice

![Initial scene](2026-11-17/01-initial-scene.png)

### Expanded workbench after exchanged attacks

![Editable workbench combat](2026-11-17/05-workbench-combat-journal.png)
