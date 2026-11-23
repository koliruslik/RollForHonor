# Godot runtime and test scenes

[Project overview](../README.md) · [Documentation index](README.md)

## Runtime composition

`GameBootstrap` creates one session in `_EnterTree`, before child nodes enter
the tree. It exposes `CombatState` for explicit participant/resource registration
and `Attacks` for launching an `AttackDefinition` with request/source identities.
An accepted launch returns an `AttackInstance`; call its `ResolveContact` for
each eligible physical contact and inspect the returned commit result. Resources
and cooldown are committed at launch, not charged again on each impact.

`InMemoryCombatState` implements the combat, initiation and equipment state ports. It stores immutable
combatant views, resources, cooldowns and deduplication receipts. Reads and writes
are synchronized; each successful transaction publishes one replacement state.
A conservative session-wide version rejects stale proposals without partial
changes or automatic retries. State is session-local and is not persisted; this
is not a complete character/inventory model.

The baseline `BasicAttackPipelineFactory` composes attack rolls, hit quality,
typed damage, defense and new-status application. Runtime coefficients are
`0 / 0.5 / 1 / 2` for evaded/glancing/hit/critical. `NoResourceCostModifiers`
explicitly supplies no reductions. The factory also selects a single supported
`LifestealDefinition` from the payload's copied `Behaviors` collection, after
target damage/health effects. Behavior definitions are separate from `Effects`,
which still contains statuses to apply. Unknown behaviors and repeated lifesteal
definitions are rejected rather than silently stacked. Equipment inputs are recomposed
through the [equipment core](equipment.md); automatic active-status modifier collection
and selection of arbitrary existing-status behaviors remain separate work.
`RandomDiceRoller` uses `Random.Shared` with a fresh trace per impact or tick;
an explicit `Random` can be supplied for reproducible non-engine tests.

Cooldowns and attack lifetime share accumulated Godot game time. `_Process`
consumes the supplied delta once, already scaled by Godot; a paused tree does
not advance the clock. Configure the bootstrap's `CombatDiagnosticsEnabled`
and `DiagnosticView` before tree entry to send optional reports to Godot Output.
Diagnostics default to off and are separate from the scene's result-based journal.

Normal Play starts the runtime without automatically running tests or quitting.
`Bootstrap.tscn` hosts the [main menu](interface.md). Start Game loads
`ArenaBootstrap.tscn`, which hosts `Arena.tscn` in a fresh session.
`WorkbenchBootstrap.tscn` hosts the separate `Game.tscn` combat laboratory.

## Actor foundation

Reusable actor behavior lives under `godot/Gameplay`, grouped by responsibility.
`DynamicEntity : CharacterBody3D` owns combat identity and its direct child
`EntityComponent` nodes. It assigns every component's host before calling any
`OnComponentsInitialized` hook. Required-component lookup rejects missing or
ambiguous matches. Scene hosts provide dependencies and enable the actor only
after configuration; components must not use their host in their own `_Ready`.

The player physics step runs these operations in order:

1. `ActorState` reads authoritative defeat state and updates a small FSM.
2. `PlayerInput` checks the actor's movement permission and converts held controls into horizontal world intent; a disallowed mode explicitly requests a horizontal stop.
3. `CommandProcessor` consumes queued commands once in FIFO order.
4. `MovementMotor` advances the native character body, including gravity.
5. After the shared sequence, `Player` asks `PlayerInput` to read the cursor and update facing.

Components do not independently run these steps in `_PhysicsProcess`.
`Player` handles unhandled right clicks through its controller; a different
controller can supply world intent and `AttackAt` commands to the same actor
capabilities. The arena scene host supplies attack content, services and the
projectile scene. `ProjectileAttack` depends on the launch service and combat
state port, rather than the bootstrap or laboratory actor class. `ICombatBody`
maps physical bodies to combat identities and aiming points.

`ProjectileAttack` is a temporary implementation for the current single
projectile attack. When attack variety is introduced, extract a convenient
attack selection and delivery format instead of accumulating attack-specific
branches here. That future format is not decided or implemented.

`PlayerInput` owns reading movement, clicks and the continuous cursor position.
`MouseFacing` performs the ray query, aim-point calculation, marker update and
visual yaw rotation; it does not move the character body. The host calls
`PlayerInput.UpdateCursorFacing` after movement so the ray uses the actor's
updated position. No separate cursor-input component is required.

`StateMachine` coordinates registered `IState` lifecycle callbacks. Changing to
the current state does nothing; missing states, duplicate registrations and
transitions requested recursively from lifecycle callbacks fail explicitly.
Callback exceptions propagate without rollback. `ActorState` uses Active and
Defeated modes to expose movement permission; it observes fresh combat state
and can return to Active after health is restored. The controller applies that
permission. `MovementMotor` does not read the FSM, combat state or a host movement
flag: it executes final world-space intent once, applies gravity and collision,
and exposes actual velocity, horizontal speed and floor contact. There is no
separate locomotion-state enum. Zero intent brakes; `StopHorizontalMotion`
immediately clears horizontal velocity and intent while preserving vertical
velocity. The controller uses that explicit stop when defeated, so gravity
continues and held movement cannot restart motion until recovery.
Application remains authoritative for launch refusal, resources and cooldowns.

`CommandProcessor.CommandLifetime` is a read-only placeholder, currently null.
Age-based queue expiry is not implemented; its duration, clock and retry policy
must be defined before enabling retention. Commands have no automatic retry or
input buffer window. A refused shot is consumed once, so it cannot fire later
when a cooldown expires. Movement can continue during shooting. There is no layered state machine or global event
bus; launch/contact events feed the existing scene-owned journal directly.

## Greybox arena

Press **F5** from `godot/project.godot` to enter the arena through
`Bootstrap.tscn`. The editable `Arena.tscn` contains a 120×120 colored greybox
map, obstacles, passages, 0.4 m and 4 m terraces, ramps, five stationary targets,
an angled orthographic following camera and a directional arrow on the player.
A red Sentinel is an active ranged opponent; the five training targets remain
stationary. The arena uses untextured greybox materials.

| Input | Action |
| --- | --- |
| W / A / S / D | Move relative to the camera, independently of facing |
| Mouse position | Face the observed surface point, rotating only visual yaw |
| Right click | Request a projectile attack; movement can continue |
| Mouse wheel | Zoom the camera |
| H / Reference | Open searchable rule and session explanations; gameplay pauses |
| R / Restart arena | Start a fresh arena session |
| F2 / Combat laboratory | Open the combat workbench in a fresh session |

The camera follows position independently of facing. Physical WASD bindings are
configured in Project Settings → Input Map (`arena_forward/back/left/right`).
The player reads held movement actions each physics step and queues discrete
shoot commands. `PlayerInput` converts camera-relative axes into world-space
intent; `MovementMotor` handles acceleration, braking, gravity, collisions and
physical observations without depending on a camera or actor mode.
`MouseFacing` handles the surface ray and horizontal arrow orientation.
`ProjectileAttack` calls the existing Application services. Release movement keys to brake.
Left click does not move or attack; each right-button press requests one shot.

`Arena/Player.tscn` and `Arena/GreyboxMap.tscn` remain editable scene assets.
Movement uses native character collision against terrain, walls and targets.
There is no automatic obstacle routing.

Each accepted shot costs 10 of the initial 500 mana, starts a 0.25-second
cooldown, travels at 16 units/second and lives for at most 4 game seconds.
It uses the current source/target state at contact and the same Application
commit and result-based journal path as the workbench. The separate d20 repeats
the natural roll of the latest newly committed impact.

`AirborneProjectile` fixes its three-dimensional direction at launch and flies
straight without gravity, homing or per-frame ground-height correction. The
launch point is 1 m above the actor's feet, without a forward spawn offset that
could skip a nearby wall. Attack aiming raycasts actors, terrain and obstacles,
excluding the source. A target uses its fixture body center (0.9 m above its
feet); empty terrain uses the observed surface plus 1 m; an obstacle uses its
actual surface point. The actor's visual still rotates only in yaw.
The visible cursor marker uses that same closest-surface ray (excluding the
player), sits slightly outside the surface and aligns its disc to the hit normal.
It therefore appears on target bodies and vertical walls as well as the ground.

Swept body collision stops the projectile at an actor, wall or the ground and
reports that contact through the existing Application/journal path. A shot can
pass above descending terrain, hit a higher/lower target when the line is clear,
or strike a slope/cliff that obstructs it. Expiry still ends an unobstructed
shot after its lifetime. Terrain uses physics layer 5 (mask 16), obstacles layer
3 (mask 4), actors layer 2 (mask 2); the projectile collision mask is 22.

## Ranged enemy

`Arena/Enemy.tscn` hosts an `Enemy : DynamicEntity` with the same actor-mode FSM,
command processor, movement motor and temporary `ProjectileAttack` component
as the player. `EnemyController` runs in the Input stage, supplies world-space
intent and queues `AttackAt` commands. It owns no HP mutation, attack arithmetic
or collision integration. Player and enemy use independent combat identities
in the existing session; changing scenes resets both.

The controller detects the configured living player within 18 world units using
a three-dimensional distance and an unobstructed ray between body aiming points.
It pursues directly at 3 units/second while beyond 7 units, turns its visual yaw
toward the player, then stops horizontal motion and fires at the current body
position when the Application cooldown is ready. Losing visibility, leaving
detection range, losing the target or observing either actor's defeat stops
movement and new attacks. Gravity continues. There is no navigation, last-known
position memory, prediction, strafing or retreat behavior; walls can prevent
engagement and the controller does not route around them. Obstacle routing
and extended aggro/threat behavior are deferred.

The fixture has 40 HP, armor 2 and evasion 10. Its free basic shot has attack
modifier +5, `1d6 + 2` piercing damage, a 1.5-second source-local cooldown and a
4-second lifetime. Red projectiles travel at 12 units/second along a fixed 3D
launch direction and use the same swept collision and impact pipeline as player
shots. The configured cooldown reader uses the shared session clock rather
than an AI timer. `ThinkingEnabled` can disable decisions for isolated scene
tests; it is enabled for ordinary play.

The enemy label shows current HP and Idle/Engaged/Defeated observations. These
labels describe controller activity and authoritative defeat, not another
locomotion FSM. The shared journal identifies the actual attack source and
recipient; its separate d20 shows the most recent committed impact from either
side. A defeated actor remains as a physical body; press R to restart.

## Combat laboratory

Build the solution, open `godot/project.godot` in Godot .NET and press **F5**
to run the arena through `Bootstrap.tscn`, then press **F2** for the laboratory.
Alternatively, open `WorkbenchBootstrap.tscn` and press **F6** to run it directly.
Open `Game.tscn` to edit its
objects visually; it depends on its parent bootstrap and cannot run standalone
with F6.

The scene includes a player, enemy, floor, camera, lighting and an editable
combat workbench. Press **Space** or **Player attacks** to launch a projectile;
press **E** or **Enemy attacks** to attack the player. Each side owns its mana
and cooldown. A successful
launch costs 10 of the initial 100 mana and starts a 0.8-second cooldown. Failed
launches create no projectile. Each arrow travels at 6 units per game second,
ends at its first target/obstacle contact, or expires after 3 game seconds.
**Restart session** reloads the bootstrap and resets both state and visuals.

Select **Melee** in the delivery dropdown and enable **Bring enemy into melee
range** to test a close-range strike with the same Space/E controls. Projectile
and melee delivery implement the Godot-side Strategy contract `IAttackDelivery`;
both request an ordinary Application launch and share its snapshot/calculation/
commit path. Each mode has its own source-local cooldown key, while resource
balances are shared. Changing selection affects future launches only.

`DemoHurtbox` is a separate actor-owned contact area on collision layer 4.
`DemoMeleeHitbox` monitors that layer during a 0.45-game-second demo swing;
it follows its parent actor and reports `AnimationFinished` when that timed
window ends. The blade/range visuals are greybox indicators, not a final rig or
animation graph. A fresh hitbox is spawned per accepted strike, so targets
already in range are detected by real physics entry events. The source is
ignored. Application owns once-per-target admission: several targets may be
processed within one window, including reentry rejection for a previously
contacted target. No second health store or damage formula lives in a delivery
strategy. Melee does not stop on the first target, and a missed strike is not
refunded. Current melee contact geometry is a forward-offset sphere, not a
weapon-accurate sweep, obstruction/line-of-sight solver or terrain-following
delivery. Those behaviors are outside this bounded slice.

`DemoCombatant` maps scene bodies to session identities. `DemoProjectile`
forwards real `Area3D.BodyEntered` callbacks to the accepted attack; moving the
visual alone does not calculate damage. Actors use collision layer 2 and
environment bodies layer 3; the projectile monitors both and ignores its source.
The sample speed/shape relies on discrete overlap detection; this is not a
high-speed projectile or swept-collision implementation.

The scene uses a 1920×1080 design viewport, opening in a 1280×720 window with
canvas scaling. Both stat panels expose current/maximum HP, armor, evasion,
attack modifier, damage dice/count/flat bonus/type and every supported typed
resistance. Resistance inputs are percentages; negative values are weaknesses.
**Apply draft stats** explicitly applies HP/defense edits, including any draft
current HP, while preserving live statuses, mana, cooldowns and receipts. Invalid
HP bounds leave state unchanged and produce a journal entry. Weapon/effect
selections affect the next launch; defense edits affect subsequent contacts,
including projectiles already in flight. Laboratory edits remain explicit combat-view edits; arena actors use the [shared character stat model](stats.md).

The journal uses returned calculation data, independently of optional diagnostic
output. It shows source/target names, admission and commit status, attack roll
and modifier, input defenses, ordered typed damage stages and coefficients,
component dice/flat bonuses, complete dice trace, effect changes and actual
clamped HP changes. Damage types have distinct colors; proposed changes are
marked separately from applied changes. The separate d20 repeats only the
natural die value. Text can be selected and copied; Clear removes the journal
and the view retains at most 100 entries. Modifier tooltips are not implemented.

`DemoEffectCatalog` is a small fixture catalog outside Domain. Burning and
Bleeding selections attach existing `PeriodicDamageEffectDefinition` values to
the next attack payload (test formulas: 1d4 Fire and 1d4 Slashing). Active statuses
and their application/replacement appear in the live panel and journal, with a
captured source. The existing baseline applies selected statuses on processed
contact, including evaded contacts; this work does not introduce a new successful-
hit condition. Production bleeding-specific defense rules and status durations
remain unspecified; the fixture tick pipeline uses current armor/resistance rules.

Enable **Vampirism on attack** and set its percentage (0–100) on either side.
`LifestealEffect` heals from actual opponent HP lost after defenses and any prior
target health adjustments, excluding overkill. Healing rounds upward to whole
HP and finalization clamps the source to maximum HP. Target damage and source
healing publish atomically in the same resolution. Self-damage, ticks and zero
actual HP loss do not trigger vampirism. The behavior must be scheduled last
among damage/target-health effects; subsequent steps must not alter that basis.

**Next tick** advances one global workbench round: each periodic instance on
living participants is attempted once, as a separate transaction with fresh
recipient state, dice and resolution identity. Source state remains captured
at application, even after source death. Removed/replaced/uncaptured instances
and defeated recipients are rejected before dice. Ticks do not reroll d20,
charge mana, restart cooldowns or trigger vampirism. Conflicts return without
hidden retries; a later round is a new attempt, not a replay of the failed one.

Enable **Automatic ticks** to run those same rounds at the selected interval
(default 1 game second). Manual ticking is disabled while automatic mode is on.
Mode/interval changes restart the deadline; game-time pause holds it, and elapsed
intervals are processed in order. This is a scene-wide test clock, not individual
effect application-time scheduling. Statuses have no automatic expiry in this
slice and remain until replacement/reset; defeated recipients stop ticking and
can resume if explicitly restored through the editor. The journal identifies
each tick and its captured inputs while keeping the last attack's d20 display.
`PeriodicTickResult` returns these facts directly; it does not masquerade as an
attack diagnostic report. No weapon-property pipeline stage is introduced.

For automated checks, see [Verification](README.md#verification).
