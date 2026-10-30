# P3-17 — terrain acceptance and P4 handoff

30 October 2026. P3 terrain workflows and the requested WORLDGEN addition are
accepted at the implemented browser/portable scope. The evidence below separates
source-derived rules, new authoring/generation behavior and outstanding platform
or later-gameplay gates. P4 railway construction is the next implementation phase.

The owner accepts the current terrain-edit UX latency. The earlier **250 ms
full-size authoritative edit target remains unmet**; its miss is recorded, not
rounded into a performance pass. Continuous paint was requested separately and
uses the same validated commands. Existing native/device, deferred-browser, P2
visual/crop/optional-suite, P10 historical-import and P11 full-city gates remain
open. None is silently rerun or closed by this handoff.

## Delivered behavior and scope

| Scope | Workflow and evidence |
| --- | --- |
| Source terrain, with recorded portable adaptations | [Empty creation](web-parity-empty-world.md), [image/fixed-map imports](web-parity-heightmap.md), [terrain state](web-parity-terrain-state.md), [saves/transport](web-parity-terrain-transport.md), [rendering/picking](web-parity-terrain-rendering.md), [corner strokes](web-parity-corner-terrain.md), [rectangle/repair](web-parity-rectangle-terrain.md), [terraces/cuts](web-parity-terraces.md), [authoring forests/creeks](web-parity-authoring.md). |
| New requested WORLDGEN behavior | [Seeded relief](web-parity-seeded-relief.md), [islands/coasts](web-parity-coasts.md), [lakes/rivers](web-parity-water.md), [forest cover/starting-area reservation](web-parity-forests.md), [generation UX](web-parity-generation-ux.md). These do not add ExAV source-scenario credit. |
| Owner authoring extensions | Direct bare-ground and bounded rectangle terraces; continuous mouse-held paint. These are explicit browser extensions, not claims about original controller gestures. |
| Supported current format | Save 7 / protocol 5, exact actual terrain, realized trees, stable IDs/revisions/allocator, authoring mode, provenance, clock and random streams. Earlier saves remain rejected without conversion. |

The [12 terrain decisions](parity/terrain-review.json) remain authoritative:
stored dimensions/name adaptation (001), global water (002), quarters and explicit
cut/terrace discontinuities (003), revalidated atomic publication (004), detached
creation without test trains (005), persisted mode without bypasses (006), source
heightmap arithmetic (007), forest/creek versus generated ecology (008), source
zero terrain booking versus later owned purchases (009), safe repair and asymmetric
borders (010), terrace/cut state loss (011), and browser gestures/errors (012).
Paint extends 012 by explicit owner direction; ordinary Shift strokes retain
their original movement-only collection and one confirmation.

A cut or restored Mountain above an existing terrace now preserves the terrace's
backing surface. Removing that upper Mountain must not turn its still-present
terrace backing into an invalid orphan. Differential dense-world tests exercise
this combination against full save restoration.

## Integrated topology matrix

[The maintained manifest](parity/terrain-acceptance.json) defines 15
`WORLDGEN-ACCEPT-*` scenes crossed with **64×80×24 and 128×160×24**, water **1 and20**,
and seed **Int32 minimum**: dry plains, maximum-detail mountains, island, N/E/S/W
coasts, adjacent NE and opposite NS coasts, inland lakes, coast/lake/river
combination, sparse and dense single-species forests, mixed six-species forest,
and bare hills. All **60 requests** are generated twice with exact canonical
save equality, then edited through authoritative terrace placement/removal and
saved/reopened exactly. The request remains provenance; reopening never regenerates.

The independent Python packed-state oracles check **120 before/edited saves**:
actual quarter-height wet edges, canonical XY face connectivity, boundary sectors,
sea/lake components, river endpoints and coverage. Forest legality, source scatter
bounds/species and the connected level reservation are independently checked on
the generated natural surfaces. The intentional cut remaining after terrace
removal is checked through wet-edge topology and full graph restoration; it is
not mislabeled as an unedited natural forest fixture.

Repeatable hashes establish determinism; the independent geometric assertions
establish that the resulting maps meet their requests. [The acceptance review](parity/terrain-acceptance-review.json)
records each matrix result, hashes/metrics, translation dispositions and current
implementation/evidence hashes. Earlier small literal relief/coast/water/forest
fixtures remain separate independent arithmetic expectations. Existing tests also
exercise every coast-side combination, contradictory requests, maximum feature
counts, impossible starting areas, bounds/water extremes and bounded failure.

## Continuous terrain paint

Use **Modify terrain → Terrain selection → Paint terrain**, then choose
**Raise terrain** or **Lower terrain**. Hold the left mouse button and drag;
holding still repeats at that corner. Each successful dab changes its shared
corner by one quarter level, following the existing natural-slope constraints.
Ctrl reverses the action. The brush stops changing a point at a source legality,
occupancy, floor or roof limit; move to another corner to continue sculpting.

Dabs use authoritative read-only previews followed by a revalidated `editCorner`
command. There is at most one preview/commit in flight, with a150 ms interval
after completion. Pointer movement replaces the next target, without a queued
trail of commands. A stationary brush retains the selected canonical corner and
reads its current ground. No new server command, protocol/save version, terrain
algorithm, price or role bypass is introduced.

Painting is an immediate shared-world editing gesture, and the EN/JA help states
that forest decoration/ownership can be lost and completed edits remain. Release,
Escape, pointer cancellation/lost capture, leaving the canvas, focus/visibility
loss, panel/tool changes, camera/height-cut changes, epoch/connection/role changes
and ambiguous/rejected outcomes stop further edits. A released pending preview
cannot submit a late command. An already submitted command can finish; it is
never automatically undone or replayed. Reconnecting does not resume painting.
Coordinates retain the explicit preview/confirm alternative for keyboard use.
No paint mode, held button or draft is persisted in preferences or saves.

## Capacity and implementation changes

[The capacity policy](web-parity-map-capacity.md) remains H/V1–2048, at most
1,700,000 cells, Z1–64; generated H/V≥16, Z≥8, water1…Z−4. The concrete parity
extent is **1500×1090×45 stored**. Image input remains8 MiB/4096 per side/
4,194,304 pixels; state/save admission remains256 MiB with separate expansion,
work, worker, transfer and candidate bounds. No limits were raised for this pass.

Bounded source drafts now validate changed columns and all touching corner
neighborhoods against immutable predecessors. The initial frozen spatial/occupant
indexes are shared with persistent edits, without retaining a chain of world
objects. Full constructors and uploaded saves still validate the entire graph.
The session accepts a draft's validation only for its exact predecessor token;
independently constructed replacements retain full revision/identity checks.
Canonical JSON admission uses a conservative metadata/definitions plus exact
base64-length bound, avoiding full save encoding on the command FIFO. Writing
still enforces actual bytes, and differential tests compare the bound with saves.

Transport shares unchanged read records and computes consecutive deltas from
validated changed IDs/cells. Skipped/coalesced predecessors retain the general
comparator. The browser validates changed columns and touching corners through
paged immutable indexes; malformed geometry still fails before publication.
Rendering uses that validated index rather than rebuilding world-sized maps for
every dab.
Creation also retains its already validated immutable domain world instead of
decoding a second copy of its own save beside the existing shared world. Mutable
clock/random/mode headers are copied and the exact registered handler profile is
required. Uploaded saves keep their complete untrusted-input validation. This
addresses the first sweep's7.02/7.36 GB island/dense-forest server RSS failures
without forced production GC or changed memory limits. Client/server differential tests compare incremental results with full
validation/restoration, check predecessor immutability and reject corruption.

Full-size mode/scenery CLI commits measured **0.2–25 ms** in the initial probe,
compared with the prior787–1,106 ms samples. End-to-end64-cell forest commits with
100 ms injected RTT measured **154.8 ms p95 on empty terrain and512.4 ms p95 on
resource-heavy terrain** in the first run. These are different measurements; the
latter retains the explicit250 ms miss accepted by the owner for current editing.
Final capacity artifacts separately record fresh rendering/picking, zoom, generation,
transfer/rejoin, saves/checkpoints, memory and edited-world continuation results.
Paint is paced by acknowledged edits; its throughput depends on world density and
network latency. These terrain scenes do not establish full-city simulation limits.

## P4 terrain and support handoff

Use `TerrainWorld`, not generation provenance, as the authoritative terrain query.
The synthetic reference rail loop remains a separate world profile; terrain worlds
have zero rails. The P3 profile has handlers for terrain/scenery only: adding rail,
slope, pier, platform or other occupancy requires P4's reviewed mixed-world model,
content bindings, versioned persistence/transport and legality rules.

| P4 question | Current authoritative query / exact semantics |
| --- | --- |
| Stored bounds and canonical position | `World.Space.ContainsHorizontal`, `ContainsVoxel`, `ToStorage`, `ToLocation`; upper endpoints exclusive. Stored borders are not the train-visible border. |
| Ground and surface | `World.Cell(h,v)`: integer ground plus Natural/Cut/Terrace. `World.At(p).Relation`: `z<ground` underground, equality ground, greater elevated; outside is separate. |
| Water | `World.At(p).Underwater` means `z<WaterLevel`, independently of ground/occupancy. At the global water level it is false. Decorative Creek changes neither water nor ground. |
| Surface geometry | `World.FineHeight(p,corner)` and `OccupantAt`: NE/SE/SW/NW quarters, preserved all-four source states and explicit cut/terrace discontinuities. Ground equality alone does not establish rail support. |
| Existing occupancy | `World.Spatial.At(p)` supplies the whole-voxel owner and transparency. Transparent and silently reclaimable are independent; transparency never grants overlapping construction. `Reusable` is only the spatial predicate. |
| Terrace support | A Terrace occupies ground−1 with backed-up corners; a Mountain/forest may occupy ground above it. Preserve both identities and obstruction checks when evaluating piers/tunnels/slopes. |
| Train borders | `World.IsTrainBorder(p)` uses the separate source elevation-dependent predicate. Never shrink terrain storage to this visible/train area. |
| Browser spatial inspection | `SessionHost.PreviewSpatial` checks epoch, projection, storage, alignment and inside/outside. It is not a railway legality/support/cost quote. |

`TerrainAcceptanceTests.P4_queries_distinguish_ground_water_elevation_terrace_support_and_outside_without_build_legality`
executes the underwater cut/terrace/forest/creek scene from
`TerrainTransportScenario.Create()`. Its literal expectations include occupied
underground terrace support, occupied sloped ground, vacant elevated space,
underwater vacant ground and outside Z. `TerrainWorld.Empty` supplies a clear flat
baseline; the matrix's plains/mountains/shore/water/forest scenes supply legal
terrain without prebuilt railway state; `TerrainCapacityScenario.Create` supplies
dense terrace/forest occupancy for scale work.

P4 still owns level/sloped/underground/elevated rail construction, foundations and
piers, stored slope ground flags, bridges/tunnels and endpoint rules, occupants and
train guards, construction/removal accounting, authoritative cost/legality preview,
atomic transactions and their UI/continuation. Retain source geometry differences
GEO-004/006 and EXAV-DEP-005/007; P3 queries do not settle those decisions.

## Verification and reproduction

Run from the repository root unless stated otherwise:

```sh
dotnet build FreeTrain.Online.slnx -c Release --no-restore
dotnet test FreeTrain.Online.slnx -c Release --no-build --no-restore
deno task --cwd web test
deno task --cwd web build
python3 -m unittest discover -s scripts/tests
python3 scripts/review-terrain.py --check
dotnet build tools/TerrainTransportProbe -c Release --no-restore
dotnet tools/TerrainTransportProbe/bin/Release/net10.0/TerrainTransportProbe.dll --acceptance doc/parity/terrain-acceptance.json tmp/p3-17/matrix
python3 scripts/check-terrain-acceptance.py tmp/p3-17/matrix
```

From `web`, use the existing `playwright.config.ts`, `playwright.capacity.config.ts`
and `playwright.optional.config.ts` suites with fresh evidence directories.
Capacity tests own disposable servers/saves and explicitly reauthenticate after
restart. Source/prepared optional tests use original synthetic fixtures; they do
not claim repaired canonical non-free-suite compatibility. The pinned Linux
Arm64 build includes the decoder license text in the frontend build context and
verifies distribution plus full-size edited checkpoint continuation.

Final checks: **727 .NET,147 client,142 Python cases** (one existing Python
skip), **120/120 ordinary Chromium**, final **66/66 canonical Chromium**, focused
paint **3/3**, capacity **16/16**, and optional source **4/4** / prepared **4/4**.
The final capacity run observes **1.1 ms** pointer-to-held-paint
feedback at1500×1090×45, repeated confirmed dabs, release with no queued edits and
exact reopening. Full-size64-cell forest feedback at100 ms injected RTT is
**172.7 /506.5 ms p95** on empty/resource-heavy terrain,
so the owner-accepted dense edit miss remains visible.

The initial full capacity sweep passed13/15; island and dense-forest server RSS
exceeded6 GiB. After the creation fix, the complete16-case sequence passes the
unchanged memory/render/clock/rejoin/transfer/checkpoint gates. Island/dense-forest
server peaks are5,889,327,104 /5,509,955,584 bytes. The final ordinary run passes
120/120; its earlier118/120 attempt and corrected keyboard readiness test remain
separate evidence, alongside the final66-case canonical follow-up after the
creation change. No discarded attempt is relabeled as a clean exact-revision run.

Linux Arm64 image `sha256:be8a8a33c6c3d277d848e2187a9377bb7b4587787bf329fbfbeb01d1d7fa8d44` passes238-asset/505-media/four-notice distribution checks and
the622-chunk30,531,590-byte edited resource-heavy save/reopen/checkpoint restart.
Its4-CPU,6-GiB container records a1,451,286,528-byte cgroup memory peak;
no Skia artifacts are present. Test containers and volumes were removed.

Fresh results and artifacts are recorded in the acceptance review. Named hardware
is the existing64 GiB Apple M1 Max/macOS26.7.1 environment, .NET10.0.402/10.0.13,
Playwright1.64.1 Chromium. Linux uses the pinned Deno2.9.7 build; native Deno is2.9.8.
No Windows runtime capture, deferred browser certification, hardware matrix or
historical-game save was executed by this acceptance.
