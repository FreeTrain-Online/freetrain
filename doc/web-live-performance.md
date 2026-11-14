# Live world update performance — 14 November 2026

The 400×600 checkpoint with two trains, two live stations and six city buildings
exposed repeated transmission and validation of unchanged city heat, plus broad
terrain-mask scans on train moves. The initial normal-speed recording used
91–94% of a browser CPU core, delivered 7–8 MB/s of decoded WebSocket payload,
and had 367–400 ms p95 browser frame gaps. Detailed baseline evidence is in
`tmp/live-performance-2026-11-14/analysis.md`.

## Heat references (protocol 11)

`Delta.cityHeatBaseRevision`, when present, equals both the delta's
`baseRevision` and the receiving client's current revision. The delta's
`mixed.city.heat` retains its horizontal/vertical dimensions and carries empty
`q`, `back` and `rho` arrays. This explicitly references the exact heat buffers
in that immutable predecessor, within the same world epoch. The client checks
the base, dimensions, placeholder and validated predecessor before reconstructing
the full read state. Empty buffers without a reference, references without city
state, mixed full-buffer/reference payloads and wrong bases are rejected.

The publisher compares all three buffers by exact Single bits before referencing
them. Changed heat, newly introduced city state, joins, resync and replacement
snapshots carry complete buffers. Saves and checkpoints continue to store full
heat in Save 11; no migration or compatibility negotiation was added. Protocol
10 clients receive the existing HTTP 426 response and must refresh alongside
the server update. Limits and coherent base-to-successor publication are unchanged.

## Validation reuse

The client compares incoming mixed records by value and retains equal immutable
subtrees. Revisions alone never authorize reuse. This makes unchanged geometry,
account history and heat available by identity, without serializing the complete
mixed graph for every clock/movement update.

Geometry certificates cover terrain data, owner classification, footprints,
railway components, slopes and tunnel backups. Runtime station counters, city
timers and construction state are checked separately. Natural terrain validation
can survive mixed-owner changes only when all terrain owners, terrain data and
tunnel backups remain identical. Changed geometry still receives full spatial
validation. Heat certificates require frozen buffers and retain dimension checks.
Entity indexes are shared between city, fleet, station and successor checks and
owned weakly by immutable arrays, allowing obsolete worlds to be collected.

Successor checks still reject journal rewrites, allocator/revision rewind,
unversioned owner changes, invalid passenger/provider graphs and stale bases.
The voxel payload check visits actual voxel/slope owners instead of iterating
every natural terrain entity on every update.

## Renderer invalidation

The renderer caches a key of visual inputs when read state changes. Heat,
accounts, station counters and scheduler-only changes do not prepare a new static
scene. Car poses/reversal, rail components, platform appearance, construction art,
terrain and presentation variants still invalidate their visual dependencies.

Ground masks are grouped into projected 64-pixel buckets. A train-only change
does not inspect the complete previous terrain operation list. Changes to static
masks conservatively map affected buckets back into ground columns over the full
height range, then compare their mask signatures. Existing source depth order,
old/new damage bounds, overlap replay and alpha picking are preserved.

## Verification

Regression coverage checks exact heat reuse and replacement, malformed references,
signed-zero changes, station-only updates against full validation, stale/malformed
owners, no full terrain-operation scan for ordinary car moves, and coverage of
every changed full-render column during city stage/build/removal transitions.
Existing rendering, save/reopen, station, fleet and contract suites also apply.

Validation completed with 2,395 server tests, 408 client tests, two memory
retention checks, and nine browser checks covering English/Japanese construction,
city transport and checkpoint continuation, train startup/speed/focus recovery,
and departure sound including retries, reconnect and rejected callbacks. Content,
generated-contract, locale and Svelte checks pass. The sound cases were rerun
with their complete generated fixtures after an initial missing-fixture failure.

## Checkpoint measurements

The same preserved checkpoint was restored for each scenario in a disposable
Release server. Both baseline and optimized runs used headless Chromium 156,
1440×1000, DPR 1, zoom 150%, the same camera, and approximately 12.5-second
recording windows. Normal-speed results were repeated independently:

| Metric | Baseline | Optimized |
|---|---:|---:|
| Client task CPU, % of one core | 91–94% | 40–41% |
| Browser frame cadence (rAF callbacks/sec) | 11–12 | 55–56 |
| Frame gap p95 | 367–400 ms | 16.8 ms |
| Incoming WebSocket application payload | 7.1–8.0 MB/s | 1.13–1.17 MB/s |
| `applyDelta` inclusive sampled time/window | 8.65–8.70 s | 1.10–1.17 s |
| `validMixed` inclusive sampled time/window | 5.16–5.56 s | 0.66–0.68 s |

The representative unchanged-heat delta falls from about 2.12 MB to 187 KB,
approximately 91% fewer bytes. Total bandwidth falls about 84–85% in paired
runs; the optimized client receives more individual publications, so total
traffic reduction differs from the per-message reduction. Validation timings
are nested within update timings and must not be added together.

With sound enabled the optimized recording used 44% client CPU, had 16.8 ms
p95 frame gaps, and recorded three media playback calls. Discrete rendering
used 22% client CPU. Fast-speed results depend on the trains' waiting/movement
phase and should not be treated as a normalized renderer comparison.

The independent repeat reported no page errors or console warnings and checked
that the rendered revision caught up exactly with the paused authoritative save.
Three 139–181 ms long tasks remain in that recording. Server CPU remains around
1.5 CPU-seconds per second; server serialization/admission work was outside
these first three optimizations. Individual scenario windows are short, and
frame cadence counts browser callbacks rather than distinct painted game frames.

Evidence: `tmp/live-performance-2026-11-14/{report.json,repeat/report.json}`
contains the baseline; `after/report.json` and `after-repeat/report.json` contain
optimized results. `cpu-attribution.json` contains source-mapped samples, and
`after/build-assets/` preserves the matching bundles/maps. The temporary
`tmp/profile-live.ts` harness can rerun the preserved checkpoint with
`PROFILE_SAVE`, `PROFILE_OUTPUT` and optional `PROFILE_CASES=running` overrides.

These changes do not close the broader P11 capacity, native fidelity or
multi-client performance gates.
