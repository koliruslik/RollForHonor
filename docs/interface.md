# Game interface

[Project overview](../README.md) · [Documentation index](README.md)

## Screens and navigation

Normal Play opens the title screen through `Bootstrap.tscn`. Start Game loads
`ArenaBootstrap.tscn`, creating a fresh combat session and a once-only choice of starting bow, sword or wand. Settings
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
Selected and equipped-item cards use 400-pixel widths beside inventory. Each
card's height follows its rendered properties and visible actions; comparison
cards omit the action space. Alt details and wrapped text can increase its height.
Cards are limited to the available screen height, with property scrolling on long
pinned cards. The action and result keep their own spacing below the properties.
The bottom
combat bar stays centered in the viewport; side panels never move the HUD. The title/settings use a
centered 1920×1080 design surface. Captures cover 1920×1080, 1280×720 and ultrawide.

Committed hits and periodic damage display floating amounts around enemies and
training targets. Each damage type has its own post-defense number using the
shared type color. Glancing hits use pale text, critical hits use bold text,
and evaded attacks display a gray localized Miss / Промах. Numbers rise and fade,
follow camera projection, survive target removal and freeze while paused.

| Input | Result |
| --- | --- |
| I or Inventory button | Open/close inventory independently |
| C or Character button | Toggle the character panel independently |
| G | Pick up the selected nearby item within 2.5 world units |
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
stats from the owning adapter. Its top area contains Strength, Dexterity,
Intelligence and Luck in a 2×2 arrangement of faceted d20 tiles, with level / 10 above. Each tile has an icon
above its resolved value. Click or focus/activate it to open the shared
explanation, including the intended role and current implementation limits.
The attributes default to zero; STR/DEX/INT gate equipment and Luck affects captured loot rolls.
Health and mana show current/max values. The EHP row shows average full-health EHP across six damage types. It has no
hover tooltip; click it for each type and all 15 mixed 50/50 pairs, with nested
resistance and defense explanations. Its reference assumptions are described in
[Character stats](stats.md#effective-health-display). Typed
damage values show signed percentage bonuses: a resolved coefficient of 1.06
is displayed as +6%. Nonzero flat typed damage bonuses have separate signed rows.
Movement speed shows the percentage bonus relative to the character profile's
unmodified starting speed: 6 to 6.84 displays +14%, and unchanged speed displays
0%. With a zero baseline the percentage is undefined and displays a dash.
These values do not represent DPS estimates. Armor, evasion,
source attack bonuses and typed resistances use their real runtime
values. Critical roll bonus and multiplier describe the active independent d20 check.

Pause, settings and the reference stop the world. Closing those screens restores the previous
pause state, preserving a pause held by another owner. Opening or closing character
stats never opens or closes inventory, and vice versa.

When all ten enemies are defeated, a nonmodal banner announces the end of the
round and free time. Continue exploring dismisses it; Next round remains
available beside the objective. No countdown or automatic wave interrupts loot
collection. Two enemies appear at a time, with the next pair waiting for both
defeats. Health and mana recover from their resolved flat points-per-second
stats, initially 2 HP/s and 10 mana/s, including free time. The sheet displays
these rates as units per second rather than percentage bonuses. Recovery stops
on pause and after defeat; fractional HP accumulates until a whole point can heal.

Playable rounds automatically collect only gold within 2.5 world units, checked
every 0.25 game seconds. Ctrl+G or the compact Auto gold toggle in Nearby loot
switches the mode for the current run, with a notification and journal entry.
`GreyboxArena.AutoGoldPickupEnabled` defaults to true on a fresh run. Item pickup
always requires G, click or drag; automatic gold does not require bag space.
Pause, defeat, starter selection and dragging suspend automatic gold pickup.
Text-field focus suppresses the hotkey, and Ctrl+G does not also activate plain G.

Nearby items still appear in a scrollable menu within 2.5 world units. The nearest is
selected initially; hover selects another row for G, click takes that exact item,
and dragging a row into the open inventory places it in the chosen free rectangle.
The compact translucent menu grows to at most five visible rows and scrolls
additional entries. A high-contrast five-game-second notice reports full bags.
Character-sheet scrolling is consumed by the GUI and does not zoom the world.
Rows persist while dragging, and moving out of range rejects the pickup. Failed
pickup preserves the world item and displays feedback even with inventory open.

Gold has its own wallet value in inventory. Captured gold on the ground appears
in the nearby menu and is collected automatically or with G/click, without occupying bag
cells. Its amount is retained across rejected pickups. Sentinel guarantees 6–14
gold on the ground and optional equipment; see [rewards](rewards.md).

## Action journal and admin tools

The bounded action journal uses consecutive rows without blank separators and retains combat calculations and dice links alongside
defeats, pickups, dropped items, equipment changes and admin mutations. Noncombat
entries do not replace the last d20 result. Gold pickups also appear in this journal.

F3 opens compact live World/Items/Stats tabs. Spawn current catalog enemies,
create current catalog items, or edit player/enemy base stats; equipment bonuses
remain composed through the canonical adapter. The world keeps running. Sandbox
enemies do not count toward round completion. A full bag places an admin-created
item on free ground near the player. Admin gold grants are not implemented.

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
feedback and preserves equipment, stats and ownership. World pickups use
`ItemAcquisitionService` to validate the same complete fixed-bag layout before
a version-checked ownership grant. `RegisterOwnedItem` remains an administrative
storage grant for initial kits and fixtures.
The grid rejects an overflowing refresh atomically; it never adds hidden rows.
Select a bag item and choose Drop item on its card, or drag it out of inventory
onto uncovered world space, to leave it on free ground near the player. Native drag routing
keeps drops over inventory/UI controls inside those controls; failed bag moves
do not fall through into world release. Equipped items can also be released directly from their card or dragged from the
paper doll; ownership and equipped bonuses are removed together. Dependent
equipment, such as a quiver returned after its bow is dropped, must fit in the bag
before the operation is accepted.
Dropping and picking up preserve the exact item identity and captured rolls.

Hover an item for a slightly translucent card placed beside the source. It
chooses a free right/left/below/above position within the viewport, avoiding
the cursor and hovered item/property whenever space permits. Alt inspection
retains its stationary interactive behavior. Hover cards hide during dragging. Left-click
pins it for comparison. Cards use 400 design pixels of width and compact
value-first property lines. Affix family names are not repeated as headings.
Equal grouped damage, resistance and attribute modifiers share one concise line;
status affixes show chance and a compact periodic formula. The default view hides
all tier labels. Alt adds only each captured current tier, range and quality;
available tier pools are never listed on item cards or affix inspection cards.
T1 is strongest and T7 weakest for every family. Releasing Alt removes the
additional details. Long exceptional content remains scrollable, with action
buttons and feedback kept inside the viewport.
Damage and resistance text use matching, muted semantic colors for each of the
six damage types. The common word "damage" stays neutral and is not part of
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

`godot/Content/Loot` supplies the production starter selection, enemy drops and
admin creation. A playable run pauses for a once-only choice of a Common bow,
sword or wand; no other items or points are granted. All three temporarily use
the same bow attack. `starter-items.json` and `StarterItemCatalog` remain the
explicit laboratory/presentation fixture. Opening UI never grants items.

Future export presets must include the authored JSON content in the package.
Owned-item crafting, inventory save/load,
progression/class screens and game save/load remain separate work.

## Searchable reference

Reference is available from the title screen, pause menu, the HUD button and H.
It opens on Monsters, with a creature library and details for the selected entry.
Items and Affixes are full-width tables; Rules keeps grouped explanations, and
Journal shows captured session history. Search stays within the selected section.
Table filters select equipment slots or loot rarity. Per-monster tables show both
rarity totals and individual-base chances per kill, with rarity-colored rows.

`ReferenceCatalog` indexes the existing `CombatGlossary` topics and authored
`Content/Reference/topics.json`. Shared `ItemInspectionTopics` supplies the same
actual affix values, ranges, quality and tier pools used by item cards.
`InspectionContent` renders both the reference article and ordinary floating
cards. Related links open the existing `InspectionStack`; Open as card also
supports pinning and dragging. Pins survive closing the reader.

In a running game, opening the reference captures recent rolls and the retained
combat journal. Rolled affixes are inspected on their actual items in inventory;
the reader has one shared affix table instead of separate per-item affix articles.
Reopening refreshes session snapshots; existing pins retain their captured facts. The title-screen reader omits session-specific
entries and never grants items. Reading does not reroll or resolve combat.

The reader pauses gameplay while open, returns to the pause menu if opened there,
and preserves a pause owned elsewhere. Search owns text input, preventing I/C/H
from triggering other UI shortcuts while typing. Escape closes transient cards
first, then the reader. Inventory/character windows retain their prior state.

## Localization

Settings offers **Українська** (`uk`) and **English** (`en`). Apply saves the
selected language in `user://presentation.cfg` before changing the running
interface. Back/close discards an unapplied selection; Reset Defaults selects
Ukrainian in the form until Apply is pressed. Missing, invalid or unsupported
saved language fields fall back to Ukrainian. A failed save leaves the language
unchanged. Launch with `godot --path godot --language en` or `--language uk` to
override the saved startup choice. English source messages remain the fallback.

Switching language does not recreate the scene or reset combat, equipment,
inventory positions, roll history or pinned cards. Native text bindings and
constructed rich text refresh in place. Reference search retains its query,
category and selected entry identity; its captured articles and item facts use
the current language. Numeric facts and captured item rolls remain unchanged.

`Content/Localization/uk.po` is registered with Godot's TranslationServer.
It covers title/settings/pause screens, arena HUD and hints, inventory and
character sheets, item and affix display names, linked explanations, reference
articles/categories and the combat laboratory. Native scene labels use Godot's
automatic translation; constructed text uses `UI/Localization/L` to translate
complete templates before inserting values. Numeric facts keep invariant decimal
formatting, dice notation, ranges, quality and tier IDs. The product title stays
in English. Developer exceptions, console diagnostics and technical identifiers
are outside the player-facing catalog.

Item definitions and reference JSON retain English source text and stable IDs.
Presentation translates their display fields when building cards and the search
index. Search matches Ukrainian text case-insensitively as well as stable entry
IDs. The same immutable item values feed both cards and reference entries;
translation never rerolls items or changes Domain calculations. New authored text
needs a matching catalog entry. Keep `{0}`-style placeholders and their numeric
formats intact when editing translations.

Rich-text damage-word styling recognizes English and Ukrainian forms without
turning the common word into a link. Existing native metadata links and card
pin/close behavior retain their original identities in both locales.

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
overhead health/mana bars and the interface language. These bars contain no numbers; target HP numbers
remain in the top target panel. Apply saves `user://presentation.cfg` before applying
the new values. Back/close discards unapplied edits; Reset Defaults changes the
form until Apply is pressed. Invalid stored fields fall back to supported defaults
or clamp volume to [0,1]. Headless processes skip display-mode calls.

Presentation code lives in `godot/UI/Common`, `Menu`, `Settings`, `Hud`, `Reference`
and `Inventory`. `InspectionStack` owns explanation branches; `InspectionPane` draws
one card; `InspectionTopic` retains text and lazy related links. `CombatGlossary`
owns shared colors and rule descriptions. `ItemDetailsPanel.Properties` keeps
item rendering separate from card sizing and input. `ArenaHud` composes the controls
and routes top-level input. `InventoryWindowController` owns item selection,
comparison, equipment requests and pickup feedback. `CombatHudPresenter` reads
live health, mana, character stats and targeting data, and connects the action
journal to observed combat results. `HudNavigationController` owns pause,
settings and reference navigation, including restoration of the previous pause
state. `ArenaRoundPresenter` owns round banners, nearby loot and capacity notices.

Panels emit user requests such as `EquipRequested(itemId, slot)`; the item-window
controller invokes `EquipmentService` and refreshes inventory and character data
through ordinary calls. Application checks spatial capacity before committing
all equipment changes, including dependent offhand returns. The shared Domain
`InventoryLayoutService` retains occupied cells and applies remembered or requested
return positions in both the operation check and the rendered grid. Item rules
and arithmetic remain in Domain; equipment commands remain in Application.
The legacy detailed combat laboratory and its result-based journal remain available through F2. The arena's old diagnostic controls are
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

Reference opens on the bestiary and separates Monsters, Items, Affixes, Rules and
Journal. Select a monster to read its actual stats, attack definition and concrete
per-kill loot entries. Aggregate rarity and no-equipment rows are omitted from
this list; generation probabilities remain unchanged.

Items are ordered by generation, then rarity, then branch. The table shows only
item name, ilvl, requirements and properties. Rarity is encoded by the name/frame
color, with a rarity filter available. ilvl displays the actual affix gate, separate
from the character's required equipment level; unreleased values show a dash.
Repeated page titles and introductory paragraphs are omitted. Column headings
and cells open captured inspection cards with nested links to damage, defenses,
requirements and affix-level rules. Weapon rates use compact localized units;
base mana/second is base cadence × cost/attack, explained in the mana-cost card.

Affixes use a T7–T1 selector with the selected tier's value range and minimum ilvl,
plus slot/search filters. Switching tiers preserves search/filter state. All
families have the same presentation; no status column or drop-status paragraph
is shown. This UI choice does not enable additional families in generation.
F3 lists the 21 released generation-1 bases. Items selects a base, rarity and
item level, then up to the rarity's configured affix capacity. Each row selects
an enabled, slot-compatible family, a tier and explicit quality. Poison, ignite
and bleed expose all T1–T7 tiers at any administrative item level, plus an
admin-only T0 with T1 damage and guaranteed application on every successful hit
regardless of attack speed. Other families retain their ilvl tier gates.
Duplicate families, unavailable tiers and overflow beyond rarity capacity are
rejected. Creation uses the normal inventory acquisition and ground fallback.

Edit item accepts a native drag from inventory or equipment, or an owned-item
selector. Loading does not remove the item. Save retains its identity, captured
base and bag placement, replaces edited affixes and preserves untouched rolls.
Equipped item/stat changes publish atomically; health/mana may clamp downward,
but raising their maxima never heals/refills. A stale or removed item is rejected.
The panel fits beside an open inventory, scaling to narrower design widths.
Restart the session after content changes to create new captured items.

Gold rows in nearby pickup retain a compact brass border even when another item
is selected. Selection changes preserve the border and do not change row height.
Player-facing Ukrainian instructions use the formal plural address.

Sword and wand bases use their own vector icons in the bag, equipped slots,
item details, nearby loot and the primary attack button. The attack button reads
the currently equipped weapon, including its rarity color, when the HUD refreshes.
The bow retains its bow icon.

### Production status inspection

The character offense section shows reference-speed chance and tick formula
for poison, ignite and bleed. Click for base timing, cadence normalization,
reapplication and defense rules loaded from the active status content.
The selected-target HP panel expands only while at least one status is active;
without effects it uses the compact name/HP layout. It shows clickable poison,
ignite and bleed icons,
with a stack badge for multiple instances. Clicking opens the existing inspection
card with captured formulas, duration, interval, damage coefficient and slow,
alongside the content-authored base rules. Pinned details retain the clicked
snapshot. Small matching icons appear beside overhead enemy/target HP bars,
respecting enemy-bar visibility preferences. Icons disappear when effects clear,
expire or the target dies; switching selection refreshes the upper row.
The journal
records applications, refreshes and actual periodic damage without replacing
the retained attack d20. Item inspection and the affix reference use the same
content-authored duration, dice sides and per-die flat adjustment.
