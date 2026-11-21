# Items and affixes

[Project overview](../README.md) · [Documentation index](README.md)

## Ownership and scope

Domain `Items` separates `Models`, `Configuration` and `Services`. Affix
definitions, tier ranges and rolled instances live under `Items/Affixes`.
Definitions and owned values are immutable; constructor collections are copied.
There is no dependency on Godot, storage or the live combat adapter.

`ItemDefinition` holds a stable content ID, display name, compatibility tags and
implicit modifiers. `ItemInstance` captures that immutable base, an externally
allocated instance ID, explicit rarity and actual rolled affixes. Implicit
modifiers do not occupy affix slots.

`AffixDefinition` holds a stable ID, exclusivity family, required tags, selection
weight and tier definitions. Each `AffixTier` has its own power requirement and
one or more `AffixModifierRange` entries. All required tags must be present on
the base. At most one affix from each family may occupy an item; prefix/suffix
categories are not required.

`AffixInstance` retains its owned ID, definition/family/tier IDs, compatibility,
d100 quality and concrete modifier values. Reusing an item does not reroll it or
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
existing rules. No gameplay item consumer is registered in the arena yet.
Engine-independent callers can supply a different table directly.

`ItemStatRules` separately defines which operations may appear in item content:

| Current stat category | Allowed item operations |
| --- | --- |
| AttackModifier | Flat |
| MovementSpeed, CriticalChance, CriticalMultiplier | Increased, More |
| Global or typed DamageMultiplier | Increased, More |
| MaxHealth, MaxMana, Armor, Evasion, typed Resistance | Flat, Increased, More |

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
dice bonus. Primary-attribute derivations, general dice-bonus targeting, added
flat damage, attack speed and functional independent crit rules remain future
stat work. Current critical stats are reserved, as described in [stats](stats.md).

## Generation

`ItemGenerator` receives a validated `ItemCatalog`, `ItemFactory`, `AffixRoller`
and an explicit `AffixTierSelection`. A request supplies the instance/base IDs,
rarity, occupied count and available power. No d20 or Luck formula is inferred:
the caller supplies the power result, which gates each tier without being spent.

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

`AffixRoller` rolls one shared d100 quality for all modifiers in an explicitly
selected tier. Its current value policy maps 1 to Minimum and 100 to Maximum:

```text
round(Minimum + (Maximum - Minimum) × (roll - 1) / 99,
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
no bundled production item/affix pool, item drop service or crafting UI yet.

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
equip an item, heal the actor or modify a historical snapshot. Equipment ownership,
atomic loadout updates, inventory, acquisition and save/load are not implemented.
