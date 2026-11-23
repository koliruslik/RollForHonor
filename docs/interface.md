# Game interface

[Project overview](../README.md) · [Documentation index](README.md)

## Screens and navigation

Normal Play opens the title screen through `Bootstrap.tscn`. Start Game loads
`ArenaBootstrap.tscn`, creating a fresh combat session and starting kit. Settings
opens the same presentation-preference panel used in the pause menu. Reference
opens the shared game guide without creating a session. Exit closes the application.

The game uses a shared native Godot Theme: charcoal/slate panels, muted metal
accents and serif headings with a non-pixel body font. The arena uses the same
stone and metal palette. Rarity supplies the tint of each item's own frame and
header; epic purple is not a global interface color. Two-pixel antialiased frames
remain visible at fractional viewport scales. Item icons use independent centered
TextureRects instead of Button's combined text/icon placement.
Window-close controls share a compact button style and a square 40-by-40 design
size; their padding does not inherit the larger action-button minimum height.

The HUD scales by viewport height and fills its actual width. Character stats dock
to the left edge (360 design pixels), inventory to the right (560), both full-height.
Selected and equipped-item cards use 320-pixel widths beside inventory. Each
card's height follows its rendered properties and visible actions; comparison
cards omit the action space. Alt details and wrapped text can increase its height.
Cards are limited to the available screen height, with property scrolling on long
pinned cards. The action and result keep their own spacing below the properties.
The bottom
combat bar stays centered in the viewport; side panels never move the HUD. The title/settings use a
centered 1920×1080 design surface. Captures cover 1920×1080, 1280×720 and ultrawide.

| Input | Result |
| --- | --- |
| I or Inventory button | Open/close inventory independently |
| C or Character button | Toggle the character panel independently |
| H or Reference button | Open the searchable reference and pause the game |
| Escape or Menu button | Close the current overlay, or open/close pause |
| R | Restart the arena while gameplay is active |
| F2 | Switch between the arena and the combat laboratory |

Pause offers Resume, Restart, Reference, Settings and Main Menu beneath a centered Session
heading. Full application exit is available from the title screen. Restart and
returning through Start Game create fresh sessions; current equipment
and resources are not saved. Settings persistence is separate from game saving.

## HUD and character information

The HUD displays current health/mana reservoirs, the last committed combat d20,
the selected target's actual HP/state, the current objective and the attack cooldown
state. The minimap draws current map footprints and real player/opponent
positions. Objectives have a translucent background. Native controls intercept
clicks inside their panels; the uncovered world remains interactive. Inventory
and character windows do not pause movement, combat or the shared clock.

The character window follows the left-hand wireframe layout and reads resolved
stats from the owning adapter. Health and mana show current/max values. Typed
damage values show signed percentage bonuses: a resolved coefficient of 1.06
is displayed as +6%. Nonzero flat typed damage bonuses have separate signed rows.
Movement speed shows the percentage bonus relative to the character profile's
unmodified starting speed: 6 to 6.84 displays +14%, and unchanged speed displays
0%. With a zero baseline the percentage is undefined and displays a dash.
These values do not represent DPS estimates. Armor, evasion,
source attack bonuses and typed resistances use their real runtime
values. Reserved independent critical stats are not presented as active mechanics.

Pause, settings and the reference stop the world. Closing those screens restores the previous
pause state, preserving a pause held by another owner. Opening or closing character
stats never opens or closes inventory, and vice versa.

## Equipment, inventory and item cards

The right-hand paper doll places the main hand and offhand on opposite sides of
body armor, with the helmet, amulet, boots and two rings around them. A quiver
requires a bow; a shield permits a one-handed weapon or an empty main hand.
Changing/removing the main weapon returns incompatible dependent equipment to
the bag and removes its bonuses in the same committed change. Both offhand
categories have distinct icons and authored starter examples.

The bag has a fixed **12-column × 5-row** grid, without scrolling, hidden rows
or automatic growth. Base footprints come from `inventorySize` in the starter
JSON: bows/body armor/shields 2×3, helmets/boots 2×2, quivers 1×3 and jewelry 1×1.
Cells touch, and an item occupies one continuous rectangle. Dragging preserves
the grabbed cell; out-of-bounds and overlapping drops leave the layout intact.
Reordering survives refreshes and window reopening within the running session.
Positions are presentation state and are not serialized or stored in the combat
adapter. Item rotation is not implemented.

Right-click to equip/remove; drag to a compatible equipment slot or a free bag
rectangle. Replacement and removal check room for all returning items, including
a dependent quiver, before publication. A rejected action displays inventory
feedback and preserves equipment, stats and ownership. This is currently a UI
capacity check: future pickup/grant use cases must enforce the same fixed bag
before publishing ownership, since `RegisterOwnedItem` remains a storage grant.
The grid rejects an overflowing refresh atomically; it never adds hidden rows.

Hover an item for a cursor-following card clamped to the viewport. Left-click
pins it for comparison. Affix names, selected tiers and individual rolled values
are always visible beneath the base properties. Hold Alt to add captured ranges
and quality in the format `value [minimum/maximum] quality% tier`, plus the actual
tier pool of each affix. Releasing Alt hides this additional information while
keeping the affixes visible. T1 denotes the strongest content tier; pools can end
at T16, T15, T4 or another authored tier. A complete numbered pool is shown as
`T1–T16`; incomplete or custom pools list their actual IDs without inventing tiers.
The current starter-kit fixtures define only T2, so their available pool is T2;
complete progression and balance tables remain content work.
Damage and resistance text use matching, muted semantic colors for each of the
seven damage types. The common word "damage" stays neutral and is not part of
text links; the type or modifier is the underlined target. Item rarity retains its
original green, blue, purple and gold accents. Italic text identifies affix modifiers and properties changed by
affixes; rarity remains on the item frame and header. Underlined text is clickable.
Hover a modified property in a pinned card to preview its contributing affixes,
or click to inspect its explanation and captured affix links. Empty slots have darkened icons and a plus
marker; occupied slots have full-color icons and rarity frames.

## Linked explanations

Click underlined damage, resistance or armor text in an item card or character
sheet to open a stationary explanation. Related underlined concepts open further
cards, such as Piercing damage → Armor → Physical damage. Following another link
in a parent replaces its descendant branch. A card's close button removes that
card and its descendants; Escape closes only the deepest explanation before
closing the underlying inventory/character window. Independently pinned children
survive closing their parent. These cards never pause the game.

Each explanation has a pin button. Pinned cards survive closing inventory,
character stats or their source item details, and opening a new explanation.
Escape dismisses unpinned levels first and leaves pins alone. Closing a pinned
card explicitly removes it; unpinning makes it transient again. Drag any card by
its header to reposition it; its new position remains fixed and bounded by the
viewport. Pinning also keeps the current position. Pins belong to the running
session and are discarded when restarting or leaving it.

Hold the current details binding (Alt by default) while hovering an item to both
reveal its ranges and hold the cursor card still. The card accepts clicks while
Alt is held, allowing movement from the item into its underlined text. Releasing
Alt restores normal hover behavior; explanations already opened by clicking stay
available until closed. Captured affix explanation values do not change when Alt
is subsequently released or another branch is selected.

Cards fit their text, remain inside the design area and move together into side
columns when space permits. Longer branches cascade if all columns cannot fit.
Long explanation bodies scroll. Plain content is added through native rich-text
APIs; authored names and descriptions are not interpreted as markup.

The dark panels, semantic text accents and linked inspection take visual cues
from [PoE-style tooltip navigation](https://poe.ninja/docs/tooltips). The
explanations describe RFH rules: armor reduces only physical damage, physical
resistances also exist, typed resistance is capped at 100%, and damage types alone
do not automatically apply status effects. This is a presentation glossary, not
a replacement for Domain combat calculations.

## Item content and combat properties

Only implicit property keys appear in Base Properties. Affixes can modify those
values, including weapon damage, but affix-only stats are listed once in Affixes
instead of being duplicated as apparent base properties.

Weapon bases store actual attack frequency, mana cost and typed dice/flat damage
in Domain `WeaponProperties`. All authored starter bases have implicit properties.
The arena's launch-time content provider reads the equipped weapon, so the card
and launched dice/cost/cooldown agree. Without a weapon the prior prototype
attack remains available. Added flat weapon damage is included in the colored
damage formula; it is not rolled again.

The card's captured properties and its action use the current owned instance. Its action invokes
the existing Application `EquipmentService`, then refreshes slots and actual
stats. An item without equipment tags remains inspectable with a disabled equip
action. Two rings use distinct owned instances; a free right-ring slot is chosen
when the left slot is occupied. Replacing an occupied compatible slot retains the
old item in inventory. An equipped item's card offers Unequip. A second card
shows the actual item currently occupying the replacement slot, including its
base properties and affixes. Both cards have independent close buttons. Closing
selected-item details also closes its comparison without closing inventory.

`godot/Content/Items/starter-items.json` provides twelve authored starter items for
this UI pass. `StarterItemCatalog` creates Domain definitions and affix instances
with retained values and owner-scoped instance IDs. It does not implement enemy
drops or Luck-driven crafting. Starting-item creation uses the
editable `godot/Items/ItemRarityRules.tres` table. The scene grants the kit once
when registering its player; opening/refreshing UI never grants another copy.

Future export presets must include the authored JSON content in the package.
World pickup/drop flows, owned-item crafting, inventory save/load,
progression/class screens and game save/load remain separate work.

## Searchable reference

Reference is available from the title screen, pause menu, the HUD button and H.
The reader has a section filter, word-based search, an entry index and an article.
It covers combat, damage/resistances, stats, equipment, rarity, tiers, quality,
modifier stacking, controls and session behavior. Reserved or planned mechanics
are identified explicitly instead of presenting them as active rules.

`ReferenceCatalog` indexes the existing `CombatGlossary` topics and authored
`Content/Reference/topics.json`. Shared `ItemInspectionTopics` supplies the same
actual affix values, ranges, quality and tier pools used by item cards.
`InspectionContent` renders both the reference article and ordinary floating
cards. Related links open the existing `InspectionStack`; Open as card also
supports pinning and dragging. Pins survive closing the reader.

In a running game, opening the reference captures current owned items/affixes,
recent rolls and the retained combat journal. These are immutable inspection
snapshots, not generated sample values. Reopening refreshes them; existing pins
retain their captured facts. The title-screen reader omits session-specific
entries and never grants items. Reading does not reroll or resolve combat.

The reader pauses gameplay while open, returns to the pause menu if opened there,
and preserves a pause owned elsewhere. Search owns text input, preventing I/C/H
from triggering other UI shortcuts while typing. Escape closes transient cards
first, then the reader. Inventory/character windows retain their prior state.

## Localization readiness

The game currently displays English. Language selection and locale persistence
are not implemented. Godot's translation support can provide English (`en`) and
Ukrainian (`uk`) without changing Domain combat/item/stat rules. The presentation
layer requires a full text migration rather than only a Settings dropdown:

- Move labels, descriptions and formatted sentences to stable translation keys
  with placeholders. Localize item/affix names through stable content IDs.
- Persist the validated language with `PlayerPreferences` and apply it through
  `TranslationServer.SetLocale`, with English as the fallback.
- Refresh constructed rich text, reference search/indexes and captured card text
  on language changes while retaining original dice, ranges and numeric facts.
- Replace the English-specific damage-word markup rule with semantic text runs;
  verify Ukrainian glyphs, wrapping, percentages and longer labels in both locales.

Translation catalogs belong to Godot content; language-independent IDs and
calculation models stay unchanged. Reference entries already have stable IDs,
but the current captured `InspectionTopic` strings still need localization.

## Targets, combat state and recent results

Left-click a living combat body to select it; click it again or click the ground
to clear selection. A simple world-space projected frame highlights the selected
body. The top panel exists only for a living selection, showing its name and HP
numbers. Overhead bars omit names and values. The minimap shows every registered
training target and enemy, highlights selection and draws defeated bodies as crosses.

Domain `EngagementState` separates explicit selection from an automatic five-second
combat timeout. Godot `CombatAwareness` is a shared actor component on player and
enemy scenes; it reads health and the session clock. Committed attack interactions
refresh both actors' combat state. Manual selection survives timeout and unrelated
contacts; automatic selection expires, and defeat invalidates selection. These are
presentation/controller state, not new damage permission or aggro/pathfinding rules.

The compact Combat journal shows the latest outcomes and damage before/after
mitigation. Underlined outcomes open an attack explanation; damage totals open
recorded typed damage and links to its calculation stages. Details expands the
bounded history and adds direct dice links. Last Roll is a centered d20-shaped
control: click its value, shape or underlined caption to open the retained roll
history, then select an attack for its formulas, faces and bonuses. These use the
same pinnable, draggable explanation cards. Hover does not open a dice tooltip.
All values are retained result facts, not rerolls or parsed diagnostic strings. The F2 laboratory
continues to offer its separate technical journal.

Four skill cells contain an active basic attack icon and three locked placeholders.
The current mappings are RMB, Q, E and F; locked placeholders execute no skills.
A small translucent gameplay hint block above Menu reads current InputMap bindings.
It contains movement, zoom, attacks, targeting and window shortcuts. Opening
inventory or character stats hides that block: each panel has its own small,
muted footer with the actions relevant to that panel. Inventory lists inspect,
equip/remove, comparison, drag/drop, expanded affix details and close; character
stats lists scrolling and close. Both footers remain visible when both panels
are open. Gameplay hints are also hidden in pause and settings. Gameplay and
interface defaults are registered in `GameInputBindings` without overwriting
existing mappings. A controls-rebinding settings screen and binding persistence
are not implemented in this pass.

## Preferences and code ownership

The supported settings are windowed/fullscreen mode, vertical synchronization
and linear master volume, plus enemy overhead health and optional player
overhead health/mana bars. These bars contain no numbers; target HP numbers
remain in the top target panel. Apply saves `user://presentation.cfg` before applying
the new values. Back/close discards unapplied edits; Reset Defaults changes the
form until Apply is pressed. Invalid stored fields fall back to supported defaults
or clamp volume to [0,1]. Headless processes skip display-mode calls.

Presentation code lives in `godot/UI/Common`, `Menu`, `Settings`, `Hud`, `Reference`
and `Inventory`. `InspectionStack` owns explanation branches; `InspectionPane` draws
one card; `InspectionTopic` retains text and lazy related links. `CombatGlossary`
owns shared colors and rule descriptions. `ItemDetailsPanel.Properties` keeps
item rendering separate from card sizing and input. `ArenaHud` separates layout composition from interaction/lifecycle
coordination. Item rules and arithmetic remain in Domain; equipment commands
remain in Application. The legacy detailed combat laboratory and its result-based
journal remain available through F2. The arena's old diagnostic controls are
hidden, and retain the committed d20 bridge used by existing regression tests.

Engine tests exercise actual buttons, settings-file round trips, discard-on-close,
live equip/unequip and movement, independent windows, pause ownership, native
drop validation, right-click actions, comparison dismissal and non-equipment
item inspection. Additional cases verify stable HUD geometry, conditional target
visibility, weapon launch content and retained affix ranges. Rendered smoke checks
send real mouse drag, right-click, left-click and Alt events through the viewport. Captures cover navigation, settings, HUD, item cards,
pause and three viewport sizes. Development captures are stored outside the game
repository.

Inspection tests cover real modifier-event dispatch, stationary interactive Alt
cards, nested branch replacement, Escape/close ownership, captured affix facts,
typed character links and placement at viewport edges. Native rendered checks
also verify a real left-click on underlined item damage; deeper captured branches
use the same RichTextLabel metadata signals exercised by the engine tests.
Additional checks cover pinned-child survival, drag retention, affix-only display,
centered d20 geometry and linked journal facts from an actual physics contact.
