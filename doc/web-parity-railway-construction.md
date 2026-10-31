# P4-07 terrain legality, elevated track and automatic piers — 31 October 2026

P4-07 delivers detached normal-rail construction over the
[P4-06 topology drafts](web-parity-railway-topology.md).
[NormalRailwayConstruction](../src/FreeTrain.Online.Simulation/NormalRailwayConstruction.cs)
validates actual terrain and occupancy, quotes the complete operation and returns
geometry/allocator/accounts as one immutable result.
[RailwaySupports](../src/FreeTrain.Online.Simulation/RailwaySupports.cs) supplies
source support election and owner-agnostic teardown. **Next: P4-08 live rail
rendering and picking.** Browser controls, commands, localization of these domain
reasons and two-client construction remain P4-09. No live tool is enabled here;
Save8/protocol6, bundled content and the terrain new-world profile are unchanged.

Authority remains stable ExAV 2.1.1.12/V2, archive Git
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, `trunk/`. Implementation was checked
against pinned `SingleRailRoad.cs`, `BridgePierVoxel.cs`, `Traffic.cs`,
`RailRoadController.cs` and `Disambiguators.cs`, with the existing
[source contract](web-parity-railway-contract.md),
[hashed review](parity/railway-review.json) and
[independent fixtures](web-parity-railway-fixtures.md).
This is source-derived verification, not an original-runtime or native capture.

## Terrain, starts and costs

The first point must contain rail or have `Z <= actual local ground`; a road,
pole or vacant elevated cell alone is not a usable start. Both endpoints and
every inclusive sign-vector route cell must be inside stored H/V/Z bounds.
The second point retains the first Z; noncoplanar input rejects. Z0 and the last
stored level are legal when their actual occupancy/water permits. The separate
elevation-dependent train border does not shrink construction storage. Height
cuts and generation provenance never determine legality or prices.

For each build cell, source rejects
`ground <= Z && Z <= water && ground < water`, including the water plane itself.
A vacant buried cell is legal even below the global water plane. Mountain
occupancy, including flat transparent Mountain/forest, is nonreclaimable. An
occupied terrace backing also blocks buried rail. Clear terrace tops, cuts,
vacant elevated cells and occupied below-ground support columns are evaluated
independently. No corners are flattened or terrain regenerated for construction.

A registered silently reclaimable occupant is removed as a whole entity and
reported with its spatial identity and terrain payload. The current terrain
profile supplies decorative Creek reclamation at value zero; forests are not
silently reclaimable. Pricing retains the per-visited-cell entity value rather
than deduplicating source charges. Multi-cell valued P7 entities are not currently
registered by the mixed profile; their removal handlers remain future work, while
the independent P4-04 booking vectors retain their per-cell value expectations.

Build charges every inclusive cell, including unchanged endpoints:
`6,000,000 * M(Z-ground) + reclaimedValue`, where negative height uses `-2*height`
and nonnegative height uses `height+1`. Removal counts rails without cars,
including subtype/edge no-ops, at
`2,000,000 * (abs(endZ-water)+1)` each. Empty removal preserves the source
zero-valued spend call. Support election and teardown add no separate charge.
Construction mode grants no price exemption.

## Piers and identity preservation

Election runs only for newly created rails, including rail added to a road-only
TrafficVoxel. It scans **every** voxel Z0 through railZ−1 first; any occupant,
even buried, transparent or silently reclaimable, suppresses all piers in that
column without rejecting the rail. A clear column gets piers from local ground
through railZ−1. A cell at the rail height is not part of the obstruction scan.
Normal rail selects the exact default pier binding. The shared helper accepts
explicit registered top/body bindings and stores each selected definition/sprite;
independent style fixtures verify different top/body and slopeNE selection.
Actual slope/bridge style registration and workflows remain P4-10/12, with no
new renderer or bundled-art claim here.

Normal removal calls teardown for **every** visited column, including empty
cells, wrong-edge/special/slope no-ops and car-protected rails. GEO-004 removes
every pier strictly below the selected Z regardless of its supported owner.
Preview lists the exact removed voxel, pier owner ID and supported owner ID.
Piers at/above the selection, other voxel types, foreign supported rails and
unvisited columns retain their exact identities. No ownership approval gate or
implicit full-ramp cleanup is introduced.

Unchanged rails, terrain entities/corners/backups, roads, poles and support
identities are preserved. Changed TrafficVoxel owners keep their ID and increment
their revision; empty owners are removed; fresh traffic/piers receive monotonically
allocated IDs. Reclamation alone advances terrain revision. No terrain revision
is manufactured for ordinary rail/pier changes.

Source Traffic callbacks remove a crossing accessory when its rail/road geometry
no longer qualifies and create one for an eligible bare accessory slot. Losses
are explicit; roads and ordinary pole accessories remain. Missing/ambiguous exact
crossing handlers reject rather than silently skipping the callback. A platform
reference whose rail would disappear rejects with `platformDependency`: P6 owns
the portable platform callback and P4-20 the full cross-tool matrix. This boundary
does not invent station deletion or leave an invalid component graph.

## Preview and publication contract

`PreviewBuild` / `PreviewRemoval` return a read-only construction plan with source
topology, stable reason, exact booking, reclaimed whole entities, elected piers,
removed piers and crossing losses. Candidate IDs are provisional, never reserved;
the source world, allocator, accounts, clock, random streams and mode do not change.
Rejection returns no partial booking, reclamation or support losses.

`Apply` requires the **exact source-world object**, recomputes the plan and validates
the complete graph before calling `MixedConstructionState.Replace`. Geometry,
allocator and ordered booking either all return together or none return. Equal
numeric revisions from a different world are stale; a successful plan cannot be
replayed against its successor. Overflow, unavailable exact bindings and capacity
failures preserve the original state. The current durable bound is **8,192 total
railway voxels including piers**, with **8,192 account transactions**; tests bind
these domain constants to protocol limits. This is admission, not measured P4
capacity acceptance. Session role/epoch/content/FIFO/receipt fences and network
admission remain mandatory for the P4-09 command consumer.

Stable construction reasons include the existing topology reasons, plus
`unusableStart`, `underwater`, `obstacle`, `unavailableReclamationHandler`,
`platformDependency`, `unavailableCrossingBinding`, `unavailablePierBinding`,
`railwayCapacityLimit` and `constructionOverflow`. Apply also enforces
`staleRevision` and `accountCapacityLimit`; booking validation can reject invalid
time/context or arithmetic without publishing geometry.

Testing exposed recursive generated `Heading.ToString()` output through its
computed heading properties. An explicit bounded formatter now permits railway
plan diagnostics without overflowing the process stack; its regression is included.

## Verification

[RailwayConstructionTests](../tests/FreeTrain.Online.Tests/RailwayConstructionTests.cs)
adds **41 focused cases**. Literal price expectations cover ground, elevated,
buried and below-water buried routes, charged unchanged endpoints, inclusive water
rejections, empty/no-op/car removal and eight headings. Five independent support
fixtures directly verify default, top/body, slopeNE, at-ground and buried election.
Additional canonical graphs verify below-ground transparent obstruction, foreign
pier teardown beneath no-op/car/empty cells, retained high/unvisited piers, real
P3 terrain/corners/terrace/forest state, slope fillers and whole-ramp preservation,
creek loss, road/pole/crossing callbacks, platform rejection, exact-source/replay
fences, numeric overflow, voxel/account budgets and exact save/reopen continuation.

Reproduce from the repository root:

```sh
.tools/dotnet/dotnet test tests/FreeTrain.Online.Tests --no-restore -m:1 -p:UseSharedCompilation=false --filter FullyQualifiedName~RailwayConstructionTests
.tools/dotnet/dotnet test FreeTrain.Online.slnx --no-restore -m:1 -p:UseSharedCompilation=false
python3 scripts/railway-fixtures.py --check
python3 scripts/review-railway.py --check
python3 scripts/railway-fixtures.py --source-root /path/to/freetrain-exav-archive --check
python3 scripts/review-railway.py --source-root /path/to/freetrain-exav-archive --check
python3 -m unittest discover -s scripts/tests
python3 scripts/check-project-boundaries.py
git diff --check
```

Fresh verification passes **865/865 full .NET tests**, including **41/41 focused
construction cases**, and **161 Python tests with one existing skip**. Default
and explicit pinned-Git fixture/review checks, project boundaries and whitespace
checks pass.
Local logs: `tmp/p4-07-targeted.log`, `tmp/p4-07-dotnet.log`,
`tmp/p4-07-python.log`. The sandboxed test host could not bind its local socket;
successful .NET execution uses the allowed local-socket context. No browser,
client or native suites are rerun for this domain slice. Existing P2/native,
deferred-browser, optional-suite, city capacity and historical-import gates remain.
