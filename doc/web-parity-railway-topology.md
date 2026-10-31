# P4-06 normal routes, attachment and detachment — 31 October 2026

P4-06 implements detached railway topology over the canonical mixed world.
[RailwayTopology and NormalRailway](../src/FreeTrain.Online.Simulation/RailwayTopology.cs)
produce immutable rail-component replacements and source support call sites.
**[P4-07 terrain legality, elevated track and automatic piers](web-parity-railway-construction.md)
now consumes these drafts.** The drafts themselves remain inputs to that validator,
not construction authorization or a publishable mixed-world transaction. Rendering and the first playable browser tool remain
P4-08/09. Save8/protocol6 and the current read-only mixed-world shell are unchanged.

The sole behavior authority is stable ExAV 2.1.1.12/V2, archive Git
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, under `trunk/`. The existing
[P4-01 source contract](web-parity-railway-contract.md),
[66-file hashed review](parity/railway-review.json) and
[P4-02 independent vectors](web-parity-railway-fixtures.md) remain the specifications.
Implementation was checked against pinned Git blobs for `SingleRailRoad`,
`JunctionRailRoad`, `RailRoad`, `RailPattern`, `SlopeRailRoad`,
`SpecialPurposeRailRoad` and the signal subtype. This is source-derived
verification; no original-runtime capture or native acceptance is claimed.

## Domain behavior

`RailwayTopology` binds all 60 serialized pattern IDs to their actual ports.
Normal and junction patterns retain source ordering/identities; slope ports are
independent of visibility, stored art and paired cells. The distinct synthetic
reference loop retains its own closed-network invariants.

A route uses the source same-level sign-vector walk and includes both endpoints.
Each step changes each unfinished coordinate by its sign; it does not search for
a shorter path around obstructions. Thus `(8,8,2) → (11,9,2)` yields
`(8,8,2),(9,9,2),(10,9,2),(11,9,2)` with patterns `7,5,4,4`.
Reversing those endpoints yields a different trace,
`(11,9,2),(10,8,2),(9,8,2),(8,8,2)`, with patterns `7,6,4,4`.
Endpoints use a straight pattern when creating rail; internal turns use their
incoming and outgoing ports. Coincident points return an empty geometry walk
and reject either construction/removal draft with `samePoint`, before direction
lookup. Noncoplanar input rejects.

Existing rail can attach at either endpoint only; any existing middle rail
rejects, even if its edges already match the requested route. An existing edge
succeeds unchanged. A normal rail is well connected only when two of its own
ports have reciprocal rail in adjacent cells at the same Z. Wrong-facing,
different-level and unrelated-edge neighbors do not count. An open rail retains
the first source-ordered edge at least three eighth-turns from the new direction.
A well-connected normal rail permits the source angle1/3/4 combinations and
becomes the exact junction. Junctions, slopes and special-purpose rails accept
only existing ports. Right-angle patterns are never invented.

Normal detach clears the rail when either requested port matches. Junction
detach clears on two matches, converts to a normal pattern on one match, and
stays unchanged on zero matches. All 16 reviewed source null-pattern hazards
reject the whole draft as `invalidRailPattern`: no earlier cell replacement,
charge count or support-call list escapes. Slope and special-purpose subtypes
never detach through the normal tool. Unchanged special endpoints retain the
same component, recipe, tunnel backup and signal controller reference.

## Immutable plan and P4-07 handoff

`NormalRailway.PlanBuild` / `PlanRemoval` return a `NormalRailwayPlan` containing:

- The exact immutable source world, endpoints and operation kind, plus a stable
  rejection reason. Any rejection returns an empty cell list.
- Ordered cells with incoming/outgoing headings, before/after `RailComponent`,
  car-protection and actual-change flags. Component replacements resolve one exact
  profile binding; missing or ambiguous bindings reject rather than substituting
  content or using a catalog entry as authorization.
- The source charged-cell count: all inclusive build cells, including unchanged
  endpoint attachments; removal counts every rail without a car, even when detach
  is a no-op. A car blocks building and skips removal/charging at its cell.
- Source support call sites: new rail cells for election, every visited column
  for removal, including empty cells, wrong-edge rails and protected cars.

`TopologyValid` deliberately reports topology only. The draft neither reserves
IDs nor modifies terrain, spatial entities, components, accounting, clock, random
streams, mode or listeners. It contains no cash quote or live commit method.
`RailwayBooking.NormalRemoval` consumes its eligible count after full validation;
zero eligible cells still produce the source zero-valued spend call.

P4-07 must validate actual ground/corners/global water, space/occupancy,
reclamation and whole-entity losses, roof/floor/border rules, pier obstruction/
style/election and every owner-agnostic teardown loss. It must revalidate or
recompute the draft against the current authoritative world, retain unrelated
identities/components and resolve dependent platform/accessory cleanup before
constructing a whole valid graph. `MixedConstructionState.Replace` then stages
that graph and the complete source-priced booking together. A valid topology
draft alone grants no right to build over a Mountain, slope filler or other
occupant, and does not establish elevated/underground construction legality.

Admission widens endpoint differences before arithmetic, bounds route work to
**8,192 inclusive cells**, then checks every cell against stored H/V/Z bounds
before reading it. This is an initial detached-domain work bound, not measured
network capacity; P4-23 owns capacity acceptance. Extreme Int32 input rejects
before unbounded iteration/allocation. Boundary-neighbor connectivity uses widened
arithmetic and missing outside neighbors do not connect.

## Verification

[RailwayTopologyTests](../tests/FreeTrain.Online.Tests/RailwayTopologyTests.cs)
consume the existing literal JSON directly, without changing expected data or
using production output as an oracle. They check all 60 port tables, all 576
attachments, all 720 detachments (including 16 hazards), all five connectivity
cases and all 192 normal attachment inputs through actual mixed-world endpoint
planning. Fourteen accepted literal routes retain exact cells/patterns/change
indices, including both source heading-change directions. Water/ground/reclamation
legality in those inputs remains the P4-07 gate.

Additional cases cover all eight route directions, inclusive endpoints, protected
cars, charged unchanged endpoints, zero/mismatched detach no-ops, late junction
rollback, subtype-preserving special endpoints, tunnel backups, signal references,
road/accessory/platform boundary preservation, exact-binding failures, stored
bounds/work limits, same-point rejection and exact saved-state immutability of
accounts/allocator/clock/random/mode. The source/oracle checks retain all 1,484
independent vectors; that total is not a claim that every P4 subsystem is ported.

Reproduce from the repository root:

```sh
.tools/dotnet/dotnet test tests/FreeTrain.Online.Tests --no-restore -m:1 -p:UseSharedCompilation=false --filter FullyQualifiedName~RailwayTopologyTests
.tools/dotnet/dotnet test FreeTrain.Online.slnx --no-restore -m:1 -p:UseSharedCompilation=false
python3 scripts/railway-fixtures.py --check
python3 scripts/review-railway.py --check
python3 scripts/railway-fixtures.py --source-root /path/to/freetrain-exav-archive --check
python3 scripts/review-railway.py --source-root /path/to/freetrain-exav-archive --check
python3 -m unittest discover -s scripts/tests
python3 scripts/check-project-boundaries.py
git diff --check
```

Fresh verification passes **824/824 full .NET tests**, including **28/28 focused
railway topology tests**, and **161 Python tests with one existing skip**.
Default and explicit pinned-Git railway fixture/review checks pass, as do project
boundaries and whitespace checks. The initial sandboxed .NET attempt compiled
successfully but could not bind its test-host socket; both successful runs used
the allowed local-socket execution context.
Local logs: `tmp/p4-06-targeted.log`, `tmp/p4-06-dotnet.log`,
`tmp/p4-06-python.log`. Client/browser/native suites are not rerun for this
backend-domain slice; no wire/save/UI assets or runtime handlers change. Existing
P2/native/deferred-browser, full-city capacity and historical-import gates remain.
