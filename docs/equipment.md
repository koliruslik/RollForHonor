# Equipment

[Project overview](../README.md) · [Documentation index](README.md)

## Layer ownership

Domain `Items/Equipment` separates Models, Configuration and Services.
`EquipmentLoadout` captures occupied slots with unique owned instance IDs.
`EquipmentRules` maps slots to required item-base tags. `EquipmentLoadoutService`
creates immutable equip/replace/unequip results. `EquipmentStatComposer` rebuilds
the entire equipment contribution through the existing `ItemModifierProjector`.
These types know nothing about actor ownership, Godot or session storage.

Application `EquipmentService` handles `EquipItem` and `UnequipItem`. It reads
owned items and rules, rejects ordinary ownership/compatibility failures, builds
the Domain loadout and requests a version-checked commit. The adapter contract
requires stat recomposition and equipment publication to succeed together.

Infrastructure implements `IEquipmentStateAdapter` on `InMemoryCombatState`.
Equipment operations live in a separate partial file under
`Infrastructure/Equipment`, sharing the same immutable session, lock and version
as impacts, attack costs and cooldowns. Inside that boundary, pure Domain
composition/calculation and `CombatStatProjector` prepare the replacement state.
No state is assigned until equipment, profile, resolved stats, combat view and
clamped resource values are ready.

Godot's existing `ActorStats` reads resolved speed for the motor. The
[game interface](interface.md) connects item cards and equipped slots to the same
Application operations, using an authored starter kit without item drops.

## Minimal slot rules

| Slot | Required base tag |
| --- | --- |
| Weapon (main hand) | `weapon` |
| OffHand | `offhand` |
| BodyArmor | `body-armor` |
| Helmet | `helmet` |
| Boots | `boots` |
| Amulet | `amulet` |
| RingLeft | `ring` |
| RingRight | `ring` |

Every instance occupies one slot. A two-handed bow can be paired with a quiver;
it is not duplicated across the two slots. `EquipmentPairRule` defines compatible
offhand/main-hand category tags independently of specific bases. Defaults pair
`quiver` with `bow` and `shield` with `one-handed`; a shield also permits an empty
main hand. An absent main weapon does not permit a quiver. Unknown offhand
categories are rejected by these default rules. Inject different pairing rules
to introduce other combinations; providing no pairing table preserves tag-only
compatibility for custom equipment configurations.

Equipping a new main weapon removes a now-incompatible offhand from the proposed
loadout. Removing a bow also removes its dependent quiver. Both remain owned;
bonuses are recomposed from the resulting loadout in one atomic commit. Direct
adapter proposals are checked against both slot tags and the complete hand pair.
The game UI also checks bag space for all returned items before the operation.

Both rings require distinct instance IDs; matching bases are allowed. A base can
retain general affix tags such as `armor` alongside its specific equipment tag.
The generic `armor` tag alone does not match BodyArmor. Item rarity determines
affix capacity, not slot or hand compatibility.

## Operations and ownership

`RegisterOwnedItem` grants a canonical immutable instance to an existing
stat-managed actor. Instance IDs are unique across the session. Granting ownership
advances the version without equipping the item or changing stats. Legacy
laboratory actors registered through `AddCombatant` are not equipment owners.

```csharp
var equipment = new EquipmentService(state);
state.RegisterOwnedItem(actorId, boots);

var equipped = equipment.EquipItem(actorId, boots.Id, EquipmentSlot.Boots);
var removed = equipment.UnequipItem(actorId, EquipmentSlot.Boots);
```

The actor must already be registered through `AddStatCombatant`. The example
assumes Application Equipment Services and Domain Items Equipment Models imports.

Equipping replaces the target slot's occupant. Replaced/removed items remain
owned. An instance already in another slot is rejected. Equipping the exact
current item or removing an empty slot is an unchanged operation: no new version,
stat calculation, healing or resource change. Even unchanged operations verify
the expected version, so a concurrent unequip cannot be hidden by an old read.

Expected gameplay failures return ItemNotOwned, IncompatibleSlot or
AlreadyEquippedElsewhere. Publication returns Applied, Unchanged or
VersionConflict. Application performs one attempt without silently retrying.
Invalid arguments, inconsistent port data, forged direct proposals and arithmetic
failures throw without partial publication. The adapter independently rechecks
canonical ownership and compatibility; copying an owned ID cannot substitute
different item values.

## Stat inputs and resources

Each stat-managed actor stores an explicit **non-equipment profile** separately
from its composed profile. Equip/unequip rebuilds item modifiers from the complete
current loadout in slot order, preserving other-source entries without filtering
by source-name prefixes. Removing one item removes only its contribution.

`GetStatProfile` includes equipment. The legacy `UpdateStats` complete-profile
edit is permitted only with an empty loadout, preventing it from overwriting
active equipment. To change base or other-source inputs with gear equipped:

```csharp
var inputs = state.ReadEquipment(actorId).NonEquipmentProfile;
var updated = new StatProfile(
    inputs.BaseValues.With(new StatKey(StatType.MaxHealth), 120),
    inputs.Modifiers);
state.UpdateNonEquipmentStats(actorId, updated);
```

The API expects non-equipment inputs; equipment modifiers must not be passed
back as other-source entries. It recomposes the current loadout inside the same
atomic boundary. These explicit host edits are separate from optimistic player
equipment commands.

Increasing maximum HP or mana never heals/refills. Reducing a maximum clamps the
current value downward. Effects, cooldowns and accepted attack receipts survive.
Old reads remain immutable. Launched attacks still pay once; ordinary contacts
read fresh source offense and target defenses. Periodic effects retain the source
snapshot captured when applied.

## Verification and remaining work

Tests cover all eight slots, hand compatibility, dependent offhand returns,
replacement/removal, distinct rings, other-source preservation, canonical ownership, duplicate registration, idempotence, version
conflicts including stale no-ops, overflow rollback, resource clamping, effects,
launch receipts/cooldowns and in-flight impacts. An actual Godot test verifies
boots change motor speed and armor and unequipping restores previous values.

This is an in-memory equipment core with a fixed-grid game inventory interface.
The UI checks spatial capacity for its equipment actions. Ownership grants do not
yet enforce bag placement; future pickup/transfer use cases, owned-item crafting
and persistence must share a consistent publication boundary with affected
loadouts and stat profiles.
