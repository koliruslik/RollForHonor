# Rewards and gold

[Project overview](../README.md) · [Documentation index](README.md)

## Current boundary

The session has a gold wallet and a captured world-gold pickup path. Gold is not
credited when a reward is captured. A living player must approach within 2.5 world
units; playable rounds collect gold automatically, with G and click pickup also available.
Ctrl+G and the nearby-menu toggle control automatic gold for the current run. Items
are always collected manually; gold collection never depends on free bag space. Gold occupies no inventory cells
and cannot be dragged into the bag. The inventory displays the current wallet.

Sentinel and sandbox Sentinel use the accepted live tables: guaranteed 6–14 gold,
with baseline Luck-0 equipment outcomes none 55%, Common 25%, Uncommon 15%, Rare 4%, Epic 0.9%
and Legendary 0.1%. Training targets do not grant rewards. The [live tables](loot-tables.md)
list every registered monster, playable base and affix. Browse the same data in Reference
under Monsters, Items and Affixes. Selling and spending remain separate work.

`GameLootCatalog` reads `Content/Loot/monsters.json`, `bases.json` and `affixes.json`.
Normal rounds and sandbox kills share the same generation path. The existing
per-source defeat guard captures the complete outcome once; blocked gold/item
placement keeps those objects for retry. Neither death nor placement credits a wallet.
The equipment selection uses one d1000000. Base cumulative probabilities F are
adjusted to `F ** (1 + max(0, Luck) / 100)` and rounded half upward to integer
thresholds. This preserves the approved base probabilities at Luck 0 and boosts
all better-outcome tails, rather than transferring every bonus face to Legendary.
At Luck 0/10/50/100, any-equipment chance is 45%/48.1917%/59.2109%/69.75%;
Legendary is 0.1%/0.11%/0.15%/0.1999%. Threshold quantization is 0.0001%.

Slot selection uses unchanged conditional weights, then eligible bases compete
within that slot. Item-level gates, affix occupancy and family weights are not
changed by this selection policy. Gold remains guaranteed with its existing
quantity policy. Captured results are never rerolled on inspection or pickup.
The live bestiary uses the captured player's Luck, including after locale changes;
title-screen tables default to Luck 0. Generated tables include the approved curve.

## Group rolls

Domain `Rewards` defines immutable threshold bands and independent groups.
`LootGroupRoller` takes a configured dice formula for each group, a caller-resolved
nonnegative Luck bonus and an injected `IDiceRoller`. It selects the highest band
reached by the natural total plus that bonus. An explicit empty band permits no
item; an equipment result retains its eligible pool and requested rarity for the
separate item-creation step. It does not choose affix counts, tiers or values.

The agreed monster-table design has a guaranteed gold group and optional
independent equipment groups. Equipment can remain one group or split into
weapons, armor and jewelry. Each group may yield at most one result; an empty
result in one group does not suppress rewards in another. Guarantee gold by
configuring only gold outcomes in that group, including the lowest roll.

Thresholds can put a higher band just above the unmodified maximum. When a Luck
bonus first reaches that band, only the top natural result qualifies; its chance
then grows as the bonus increases. These low-level types do not choose a group-selection Luck formula; the live equipment
composition applies `LootLuckPolicy` before rolling. That selection bonus remains separate from amount Luck.

A selected gold band consumes one additional natural d100. With captured
`L = max(0, Luck)`, the agreed amount policy is:

```text
effectiveRoll = min(100, naturalD100 + L)
amount = min + floor((max - min) * (effectiveRoll - 1) / 99)
```

`GoldRange` maps an effective result from 1 through 100 to whole amounts without
exceeding the selected range. Fractional Luck is retained until the final floor.
The amount policy intentionally increases maximum-amount frequency; it does not
sample a uniform integer from the range. `GenerationLuck` handles negative and
very large stat inputs without overflowing while applying the cap.

`LootGroupRoll` retains the natural selection roll, independent selection bonus,
chosen outcome, natural quantity d100, captured Luck, effective quantity result
and actual amount. Its optional `luck` argument controls amount only; it does not
implicitly change group selection. Live equipment tables apply their separate explicit policy.
The Domain roller is stateless; the arena defeat path captures the complete generated reward once before exposing world drops.

The old d20 amount mapping has been replaced for newly generated rewards.
Already captured amounts are immutable and are not recalculated. Frozen research
price/probability outputs based on the old mapping are not validation of this
policy and have not been rewritten or promoted to production balance.

## Captured gold and atomic pickup

`InMemoryCombatState.CaptureGoldReward` validates that the source actor is
actually defeated. Its first positive captured amount is retained for that actor
identity; repeated capture cannot replace it or credit a wallet. The optional
`RollContext` retains matching generation dice, effective results and Luck through
placement and collection retries; mismatched provenance is rejected. The zero-gold
case is represented by not creating a gold reward, rather than a zero-value
world object. Production tables must meet the agreed guaranteed-gold rule.

`GoldPickupService` reads a wallet version and requests one commit. The adapter
credits the canonical amount and records consumption together under the shared
session lock/version. A stale version, defeated collector or missing reward does
not consume the reward or credit gold. Repeated collection, including by another
collector, returns `AlreadyCollected`. Balance/version overflow publishes nothing.
No client-supplied amount is trusted at pickup, and no retry rerolls a reward.

Wallet balances are separate from combat resource pools, so existing mana reads,
regeneration, equipment clamping and attack costs do not interpret gold as mana.
Wallets and captured rewards store nonnegative/positive whole quantities in
64-bit integers; the agreed gold-range roller produces the same integer unit. Currency spending and sale pricing are not
implemented by the collection port.

`GreyboxArena.PlaceGoldReward` accepts an already calculated amount and searches
for clear nearby ground. Gold and item drops participate in the same spacing
checks. Blocked placement retains the captured amount and retries placement on
game time, without rerolling or crediting the wallet. Duplicate placement requests
cannot create a second representation. Drops remain across rounds in the same
session; fresh bootstrap sessions start with empty wallets. Persistence is planned.

## Verification

Domain tests cover independent groups, empty equipment outcomes, newly unlocked
thresholds and integer quantity mapping. Application tests cover capture versus
credit, duplicate collectors, conflicts, invalid sources, overflow rollback and
session isolation. Godot tests exercise native gold rows, wallet display, pickup
range, pause and unchanged bag placement. Live-catalog engine tests additionally check every rarity boundary, all base pools, level gates, Luck and reference coverage.
