# C# backend performance — 14 November 2026

The next optimization should target repeated simulation graph reconstruction and
validation. Serialization remains worth improving, but it is a smaller part of
the measured running workload than the earlier client investigation suggested.

The checkpoint contains 55,179 natural terrain owners, 177 additional spatial
owners, 169 railway voxels, two trains, two stations and six city structures.
Small train updates repeatedly rebuild and compare this much larger owner graph.

**Benchmark follow-up:** [Backend benchmarks and correctness checks](../tests/fixtures/performance/README.md)
now compare base and PR measurements on the same CI runner and publish medians,
ranges and allocation changes to the PR. Performance numbers have no pass/fail
thresholds. Canonical save hashes, continuation, protocol invariants and historical
world collection remain correctness checks in the normal C# suite.

## Measurements

Release build at `ca601faa5aafca66eee2df8bbc0eefedd5e8696b`, SDK 10.0.402,
runtime 10.0.13, ARM64 macOS. Each scenario starts a separate disposable server
from the same checkpoint, warms for ten seconds, and records about twenty
seconds. Minimal WebSocket consumers use the actual authentication, chunked join,
heartbeat, command, publication and socket-send paths. They parse messages but
perform no game rendering. A metadata bootstrap request once per second measures
session read latency and adds a small amount of snapshot work.

The table uses runs **without stack sampling**; runtime counters remain enabled.
CPU is process CPU time divided by elapsed wall time, as a percentage of one core.
Allocation and payload rates use decimal units.

| Scenario | Server CPU | Allocation rate | Gen 2 collections/sec | GC pause ms/sec | Aggregate payload | Bootstrap latency p95 |
|---|---:|---:|---:|---:|---:|---:|
| Paused, one peer | 6.5% | 0.42 MB/s | 0 | 0 | 0 | 10 ms |
| Running, one peer | 150.6% | 2.29 GB/s | 5.7 | 55.5 | 1.37 MB/s | 467 ms |
| Paused, four peers | 10.1% | 1.44 MB/s | 0 | 0 | 0 | 10 ms |
| Running, four peers | 152.3% | 2.34 GB/s | 5.5 | 50.3 | 5.11 MB/s | 373 ms |

Running peers received 141 and 131 deltas each in the respective windows, about
7.1 and 6.6 publications/sec. Four peers received equal publication counts.
No peer disconnected or received a rejection. Every captured city delta used
the protocol 11 heat reference; none replaced heat during these short windows.
The authoritative final saves were paused successfully.

Similar CPU with four peers is not evidence of spare capacity: the simulation
already misses its intended pacing, and delivered work differs between windows.
The isolated simulation benchmark below averages 105 ms per game minute, whereas
normal playback budgets 50 ms per minute. Live start/end save clocks bracket a
slightly wider interval than the CPU window and should not be divided by that
window to claim an exact simulation rate. Bootstrap latency measures a session
read waiting on the shared gate, rather than gameplay input latency.

## Managed work and allocations

Separate one-peer and four-peer recordings used `dotnet-trace` 10.0.750501 with
`dotnet-sampled-thread-time,gc-verbose`. The analyzer selects `Thread/Sample`
events whose `Type` is `Managed` and whose stack contains application code.
There are 10,106 such samples in the one-peer trace and 10,494 in the four-peer
trace. Both report zero lost events.

| Inclusive call path | One peer, managed samples | Four peers, managed samples | One peer, sampled allocation bytes |
|---|---:|---:|---:|
| `MixedSessionState.AdvanceOneMinute` | 94.3% | 88.5% | 96.5% |
| `MixedWorld.Replace` | 92.6% | 86.8% | 96.1% |
| `MixedSessionState.ValidateCandidate` | 46.2% | 43.1% | 48.3% |
| `CityHeat.NotifyChanges` | 30.2% | 27.9% | 34.3% |
| `SpatialIndex` constructor | 20.7% | 19.9% | 16.2% |
| `MixedWire.Snapshot` | 3.6% | 4.3% | 2.0% |
| `StateDifference.Between` | 1.3% | 5.1% | 1.3% |

Rows overlap and must not be added together. For example, candidate validation
calls world replacement, which calls heat notification and constructs an index.
The allocation trace attributes approximately 35.6 GB over its twenty-second
window. Allocation ticks estimate attribution of allocated bytes; their type
names describe sampled allocation sites, not an exact object census.

These managed stack shares are **not percentages of whole-process CPU**. Native
runtime/GC/JIT execution is not fully attributed, and external/waiting samples
are excluded. The sampler also perturbs execution: paused process CPU rises from
6.5% in the control to 34.9% with tracing, and the sampled running cases deliver
less work. Raw `topN` reports include waiting threads and should not be presented
as CPU attribution. The [.NET tracing documentation](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-trace)
explains why the old `cpu-sampling` profile was renamed to sampled thread time.

An independent, single-process Release probe measured the existing functions
without any tracing or networking. Each operation receives five warmup calls;
measurements use 30 iterations, or 100 for minute advancement and snapshots.
Allocated bytes use `GC.GetTotalAllocatedBytes`, and wall time includes GC.

| Existing operation | Mean wall time | Allocated bytes/operation |
|---|---:|---:|
| `CityHeat.NotifyChanges(world, world)` | 26.6 ms | 54.8 MB |
| `MixedWorld.Replace` with unchanged inputs | 57.7 ms | 152.8 MB |
| `MixedSessionState.AdvanceOneMinute` | 104.8 ms | 255.2 MB |
| `MixedWire.Snapshot` on unchanged session | 0.58 ms | 33.1 KB |
| `MixedWire.ValidateAdmission` on unchanged session | 46.4 ms | 6.25 MB |

The identical-world notification returns the original city graph, despite doing
the full scan. The replacement benchmark constructs a validated successor with
unchanged payloads. Minute advancement executes the real callbacks across 100
successive minutes; its cost is an average across this checkpoint's phases.
Admission is expensive in isolation but is not on ordinary minute advancement.
These are operation measurements, not implemented savings or speedup forecasts.

## Proposed implementation order

### 1. Reuse already completed successor validation

[MixedSessionState.ValidateCandidate](../src/FreeTrain.Online.Simulation/MixedSessionState.cs#L164)
calls `World.Replace(...)` and discards the newly reconstructed world. Production
callbacks have already obtained their candidate through validated replacement;
the same constructors, spatial checks and heat notifications run again.
This validation subtree accounts for 46% of sampled managed work and 48% of
sampled allocations in the one-peer recording.

Add an internal certificate recording the exact predecessor and successor
identity after `MixedWorld.Replace` finishes. Consume it at the session boundary,
following the existing terrain `IsValidatedSuccessorOf` pattern. Retain profile,
revision, graph-presence, account-prefix, booking-time and callback/scheduler
checks. Candidates without the exact certificate still receive full validation.
Use weak ownership or bounded metadata so a certificate cannot retain a chain
of historical worlds. Revision equality alone cannot authorize reuse.

Acceptance: a normal train callback validates its world successor once; forged
candidates, certificates for another predecessor, allocator/revision rewinds,
changed unversioned payloads and failed-minute rollback remain rejected.

### 2. Stop allocating comparison machinery for unchanged natural owners

[MixedWorld.Replace](../src/FreeTrain.Online.Simulation/MixedWorld.cs#L254)
builds terrain ID sets and loops over all 55,356 combined owners. For every
unchanged owner it creates ordered railway sequences, even when both sequences
are empty, clones an entity in `SameEntity`, and performs fence/listener checks.
`SameFences` builds a set even for owners without fences. Ordered railway LINQ
iterators alone account for about 18% of the sampled allocation attribution.

First add exact reference and empty-array fast paths within the existing checks;
reuse terrain owner membership when the terrain object is unchanged. Compare
railway payloads through stable per-owner indexes or canonical ordered arrays,
and cache fence memberships by immutable city graph/owner. Then restrict full
successor comparisons to changed owners and their checked dependencies. Sharing
an entity object by itself does not certify unchanged voxel payloads, fences,
station links or owner classification.

Acceptance: ordinary car movement creates no per-natural-owner sorting iterators
or empty fence sets. Changing payloads under an unchanged owner revision still
fails, including station listener and fence changes.

### 3. Make conductivity notifications depend on changed owners and roads

[CityHeat.NotifyChanges](../src/FreeTrain.Online.Simulation/CityHeat.cs#L140)
builds two complete position-to-owner dictionaries by expanding every footprint,
then concatenates and scans their keys. Car component updates usually change
neither occupancy owner nor road definition. The scan still runs, including
during the redundant validation above.

Pass a checked ownership/road change set from construction and replacement.
Skip notification when it proves no relevant assignment, removal or road creation
occurred. Otherwise visit the union of the changed owners' old/new footprint
columns and changed road columns. Preserve the existing source-compatible
notification gaps in CITY-D003 and indexing in CITY-D001; changing those would
change simulation behavior. Keep a conservative full comparison for arbitrary
uncertified candidates.

Acceptance: train-only movement and station counter updates produce no complete
footprint scan and retain the same heat object. Assignment/removal, multi-cell
owners, roads, construction completion and terrain interactions produce exactly
the current conductivity buffers, including cases that deliberately do not notify.

### 4. Reuse occupancy indexes and geometry validation

[MixedWorld's constructor](../src/FreeTrain.Online.Simulation/MixedWorld.cs#L41)
creates a new combined `SpatialIndex` for each candidate. The
[index constructor](../src/FreeTrain.Online.Simulation/SpatialEntities.cs#L115)
sorts every owner, expands every footprint and freezes a complete location map.
Most train callbacks change car payloads or owner revisions while footprints and
terrain remain stable.

Generalize the existing terrain index overlay to mixed owners, or split immutable
terrain occupancy from a small dynamic owner index. Update affected cells when
an entity revision changes so lookups return the current entity; preserve
multi-cell footprints, transparency and overlap checks. Reuse heat finite-value
validation only for the exact immutable buffers and matching dimensions.
Keep full construction on load and geometry replacement, and compact overlays
without retaining predecessor worlds.

Acceptance: ordinary car movement rebuilds no natural occupancy map; overlap,
outside-world, tunnel, slope and transparency checks still hold. Repeated moves
and save/reopen must not grow retained predecessor chains.

### 5. Reduce repeated publication and serialization work

Build a shared immutable snapshot once per committed state and reuse it across
peers. Include epoch, revision, playback and motion dependencies in its cache
identity. [Publish](../src/FreeTrain.Online.Server/NetworkSession.cs#L373) currently
calls `host.Snapshot()` before checking whether the peer already has that revision.
Mixed snapshots create fresh terrain session wrappers, losing the wrapper-keyed
snapshot cache and repeating provenance, clock and geometry-publication work.
Paused allocation grows from 0.42 MB/s with one peer to 1.44 MB/s with four peers.

Cache city heat projections by immutable heat identity, keeping mutable wire
arrays private. Serialize each outbound delta once into a bounded envelope and
use the same bytes for the size check and socket send. Cache common deltas only
for peers with the same exact predecessor; retain per-peer base, queue, timeout
and coherent-transfer rules. Joins/replacements and saves still need full heat.

This is a smaller immediate target: simulation consumes 94% of one-peer managed
samples. Publication/differencing grows in relative importance with four peers.
Avoid projecting a precise overall speedup from eliminating a second encoding.

Implemented follow-up: the host keeps its latest canonical snapshot, keyed by
epoch, revision, playback rate and motion sequence. Exact predecessor identities
share deltas within one publication; weakly owned, size-bounded outbound encodings
serve both admission and socket sends. City heat projections share immutable
buffers by heat identity, preserving full JSON buffers for saves and replacements.
Prototype snapshots still include their mutable control-lease dependencies.

Local Release validation against clean `45163d9e9682` passed 2,424 correctness
tests and all ten benchmark checks in each of three base/working-tree samples.
Median allocations per network tick fell from 48,985 to 596 bytes for four paused
peers and from 15,282,420 to 10,716,600 bytes for four running peers. Running
publication medians were 14.961 → 11.180 ms with one peer and 16.292 → 7.115 ms
with four. All operation counts and canonical save hashes matched. Unrelated
minute/replacement timings varied, including a +8.9% large-minute median; these
short process measurements do not establish an overall simulation speedup.
Full local results: `tmp/item5-benchmark-comparison/summary.md` and `report.json`.

### 6. Profile command admission separately after the minute path

`MixedWire.ValidateAdmission` serializes full heat and creates new JSON options;
`ValidateTransition` adds successor serialization. These functions run on load,
edits and saves rather than every simulation minute. The measured isolated
46 ms admission cost makes them a follow-up for construction/preview latency,
especially as journals grow. Reuse serializer metadata/options and exact cached
section byte counts where immutable dependencies prove validity. Preserve the
complete-state and 16 MiB delta limits. This investigation does not establish
their cost under a construction-command workload or an autosave boundary.

## Validation and remaining scope

No gameplay implementation or original save was changed. Release backend and both
temporary C# probes built with zero warnings/errors. The profiling harness checked
accepted run/pause commands, live peer continuity, equal four-peer publications
and heat-reference use. No gameplay test suite was needed for these analysis-only
changes.

The first implementation should rerun the same scenarios and compare allocation
per simulated minute as well as CPU, delivered simulation/publication rate and
session read latency. Proposed regression gates: no duplicate world reconstruction
for certified train successors; no whole-map conductivity/occupancy rebuild for
ordinary train-only movement; bounded retained memory; exact save/reopen
continuation; unchanged malformed-successor rejection. Validate against the full
simulation, station, city, persistence and network suites. Do not replace
deterministic correctness tests with timing assertions.

These short local recordings do not cover the scheduled heat diffusion boundary,
day/hour effects, long journal growth, autosave, slow receivers, reconnect storms,
32 peers or production Linux. Tiered JIT continues during the windows. Peer
comparisons and microbenchmark means are supporting evidence, not capacity gates
or a guarantee of a particular improvement.

## Evidence and rerun

All evidence is under `tmp/live-performance-2026-11-14/backend/`:

- `summary.json`, `report.json`, `controls/report.json`: normalized metrics and raw scenario observations.
- `running.nettrace`, `four-peers.nettrace`, `paused.nettrace`: managed stacks and allocation events; matching `.speedscope.json` files are available for inspection.
- `running-attribution.json`, `four-peers-attribution.json`: managed-only work and sampled allocation attribution, including raw method names and call paths.
- `*-counters.json`: runtime counters; `microbenchmarks.json`: isolated operation costs.
- `manifest.json`, `server-build/`: source/checkpoint hashes, tool versions and matching application assemblies/PDBs.

Harness: `tmp/profile-backend.ts`. Allocation/stack reader:
`tmp/BackendTraceAnalysis/`. Isolated probe: `tmp/BackendProbe/`.
The trace reader references assemblies shipped with the locally installed tool.

```sh
dotnet tool install dotnet-trace --version 10.0.750501 --tool-path tmp/backend-diagnostics-tools
dotnet tool install dotnet-counters --version 10.0.750501 --tool-path tmp/backend-diagnostics-tools
dotnet build src/FreeTrain.Online.Server -c Release --no-restore
BACKEND_OUTPUT=tmp/backend-rerun \
  deno run -A --config web/deno.json tmp/profile-backend.ts
BACKEND_OUTPUT=tmp/backend-rerun-controls \
  BACKEND_CASES=paused-repeat,four-peers-repeat \
  deno run -A --config web/deno.json tmp/profile-backend.ts
dotnet run --project tmp/BackendProbe -c Release -- \
  tmp/live-performance-2026-11-14/checkpoint-copy.json
dotnet run --project tmp/BackendTraceAnalysis -c Release -- \
  tmp/backend-rerun/running.nettrace
```

The harness defaults to the five original profiling cases; `BACKEND_CASES`
selects additional controls or a subset. Keep probe execution separate from live
recording so the probe does not compete for CPU. Trace conversion can outlast the
recording. Each server uses an ephemeral loopback port and its own temporary save
directory, removed on completion.
