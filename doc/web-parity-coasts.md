# P3-09 islands and directional coasts — 29 October 2026

Game → New world → Generated relief now offers Inland, Island and independent
North/East/South/West coast selections, global sea level and target sea coverage.
English/Japanese controls, exact surface-water overview, realized coverage/land
cell/sea-component counts and correct stored-view compass ship together.
Preview remains detached; Owner confirmation installs the exact retained Save7.

This is **WORLDGEN-COAST-1**, a new procedural addition under the reserved
`terrain-generation-1` profile. ExAV supplies the shared terrain geometry and
normalization rules, not this coast generator. The pinned source contract,
77 source fixtures and existing P2/native/browser deferrals retain their scope.
See [the terrain contract](web-parity-terrain-contract.md),
[seeded relief](web-parity-seeded-relief.md) and
[capacity admission](web-parity-map-capacity.md). Save7/protocol5, the canonical
terrain profile, prepared content and gameplay random-bank version remain current.

## Topology and coverage contract

Directions use canonical XYZ, N=(0,−1), E=(1,0), S=(0,1), W=(−1,0).
They do not label the four H/V storage edges. WORLDGEN-D001 partitions boundary
storage cells by normalized XY displacement from the footprint bounds, using
integer cross products and Y-favored ties. The overview explicitly labels its
stored H/V view: N↖, E↗, S↘, W↙. A true north-up/isometric preview remains P3-12.

A **sea cell** has at least one actual quarter corner strictly below four times
the global water level. This means a positive-area submerged patch, rather than
merely a low buried cell base. A cell whose four corners meet/exceed the plane
is dry. Coverage counts these sea cells divided by all stored cells; it is a
cell-coverage approximation, not integrated water-polygon area. The acceptance
tolerance is **±5 percentage points**, measured after normalization and repair.
Mixed shoreline cells may contain both exposed and submerged surface.

Connectivity uses canonical XYZ cell faces. Sea traversal additionally requires
at least one endpoint of the shared surface edge below the water plane; two wet
cells separated by an entirely dry shared edge are different sea components.
Diagonal/corner contact never supplies a connection. The connected main landmass
is measured on fully dry cells; mixed shoreline fragments are not counted as
additional main landmasses or promised buildable starting areas.

- Inland preserves P3-08 dry relief, with no sea.
- Every accepted generated coast has exactly one nonempty fully dry landmass.
  Each sea component reaches a requested boundary sector. Enclosed sea pockets
  are not accepted as coasts; lakes remain P3-10.
- Island additionally requires every stored boundary cell wet and exactly one
  surrounding sea component. Its side flags must be empty.
- Selected sides require a sea contact in every selected sector and fully dry
  cells on all unselected boundary sectors. Sea components can be separate:
  opposite coasts ordinarily form two coastal seas around one connected landmass.
  All 15 nonempty side masks are supported, subject to measured acceptance.
- Coverage5–75, H/V16–2048, at most1,700,000 stored cells, Z8–64 and water1…Z−4
  share the current generated admission. Small islands at low coverage can fail
  because their rim alone exceeds the allowed fraction. Settings are never
  silently clamped or replaced with a different coast layout.

## Deterministic shaping, normalization and diagnostics

[`CoastTerrain.cs`](../src/FreeTrain.Online.Simulation/CoastTerrain.cs) freezes
these integer operations after the independent P3-08 relief samples:

1. Seed wet boundary cells from selected WORLDGEN-D001 sectors. Protect all
   unselected boundary cells. Compute canonical face-distance from sea seeds,
   excluding protected cells. Compute distance from all protected dry boundary
   cells and the central stored cell; the center alone anchors an island.
2. Start with `s = floor(seaDistance×65536/(seaDistance+dryDistance))`.
   Sample three octaves of cubic-smoothed value noise in canonical XY, with
   wavelengths `max(4,min(H,V)/3)`, half and quarter (integer floors, minima2/1)
   and weights4:2:1. Center their weighted mean at zero by subtracting32768,
   giving `f`. Rank by `s + trunc(f×s×(65536−s)/65536²)`, then a bounded8-bit
   stateless UInt32 hash and finally row-major index. The noise forms broad bays
   and smaller headlands instead of only breaking distance ties. Displacement
   fades to zero at sea/dry anchors and scales with map size; all arithmetic is
   integer/fixed point. The named `coast` stage seed and attempt number provide
   independent randomness; relief samples and gameplay streams are not consumed.
3. Carve the first `floor(cells×(targetPercent+attempt)/100)` cells to water−1.
   Keep the original relief elsewhere, then apply shared source lowering and
   quarter texturing. Derive wet corners from the resulting canonical surface.
4. Flood sea across actual wet faces from the boundary. Fill any enclosed wet
   components to water+1, normalize once more, then recheck every predicate.
   Filling is a bounded repair, not a new lake or an arbitrary terrain bypass.
5. At most four attempts run sequentially. Each uses its own coast-stage seed
   and at most one enclosed-sea repair. All generation passes share one work
   meter; retries never reset it. Array sizes are bounded by stored cells and
   the fixed-width key sort is charged before execution. Cancellation is checked
   in bounded batches, before/after sorting and before publication. There is no
   H×V×Z buffer, live random-bank handle or unbounded routing/repair loop.

If the bounded generator cannot satisfy the request, HTTP400 returns
`geographyImpossible` plus a concrete constraint (`coast-coverage`,
`coast-land-connectivity`, `coast-enclosed-sea`, `coast-sea-connectivity`,
`coast-boundary`, `coast-island-rim` or `coast-sector`). This reports failed
representability in this generator, not a mathematical proof about every possible
algorithm. EN/JA feedback distinguishes coverage, boundary and connectivity
failures and suggests actionable changes. Failure releases the worker/candidate
and preserves the current world. Unsupported lakes/rivers/vegetation/starting-area
requests still reject; their parameters are not silently ignored.

The existing shared worker leases, 30-second deadline, four-job/global4GiB
reservation, 120-second candidate expiry, Owner/connection/epoch/revision/content
fences and no-replay confirmation remain. Geometry, provenance and RNG persist as
actual Save7 state; reopening never reruns generation. Candidate preparation
computes metrics and a one-bit-per-cell surface mask on the detached worker.
The HTTP mask is row-major, least-significant bit first, base64 encoded. Preview
colors use it rather than incorrectly coloring every buried low base as water.
The full candidate terrain and hash still govern exact confirmation.

## Independent fixtures and large-map evidence

[`coast-fixtures.json`](parity/coast-fixtures.json) freezes21 independent Python
cases: all15 side masks, island, odd/minimum dimensions, signed seed extremes,
low/high water, coverage extremes and explicit impossible small-island failure.
The Python evaluator uses canonical XY dictionaries, shortest-path min-plus
normalization and actual shared-edge surface floods. .NET compares literal ground
and packed-corner SHA256, component counts, coverage and sector contacts, alongside
an independently constructed XYZ oracle over48 seeded coast combinations.
A flat-relief regression additionally checks replay and visible sea-mask changes
between seeds for island, north, adjacent and opposite coasts at128×96. This
isolates shoreline variation from relief and request-provenance differences.
A literal four-cell ridge regression proves wet neighbors separated by a dry edge
remain two seas. Existing11 relief literal hashes remain unchanged. Both fixture
regeneration checks now run in `check-web-prototype.sh`.

### Original integration measurements

After the fractal adjustment, all535 .NET tests pass, including the existing
coverage/water-level, connectivity, replay, save/reopen and HTTP checks. Both
independent fixture checks pass (21 coast and11 unchanged relief cases).
All seven1500×1090×45 CLI cases generate, reopen byte-exactly and pass the
independent packed-save wet-face oracle. Generation takes4.82–6.89s; realized
water coverage is39.9999–40% except opposite coasts at38.915%, within tolerance.
The256×192 before/after surface-mask comparisons are under
`tmp/p3-09/organic/coast-comparison.svg` (before left, after right).

The measurements below and the browser/Linux integration evidence later in this
report predate the fractal shoreline adjustment. They remain historical evidence
for the surrounding P3-09 workflows. Current shaping validation and large-map
measurements are recorded in `fractalShaping` in the machine-readable review;
browser/Linux capacity measurements were not repeated for this adjustment.

[`check-coast-topology.py`](../scripts/check-coast-topology.py) independently
decodes actual packed Save7 ground/corner bytes and floods in canonical XYZ. It
checks all seven1500×1090×45 fixtures: island, N, E, S, W, N+E and N+S. These use
Int32-minimum seed, mountains100/scale4/smoothness0 and water4. All have one
connected fully dry landmass and only boundary-connected sea components. The
opposite case has two seas; the others have one. Every save reopens byte-exactly.

| Coast (target40%) | Actual sea | Seas | Generate (s) | Save bytes | Packet bytes | CLI peak RSS (GiB) |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| island | 40.0000% | 1 | 5.243 | 54,400,877 | 54,394,535 | 1.671 |
| north | 40.0000% | 1 | 4.641 | 54,057,165 | 54,050,823 | 1.673 |
| east | 39.9998% | 1 | 4.503 | 54,029,929 | 54,023,587 | 1.666 |
| south | 40.0000% | 1 | 4.615 | 54,046,501 | 54,040,159 | 1.673 |
| west | 39.9999% | 1 | 4.589 | 54,032,453 | 54,026,111 | 1.665 |
| adjacent | 38.6653% | 1 | 6.803 | 53,265,505 | 53,259,163 | 1.657 |
| opposite | 36.3761% | 2 | 6.351 | 52,679,213 | 52,672,871 | 1.643 |

Measurements use fresh CLI processes on the existing64GiB M1 Max environment,
macOS26.7.1/Arm64, .NET10.0.13/SDK10.0.402. Generation time, full build/save/reopen
process RSS, managed retention and browser aggregate RSS are distinct measures.
They are terrain-consumer evidence, not full-city simulation/hardware acceptance.
The new browser capacity case enforces the existing30-second preview,60-second
ready,33.4ms pan-paint p95 and6GiB server/browser RSS budgets, while checking exact
preview/commit/save/rejoin/checkpoint restart, picking and canvas reuse.

Final full-size island browser preview took7.657s; preview through confirmed ready took19.139s. Warm pan paint p95 was8.8ms. Observed aggregate server/browser RSS peaks were2.343/1.624GiB; retained page JS after GC was223.7MiB. All budgets pass. The final Linux/Arm64 image is `08ff25a5d78bd7eb673ebc5ff05e52e524463fb2eb31de4fa0536cf3773f85a0`; full-size island and opposite-coast detached preview, exact chunked publication and save/reopen pass. Island uses54,400,877 Save bytes and1107 chunks; opposite uses52,679,213 bytes and1072 chunks. Both checkpoints survive exact restart. After the final workflows, container cgroup current/peak memory was3.647/3.677GiB (whole container, not per-job peak). No Skia artifacts are present; only checkpoint/writer-lock files persist under `/data`.

## Verification, reproduction and remaining gates

The original slice review, based on `5a10aeaf2d3a0124b7ddbe451ed58cac1d7a60c3`, recorded534 passing .NET tests, including21 coast cases and relief/coast HTTP two-client/checkpoint cases. The fractal adjustment raises the current passing total to535, as detailed above. Original integration results: client tests pass129; Python runs142 (141 pass, one existing skip). The full ordinary Chromium suite passes86/86 before the final shared-wet-edge tightening; all14 terrain cases and the large-island capacity case pass again after that tightening. Optional source and prepared modes each pass4/4 before that tightening. Original Release/frontend and Linux builds, generated/content/locale checks, both independent fixture checks, seven packed-save wet-edge oracles, pinned40-file terrain review, decoder/source-license checks, project boundaries and whitespace pass. The [machine-readable review](parity/coast-review.json) distinguishes original integration measurements from current fractal-shaping verification.

The initial HTTP failure test omitted the required connected WebSocket and was
corrected to exercise the authenticated Owner lifecycle. The first large browser
restart fixture omitted incarnation-local reauthentication; it was corrected to
use the existing access gate. Stricter independent shared-edge verification then
identified enclosed water in the earlier large opposite-coast candidate; final
repair/verification uses actual wet edges. No coverage tolerance, terrain hash or
connectivity expectation was relaxed to accept a failed candidate.

Local logs, generated saves and measurements are under `tmp/p3-09/`; small EN/JA
preview/live screenshots and the final full-size live screenshot were visually
inspected. The narrow form scrolls internally and its controls stay reachable.
The capacity live frame is a zoomed mountain viewport; its complete island topology
is established by actual-state oracles and the overview, not that single frame.

Reproduce with `python3 scripts/coast-fixtures.py --check`, the .NET suite and
`deno ... playwright test --project=canonical terrain.spec.ts`. Generate large
fixtures with `TerrainTransportProbe --capacity 1500 1090 45 coast-island DIR`
(and coast-north/east/south/west/adjacent/opposite), then run the independent
`check-coast-topology.py` on their saved worlds. The capacity browser test is
selected by `--config playwright.capacity.config.ts --grep 'minimum parity extent coast'`;
set `FREETRAIN_ONLINE_PROFILE_EVIDENCE` to a fresh directory.
`check-map-capacity.py --generate-coast` verifies packaged preview/confirmation,
exact chunked publication/save/reopen; `--restart` checks the retained checkpoint.

**P3-09 is complete at slice scope. Next: P3-10 lakes and rivers.** Forests,
starting areas, generation UX completion, authoring/editing and P3-17 remain open.
Later consumers must extend capacity evidence, with final capacity acceptance
before P3-17. Existing P2 crop/fidelity/optional-source repairs, original-runtime,
native/device/deferred-browser, full-city/import and overall P3 gates retain their
actual status. BGM remains deferred by owner policy.
