**P3-16a capacity update — 29 October 2026:** [Current packed state, coherent transfer, admissions and evidence](web-parity-map-capacity.md) supersede the temporary size/byte/working limits and Save 6/protocol 4 statements below. Earlier measurements remain historical slice evidence. Current envelopes are H/V≤2048, at most 1,700,000 stored cells, Z≤64 (generated minimum H/V16,Z8), Save 7/protocol 5 and 256 MiB assembled state. The concrete scale target is 1500×1090×45 stored. Overall P3 and the recorded P2/native/full-game gates remain open.

# P3-08 seeded relief — 29 October 2026

Game → New world → Generated relief adds reproducible dry plains, hills and
mountains, with signed Int32 seed, relief, feature scale and smoothness controls
in English and Japanese. Preview remains detached; Owner confirmation consumes
exact retained Save6 bytes. Actual ground and quarter-corner Mountain entities,
profile and normalized request provenance persist. Loading never regenerates.
Save6, protocol4, terrain-state-1 and gameplay random-bank version1 are unchanged.

This is the new **WORLDGEN-RELIEF-1** procedural addition under the already reserved
`terrain-generation-1` profile. ExAV does not supply this noise algorithm. The
shared natural-surface normalization retains the pinned source lowering and
quarter-texturing rules described in [the heightmap report](web-parity-heightmap.md)
and [terrain source contract](web-parity-terrain-contract.md). It covers the
procedural extension to MAN-M01/O02/A02 and DEV-P09/P17 alongside EXAV-13 terrain;
it does not inflate the ExAV source-fixture denominator.

## Algorithm contract

`Simulation/ReliefTerrain.cs` freezes these operations:

- Root seed is an invariant signed Int32. The existing SHA256-framed `relief`
  stage seed, attempt0, supplies a stateless UInt32 lattice hash. Octaves are
  separated by fixed hash constants. No `Random`, `GameRandom`, live session or
  durable random-bank handle is accepted or consumed.
- Coordinates use doubled stored H with half-cell odd-V staggering. Three
  value-noise octaves use wavelengths `scale`, `max(2,scale/2)` and
  `max(1,scale/4)` cells. Integer division truncates toward zero; hash overflow
  wraps UInt32. Q16 interpolation uses the pinned cubic fade operation order.
- Smoothness controls the detail weight `floor((100−smoothness)×256/100)`.
  Coarse/detail/fine weights are256, detail and detail/2. Mountains transform
  the blend to `65536−abs(2×value−65536)` ridges. Hills retain value noise;
  plains reduce amplitude. This is deterministic layered noise, not erosion.
- Quarter-height baseline is `4×(water+1)`. Available amplitude is
  `4×(Z−1)−baseline`, multiplied by relief0–100 and style strength25/65/100
  for plains/hills/mountains. Integer sampling yields quarter heights; whole
  ground is truncated before shared normalization. Relief0 is exactly flat
  one level above water. This slice is entirely dry.
- The source normalizer only lowers excessive whole-ground differences, using
  its bounded repeated XY quartet scans. Qualified flat corners use the
  half-H quarter sample; final shared corners and stable row-major Mountain
  IDs pass canonical world continuity/occupancy validation.

Default styles retain the P3-02 values: plains20/32/75, hills50/24/50,
mountains85/32/25 (relief/scale/smoothness). Changing style resets those three
values. The form initially uses H32,V32,Z16,water4,seed0. The shared contract's
64² generated preset and starting-area64 remain future defaults; the live form
explicitly supplies32² and starting-area0. Unsupported coast/lake/river/forest/
starting-area requests are rejected rather than ignored. Generation streams for
later features remain reserved and independent; changing inactive forest
clustering or river meander does not change relief geometry.

Smoothness attenuates fine detail; it does not promise a monotonic decrease in
all relief metrics after ridge shaping and source lowering. In the fixed Z32,
seed0,mountains100,scale8 sample, smoothness0→100 lowers horizontal second-difference
sum424→329, while first-difference sum rises838→890 as broad ridges emerge.
Default Z32,seed0 normalized whole-height ranges are plains5…5, hills7…10 and
mountains18…26. A tiny plain can quantize to flat terrain; higher relief/vertical
headroom produces broader variation. These are measured properties, not target
coverage or future topology guarantees.

## Candidate service and admission

`POST /api/worlds/generated/preview` accepts name, explicit stored dimensions,
water, typed generated options and epoch/revision. It uses the existing wire
convention: enum values are numbers and Int64 epoch/revision are decimal strings.
There is no user-supplied algorithm path or content loader. Terrain uses exact
bundled prepared recipes. Confirmation/cancellation reuse the existing candidate
endpoints; the candidate digest covers request identity, content identity and
actual Save6 bytes. Confirmation never reruns the algorithm.

The shared worker/input lease reserves before starting a job; the FIFO validates
Owner, connection, epoch, revision, supported features and content. Generation
runs outside the FIFO under a linked request-abort/30-second deadline token.
Every sample and normalization batch spends the shared work allowance and probes
cancellation. A raced FIFO start is observed and cancelled by its own ID.
Publication rechecks the captured host/connection. Cancellation, late completion,
role changes, stale worlds, invalid requests and forged hashes preserve the world.
The form invalidates previews when inputs change and aborts outstanding requests;
late results cannot enable confirmation. Expiry and uncertain-commit behavior
remain shared with [empty creation](web-parity-empty-world.md).

Live admission is **H/V16–32, Z8–64, water1…Z−4**. Relief0–100, feature scale4–128,
smoothness0–100 and any signed Int32 seed are accepted. Separate empty/heightmap
admissions remain unchanged. Generated terrain now reserves1536 transfer bytes/cell
and16,384 working bytes/cell plus2,228,224 overhead; actual save and snapshot must
fit both the estimate and16MiB state cap. The shared hard reservation is96MiB per
job, at most four globally including retained candidates and image uploads.
Generated128² no longer fits that estimate. Larger presets remain disabled and
[capacity parity](web-parity-world-generation.md#p3-16a--map-size-capacity-parity)
remains P3-16a.

## Independent fixtures and verification

[Eleven literal fixtures](parity/relief-fixtures.json) are produced by
[`scripts/relief-fixtures.py`](../scripts/relief-fixtures.py), an independent Python
fixed-point evaluator. Whole-ground normalization uses multi-source shortest-path
min-plus distances on the shared-corner neighbor graph rather than the runtime's
in-place scanning traversal. The fixtures freeze raw quarter samples and SHA256
of row-major whole-ground bytes and UInt16LE packed corner values, including
odd stored lengths, minimum headroom, extreme signed seeds and high-frequency
maximum relief. Run `python3 scripts/relief-fixtures.py --check` to verify the
literal artifact. .NET checks bind the production algorithm to these literals,
independent legal geometry, dry cells, preset metrics and meaningful controls.

The preview SVG shows actual whole-ground heights. Full quarter-corner terrain
renders on the preserved live canvas after confirmation. Expanded isometric
preview, realized metrics and compass UX remain P3-12; coast, lake/river and
vegetation/starting-area algorithms remain P3-09–11. Generated labels/help are
new WORLDGEN wording reviewed in both locale files, with no invented Japanese
source binding. Shared name/dimensions/water/confirm text retains P3-06 provenance.
No desktop source strings or source-only tools are promoted to shipped coverage.

Fresh verification and resource measurements are recorded below and in the
[machine-readable review](parity/seeded-relief-review.json).
Existing native/GDI/device and deferred-browser acceptance, P2 crop/fidelity/
optional-pack repairs, larger capacity and overall P3 remain open. BGM remains
deferred by owner policy.

## Resource and packaged-runtime evidence

Release measurement environment: macOS26.7/Arm64, .NET SDK10.0.402/runtime10.0.13.
The synchronous samples warm content/serializers, then measure generation,
canonical serialization, candidate completion/validation, snapshot serialization,
confirmation and Save6 restore/reserialization. Managed numbers are thread-local
allocation churn, not peak RSS or latency percentiles. All fit the shared96MiB
hard reservation; they do not establish whole-city capacity.

| Stored dimensions / water / seed | Mountain occupants | Save bytes | Snapshot bytes | Managed allocation bytes | Elapsed ms |
| --- | ---: | ---: | ---: | ---: | ---: |
| 16×16×8 /1 /0 | 167 | 84,124 | 77,773 | 10,542,720 | 65.49 |
| 32×32×64 /1 /−2147483648 | 978 | 442,300 | 435,949 | 46,791,120 | 122.46 |
| 32×32×64 /60 /0 | 48 | 62,210 | 55,860 | 19,525,536 | 49.19 |

These use mountains100/scale4/smoothness0, exercising maximum relief/frequency
and minimal/high water headroom. Four simultaneously retained mountains85 worlds
at32²×64 each hold429,613 Save6 bytes (423,262-byte snapshots); a fifth job is
rejected until one is cancelled. Preview leaves all four live saves unchanged.

Linux/Arm64 image `d66fe08d6625806d021c1ac5d100f4562f859c4139c7846d4a072f9f9553d043`
builds successfully with pinned locked dependencies and frontend checks. A raw
HTTP/WebSocket smoke verifies32²×64 mountains100/scale4/smoothness0 at seed
−2147483648: detached preview, exact confirmation/publication, save/reopen and
byte-identical checkpoint restart. The save is442,280 bytes and snapshot435,929
bytes (the shorter world name explains the difference from the table).
Only checkpoint and writer-lock files persist under/data. The post-workflow
whole-container cgroup current/peak reading is187,154,432/187,154,432 bytes;
this includes startup, content, runtime and GC and is not per-job peak memory.
The temporary container was removed after verification.

## Fresh checks and remaining gates

Working tree based on `7aed3595ff30667f8bf58f30c75f984dcf463c52`:
all484 .NET tests pass, including10 new relief cases. Client tests pass120;
Python runs142 (141 pass, one existing skip). Release build and frontend
Svelte/TypeScript/generated/content/locale checks pass without warnings/errors.
The independent relief checker, pinned40-file terrain review, decoder source/
license checker, project boundaries and whitespace checks pass.

The complete Chromium suite passes84/84 (30 canonical,54 historical), and final
terrain checks pass12/12. Optional source and prepared fixture modes each pass4/4. Generated UI cases cover preset defaults, seed replay
and variation, all three controls, invalid ranges, input invalidation, cancellation
with a held late response, exact candidate confirmation, canvas reuse, two clients,
forged/stale/unsupported requests, Observer rejection and byte-exact save/reopen.
The shared .NET guards also cover Operator denial and stale/disconnected candidate
behavior. Existing empty and heightmap fixtures remain green after extracting
shared normalization. The browser request enum mismatch and two test-oracle issues
(collection equality and a nonmonotonic smoothness metric) were corrected before
accepting these results; no terrain hash or geometry expectation was relaxed.

Screenshots in local `tmp/p3-08/browser-final/relief/` were visually inspected:
actual heights are readable in the scrolled preview, the live quarter-slope mountain
scene renders after the required profile rebootstrap, and Japanese390px fields
remain reachable with internal scrolling and no document overflow. The final
live screenshot waits for connection readiness after panel closure; an earlier
capture caught the expected profile-rebootstrap transition instead of the world.
Logs and the temporary container probe are under local `tmp/p3-08/`.

P3-08 closes only seeded relief. **Next: P3-09 islands and coasts.** No coast,
lake/river, forest/starting-area, authoring/editing, large-map, original-runtime,
new browser-engine or overall P3 acceptance is implied by these checks.
