# Roll For Honor

[![CI](https://github.com/koliruslik/RollForHonor/actions/workflows/ci.yaml/badge.svg)](https://github.com/koliruslik/RollForHonor/actions/workflows/ci.yaml)

Roll For Honor is a 3D action-game prototype built with Godot and C#.
It combines a playable greybox arena with a combat laboratory for inspecting
d20 attacks, typed damage, defenses and effects.

## Current state

- WASD movement, mouse-facing, a following top-down camera and aimed projectiles.
- Rounds of ten randomly placed ranged enemies, appearing in successive pairs,
  with a completion banner and unlimited free time before starting the next round.
- Guaranteed world gold and optional equipment from accepted Sentinel tables; nearby gold is picked up automatically within 2.5 units (Ctrl+G toggles it). Items use manual pickup.
  G, mouse pickup and dragging into an open bag remain available. Drop bag items with the card button or by
  dragging them onto uncovered world space.
- Weapon affixes can poison, ignite or bleed. Chances normalize to actual attack speed; poison stacks independently, ignite/bleed refresh only their duration, and bleeding slows movement.
- Natural-one misses, rating-based glancing hits and armor, and a separate critical d20 (20+ succeeds, base ×2).
- Flat health/mana recovery stats, initially 2 HP/s and 10 mana/s for the player.
- An action journal for combat, defeats, pickups, equipment and sandbox changes.
- Session-local gold wallets, world pickup, and captured Luck for gold and generated affixes.
- F3 sandbox tools for monsters, actor stats, custom item/affix creation and drag-in editing of owned equipment.
- A separate editable laboratory with projectile/melee attacks, vampirism,
  Burning/Bleeding fixtures, manual/automatic ticks and a detailed combat journal.
- Shared character stats with editable player/enemy definitions and Flat/Increased/More modifiers.
- Four primary attributes in a 2×2 grid of clickable d20 tiles: Strength, Dexterity, Intelligence and Luck; level cap 10.
- Immutable item/affix models, configurable rarity capacity and procedural generation from a supplied pool.
- Paid item upgrades: guaranteed enhancement capped by rarity, evolution with excess-level transfer, locked full-affix rerolls and random affix addition. Open **Upgrade items** beside the inventory controls.
- Captured Luck bonuses for affix-tier d20 rolls and capped d100 quality/gold amounts; existing rolls remain immutable.
- Main-hand/offhand equipment with owned items, atomic stat updates and capacity-checked, version-checked equip/unequip operations.
- Styled title/settings/pause screens, target selection, clickable status icons, resource bars, combat history and dice traces.
- Floating damage shows separate numbers in distinct type colors, dim glancing hits, bold criticals and localized gray misses.
- Independent live inventory/character windows, muted damage colors, pinnable and draggable explanation cards, Alt inspection and right-click equipment.
- Character-sheet EHP averaged across six damage types, with click-only details for each type and all 15 mixed pairs.
- A searchable game reference with a bestiary, colored loot tables, 105 named bases across three branches and five generations (21 first-generation bases live), all 57 affix families and recent combat.
- Ukrainian/English interface with saved live language switching, localized item/affix names and searchable reference.
- Engine-independent combat rules and Application use cases, version-checked
  in-memory state, console scenarios and automated tests.

This is an early prototype. Training targets are stationary; the enemy uses
direct pursuit without pathfinding. Each run starts with one chosen Common bow, sword or wand.
All three temporarily use the same bow projectile, mana and cadence. Procedural maps and game save/load are not implemented.

Browse [live loot, per-monster rewards and all affix tiers](docs/loot-tables.md),
or open Reference in the game and choose Monsters, Items or Affixes.

## Run the prototype

Install the **.NET 8 SDK** and **Godot 4.7.2 .NET** (the C# edition).
From the repository root:

```powershell
dotnet restore RollForHonor.sln
dotnet build RollForHonor.sln --no-restore
```

Open `godot/project.godot` in Godot .NET and press **F5** for the main menu, then choose **Почати гру** (Start Game).
The interface initially uses Ukrainian. Choose **Мова / Language** in Settings
and Apply to switch immediately between Ukrainian and English; the choice is saved.
Launch with `godot --path godot --language en` to override the saved startup language;
see [localization](docs/interface.md#localization) for scope and catalogs.
Press **F2** to switch between the arena and combat laboratory. Switching scenes
or restarting creates a fresh session and resets its state.

### Arena controls

| Input | Action |
| --- | --- |
| WASD | Move relative to the camera |
| Mouse | Face the cursor surface |
| Right click | Shoot in the world; equip/remove items inside inventory |
| Left click | Select/clear a world target; pin an item for comparison |
| Alt | Show affix ranges, quality and current tier while inspecting items |
| Mouse wheel | Zoom |
| F3 | Toggle live admin tools |
| G | Pick up the selected nearby item within 2.5 world units |
| Ctrl+G | Toggle automatic gold pickup |
| I / C | Toggle inventory / character stats independently; gameplay continues |
| H / Reference | Open the searchable game reference; gameplay pauses |
| Escape | Close an overlay or open pause/settings/navigation |
| R | Restart the arena |
| F2 | Open the combat laboratory |

After the tenth defeat, explore and collect loot for as long as you like.
Choose Next round to start another five pairs; health, mana, equipment and
uncollected loot carry over. Primary attributes currently default to zero;
XP and point awards remain unconfigured. STR/DEX/INT are equipment requirements, not direct combat bonuses.

The laboratory provides stat editors and attack/tick buttons. See the
[scene guide](docs/godot.md) for its controls and fixture limitations.

## Development

```powershell
dotnet test RollForHonor.sln --no-build --no-restore
dotnet run --project tools/RollForHonor.Console --no-build --no-restore -- smoke
```

[Verification instructions](docs/README.md#verification) also cover the actual
Godot test process. CI builds, tests and imports the engine project on pull
requests and pushes to `main`.

| Location | Contents |
| --- | --- |
| `src/` | Domain, Application and Infrastructure projects |
| `godot/` | Scenes, controls, physics and presentation |
| `tests/` | Domain and Application tests |
| `tools/RollForHonor.Console/` | Smoke scenarios, diagnostics, text duel and benchmarks |
| `docs/` | Technical guides and verification instructions |

## Documentation

- [Documentation index and verification](docs/README.md)
- [Combat architecture, rules and result contracts](docs/combat.md)
- [Godot runtime, arena and combat laboratory](docs/godot.md)
- [Game interface, equipment windows and settings](docs/interface.md)
- [Console scenarios, IDE profiles, diagnostics and benchmarks](docs/console.md)

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
