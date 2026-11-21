# Character stats

[Project overview](../README.md) · [Documentation index](README.md)

## Ownership

Domain `Stats` contains typed `StatKey`/`StatType` identifiers, immutable base
`StatBlock` values, `StatModifier` entries with source identifiers, `StatProfile`
inputs and the deterministic `StatCalculator`. These types have no Godot, item
storage or service dependencies. Current HP and mana are runtime state, not stats.

`InMemoryCombatState.AddStatCombatant` resolves a profile and registers current
HP/mana at their maxima. `UpdateStats` publishes the complete replacement profile,
resolved values and combat projection under the same lock and session version as
combat state. Increasing maxima does not heal/refill; reducing maxima clamps
current amounts downward. Effects, cooldowns and receipts survive stat changes.
An unsuccessful calculation/projection publishes nothing. Old snapshots remain
immutable and existing optimistic version checks reject stale proposals.

`CombatStatProjector` maps resolved values to concrete combat inputs. HP maxima,
armor, evasion and d20 bonuses are floored to integers with checked conversion.
`CombatantSnapshot` retains typed defenses and carries a small `CombatOffense`
view for extra attack modifier and typed outgoing coefficients. It contains no
universal stat dictionary, character definition or equipment references.
`CombatSnapshotBuilder` assembles already-projected views from one adapter read;
it does not reread Godot nodes or independently recalculate equipment.

Legacy `AddCombatant`/`UpdateCombatant` views remain available for the laboratory
and existing diagnostic fixtures. A stat-managed actor rejects direct edits to
derived HP maxima/defenses; change its profile through `UpdateStats`. Explicit
current-health/effect updates retain its resolved offensive inputs.

## Calculation

The supported attributes are MaxHealth, MaxMana, Armor, Evasion, MovementSpeed,
AttackModifier, CriticalChance, CriticalMultiplier, typed Resistance and global
or typed DamageMultiplier. Fractions use 0.2 for 20%; an outgoing coefficient
of 1 is neutral. Missing maxima HP and damage/critical coefficients default to
1; other values default to 0.

Ordinary attributes use:

```text
(base + sum Flat) × max(0, 1 + sum Increased) × product(1 + each More)
```

Negative Increased values reduce the additive pool. Negative More values are
independent Less multipliers; values below -1 are rejected. Modifier amounts
and factors are sorted before aggregation for order-independent evaluation. HP maxima cannot fall
below 1; armor, evasion, mana and movement cannot become negative. Resistance
may be negative for weakness and is capped at 1, matching the existing combat
contract; no new 75% resistance cap is introduced. CriticalChance is clamped to
[0,1]. Integer combat projection floors after stacking, not between modifiers.

Outgoing damage has one combined global+typed Increased pool and all matching
More factors. Base global and typed coefficients multiply; both default to 1.
Flat modification of a damage coefficient is rejected: use Increased or More.
With neutral bases, +20% global Increased, +30% Fire Increased and 20% global
More produce a Fire coefficient of `(1 + .2 + .3) × 1.2 = 1.8`.

The basic attack pipeline adds the resolved source d20 modifier to the payload's
attack-specific modifier. After raw dice, `SourceDamageStep` applies each typed
source coefficient once, rounding component damage upward, then hit quality and
defenses run normally. Breakdown entries report the actual combined coefficient
with a `CharacterStats:<type>` origin; they do not invent item-level attribution.
Neutral coefficients leave existing damage and dice counts unchanged.

Godot's periodic pipeline uses the same source scaling step against the effect's
captured source snapshot; target defenses remain fresh. Stats updates do not
rewrite a captured source or reroll historical results.

CriticalChance and CriticalMultiplier are stored/calculated only. Current
critical outcomes still follow d20 quality and existing quality coefficients;
no second random critical roll is enabled. A future crit policy needs a separate
gameplay decision. Item instances, slots, automatic equipment/effect modifier
collection, affixes, save/load and inventory UI are not implemented yet.

## Editable content

`godot/Arena/PlayerStats.tres` and `SentinelStats.tres` use `ActorStatDefinition`:
base HP/mana, armor, evasion, speed, attack/critical values, typed fractional
resistances and an optional list of `StatModifierDefinition` resources. A
modifier selects its stat, optional damage qualifier, operation, amount and
source identifier. Resource content is copied into an immutable runtime profile;
later Resource edits do not silently change a live actor. Publish a new profile
through the adapter to apply changes during a session.

Both actors have an `ActorStats` capability. It reads resolved values and supplies
movement speed to the existing motor before input/movement run; it does not own
HP or modifier arithmetic. Initial values preserve the previous game balance:
player 100 HP, 500 mana, armor 2, evasion 12, speed 6; Sentinel 40 HP, armor 2,
evasion 10, speed 3. Attack definitions retain their own dice, flat damage, costs
and cooldowns. Their existing +8/+5 d20 modifiers remain attack-specific; both
base source bonuses start at 0.
