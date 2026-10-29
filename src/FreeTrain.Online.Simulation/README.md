# Simulation baseline

`FreeTrain.Online.Simulation` contains the canonical game logic. Stable FreeTrain
ExAV 2.1.1.12 is the source authority; do not prefix new types, namespaces,
directories or tests with `Exav`. Keep source/version names in provenance and
research evidence where they identify the authority.

`Prototype/` and `FreeTrain.Online.Simulation.Prototype` explicitly isolate the
obsolete original-FreeTrain gameplay baseline. Its four-direction geometry,
clock, random generator, definitions and world remain only for explicit historical
regression harnesses and headless traces. The default service uses `ReferenceWorld`,
a validated H/V loop with the canonical source-minute clock/calendar/scheduler
and versioned world-owned random streams, with no prototype subsystem dependencies.
See [P1-02](../../doc/web-parity-clock.md) and [P1-03](../../doc/web-parity-random.md).
[P1-04](../../doc/web-parity-spatial-contracts.md) adds Cube, explicit footprints,
stable entity IDs and world-owned voxel occupancy. Only Save 7 is supported; it
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
session state and Save 7/protocol 5 while preserving the distinct reference profile.
Rendering and browser creation follow in P3-05/06.
