# Simulation baseline

`FreeTrain.Online.Simulation` contains the canonical game logic. Stable FreeTrain
ExAV 2.1.1.12 is the source authority; do not prefix new types, namespaces,
directories or tests with `Exav`. Keep source/version names in provenance and
research evidence where they identify the authority.

`Prototype/` and `FreeTrain.Online.Simulation.Prototype` explicitly isolate the
obsolete original-FreeTrain gameplay baseline. Its four-direction geometry,
clock, random generator, definitions and world remain only for explicit historical
regression harnesses and headless traces. The default service restores a saved
world or generates a seeded 400(H)×600(V) `TerrainWorld` with hills, sparse trees and
optional water. `ReferenceWorld` remains an explicit validated H/V rail-loop
fixture. Both use the canonical source-minute clock/calendar/scheduler and
versioned world-owned random streams, with no prototype subsystem dependencies.
See [P1-02](../../doc/web-parity-clock.md) and [P1-03](../../doc/web-parity-random.md).
[P1-04](../../doc/web-parity-spatial-contracts.md) adds Cube, explicit footprints,
stable entity IDs and world-owned voxel occupancy. Only Save 9 is supported; it
captures entities, allocator, clock and all random states.
Earlier save formats are rejected without conversion;
the running application rejects prototype saves and does not write them. They are not
authorities or defaults for new game rules. Original-source runtime ports outside
this project, including the reference content and manual controller, carry the
same obsolete-baseline notice.

Implement new rules in the canonical namespace. Keep compatibility adapters in
`Prototype`, so canonical rules do not depend on obsolete types. Replace each old
subsystem through the reviewed ruleset/protocol/content/save transition, with
source-derived tests. Existing prototype regression tests preserve compatibility;
passing them does not establish canonical behavior.

P3-02 adds immutable Empty/Heightmap/Generated request contracts, validation,
independent generation stage seeds, bounded work and detached job transitions.
These are [creation contracts](../../doc/web-parity-world-creation-contract.md),
not an enabled generator or a terrain world profile. Content readiness remains
explicit; prepared art cannot authorize a terrain handler. [P3-03](../../doc/web-parity-terrain-state.md) supplies immutable `TerrainWorld`,
ground/corner/water and decoration occupancy, zero-rail worlds, atomic graph
validation and an in-process read model. Content binds the exact prepared terrain
recipes. [P3-04](../../doc/web-parity-terrain-transport.md) adds composed terrain
session state and Save 9/protocol 7 while preserving the distinct reference profile.
Rendering and browser creation follow in P3-05/06.

P4-03 adds detached `MixedWorld`/`RailwayState` composition: one combined occupancy
index/allocator, Traffic component slots, eight-cell slope ownership and exact
state-handler validation. Zero rails/open ends/disconnected networks are legal.
See [state contract and handoff](../../doc/web-parity-railway-state.md). Live mixed
session/save/wire dispatch follows P4-05 persistence.

P4-04 adds source-priced `RailwayBooking`, immutable `ConstructionAccounts` with
validated capture/restore and ordered spend/rescue history, and
`MixedConstructionState` for atomic terrain/rail/allocator/account replacement.
Source initial cash, bankruptcy continuation and charged no-ops are retained;
authoring grants no cash exemption. See [booking contract and P4-05 handoff](../../doc/web-parity-railway-booking.md).

P4-06 adds `RailwayTopology` and immutable `NormalRailway` build/removal drafts:
source sign-vector routes, exact normal/junction attachment/detachment, subtype
no-ops, car protection and charged/support call sites. These are topology inputs
for terrain/support validation, with no live commit or allocation/account
mutation. See [domain behavior, independent checks and handoff](../../doc/web-parity-railway-topology.md).

P4-07 adds `NormalRailwayConstruction` read-only previews and detached atomic
construction publication over those drafts. Actual terrain/water/occupancy,
whole-entity reclamation, source support election/owner-agnostic teardown,
crossing cleanup and exact booking validate before replacement. `RailwaySupports`
accepts explicit top/body bindings for later infrastructure consumers. No live
rail tool or rendering is enabled. See [construction and verification](../../doc/web-parity-railway-construction.md).

P5-04 adds immutable `FleetGraph`, shared controller/ordered group references and
real stock car state linked bidirectionally with Traffic occupancy, using the
existing allocator. `MixedSessionState` binds construction and exactly one
one-shot callback per deployed train, with detached rollback and rebinding.
A trusted dispatcher owns simulation; P5-08 now binds `FleetMovement.Tick` by
default while retaining explicit controlled-dispatcher overrides. P5-05 supplies
[Save9/protocol7 fleet persistence and transport](../../doc/web-parity-fleet-transport.md),
including exact-profile restoration and explicit callback reconstruction.
See [fleet state, source bindings and verification](../../doc/web-parity-fleet-state.md).

P5-06 adds `FleetTrading` formation/quantity quotes and atomic inventory/accounting
commits. Purchase uses one timestamp and spend call per batch; sale clears all
Inside slots, removes membership and cancels exactly its train callback while
preserving shared controllers and other work. `ConstructionAccounts.Earn` keeps
revenue distinct from expenditure and bankruptcy funding, including exact durable
restore. See [trading contract and verification](../../doc/web-parity-fleet-trading.md).

P5-08 adds current-rail Inside movement, reciprocal destination ports, source
visible exits, complete-consist shifting, occupied-track waits and a single
Inside dead-end reversal/retry. Native/prior-head timing and operating bookings
publish with occupancy and the future callback in one detached minute. The
simple controller chooses straight at junctions and passes non-Station signals.
Advanced junction/harbor handlers retain explicit later phase boundaries; the
host safely rejects Step or pauses Run when encountered.
See [movement scope, literal traces and verification](../../doc/web-parity-fleet-movement.md).

P5-14 adds Outside countdown, ordered return selection, occupied-return waits
and ten-count missing-track retries. Countdown50 exchanges passengers before
the consist shift, preserves forwarding, and books source-distance fare revenue
before that move's operating expense. Mixed reversal and Inside-to-Outside
retry use the same dispatcher. Save9/protocol7 and packs remain unchanged.
See [Outside source bindings, traces and verification](../../doc/web-parity-fleet-outside.md).

P5-16 adds typed stop actions and junction routes, shared controller call
contexts and distinct Signal/Station harbor references. Positive waits preserve
handler effects before returning; subsequent actions retain source forwarding,
reversal, counter reset and captured-head timing. Simple chooses straight,
ignores non-Station harbors and waits30 minutes on the first Station call before
departing. P6-19 now dispatches actual slim/wide yards through current resolved
hosts and effective shared controllers, collecting passenger exchange and bell
intents with atomic occupancy/account/queue publication. Bell delivery remains
P6-20; the current contract is Save10/protocol8.
See [controller contract and verification](../../doc/web-parity-fleet-controllers.md).


Manual driving was removed on 7 November 2026. Fleet callbacks have no held-input
argument; movement, reversal, turning and timetable dispatch run from authoritative
world state and game time. Manual is no longer a fleet controller kind/provider;
saves containing it reject without conversion. Automatic Simple, Delegation and
timetable behavior remains. See [the removal decision](../../doc/web-parity-fleet-manual.md).
