# P4-10 four-position slope domain and teardown — 31 October 2026

P4-10 delivers detached, atomic slope construction and removal through
[SlopeRailwayConstruction](../src/FreeTrain.Online.Simulation/SlopeRailwayConstruction.cs).
P4-11 owns session commands, the distinct Slope task, EN/JA controls and live
browser construction/visual acceptance. Normal-track removal never becomes a
slope remover. Existing Save8/protocol6 state contracts carry all delivered
geometry without a schema change.

Authority is stable ExAV 2.1.1.12/V2, archive Git
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, `trunk/`. The implementation was
checked against pinned `core/world/rail/SlopeRailRoad.cs`, `BridgePierVoxel.cs`,
`RailPattern.cs` and `core/world/Traffic.cs`, the
[railway contract](web-parity-railway-contract.md),
[hashed review](parity/railway-review.json) and
[independent literal fixtures](web-parity-railway-fixtures.md).
This is source-derived verification, without original-runtime/native captures.

## Geometry, artwork and transitions

For cardinal climb `d`, four rail positions are
`base + i*d + (0,0,i/2)` for integer `i=0..3`. Their complementary paired cells
occupy the other level in that column. All eight cells must lie inside stored
H/V/Z bounds and be completely unoccupied. Traffic, invisible fillers, piers,
Mountain/forest, Terrace and silently reclaimable Creek occupancy all obstruct;
this tool never reclaims entities. All stored base levels from zero through
`levels-2` are legal when the complete footprint is available. Source slope
construction has no separate underwater or usable-flat-track-start predicate.

At each horizontal position, **baseZ < actual local ground** selects underground
patterns 44–59, otherwise surface patterns 28–43. Upper rail positions still
compare baseZ, rather than railZ, with local ground. Mixed artwork is legal.
Paired i0/1 cells are invisible fillers; i2/3 cells are support/wall voxels
according to that column's art. Support sprite is `climb.index+i-2`, using the
registered exact support/wall binding. Flags are stored at creation:
`below = !(i<2 && ground>=pairedZ)` and
`above = !(i>=2 && ground>=pairedZ+1)`. Terrain edits never recalculate flags
or change the selected rail/support recipes.

The plan exposes all eight members, four ordered rails, patterns, source
`level`, `zdiff` and `zangle`. `Pattern` and `ZDiff` supply the P5 transition
boundary: levels 0/3 have zero change; level1 changes +1 for travel along climb;
level2 changes -1 for travel opposite climb. Other travel directions return
zero elevation change; topology must separately require a valid rail port.
These vectors do not implement or validate moving trains.

Pier election uses **baseZ in every column**, including i2/3, after validating
the entire ramp. The shared helper scans Z0 through baseZ−1; any obstruction
suppresses the column's piers without rejecting the ramp. Clear columns receive
piers from ground through baseZ−1. N/E use the existing default style; S/W use
the newly registered `system:pier/slopeNE`, pinned core `BridgePier.bmp`
rectangle `[32,0,32,32]`, anchor `[0,16]`, stored sprite selector1. Piers remain
separate spatial owners with the ramp's shared owner as `SupportedOwnerId`.

There are now **88 exact railway bindings** and **813 prepared recipes**.
The generated server/browser catalogs and handler metadata agree. The new
recipe changes the required-pack catalog hash; current canonical Save8 and wire
fixtures are rebound. Historical save fixtures and recorded historical hashes
remain unchanged under the current-format-only development policy.

## Removal and the source-call correction

`Resolve` maps **any of the eight occupied members**, including invisible
fillers and lower support/wall voxels, to the canonical owner/base/climb.
Malformed/partial ramps are rejected by `MixedWorld` before construction code
can inspect them. A selected direction must match the resolved climb.
Both removal paths protect **all four rail car positions** before any change;
platform dependencies reject until P6 supplies their callback. Neither path
implicitly removes a train.

The slope tool clears four rail components and four paired cells. Road/pole
components survive as standalone Traffic owners allocated from the shared
monotonic allocator. An eight-cell slope owner cannot be repurposed into a
partial footprint. The tool calls owner-agnostic pier teardown below **baseZ**
in all four columns. The plan lists every affected pier, including foreign
pier owners and supported-owner IDs. Foreign rails, higher/unvisited piers and
unrelated terrain remain untouched.

**P4-10 corrects an earlier source-review omission.** `SlopeEntity.remove`
excludes `BridgePierVoxel` from its direct cube scan, but its rail branch calls
`TrafficVoxel.remove`, which invokes `teardownBridgeSupport(location,this)`.
Consequently whole-entity removal also removes lower piers, regardless of owner,
through those callbacks at each **railZ**. It removes whole Traffic voxels,
including their road/accessory payloads, and has no slope-tool spend call.
Clearing the last component in the slope tool also invokes Traffic removal;
in a validated eight-cell ramp the extra interval above baseZ contains paired
cells rather than piers, so this callback adds no piers to its explicit loss set.
`PreviewEntityRemoval` exposes the original eight members and exact pier losses
for a future trusted entity-removal consumer; it is not wired to normal rail
routes or a browser demolition command. Cars/platforms retain the portable
protection above.

The maintained railway contract/review and state/geometry documentation now
include this transitive call. This supersedes the earlier “leaves elected
piers” rationale in historical P0 geometry-extraction evidence; that historical
extraction and its hashes are preserved. The P4-02 fixture binding to the
maintained review is refreshed, with all **1,484 literal cases unchanged**.
Historical supported-owner references remain legal canonical state; their
validity does not establish that this source callback leaves lower piers.
No new ownership approval or cleanup policy is introduced: D006 applies at
these existing source teardown call sites.

## Atomic preview, booking and P4-11 handoff

Build books once: `40,000,000*M(baseZ-water)`, with `M(h)=-2*h` when negative,
otherwise `h+1`. Slope-tool removal books once:
`8,000,000*(abs(baseZ+1-water)+1)`. Local ground and construction mode never
replace the water plane in pricing. Piers add no separate booking. Whole-entity
removal has no booking; its caller owns any separately applicable demolition
policy.

Read-only previews expose legality/reason, resolved base/climb/owner, all members,
rail transitions, exact booking and elected/removed piers. Candidate IDs are
provisional. `Apply` requires the exact source-world object, recomputes the plan,
validates the complete graph and publishes terrain/railway/allocator/accounts
as one immutable result. Equal numeric revisions from another world, replay,
overflow, unavailable/ambiguous handlers and capacity/account failures preserve
the predecessor. The established 8,192 railway-voxel and 8,192 transaction
admission bounds still apply; this is not measured P4 capacity acceptance.

P4-11 must wrap these APIs with the existing participant/role/epoch/content/FIFO,
receipt and stale-preview safeguards. Its panel must translate the stable
reasons `nonCardinalSlope`, `outside`, `obstacle`, `notSlope`,
`slopeDirectionMismatch`, `occupiedCar`, `platformDependency`,
`unavailablePierBinding`, `unavailableSlopeBinding`, `constructionOverflow`,
`railwayCapacityLimit`, `staleRevision` and `accountCapacityLimit` in EN/JA.
It must show the complete footprint and pier losses, keep shared time running,
and preserve the existing persistent canvas/camera and late-response fences.

## Verification

[SlopeRailwayConstructionTests](../tests/FreeTrain.Online.Tests/SlopeRailwayConstructionTests.cs)
contains 15 focused tests, with loops covering all 26 independent slope build
fixtures, all 22 removal fixtures, all 32 slope patterns/eight travel headings,
every cardinal direction/base level and all eight removal targets. Additional
checks cover stored flags after terrain changes, all four occupied-car positions,
normal-tool charged no-ops, entity callbacks, foreign supports, suppressed
below-ground election, retained road/pole components, platform rejection,
malformed graphs, bounds, exact/ambiguous bindings, stale/replay, allocator
arithmetic, voxel/account admission and exact saved continuation.
The client renderer regression validates the added pier crop/anchor and canonical
sprite selector with immutable saved state.

Reproduction from the repository root:

```sh
.tools/dotnet/dotnet test tests/FreeTrain.Online.Tests --no-restore -m:1 -p:UseSharedCompilation=false --filter FullyQualifiedName~SlopeRailwayConstructionTests
.tools/dotnet/dotnet test FreeTrain.Online.slnx --no-restore -m:1 -p:UseSharedCompilation=false
deno task --cwd web build
deno task --cwd web test
python3 -m unittest discover -s scripts/tests
python3 scripts/railway-fixtures.py --check
python3 scripts/review-railway.py --check
python3 scripts/railway-fixtures.py --source-root /path/to/freetrain-exav-archive --check
python3 scripts/review-railway.py --source-root /path/to/freetrain-exav-archive --check
python3 scripts/check-project-boundaries.py
git diff --check
```

Fresh verification passes **888/888 full .NET tests**, including **15/15 focused
slope tests**, **178/178 client tests**, and **161 Python tests with one existing
skip**. Production build has zero Svelte errors/warnings. Generated contracts,
content/locales, default and explicit pinned-source preparation/fixture/review,
project boundaries and whitespace checks pass. The first full .NET run found
one stale 812-recipe assertion; the added recipe makes 813, and the corrected
full rerun passes.

Local logs: `tmp/p4-10-targeted.log`, `tmp/p4-10-dotnet-final.log`,
`tmp/p4-10-client.log`, `tmp/p4-10-web-build.log`, `tmp/p4-10-python.log`,
`tmp/p4-10-source.log` and `tmp/p4-10-pinned-content.log`.
The .NET test host requires local socket access.
Browser slope workflows/visual acceptance remain P4-11; no new native/human,
other-engine, hosted-deployment, moving-train or measured capacity credit is
claimed here. Full cross-tool demolition/terrain protection remains P4-20;
optional-suite and later-phase gates retain their actual status.
