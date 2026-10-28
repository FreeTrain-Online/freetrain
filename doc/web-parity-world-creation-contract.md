# P3-02 world requests, profile and resource admission — 27 October 2026

P3-02 delivers executable, immutable request/validation contracts, generation-stage
seed derivation, content capability checks, a detached job state machine and a
repeatable resource-envelope probe. [P3-03 canonical terrain state](web-parity-terrain-state.md)
subsequently adds actual ground/occupancy and in-process profile bindings;
**next is P3-04 saves and transport.** This
step enables no endpoint, generator, size selector or world replacement. P3 and
existing P2/native/platform acceptance remain open.

The [P3-01 terrain contract](web-parity-terrain-contract.md) remains the source
semantics authority. All numerical admission limits, generation defaults, image
restrictions and job policies below are new browser adaptations under
**WORLDGEN-D002**, not measurements of original ExAV capacity. Water stays one
global plane. No P1 gameplay RNG, save number, protocol identity or active world
profile changes.

## Requests and exact defaults

[WorldCreation.cs](../src/FreeTrain.Online.Simulation/WorldCreation.cs) owns the
portable DTOs and structural validator. Requests are immutable records with
immutable palettes, tagged `empty`, `heightmap` or `generated`. Shared fields are
`Profile`, `Name`, stored `Dimensions.H/V/Z` and integer `WaterLevel`. The profile
is exactly `terrain-generation-1`; unknown sources/profiles are rejected. There
is no implicit clamping, inferred image sizing, hidden V padding or floating point
parameter. Untrusted dimensions are checked before products or allocations.

| Field/choice | Exact default | Bounds/meaning |
| --- | --- | --- |
| Name | Empty string | 0–128 UTF-16 code units, no control characters. Preserve nonempty text including whitespace; localize an empty name at publication. |
| Empty source-default expansion | H30, V42, Z6, water1 | Expands ExAV's displayed 30/30/6 once; H/V in the request are already stored dimensions. Ground equals water. |
| Heightmap / generated default | H64, V64, Z16, water4 | The compact profile; image is required for heightmap. |
| Initial size profiles | Compact 64×64×16; Standard 128×128×32 | Request factory values only; browser exposure waits for P3-04/06 transport and actual terrain measurements. |
| Custom H/V/Z | Explicit values | H,V 1–128; Z1–64; at most 16,384 cells and 1,048,576 addressable voxels, additionally subject to the work budget. Empty/heightmap may be 1×1×1. |
| Water | Source-specific default above | 0 ≤ water < Z. Generated requires H,V≥16, Z≥8, water≥1 and water≤Z−4 to retain vertical headroom. |
| Root seed | Signed Int32 `0` | Entire −2,147,483,648…2,147,483,647 range. UI decimal parsing must reject fractions/overflow. New seed will explicitly draw fresh cryptographic Int32 bytes in the service, independent of gameplay. |
| Relief kind | Hills | Plains / Hills / Mountains. |
| Relief amount, feature scale, smoothness | Plains 20/32/75; Hills 50/24/50; Mountains 85/32/25 | Amount0–100, feature scale4–128 storage cells, smoothness0–100. Choosing a relief preset expands these explicit values; changing a single value does not silently reapply a preset. Amount controls available vertical amplitude, scale wavelength, smoothness filtering strength. |
| Coast | Inland, no sides, water target0% | Inland requires no sides and 0% **sea** target; lakes are separate. Island requires no side flags, surrounds the boundary with sea. Sides requires one or more N/E/S/W flags (1/2/4/8). Island/Sides sea target5–75%; suggested expansion Island30%, Sides25%. |
| Lakes | Count0, per-lake area0% | Count0–8. Off requires area0; enabled per-lake target1–20% of all cells. Suggested enabled expansion1 lake at5%. |
| Rivers | Count0, width1, meander30, Automatic outlet | Count0–4, width1–4 cells, meander0–100. Suggested enabled count1. Chosen N/E/S/W outlet must have that sea sector (or island). Automatic requires sea or a lake. EdgeToEdge is explicit and permitted only inland with no lakes. Inactive width/meander/outlet values stay bounded. |
| Forests | Coverage0%, clustering50, empty palette | Coverage0–90%, clustering0–100. None0%, Sparse15%, Standard35%, Dense65% are proposed UI expansions. Off requires empty palette; on requires1–18 distinct exact content bindings. Coverage selects eligible cells, separately from source per-cell tree density. |
| Starting area | 64 cells | Integer0…floor(H×V/4), measured connected flat dry cells, quarter offsets0. Zero disables reservation. Tiny generated requests must explicitly lower this default when necessary. |
| Other foliage | Unavailable | No unreviewed arbitrary contribution list. `Cover` capability is reserved; a later reviewed runtime/recipe/save binding must precede a request-schema addition. |

`feature-space` rejects requests where starting area + total nominal sea/lake cells
+ riverCount×width×min(H,V) exceeds 75% of cells. This is a conservative necessary
space bound, not proof of geography. Integer area division floors. Required coast
sectors, land connectivity, dry banks, lake rims, normalization, actual buildable
area and geometry remain hard post-generation validations in P3-08–11. A structurally
valid request can fail those checks; report its unsatisfied constraints rather
than alter its topology. Sea/lake target areas are approximate: initial soft target
tolerance is ±5 percentage points, reported in preview; hard topology always wins.
Lake identities and outlets must remain distinct after normalization.

## Heightmap input and decode boundary

`HeightmapInput` carries the lowercase SHA-256 of **encoded input bytes**, MIME,
encoded byte count, decoded width/height/frame count/transparency and optionally an
exact prepared fixed-map provider binding. It contains no URL or filesystem path.
PNG, JPEG, BMP and GIF are accepted formats; encoded bytes1…8,388,608, width/height
1…4096, total pixels≤4,194,304, exactly one frame, no transparent pixels. Animated,
transparent, unsupported or oversized input fails with distinct issue codes. This
opaque-only initial policy is a deliberate adaptation: do not accidentally change
RGB through browser alpha compositing. P3-07 may add transparency only through an
explicit reviewed policy, not silently.

Service decoding must verify actual signatures, encoded hash/length, headers and
decoded values, enforce limits **before allocating expanded frames**, and check
again during decoding. Client-declared metadata alone is never trusted. Decode
outside Simulation into bounded 8-bit RGB(A), preserving stored raster orientation;
do not apply EXIF orientation, ICC display transforms, browser scaling, luminance
conversion or automatic image resizing. Reject inputs the selected decoder cannot
produce under this contract. Decode/storage-specific tests and selected library
remain P3-07 work. Import uses P3-01's HSL lightness, staggered bilinear sampling,
displace→interpolate→smooth→texture order. Actual resulting terrain is persisted;
the source image will not be required to reopen a world.

## Seed and candidate identities

`StageSeed(seed,stage,attempt)` uses SHA-256 over the following UTF-8 bytes, with LF
separators and **no trailing LF**. Decimal numbers use invariant signed base10, no
plus sign or leading zeroes:

```text
freetrain.world-generation.v1
terrain-generation-1
<root Int32>
<stage ID>
<attempt 0..3>
```

Read digest bytes0…3 as little-endian Int32 and mask with `0x7fffffff`. Stages are
`relief`, `coast`, `lakes`, `rivers`, `starting-area`, `forest-mask`, `forest-scatter`,
`cover`. Allocate fresh `GameRandom` (`subtractive-55-v1`) per stage/attempt; never
borrow a world bank. Attempts0…3 are sequential whole-candidate repair/retry
attempts, with no budget reset. Never retry invisibly with a different root seed.
Forest controls cannot affect terrain/water stage draws. Gameplay stream bank
version1 stays unchanged.

Literal independently calculated root0/attempt0 vectors in stage order:
`293479539, 1988742517, 2046890731, 531666846, 1184918920, 1794192551,
1459198540, 800645966`. Relief/root0/attempt3 is `1918789643`. Tests include both
Int32 extremes and a different current culture. Derivation oracle: Python
`int.from_bytes(hashlib.sha256(payload).digest()[:4], 'little') & 2147483647`.

The request fingerprint hashes the UTF-8 `System.Text.Json` default serialization
of the **base-typed** immutable request, including the source discriminator and
all explicit inactive parameters, names and ordered palette entries; no indentation
or custom options. Palette order is semantically significant for subsequent draws.
Reject invalid requests before hashing. Changing any input invalidates the draft.
Golden fingerprint tests freeze this contract. The service retains the original
normalized record instead of reconstructing one from reordered JSON.

The actual candidate digest must bind this fingerprint, catalog-set hash and
canonical **actual state bytes**, including dimensions, water, ground, corners,
decorations/ownership/mode and required packs. P3-03/04 must freeze their encoding
before candidate generation is enabled. Timestamps, job IDs and progress do not
belong in that digest. The profile currently freezes requests, admission and stage
seeds; P3-08 must freeze integer/fixed-point relief sampling, traversal, tie-breaks
and output vectors before producing live `terrain-generation-1` candidates. No
claim of existing deterministic terrain output is made by seed tests.

## Content requirements

[CreationContent](../src/FreeTrain.Online.Simulation/WorldCreationJob.cs) is an
immutable, trusted capability snapshot supplied by Content, never an authorization
claim from the browser. Every route needs prepared **and runtime-ready** Terrain.
Each forest/fixed-map requirement must match pack ID, version, exact lowercase
catalog hash, contribution ID and capability kind. Missing packs, changed hashes,
wrong handlers and prepared-only content fail explicitly. No GUID/case fallback,
optional-pack discovery or unprepared resource execution occurs in Simulation.

[WorldCreationCapabilities.Inspect](../src/FreeTrain.Online.Content/WorldCreationCapabilities.cs)
binds all18 existing forest recipes and their six species/base/`-m`/`-g` declarations
to the bundled catalog's pack/version/hash. It checks frame readiness and deliberately
marks every entry runtime-unavailable. There is no ready Terrain capability yet,
so even Empty cannot launch against today's prepared catalog. Fake ready
capabilities exist only in contract tests. Ordinary providers retain their ExAV
IDs from the P3-01 review; fixed maps must resolve reviewed prepared resources in
P3-07. Optional capability registration remains on the separate installed pack set;
no non-free asset or derivative enters this change.

For bundled-only requests the catalog-set hash is its catalog hash. Once multiple
packs participate, the service must freeze a deterministic hash of sorted exact
pack/version/hash tuples before enabling that path. Include it in the fence and
actual candidate digest. Existing prepared catalogs are inert art inventories;
their presence alone is never gameplay readiness.

## Job lifecycle and session integration contract

[WorldCreationJob](../src/FreeTrain.Online.Simulation/WorldCreationJob.cs) implements
immutable legal transitions:

```text
Queued → Running → Ready → Committed
Queued / Running / Ready → Cancelled or Expired
Queued / Running → Failed
```

A job captures server-assigned ID, immutable request/fingerprint, session,
participant, connection, world epoch/revision and catalog-set hash. Progress is
monotonic0…99 while running; Ready is100. Completion requires the original request
hash, valid candidate hash and actual byte count within its reserved transfer bound.
Commit requires Owner, Ready, exact request/candidate hashes, matching fence and
unexpired lifetime; there is no regeneration-on-confirm path. Cancel/expiry clears
the candidate reference. Terminal records reject late transitions. Failure codes
are bounded identifiers for later EN/JA resources, not localized domain strings.

Service integration in P3-06/12 must authenticate permissions and capture/recheck
fences inside the existing session FIFO. Detached workers run outside that FIFO;
normal ticks continue. Use atomic compare/exchange against the current job identity
and record: immutable state alone does not suppress two competing worker updates.
Changing controls cancels/replaces the old job; out-of-order results cannot replace
a newer request. Closing the draft, disconnect/reconnect, content changes, epoch
replacement or any revision change cancels/invalidates its result. Confirm rechecks
Owner and fence, verifies retained candidate bytes, then publishes exactly once
through the FIFO. Other clients invalidate old picks/drafts. No mutation occurs on
Cancel, failure, stale input, unauthorized confirmation or uncertain command outcome;
reuse existing receipt/reconciliation behavior.

Initial orchestration policy: one retained job/candidate per session, four active
jobs globally, reject excess work with a busy/retry issue; do not enqueue unbounded
image buffers. Lifetime is600 seconds from creation, including queue/preview time.
Worker timeout is30 seconds from start using a monotonic deadline. Timeout is a
failure, never permission to commit a partially built candidate. A linked
cancellation token handles cancel/disconnect/timeout.
[GenerationWork](../src/FreeTrain.Online.Simulation/GenerationWork.cs) debits the
single shared work allowance before batches of1…4096 units and checks cancellation
every batch. All stages/repairs share it; resource exhaustion does not reset on
retry. The consuming algorithms must define/count primitive operations and bound
normalization scans (at most64×Z scans per attempt), not only debit one unit per
large unbounded loop. The 30-second bound is a service policy, not a measured
generation latency guarantee. Ready candidates occupy a global memory reservation
until release even if no worker runs. Four96MiB reservations cap this workload at
384MiB; live worlds, content, sockets, CLR and browser memory are additional.

## Budget measurements and initial admission

The standalone [probe](../tools/GenerationBudgetProbe/Program.cs) allocates/touches
reserved scratch, two transfer buffers, two maximum-size RGBA buffers and metadata;
it writes a verbose six-tree-per-cell JSON envelope and walks all bounded scan
units for four attempts at Z32. It allocates no dense voxel array, executes no
terrain generator and imports no external packs. The machine reports macOS26.7.1,
Arm64,10 logical CPUs, .NET10.0.13; hardware model/RAM were unavailable under the
sandbox. [Raw results](parity/world-creation-budget.json) include the environment,
exact counters and elapsed time. Timings are a single local admission probe,
not browser, decode, network, normalization or full-world performance evidence.

Reservation formulas (`C=H×V`, `P=decoded image pixels`, zero outside heightmap):

- Voxels: `C×Z` for address validation; never promise a dense allocation.
- Retained/temporary memory allowance: `2,228,224 + 2304×C + 8×P + encodedImageBytes` bytes. This reserves
  128/cell scratch, two512/cell transfer buffers,1152/cell serialization/transient
  allowance, two RGBA image buffers, the bounded encoded image,2MiB fixed/first-use allowance and two64KiB headers.
- Transfer allowance: `65,536 + 512×C` bytes, with a16MiB hard ceiling. Six source
  trees per cell fit the probe envelope. Persisted IDs/packs must be interned or
  accounted for; P3-04 must verify real encoded terrain stays within the reservation.
- Work: `C×(64×Z+256)×4 + P`, capped at256,000,000 units. This admits128²×32,
  rejects128²×64 even though its dimensions/voxel bound fit, and permits taller
  smaller maps. Images and algorithms must honor the same whole-job allowance.
- Per-job memory hard ceiling96MiB. Encoded input maximum8MiB is reserved once; the worker's
  additional decoder implementation overhead requires bounded service input storage
  and a decoder-specific measurement before P3-07 exposure; extra encoded/decoded copies are not
  another unlimited in-memory allowance under this reservation.

| Grid (probe Z32) | Sample JSON bytes | Reserved buffers touched, max image | Total managed allocation, max image | Initial admission |
| --- | ---: | ---: | ---: | --- |
| 30² | 279,301 | 44,159,488 | 45,238,496 | Yes, Empty/heightmap |
| 64² | 1,272,577 | 47,841,280 | 52,223,200 | Yes, compact |
| 128² | 5,100,033 | 61,997,056 | 79,641,184 | Yes, standard |
| 256² | 20,456,449 | 118,620,160 | 189,294,728 | No, future capacity target |

The transient allowance covers observed serializer allocation, not just the packed
arrays. The fixed allowance includes first-use costs observed at the smallest
probe. Total allocated bytes are churn across the probe; they are **not** measured
peak RSS. Record both rather than labeling allocations as resident memory. Recheck
these bounds with the canonical state, real serialization, image decoder and
legal-city measurements; estimates never replace allocation/transfer guards.

Today's SaveCodec/Kestrel upload limit remains1MiB and normal command frames16KiB.
Do not send these hypothetical candidates through those paths or expose the size
factories as a supported live-world guarantee. P3-04 owns actual terrain schema,
bounded transfer/chunking/save/checkpoint revisions and their tests. P3-06 enables
only the configurations proven end-to-end.256×256×64 and the other city proposals
remain future measurement targets. The larger probe fails dimensions, transfer,
work and memory aspirations independently; reject before allocation in production.

## Verification and handoff

Run the portable contract and source checks:

```sh
dotnet build FreeTrain.Online.slnx -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test FreeTrain.Online.slnx -c Release --no-build --no-restore -m:1 -nr:false
python3 scripts/check-project-boundaries.py
python3 scripts/review-terrain.py --check
python3 -m unittest discover -s scripts/tests
dotnet run --project tools/GenerationBudgetProbe -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
git diff --check
```

The probe's first run requires `dotnet restore tools/GenerationBudgetProbe --locked-mode`.
Contract tests cover defaults/round trips/fingerprints, overflow/tiny/high-water/work
bounds, contradictory topology, image limits/alpha/animation, missing/changed packs,
prepared-only denial, seed extremes/culture/gameplay isolation, bounded work and
cancellation, stale fences, exact candidate confirmation, Owner denial, expiry and
late results. There is no server endpoint, new UI text or browser behavior to test
in this step. P3-03/04 must bind actual terrain and canonical state encoding;
P3-06/07 own the real worker/decoder and localized controls. Existing source,
current-world/save-preservation, session and network regressions remain required.

Validation on 27 October: Release build passes with zero warnings/errors; the
full .NET suite passes **337 cases**, including **50 creation-contract cases**.
All **141 Python tests**, project boundaries, bundled preparation, offline P3-01
source checks, budget probe guards and whitespace checks pass. No client/browser
suite was run because this step adds no browser behavior; native and deferred
platform acceptance remain unverified. Local test sockets required sandbox
escalation; the final .NET run was executed with that access.
