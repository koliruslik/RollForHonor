# Items and affixes

[Project overview](../README.md) · [Documentation index](README.md)

## Ownership and scope

Domain `Items` separates `Models`, `Configuration` and `Services`. Affix
definitions, tier ranges and rolled instances live under `Items/Affixes`.
Definitions and owned values are immutable; constructor collections are copied.
There is no dependency on Godot, storage or the live combat adapter.

`ItemDefinition` holds a stable content ID, display name, compatibility tags and
implicit modifiers. `ItemFootprint` captures positive width/height in inventory
cells, defaulting to 1×1 for existing or non-equipment bases. Pure
`InventoryGridService` checks immutable rectangular placements, bounds and
overlap; the game uses a fixed 12×5 grid without automatic expansion.
Optional `WeaponProperties` captures attack frequency, mana
cost and typed dice/flat damage. The authored weapon catalog supplies it, and
the arena uses it when creating launch content. `ItemInstance` captures that immutable base, an externally
allocated instance ID, explicit rarity and actual rolled affixes. Implicit
modifiers do not occupy affix slots.

`AffixDefinition` holds a stable ID, exclusivity family, required tags, selection
weight and tier definitions. Each `AffixTier` has its own power requirement, independent minimum item-level
gate (default 1 for existing content), and one or more `AffixModifierRange` entries. All required tags must be present on
the base. At most one affix from each family may occupy an item; prefix/suffix
categories are not required.

Numbered content tiers use T1 for the strongest tier; each affix defines its own
pool, for example T1–T16, T1–T15 or T1–T4. This is independent of item rarity.
Author higher required power for stronger tiers: the generator selects by power
threshold, not by parsing the tier ID. Custom IDs remain supported. The current
isolated legacy starter-kit fixtures contain only temporary T1. Live content uses
the accepted 371-tier table: T1 strongest through T7 weakest, reversing the research
rank labels while preserving values and gates. See the [full catalog](loot-tables.md).

`AffixInstance` retains its owned ID, definition/family/tier IDs, compatibility,
natural/effective d100 quality, captured Luck/tier-roll context, concrete modifier values, captured selected-tier intervals and the
definition's available tier IDs. Reusing an item does not reroll it or
look up newer numeric ranges. Its captured base also keeps original implicit
values. A future save format must serialize these actual values, not only a seed
and content IDs; serialization and catalog migration are not implemented here.

## Replaceable rules

`ItemRarityRules` is an injected immutable capacity table. Its initial values are:

| Rarity | Maximum affixes |
| --- | ---: |
| Common | 0 |
| Uncommon | 2 |
| Rare | 4 |
| Epic | 6 |
| Legendary | 8 |

Rarity is independent of occupied slots and affix strength. A Legendary item
with no affixes remains Legendary. Filling slots cannot upgrade rarity, and
removing an affix cannot downgrade it.

Godot's `Items/ItemRarityRules.tres` uses `ItemRarityRulesDefinition`. Edit this
Resource and pass `CreateRules()` to an `ItemFactory` when composing an item
consumer. Capturing it copies the table; later Resource edits do not mutate
existing rules. The arena starter-kit loader uses this shared table. It takes an uncached, explicitly
disposed engine Resource and retains only the immutable Domain copy, avoiding
stale cached C# wrappers across repeated scene lifetimes.
Engine-independent callers can supply a different table directly.

`ItemStatRules` separately defines which operations may appear in item content:

| Current stat category | Allowed item operations |
| --- | --- |
| AttackModifier, Strength, Dexterity, Constitution, Intelligence, Luck, HealthRecovery, ManaRecovery | Flat |
| MovementSpeed, CriticalChance, CriticalMultiplier | Increased, More |
| Global or typed DamageMultiplier | Increased, More |
| MaxHealth, MaxMana, Armor, Evasion, typed Resistance, typed AddedDamage | Flat, Increased, More |

Increased and More use fractions: 0.1 means 10%. Flat resistance also uses a
fraction, adding percentage points: base 0.2 plus Flat 0.1 gives 0.3, whereas
Increased 0.1 gives 0.22. Item rules do not change how character base definitions
are represented. They cannot enable operations forbidden by the underlying
`StatModifier`, such as flat damage-coefficient changes.

A resistance-to-all affix can grant a bundle containing one modifier for every
damage type. Physical-only bonuses can similarly list Piercing, Bludgeoning and
Slashing. No grouping of nonphysical types into "magic" is assumed.

Power requirements, selection weights and value intervals are independent
content fields. Global damage, all-resistance and broad dice bonuses are intended
to cost more and grant smaller values; no final numeric balance table ships in
this slice. Only the existing attack-roll stat is currently representable as a
dice bonus. Typed AddedDamage is consumed before attack scaling and quality. Intrinsic
weapon cadence and character attack-speed modifiers are active. Typed extra damage
dice are projected into attack impacts. Primary-attribute derivations, general
Typed dice bonuses are implemented; independent critical checks are active as described in [stats](stats.md). The prepared critical affix families remain disabled.

## Generation

`ItemGenerator` receives a validated `ItemCatalog`, `ItemFactory`, `AffixRoller`
and an explicit `AffixTierSelection`. A request supplies the instance/base IDs,
rarity, occupied count, item level and available power. The legacy `Generate`
method takes the caller's power result and explicit tier-selection policy;
the Luck-aware path is described separately below.

The generator filters compatibility and power eligibility before rolling.
Affix definitions are considered in ordinal ID order, then selected by their
positive relative weights. Selecting a definition removes its whole exclusivity
family from the remaining pool. Too few eligible families fail before consuming
randomness; selection does not use repeated rejection rolls or silently return
a partially populated item. Weight-sum overflow also fails before rolling.

Higher available power preserves cheap compatible families. Tier choice is an
explicit policy: `HighestEligible` picks the greatest eligible threshold, using
ordinal tier ID for ties; `UniformEligible` retains all eligible tiers. These
are available mechanisms, not a final Luck/balance decision. Rarity itself and
the number of initial affixes are supplied by the caller, not randomly chosen.

`GenerateWithLuck(request, luck, dice)` is the agreed generation path when Luck
is available. For each selected family it rolls a separate natural d20 and adds
`min(5, floor(max(0, Luck) / 5))`. It selects the highest threshold reached within
the request's existing power cap and item level. `RequiredItemLevel` does not
change with Luck. Family compatibility, relative weights, uniqueness and the
explicit affix count remain unchanged; the constructor's legacy tier-selection
policy applies only to `Generate`.

The gated content must include a baseline tier covering natural 1 for every
selectable family. Incomplete tables fail before any dice are consumed, instead
of introducing a hidden retry or a Luck-dependent family-weight change. This
validation does not author final family thresholds or import research tier tables.

`AffixRoller` rolls one shared d100 quality for all modifiers in an explicitly
selected tier. It captures `L = max(0, Luck)` and computes effective quality
`min(100, naturalD100 + L)`. Inclusive interpolation then maps effective 1 to
Minimum and 100 to Maximum:

```text
round(Minimum + (Maximum - Minimum) × (effectiveQuality - 1) / 99,
      DecimalPlaces, AwayFromZero)
```

Ranges retain zero to six decimal places and validate their endpoints. This
initial interpolation policy can be revised independently of item storage;
previously rolled values stay unchanged. A complete generation uses the
caller-owned `IDiceRoller` and its trace. A single-option choice consumes no
selection die; d100 value rolling still occurs for a fixed-value interval.
The same content, rules, IDs, random implementation and seed reproduce the same
item. Content changes can change later outcomes, so seed-only saves are unsafe.

Example composition with an explicitly supplied content pool:

```csharp
var factory = new ItemFactory(ItemRarityRules.Default, ItemStatRules.Default);
var catalog = new ItemCatalog(itemBases, affixDefinitions, ItemStatRules.Default);
var generator = new ItemGenerator(
    catalog, factory, new AffixRoller(), AffixTierSelection.HighestEligible);
var dice = new RandomDiceRoller(new Random(seed));
var item = generator.Generate(
    new ItemGenerationRequest(instanceId, baseId, ItemRarity.Rare, 2, availablePower),
    dice);
```

`RandomDiceRoller` belongs to Infrastructure; Domain only consumes `IDiceRoller`.
The example assumes imports from Domain Items and Infrastructure Dice. There is
a live JSON pool described below. The arena generates twelve starting instances
from it, and monster tables independently select gold and optional equipment.
`StarterItemCatalog` remains an explicit compatibility fixture for presentation tests;
its authored rarity/count/qualities are not the live drop rules.

`AffixInstance.QualityRoll` retains the natural face for compatibility;
`EffectiveQualityRoll` is the value used for interpolation and displayed as
quality in item inspection. `RollContext` retains the normalized Luck, natural
tier/quality dice and effective results. Explicitly selected tiers have no tier
die. Fixed intervals and base weapon dice remain fixed. Generated affixes and
older authored-kit instances are immutable: later Luck changes or generation
calls do not reroll them.

The current enemy path captures Luck when rolling a new reward. Occurrence and
rarity use the approved fixed table; separate tier/quality/gold amount rolls apply
Luck. Existing instances keep their original values.

## Crafting and stat projection

`ItemCraftingService` returns new validated items for adding, replacing or removing
an already rolled affix. Replacing an affix can also install values rerolled by
`AffixRoller` with the original identity and tier. Failed operations leave the
original item intact. Compatibility, family uniqueness and capacity are checked
for every result.

`UpgradeRarity` is a separate explicit domain operation preserving all rolls.
It accepts a strictly higher defined rarity if that rarity can hold the existing
affixes. It does not authorize free upgrades in gameplay: recipe/material checks
and which transitions are available belong to a future Application use case.

`ItemModifierProjector` converts implicit and actual affix values into existing
`StatModifier` entries. Length-prefixed item/affix identities distinguish their
sources even when IDs contain separators. Projected entries can be composed
into a complete `StatProfile`; projection does not append entries to live state,
heal the actor or modify a historical snapshot. The [equipment core](equipment.md)
owns slot validation, canonical item ownership and atomic loadout/stat updates.
The [game interface](interface.md) supports the authored starter kit and real equip/unequip.
`ItemAcquisitionService` validates the supplied 12×5 bag layout against all
currently unequipped owned items, including footprints, bounds and overlap. It
finds a free rectangle without rearranging existing placements, or validates the
exact requested rectangle for native dragging, then requests
an atomic version-checked grant. The adapter rejects defeated owners and
duplicate ownership across the session. Failed pickup leaves the same captured
instance on the ground; retrying never rerolls it. Administrative initial-kit
grants do not use the pickup service. Crafting owned/equipped items and game
save/load remain future work.


`ItemReleaseService` uses a version-checked release port to remove one owned bag
or equipped item from a living owner. Equipped releases recompose loadout/stats
and clamp current resources at the same publication boundary as ownership
removal. Dependent equipment is retained in the bag only after a complete spatial
capacity check. Absent, stale, defeated-owner and insufficient-return-space
requests leave ownership, loadout and stats unchanged. Godot then represents the same immutable instance on
free ground near the player; reacquiring it does not generate or reroll another item.

## Live content and generation

`godot/Content/Loot` is the runtime source of truth. `GameLootCatalog` composes
Domain item models and the affix roller from these tables. It selects each rarity
slot independently with 55% occupancy, then weighted eligible families without
replacement. The seven-tier policy adds the captured level progression bonus and
capped Luck bonus to a separate d20; quality uses its independent capped d100.
Both bonuses and all natural/effective results are retained on each affix.

Starter inventory, enemy rewards and F3 item creation use this catalog. The old
`starter-items.json` and `StarterItemCatalog` remain explicit presentation test
fixtures; their authored affixes are not the production drop pool.

Extra typed damage dice are resolved in the common raw-damage step before source
scaling, quality and defenses. They do not affect periodic ticks. AttackSpeed is a
resolved cadence coefficient used by the arena's weapon cooldown. Leech and
independent critical families appear as prepared entries in the reference and are
excluded from generation until separately enabled; current affix-level-1 Sentinel families work.

The Markdown [tables](loot-tables.md) are generated from the runtime JSON:

```powershell
python tools/export_loot_tables.py
python tools/export_loot_tables.py --check
```

The visible catalog contains 105 accepted EN/UK bases across five generations and
three branches. Only 21 generation-1 bases are released for drops and F3 creation;
the remaining 84 show pending balance and cannot be generated. All 98 previous
IDs remain hidden compatibility aliases and do not enter the current loot pool.
Names use gender-correct rarity prefixes: Common bare, Uncommon Reinforced,
Rare Improved, Epic Masterwork, Legendary Perfected. Rarity text and frames
retain their color on hover/press. Equip requirements, affix-gating level and the
future display rating are separate; the proposed display-ilvl formula is inactive.

A playable run grants exactly one selected Common+0 bow, sword or wand with
zero level/attribute requirements, already equipped. All three temporarily
use the same projectile, damage, cadence and mana cost. Repeated choices cannot
grant more items. The full starter kit remains a laboratory/test fixture.
