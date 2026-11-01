# P4-03 mixed terrain/rail state — 30 October 2026

**P4-05 follow-up — 31 October 2026:** [Mixed persistence and dispatch](web-parity-railway-persistence.md)
now provide Save8/protocol6, the exact mixed profile and validated session/network
publication. Next is P4-06. The original step-boundary descriptions below retain
their historical detached-state/booking scope.

P4-03 delivers the detached canonical composition and its state-handler profile.
[P4-04 atomic construction/removal booking](web-parity-railway-booking.md) is now
delivered; next is **P4-05 mixed-world saves, wire contracts and dispatch**. P4 gameplay remains open;
this step introduces no live command, renderer, save/protocol version or network
profile. The existing terrain and synthetic reference-loop worlds retain their
current behavior.

Authority is the [P4-01 pinned source contract](web-parity-railway-contract.md)
and [P4-02 independent fixtures](web-parity-railway-fixtures.md), ExAV stable
2.1.1.12/V2, Git `883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, `trunk/`.
Tests consume the existing literal artifact directly; production output does not
supply expectations. Additional interface and failure cases are modern state
validation tests, not original-runtime captures.

## Canonical composition

[MixedWorld](../src/FreeTrain.Online.Simulation/MixedWorld.cs) combines an immutable
`TerrainWorld` with the railway records in
[RailwayState](../src/FreeTrain.Online.Simulation/RailwayState.cs). Its combined
`SpatialIndex` is authoritative for all occupancy. The terrain index is a validated
subset retained for terrain geometry/forest/terrace queries, not a second competing
occupancy authority. Every terrain entity must appear unchanged in the combined
index. A Mountain/terrace/Creek and a TrafficVoxel cannot own the same voxel, even
when one is transparent. Ground/water are column metadata; vacant buried cells
and cells with an actual Mountain remain distinct.

| State | Canonical representation and validation |
| --- | --- |
| Ordinary TrafficVoxel | One spatial entity and one `TrafficComponents` record, with rail, road, accessory, car and future platform slots. Components do not each claim the whole voxel. Rail/road/accessory absence rejects an empty or car-only TrafficVoxel. Road-only and accessory-only TrafficVoxels remain legal. |
| Normal/junction | Exact pattern-handler binding, IDs 0–11/12–27. Open ends, missing reciprocal neighbors and disconnected networks are legal state. This does not inherit `ReferenceWorld`'s connected eight-heading loop requirement. |
| Special/signal | Explicit subtype binding plus pattern, bounded art selector and direction; straight source axes, with eight-way girder and cardinal bridge/tunnel/garage/signal. Only Tunnel carries a required four-corner backup; only Signal may reference a future controller. No new reclaimability is inferred from sprites. |
| Slope | One `SlopeOwnership` and one eight-cell spatial footprint, with four Traffic members and four complementary paired members. Cardinal climb, member locations, serialized surface/underground patterns, support/wall kind and source sprite index must agree. Missing/extra/foreign members reject the graph. |
| Paired cells | Explicit filler/support/wall kind, exact support/wall recipe and stored above/below-ground draw flags. Stored art/flags are never recalculated from later terrain heights. Filler has no bitmap recipe. |
| Piers | Individual stable spatial IDs, explicit per-cell recipe/selector and separate `SupportedOwnerId`. Deleting a pier does not delete its supported rail. The supported ID may survive as a historical reference after height-bounded teardown removes its rail. P4-10 corrects the earlier `SlopeEntity.remove` rationale: its Traffic callbacks do remove lower piers. IDs must be positive and below the shared allocator; references to unrelated live entities reject. |
| Pole/crossing | One accessory slot. A retained pole does not require a rail: source clearing rail does not inherently clear the pole. A Crossing requires straight cardinal rail plus both perpendicular road edges. Pole replacement/crossing synthesis/removal callbacks remain P4-17/20 operations. |
| Future road/car/platform | Explicit trusted profile bindings only. Road carries an eight-direction edge mask; car carries positive train/car identities, with duplicate car occupancy rejected; platform requires a rail. These interfaces do not deliver P5/P6/P7 simulation or authoring. |

All railway member kinds are **transparent but occupied and nonreclaimable**.
This follows pinned `TrafficVoxel.transparent`, `EmptyVoxel.transparent`,
`SlopeSupportVoxel.transparent` and `BridgePierVoxel.transparent`; visible art
is not an opacity or reclamation test. Source piers expose their supported entity;
the portable graph gives each pier a stable voxel identity and retains that source
relationship separately, permitting owner-agnostic teardown without overlapping
independent whole-voxel owners. GEO-004/RAIL-D006 remains unchanged. Neither an
orphaned supported ID nor a missing optional support is grounds to reject an
otherwise legal rail network.

## Identities, replacement and session boundary

There is one world-local entity-ID namespace and high-water allocator across
terrain and railway owners. New IDs start at the old combined high-water mark,
start at revision zero and cannot reuse removed identities. Unchanged IDs retain
exact payloads and revisions; changed entities increment their revision by one.
An ID cannot change between terrain/traffic/slope/pier owner kinds. Reordering
members alone does not change identity. Mixed geometry has its own monotonic
revision; the terrain subset retains its existing revision and advances only
when replaced. Counter overflow rejects before publication.

`MixedWorld.Replace` constructs and validates a whole candidate, validates any
terrain successor using the existing terrain replacement contract, then checks
combined identities and payload revisions before returning it. It mutates no
input graph, allocator, session, clock, random stream or listener. Overlapping,
unknown, stale, partial-slope and allocator-reuse candidates preserve the original
world. Callers publish only the returned candidate. This is a detached state
transaction boundary, not yet a booking or authorized session command.

Terrain drafts in a mixed world must receive the **combined allocator high-water
mark**, then return terrain state and combined occupancy for validation together.
The tests include both that valid workflow and rejection of a terrain-only
allocator that would reuse a railway ID. Future commands must use combined
occupancy for all legality checks; calling a terrain-only tool directly is not
an authorized mixed-world mutation path. [P4-20](web-parity-cross-tool.md) now
implements that combined-occupancy terrain draft and authorized session path.

P4-05 should add mixed-world session/save/wire dispatch around this composition,
reusing the existing terrain provenance, construction-mode, `ClockState`,
`RandomStreamsState`, pause and session revision contracts. Do not create a second
clock, reroll forests or infer current ground from provenance. Session FIFO,
role/epoch/content fences and session revision remain the outer authority; mixed
geometry and terrain geometry revisions identify their immutable subsets.
P4-04 now adds canonical economics in `MixedConstructionState`, which returns
geometry/allocator and booking in the same immutable publication boundary.
Current `TerrainSessionState`/`SessionHost` deliberately do not accept mixed state
until these prerequisites and P4-05 full input/transport validation are delivered.

## Exact handler profile

[RailwayProfiles](../src/FreeTrain.Online.Content/RailwayProfiles.cs) registers
**87 bundled state bindings** against the existing exact pack/version/catalog
hash: 60 serialized patterns, 16 support/wall recipes, one default pier, four
special rails, two signals and four poles. Each binding resolves the complete
case-sensitive pack/contribution/recipe tuple. Prepared recipe handler, readiness,
frames and signal/pole contribution types are checked. `system:traffic` and
`system:slope` are explicit internal composition geometries, never bitmap lookups.

Prepared content by itself grants no handler. Unknown packs, aliases, recipes,
pattern/family mismatches and inappropriate component bindings reject. The trusted
constructor can supply explicit interface bindings for future consumers; bundled
roads/cars/platforms/crossings remain unregistered. Stationary structures and their
construction-site scheduler remain P4-18/P7. P4-10/12 must register exact additional
slope/bridge pier recipes/selectors before those styles can enter bundled live
state; the default-pier registration is not a fallback for missing art.
`WorldProfiles`, terrain content identity, current Save7/protocol5 and
`PreparedContentCatalog.GameplayAvailable` are unchanged. P4-05 owns the deliberate
mixed ruleset/content/save/protocol identity change.

## Verification

[MixedWorldTests](../tests/FreeTrain.Online.Tests/MixedWorldTests.cs) consume all
28 literal normal/junction patterns and all **15 legal literal slope cases**
(all cardinal directions, surface/underground/mixed art, water-relative cases and
stored flags after a ground edit). They verify zero-rail legality, disconnected
networks, trusted component coexistence, special subtype/tunnel backups, actual
nonflat terrace backups, pier retention/removal, exact bindings and source
transparency. Invalid member/owner/pattern/recipe/height/overlap/revision/allocator
cases reject without changing the original graph. A mixed terrain/rail replacement
and an allocator collision are exercised alongside byte-identical preexisting
terrain-session save state.

Reproduce from the repository root:

```sh
.tools/dotnet/dotnet test FreeTrain.Online.slnx --no-restore -m:1 -p:UseSharedCompilation=false
python3 scripts/railway-fixtures.py --check
python3 scripts/review-railway.py --check
python3 scripts/railway-fixtures.py --source-root /path/to/freetrain-exav-archive --check
python3 scripts/review-railway.py --source-root /path/to/freetrain-exav-archive --check
python3 -m unittest discover -s scripts/tests
python3 scripts/check-project-boundaries.py
git diff --check
```

Targeted verification passes **15/15 .NET cases**, including the literal pattern/slope loops.
Default and explicit pinned-Git railway fixture/review checks pass; the complete
Python evidence suite passes **161 tests with one existing skip**, and project
boundaries and whitespace checks pass. The full .NET regression passes **750/750**; the final
15/15 targeted run additionally verifies the last backup-height/profile-binding
rejection guards. Full run evidence: `tmp/p4-03-dotnet.log`; final targeted evidence:
`tmp/p4-03-targeted-final.log`; Python evidence: `tmp/p4-03-python.log`.
These logs are local verification artifacts, not committed source-oracle inputs.
Browser/native
rendering, mixed save/restart, actual booking, railway operations and full-city
capacity remain their implementing gates. Existing P2/native/deferred-browser
and later-phase gates remain open.
