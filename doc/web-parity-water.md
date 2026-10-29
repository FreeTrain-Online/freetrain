# P3-10 lakes and rivers — 29 October 2026

## Fractal shaping follow-up — 29 October 2026

Lakes now have rounded, gently irregular shores rather than stored rectangular
outlines. Rivers use a smoothly displaced fractal guide for broad bends rather
than fixed four-cell cost patches. Both reuse the shoreline's three-octave integer
noise with separate lake/river stage seeds. The existing meander control scales
river displacement; zero meander keeps shortest-path routing. Exact lake counts,
area tolerance, wet-edge connectivity, submerged core width, dry banks and outlet
checks remain unchanged. Existing saves restore their actual geometry.

[Before/after comparison](../tmp/p3-10/fractal/comparison.png): before on the left,
after on the right; lake-only, coast/lake/river and edge-channel scenes from top to
bottom. These are actual quarter-corner water masks from the same256×192×45 seed
and settings, not color changes or request-hash comparisons. The comparison was
visually inspected. The new flat-terrain regressions additionally remove translation
before comparing seeds, so moving an identical shape cannot satisfy the checks.

All **559 .NET tests**, **20 independent water fixtures**, **21 unchanged coast
fixtures**, **11 unchanged relief fixtures**, **16 terrain Chromium tests** and the
**full-size water browser capacity case** pass. Three1500×1090×45 CLI scenes
(coastal, edge-to-edge and lake-fed inland) reopen byte-exactly and pass independent
packed-save wet-face topology floods. Generation takes9.688–14.468s; process peak
RSS is1.779–2.623GiB. Final full-size browser preview takes16.249s, confirmed ready
27.732s and warm pan paint p95 is5.4ms; exact publication, reconnect, save/reopen,
picking, canvas retention and checkpoint restart pass within the existing budgets.

The original full ordinary browser/client/Python/optional/Linux results below
predate this refinement; current evidence and source hashes are recorded separately
in `fractalShaping` in [the review](parity/water-review.json). No new Linux or
native/deferred-browser acceptance is claimed for this shaping change. Overall P3
and later-slice qualifications retain their scope.

## Delivered slice


Game → New world → Generated relief supports enclosed lakes and river channels at
ExAV's single global water level. Count, approximate lake area, channel width,
meander and outlet controls ship in English and Japanese, with actual surface-water
preview and metrics. The Owner confirms the exact retained Save7 candidate;
reopening restores actual geometry without running the generator.

This is **WORLDGEN-WATER-1**, a requested procedural addition under the reserved
`terrain-generation-1` profile. ExAV supplies the terrain representation and shared
normalization, not whole-map hydrology. Save7, protocol5, terrain-content-1 and the
world-owned random-bank version remain current. Existing dry relief/coast literals
are unchanged. See [coasts](web-parity-coasts.md),
[the source contract](web-parity-terrain-contract.md) and
[capacity admission](web-parity-map-capacity.md).

## Controls and realized topology

- Lakes: count0–8; each lake targets1–20% of the stored cells. Every final lake
  component must remain enclosed behind dry cells, including a separating rim
  between lakes and any coastal sea. Area tolerance is the larger of one
  percentage point of the map and25% of the requested lake area (integer floors).
  Area is verified after lake normalization, before tributaries are carved.
- Rivers: count0–4; minimum fully submerged core width1–4 canonical cells;
  meander0–100 changes deterministic route costs. An additional low apron supports
  the core's quarter corners, so visible shoreline width can exceed core width.
  Every core cell's four corners must be below the plane; successive centerline
  cells must share an actual wet face and reach the same outlet component.
- Automatic outlets prefer coastal sea on coastal maps. Inland automatic rivers
  feed the first planned lake. A named N/E/S/W outlet must match a selected coast
  sector (or an island); its mouth lies in that canonical sector and connects to
  boundary-connected sea. Other lakes remain protected from accidental merging.
- Explicit inland **edge-to-edge** mode requires lakes off. One seeded opposing
  N/S or E/W orientation is used for all channels, preventing forced crossings.
  Channels remain separate wet components, each reaching opposing boundary sectors.
  A wide endpoint apron can also touch an adjacent sector; edge mode does not
  reserve a coastal dry-boundary sector.
- Dry banks are checked on both sides of the centerline tangent, including bends,
  within a bounded local square. The mouth/edge transition zone is exempt: its
  distance from preexisting water or the map boundary is at most twice(width+6)
  canonical face steps. This is a constant-plane channel, not downhill flow.
- The pre-carve coast still meets P3-09's coverage and connected-mainland contract.
  Rivers can divide that landmass; final dry-component counts are reported rather
  than requiring the old one-component predicate after channel carving. Protected
  coast sectors stay dry; an island retains its wet outer rim.

The existing structural space bound reserves at most75% for requested features;
final representability is stricter. Small maps, large lake counts/areas, wide or
numerous rivers and conflicting shorelines can fail explicitly. Settings are not
silently reduced. Sea coverage is the **pre-lake/river** coast target; final preview
coverage includes all actual wet cells. `SeaCells`/`SeaComponents` count open,
boundary-connected water (including edge channels); `LakeCells`/`LakeComponents`
count enclosed water, including connected inland tributaries. `RiverCount` reports
the requested channels whose transient routing witnesses passed generation checks.
The overview continues to use exact actual quarter-corner wetness, not buried bases.

## Bounded deterministic carving and verification

[`WaterTerrain.cs`](../src/FreeTrain.Online.Simulation/WaterTerrain.cs) runs after
independently normalized and validated dry relief/coasts:

1. Flood distance from coastal water and all stored boundary cells using canonical
   face neighbors. Pick the greatest-clearance lake center, resolving ties with an
   isolated lake-stage UInt32 hash. Grow a face-connected frontier ranked by
   squared Euclidean radius multiplied by `65536 + f`, where `f` is centered
   three-octave cubic-smoothed value noise with weights4:2:1. Its main wavelength
   is `max(4, ceil(sqrt(requestedArea))/3)` (integer floor of the division), then
   half/quarter with minima2/1. This replaces stored rectangular/Manhattan outlines
   with rounded, gently irregular shores. Keep a four-step rim from reserved cells.
   Subtract the deterministic integer perimeter allowance from the target core,
   then carve its one-cell canonical square apron to water−1. Reserve each basin
   before placing the next; final count/area/rim checks still govern acceptance.
2. Normalize once using the shared source lowering/corner conversion. Flood actual
   shared wet edges and validate lake counts, area and rims before routing.
3. Identify actual sea/lake outlet goals. Keep other water, protected map edges and
   prior channels behind bounded buffers. Flood **reachable** distances from goals;
   edge source gates are terminal fringe nodes, so boundary walking cannot invent
   a route. Pick a reachable dry source by greatest goal distance and seeded ties.
4. Find a legal zero-meander shortest route to select the outlet, then gently warp
   its source/outlet guide with the same three-octave noise. The main wavelength is
   `max(6,min(H,V)/5)` and displacement amplitude `max(1,min(H,V)×meander/400)`;
   the signed noise scales that amplitude and a parabolic fade pins both endpoints.
   Cache at most4097 guide samples and interpolate their displacement by projected
   progress. Final deterministic Dijkstra costs are100 plus24 per cell of distance
   from that guide, capped at800. Protected rims/channels still constrain routing.
   Zero meander retains the unwarped shortest-path behavior. Queue ties use
   row-major index. Route width is a canonical square core with one low apron;
   other channels reserve a wider corridor. Positive costs and fixed neighbor
   count bound queue relaxations. A shared lake remains a legal outlet for later
   channels even where a previous mouth reserved part of it; that reserved mouth
   is blocked without treating the target lake as unrelated water.
5. Normalize the complete terrain again. Validate actual core width, wet faces,
   outlet connectivity, two-sided banks, every enclosed lake rim and preserved
   coast boundaries. Independent source corner rules remain authoritative.

Lake and river streams are separate named stage seeds. Four sequential water-stage
attempts share the original job's work meter and cancellation token; failures restart
from the accepted pre-water coast. Coast retries are separate, not nested water
retry loops. Arrays/queues are bounded by stored cells, with no H×V×Z buffer.
The worker deadline, admission/reservation limits,120-second candidate expiry,
Owner/epoch/revision/connection/content fences and no-replay confirmation remain.

HTTP400 `geographyImpossible` includes an explicit failed constraint: `lake-space`,
`lake-area`, `lake-rim`, `river-space`, `river-route`, `river-outlet`,
`river-connectivity`, `river-width`, `river-banks`, `river-boundary` or
`river-separation`. Localized feedback suggests reducing features or changing seed,
outlet or map size. Failure releases the job and preserves the current world.
This describes bounded generator failure, not a proof that every algorithm would
find the request impossible.

Candidate preparation independently remeasures actual water components/boundaries
on its detached worker. Full channel width/path/bank witnesses are validated during
generation; they are transient and are not persisted or regenerated on save load.
Actual terrain plus request provenance is the durable result. Existing save-file
validation continues to validate canonical geometry, not to force edited worlds
back into their original generation recipe.

## Independent fixtures and source creek separation

[`water-fixtures.json`](parity/water-fixtures.json) contains20 independent Python
cases: enclosed/multiple lakes, inland tributaries, islands, each directional
outlet, opposite coasts, widths/meander, signed seed extremes, water/headroom
extremes, eight lakes, four edge channels and explicit tiny-map failures.
The evaluator uses canonical XY dictionaries, min-plus terrain lowering, heap
routing and actual wet-face floods. It freezes ground/corner SHA256 and literal
river centerlines. .NET compares hashes, checks source corner equality and
independently verifies each path and its fully submerged width.

Additional .NET cases exercise eight lakes, four coastal/edge channels and combined
lakes/rivers on128×127×64, Int32-minimum seed, water1 and maximum rough mountains.
HTTP tests check repeated impossible requests release reservations, untouched live
saves, exact two-client publication and checkpoint restart. Browser cases check
EN/JA narrow controls, invalid outlets/ranges, draft invalidation, modal Cancel,
late-response cancellation, exact preview/confirmation/reopen and canvas retention.

Decorative **Creek** entities remain separate source content: they do not carve
terrain or alter water. P3-01's literal endpoint-seed, route and blocked-site/OR-mask
fixtures remain in [terrain-fixtures.json](parity/terrain-fixtures.json), with the
77-fixture source review passing. Existing canonical Creek occupancy, rendering,
picking, save and seasonal/content tests remain in the regressions. Interactive
source creek authoring remains P3-16; this slice does not relabel these carved
channels as decorative creek placement or claim that later tool is delivered.

## Original slice capacity and verification evidence

The numbers in this section predate the fractal shaping follow-up. Current
shaping evidence is recorded above. Original measurements and checks are recorded in
[the machine-readable review](parity/water-review.json). Local logs, generated
saves and screenshots are under `tmp/p3-10/`. The supported generated admission
remains H/V16–2048, at most1,700,000 stored cells, Z8–64 and water1…Z−4;
all existing transfer/save and shared job limits remain in force.

All557 .NET cases pass; client tests pass129; Python runs142 (141 pass, one existing skip). Full ordinary Chromium passes88/88, including16 terrain cases. The water capacity case passes1/1; optional source/prepared modes each pass4/4. Release/frontend and Linux builds, generated/content/locale checks,20 water/21 unchanged coast/11 unchanged relief fixtures, pinned40-file terrain review, decoder/source-license checks, boundaries and whitespace pass.

All three full-size CLI cases use1500×1090×45, Int32-minimum seed, maximum rough mountains, water4 and two width3/meander80 rivers (plus two3% lakes where enabled). Saves reopen byte-exactly and independent packed-state wet-face floods pass.

| Scene | Wet cells | Enclosed bodies | Open bodies | Generate (s) | Save bytes | Process peak RSS (GiB) |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| water-coast | 597,252 | 2 | 1 | 18.210 | 57,816,453 | 1.793 |
| water-edge | 16,253 | 0 | 2 | 11.066 | 87,700,367 | 2.497 |
| water-inland | 117,188 | 2 | 0 | 14.260 | 83,067,891 | 2.624 |

The final browser coastal fixture previews in16.650s and reaches confirmed ready in28.664s. Warm pan paint p95 is9.8ms; sampled server/browser RSS peaks are3.049/1.652GiB. Retained page JS after GC is264.1MiB. The existing30-second preview,60-second ready,33.4ms paint and6GiB server/browser budgets pass, along with exact confirmation/download/reconnect/checkpoint restart and canvas/picking checks.

The final Linux/Arm64 image reproduces both the coastal and edge candidates exactly:1,177/1,785 chunks,57,816,453/87,700,367 Save bytes. Preview takes21.794/16.309s respectively; publication/save/reopen and checkpoint restart pass. Whole-container cgroup current/peak memory after the final restart is1.954/2.035GiB, not an uninterrupted generation-job peak. Only checkpoint state remains in the disposable container data directory.

The stricter routing matrix exposed unreachable later edge-channel sources; final planning floods from outlets through the available corridor before selecting a source. Bank checks use the tangent through bends and both local half-planes. Independent packed-state verification checks opposing contacts per channel: a wide endpoint can touch a third adjacent compass sector without losing its two required opposite contacts. The full-size viewport screenshot shows terrain rendering/picking; the actual-state flood and overview establish whole-map topology.

Reproduce independent fixtures with `python3 scripts/water-fixtures.py --check`.
Run the .NET suite and ordinary Chromium suite; the new form cases are in
`terrain.spec.ts`. Full-size CLI modes are `water-lakes`, `water-inland`,
`water-coast` and `water-edge`:

```sh
dotnet run --project tools/TerrainTransportProbe -c Release -- \
  --capacity 1500 1090 45 water-coast tmp/water-coast
python3 scripts/check-water-topology.py tmp/water-coast/world.json
```

The capacity browser test uses `--config playwright.capacity.config.ts --grep
'minimum parity extent water'` with a fresh `FREETRAIN_ONLINE_PROFILE_EVIDENCE`.
`check-map-capacity.py --generate-world --save FILE` exercises packaged detached
preview, exact chunked publication and reopen; `--restart` verifies checkpoint
continuation. The previous `--generate-coast` alias remains supported.

P3-11 vegetation/starting areas, P3-12 generation UX completion, authoring/editing,
final P3-16a capacity regression and P3-17 remain separate. Existing P2
crop/fidelity/optional-source repair, native/deferred-browser, original-runtime,
full-city/import and overall P3 gates retain their actual status. BGM remains
deferred by owner policy.
