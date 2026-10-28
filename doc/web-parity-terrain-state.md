**P3-04 follow-up — 28 October 2026:** [Save 6/protocol 4 terrain integration](web-parity-terrain-transport.md) now supplies actual state encoding, provenance/mode/clock/random composition, session replacement and bounded transport. The dated P3-03 boundaries below describe its original delivery. Rendering and live creation remain P3-05/06 work.

# P3-03 canonical terrain state — 28 October 2026

P3-03 delivers detached canonical terrain and an immutable in-process read model.
[TerrainWorld](../src/FreeTrain.Online.Simulation/TerrainWorld.cs) accepts valid
worlds with **zero rails**, validates the complete ground/occupancy graph, and
returns an atomic replacement after checking the expected revision. The live
reference world, Save 5 and protocol 3 retain their existing exact identities.
**Next: P3-04 saves and transport.** No terrain creation endpoint, generator,
renderer or browser control is enabled; overall P3 and existing P2/native gates
remain open.

## State and source bindings

The [P3-01 contract](web-parity-terrain-contract.md), its
[40-file pinned review](parity/terrain-review.json) and
[77 independent source vectors](parity/terrain-fixtures.json) remain the authority.
The production state implements these primitives, not the later editing or
scatter algorithms:

- `World.cs`: explicit stored H/V/Z dimensions, row-major cells (`v*H+h`), ground
  and one global water plane. Ground relation and underwater status are independent
  strict comparisons. All stored cells remain available even when the source's
  separate train-border predicate excludes them. The Empty factory expands the
  P3-02 source-default request into 30×42×6, ground/water 1, allocator 1 and no entities.
  Names remain verbatim; localized empty-name presentation belongs to P3-06.
- `MountainVoxel.cs`: NE/SE/SW/NW quarter offsets 0…4, nibble encoding 12/8/4/0,
  fine height `4*ground+offset`, and zero offsets when no Mountain exists. These
  corners correspond to **diagonal** Direction indices 1/3/5/7. Flat Mountains
  remain transparent occupied cells; saturated Mountains retain their ground and
  identity. Fine height cannot exceed `4*Z-1`; all-four at the top level fails.
- `MountainVoxel.isCornerMatched`: compare the four touching samples, skipping
  outside neighbors. Natural terrain must match. Explicit `Cut` and `Terrace`
  classifications permit discontinuity only at shared sites touching those cells;
  they never exempt a distant malformed natural site. No normalization or edit
  operation silently rewrites supplied geometry.
- `TerraceVoxel.cs`: the occupied voxel sits at ground−1 and backs up four offsets;
  it is unowned, non-reclaimable and has value zero. The state requires its backing
  entity. A Mountain/forest or Creek may occupy the distinct ground voxel above it.
  Terrace placement/removal and their source decoration-loss behavior remain P3-15.
- `ForestBuilder.cs` / `MountainVoxel`: a Mountain can own a forest binding plus
  the ordered realized tree X/Y positions and sprite indices. Validate the exact
  species/ground-treatment recipe, source density limit (at most six trees),
  projected position bounds, sprite range and per-contribution entity value.
  Ownership is separate from reusability/value; manual underwater eligibility is
  retained. The variant binding determines its prepared ground overlay; no image
  pixels or render-time random draws enter world state. Scatter remains P3-11.
- `CreekVoxel.cs` / `LandVoxel.cs`: a transparent, silently reclaimable ground
  occupant with zero value and connection bits 0…15. Its stored appearance never
  changes the global water level or carves terrain. Routing remains P3-10.

The immutable `SpatialIndex` owns stable IDs and one owner per voxel. Every entity
has exactly one terrain payload and an exact supported single-voxel footprint.
Both visible and transparent overlaps fail. A column admits at most the backing
Terrace and its ground occupant, so payload enumeration is bounded at twice the
cell count. Ground dimensions/admission checks run before grid enumeration;
ground, water, footprint, corner, metadata and continuity checks precede publication.
No desktop globals, test trains, listeners, image/native/UI APIs or gameplay RNG
are involved.

`Replace` checks the world revision, preserves surviving IDs, forbids allocator
rewind/reuse of removed IDs, and requires a revision increment for changed entity
or decoration data. New entities begin at revision zero. Removing the last entity
leaves a valid empty world and preserves the allocator. On any failure, the old
world, graph, allocator and read remain unchanged. This is a domain primitive;
service permissions, FIFO fences, financial effects and edit legality remain with
the corresponding playable slices.

## Prepared definitions and profile dispatch

[TerrainProfiles](../src/FreeTrain.Online.Content/TerrainProfiles.cs) registers only
the exact bundled pack/version/catalog hash under `terrain-state-1` /
`terrain-content-1`. These are **in-process state identities**, distinct from the
P3-02 request/generation profile and the live reference profile. Missing/extra/
duplicate packs, case changes and hash/version mismatches have no fallback.
Simulation accepts a trusted portable handler table from Content; browser data
cannot register handlers.

Registration verifies ready prepared ground recipes for spring/winter/water/
underground, all 18 forest treatments, the Terrace and the Creek. Forest, Terrace
and Creek definitions retain their original contribution and recipe IDs. The
Mountain has explicit runtime identity `system:terrain/mountain` /
`geometry:quarter-mountain-1`: source Mountain drawing is quarter/color geometry,
not an invented bitmap recipe. Future rendering must dispatch that geometry
separately from prepared sprite lookup. The four ground recipes remain explicit
profile bindings. Optional sets have no terrain registration in this slice.

The synthetic `ReferenceWorld` retains its own eight-heading loop invariant.
Terrain's separate profile admits no rails yet; P4 must register and validate real
rail state. This removes the loop as a universal canonical-world assumption
without allowing terrain data through the reference Save 5 or browser validators.
`WorldCreationCapabilities` remains unavailable for live creation until the
save/transport/render/worker slices are ready. Prepared catalog inventory still
makes no blanket gameplay-readiness claim.

## Verification and handoff

[TerrainWorldTests](../tests/FreeTrain.Online.Tests/TerrainWorldTests.cs) use literal
expected results, including source dimensions/asymmetric storage corners,
TERRAIN-017–020 ground/water comparisons, a four-cell shared quarter peak,
flat/saturated/top-level geometry, cuts, terrace backing plus a second surface
occupant, exact forests and creek reusability. Negative cases exercise overlap,
invalid corners/height/footprints/payloads, unavailable recipes, stale revisions,
allocator continuation, changed-entity revisions and exact pack dispatch. Existing
reference/save/network regression tests continue against the unchanged live profile.

Reproduce:

```sh
dotnet build FreeTrain.Online.slnx -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test FreeTrain.Online.slnx -c Release --no-build --no-restore -m:1 -nr:false
python3 scripts/check-project-boundaries.py
python3 scripts/review-terrain.py --check
python3 scripts/review-terrain.py --check --source-root /Users/dmark/dev/freetrain-exav-archive
python3 -m unittest discover -s scripts/tests
git diff --check
```

Validation on 28 October: Release build passes with zero warnings/errors;
**379 .NET cases pass**, including **42 new terrain cases**. All **141 Python
tests**, offline and explicit-archive P3-01 checks, project boundaries, local
document links and whitespace checks pass. The initial sandboxed .NET attempt
could not bind its runner socket; the successful suite used local socket access.
Local sockets are required by the .NET runner and HTTP/WebSocket regression tests. Logs live under
`tmp/p3-03-{build,dotnet,python}.log`. Source checks exercise the existing P3-01
specification; they do not automatically promote all 77 vectors to runtime parity.
No client/browser behavior changed, so no client/browser suite or native/device
acceptance is claimed.

P3-04 must define actual-state byte encoding and candidate digest, current-format
save/profile dispatch, bounded snapshot/delta/removal/checkpoint transport and
round-trip/restart/error-preservation evidence. `TerrainRead` is an in-process
immutable view, not that wire contract. Mode/provenance/clock/random composition
belongs to that persistence integration and the later owning slices. Remeasure
actual state/serialization allocations and transfer size against P3-02 reservations
before enabling any size preset or job. This slice establishes no end-to-end
terrain/browser capacity claim.
