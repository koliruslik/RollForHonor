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
armor and d20 bonuses are floored after aggregation with checked conversion.
Evasion also retains its decimal rating for the glancing threshold.
`CombatantSnapshot` retains typed defenses and carries a small `CombatOffense`
view for extra attack modifier, typed flat attack damage and outgoing coefficients. It contains no
universal stat dictionary, character definition or equipment references.
`CombatSnapshotBuilder` assembles already-projected views from one adapter read;
it does not reread Godot nodes or independently recalculate equipment.

Legacy `AddCombatant`/`UpdateCombatant` views remain available for the laboratory
and existing diagnostic fixtures. A stat-managed actor rejects direct edits to
derived HP maxima/defenses; change its profile through `UpdateStats` when no gear
is equipped, or `UpdateNonEquipmentStats` to preserve equipped contributions. Explicit
current-health/effect updates retain its resolved offensive inputs.

HealthRecovery and ManaRecovery are flat points per game second; only Flat
modifiers are accepted. Player content starts at 2 HP/s and 10 mana/s. Other
definitions default to zero. Recovery reads the resolved rates and current
maxima; it cannot revive actors. Whole HP is restored with a retained fractional
remainder while hurt, and full/defeated actors do not bank recovery.

## Calculation

The supported attributes are MaxHealth, MaxMana, Armor, Evasion, MovementSpeed,
AttackModifier, CriticalRollBonus, CriticalChance, CriticalMultiplier, CharacterLevel, typed Resistance and global
or typed DamageMultiplier, plus typed AddedDamage, extra damage dice, AttackSpeed,
HealthRecovery and ManaRecovery. Strength, Dexterity, Intelligence and Luck are the
four visible primary attributes, initially zero. Constitution remains a legacy
identifier, excluded from current content and UI. STR/DEX/INT gate equipment
against non-equipment stats; they do not derive combat bonuses. Luck affects
equipment occurrence/rarity and captured gold/affix rolls as described in [rewards](rewards.md).

Character level defaults to 1 and is restricted to 1–10. Items cannot modify it.
Level changes give no automatic HP, mana, regeneration, damage or healing. XP,
point awards and the level-up interface are not configured. Base CriticalMultiplier
defaults to 2; global/typed damage coefficients default to 1. Missing other stats
default to zero, except maximum HP, which defaults to 1.

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
attack-specific modifier. After raw dice, `SourceDamageStep` adds projected typed AddedDamage to attacks
once, then applies each typed source coefficient, rounding component damage upward, then hit quality and
defenses run normally. Breakdown entries report the actual combined coefficient
with a `CharacterStats:<type>` origin; they do not invent item-level attribution.
Neutral coefficients do not add damage dice. A separate critical check adds its own d20 after quality.

Godot's periodic pipeline uses the same source scaling step against the effect's
captured source snapshot; AddedDamage is not added to periodic ticks. Target
defenses remain fresh. Stats updates do not
rewrite a captured source or reroll historical results.

The game uses `CriticalRollBonus` on a separate d20: `d20 + bonus >= 20`.
The base multiplier is ×2, after quality and before defenses. Natural 20 on the
primary attack roll is full quality and does not guarantee a crit. The actual
critical die, bonus and multiplier are retained in the breakdown and inspection.
`CriticalChance` remains a legacy stored value. Critical-roll item modifiers are
restricted to amulets/rings (+0…3 per modifier). The prepared critical affix
families remain disabled until separately enabled; no crit bonus is granted at start.
[Equipment](equipment.md) recomposes occupied slots atomically. Automatic
active-effect modifier collection and game save/load are not implemented yet.

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
HP or modifier arithmetic. Player: 100 HP, 500 mana, armor 0, evasion 0, speed 6.
Sentinel: 40 HP, armor rating 12, evasion rating 40, speed 3. The Sentinel armor
was migrated from 2 at coefficient 5 to 12 at coefficient 30; evasion 40 retains
the old threshold 10 through `6 + rating/10`. Attack definitions retain their own dice, flat damage, costs
and cooldowns. The player's equipped weapon overrides intrinsic dice, costs and
attack frequency through a launch-time provider. Their existing +8/+5 d20 modifiers remain attack-specific; both
base source bonuses start at 0.

## Approved equipment generations

There are 105 named bases: three branches × seven slots × five generations.
Only generation 1 is released. Body/head/boots give STR armor 65/39/26,
DEX evasion 50/30/20, or INT armor and evasion 22.5/13.5/9 each. Sum before
flooring armor; preserve decimal evasion for hit-quality comparisons. Totals are
130 armor, 100 evasion, or 45 of each. No energy shield or blocking was added.

Flat armor affix values were explicitly migrated ×6 and flat evasion affix values
×10 in live JSON; percentage modifiers were unchanged. Existing captured instances
are not rewritten. Higher-generation defense values, requirements, XP, point awards
and the separate display-ilvl formula remain unapproved and inactive.

## Effective health display

`EffectiveHealthCalculator` estimates full-health EHP by enumerating all 20 natural
d20 faces through the actual rating quality, component rounding and defense services.
EHP is `MaxHealth × unprotected expected loss / defended expected loss`.
The unprotected comparison has no armor, evasion or resistances. Zero expected
loss is represented by null and displayed as ∞.

The character sheet's `EHP` row shows the arithmetic mean of six individual
damage-type EHP values with equal weight. If a type is immune, its EHP and this
arithmetic mean are infinite. The click-only card shows all six types and all
15 unordered type pairs. Each pair is a genuine 50/50 mixed packet: 5 + 5 damage,
not the mean of its two single-type EHP values. All reference hits have 10 total
damage and a d20 bonus of +4. Combined entries are excluded from the headline
average so no type is counted twice. Click a category to inspect its component
resistances, armor and evasion through nested cards. The row has no hover tooltip.

At 100 HP, the STR/DEX/INT reference sets produce headline means of 121.4/135.7/
122.6 respectively; individual physical values differ from unarmored nonphysical
values. Criticals, recovery, periodic damage and attack frequency are excluded.
Armor depends on hit size. Reading EHP consumes no dice and changes no state;
wounded current HP does not lower this full-health comparison.

The character sheet displays the critical coefficient as `Critical damage bonus`:
`(coefficient - 1) × 100%`. Thus the base coefficient 2 appears as +100%, and 2.5
as +150%. The combat coefficient remains unchanged. Evasion, accuracy, critical
roll bonus and critical damage bonus are clickable and link to related hit-quality
and defense explanations. Item modifier labels use the same terminology.

## Status modifiers and effective movement

Each status has `Chance` and `Dice` stats plus neutral-one `DurationMultiplier`,
`DamageMultiplier`, `TickRateMultiplier` and `SlowMultiplier` stats, prefixed
`Poison`, `Ignite` or `Bleed`. For example, an Increased 0.5 modifier to
`PoisonDurationMultiplier` changes the configured 5 s to 7.5 s for new stacks.
An Increased 1 modifier to `BleedSlowMultiplier` changes base 10% slow to 20%.
These modifiers use normal stat provenance and calculation, independently of
base values in JSON; they do not implement a talent-selection UI.

Chance is nonnegative before cadence normalization and capped afterward;
this preserves chance bonuses on fast weapons. `GetStats` applies active
captured movement slowing over stored resolved movement speed without
rewriting base profiles or equipment. Removal/expiry immediately drops that
projection. Ordinary outgoing damage boosts apply to ignite, while dedicated
status damage modifiers can strengthen any of the three effects.
