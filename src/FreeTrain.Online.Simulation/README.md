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
A trusted dispatcher is required; movement follows in P5-08. P5-05 now supplies
[Save9/protocol7 fleet persistence and transport](../../doc/web-parity-fleet-transport.md),
including exact-profile restoration and explicit callback reconstruction.
See [fleet state, source bindings and verification](../../doc/web-parity-fleet-state.md).
