# P6-26 — Legal station/passenger capacity and resources

11 November 2026. This bounded study measures constructed stations, real passenger
trains and explicitly controlled population providers. P7 city producers, P4/P5
performance findings, P11 full-city/device/long-soak acceptance and P6-27 packaging
retain their separate gates. Save10/protocol9 and production admission limits are
unchanged.

## Legal scenes and identities

`tools/TerrainTransportProbe/StationCapacity.cs` reuses the P4 gameplay-built,
reciprocally checked 36-cell rings. The station building, thin platforms, train
purchase and deployment all use their production preview/commit operations.
Every ring has two one-cell buildings and two four-cell platforms, with alternating
roofed/unroofed modules, plus one three-car Simple passenger train. The two yards
are encountered in opposite travel directions. All platforms must have a host,
car occupancy must remain exact and every deployed train must own exactly one
future callback. Each station owns hour/day callbacks; each platform owns its
source-relative daily callback. Platform upkeep is not incorrectly aligned to
midnight.

| Scene | Stored H×V×Z | Stations / platforms | Trains / cars | Controlled providers |
| --- | --- | ---: | ---: | ---: |
| small | 64×64×16 | 8 / 8 | 4 / 12 | 32 |
| medium | 128×128×32 | 32 / 32 | 16 / 48 | 128 |
| large | 256×256×64 | 128 / 128 | 64 / 192 | 512 |
| extent | 1500×1090×45 | 128 / 128 | 64 / 192 | 512 |

Eight distinct, existing rail owners per ring carry `ConstantPopulation(2500)`
with land value 10,000, `input=controlled`. They attach through `StationCatchment`,
so catchment overlap, stored listener order and demand sharing use actual source
rules. These bindings represent 80,000 / 320,000 / 1,280,000 / 1,280,000 raw
controlled population, not constructible or evolving city inhabitants. No
population structures or P7 gameplay are credited. The full stored-map scene also
retains P4's connected infrastructure, without deploying extra trains there.
Terrain outside the constructed network is flat.

These are named station workloads, separate from the proposed EXAV-CITY full-city
structure budgets and P5's 8/32/128-train moving-fleet measurements. One three-car
train per ring leaves room for four-cell source stop positions and observes both
stations over repeated circuits. It does not revise a production count limit or
claim that the full-city formation/structure proposals are met. The maintained
[result manifest](parity/station-capacity-results.json) binds the accepted canonical
save, P4 source geometry, station fixtures, content, exact building/stock/car
recipes, build and measurement harness hashes.

## Procedure and practical limits

Each domain scene starts at minute 1437 and advances 120 minutes. A separately
reopened copy advances alongside it; complete canonical bytes match at every
minute, including additional reopening at midnight and the next hour boundary.
The probe checks hosted platforms, callback identities, exact occupied car count,
admission, real passenger fare bookings and departures. Tick timing covers only
`AdvanceOneMinute`, excluding oracle serialization and validation. Raw reports
identify ordinary/hour/midnight samples and due-callback counts. Thirty independent
provider detach/reattach branches measure listener churn. Thirty independent
station removal/rebuild branches measure removal-preview cost and removal commit
plus replacement preview/commit cost, preserving
the accepted source state and normal accounting/timer/host behavior.

The shared Playwright capacity harness runs one scene at a time against disposable
Release servers and the built production client. It records actual host/toolchain,
Chromium, viewport (1880×1040 CSS, DPR1), camera and canvas dimensions. Three
page-cold reloads may use warm HTTP cache. Thirty HTTP station removal previews
measure request/response cost on the paused accepted world. Thirty local and thirty
injected-100ms-RTT run/pause feedback samples are requested using the normal clock
rate. A station feedback assertion is bounded to 15 seconds: an unresolved or
timed-out command ends that network phase, retains the failed sample and wire
trace, and restores/reloads the original paused save before proceeding. Remaining
requested samples in that phase receive no successful timing credit. The original
large/extent failures, whose strict assertions timed out at 60 seconds, are also
retained as diagnostics and explicit misses. Recovery never replays an unresolved
command.
The injection delays commands 50ms outward and deltas/receipts 50ms inward; it does
not simulate throughput limits, jitter or snapshot RTT. Feedback includes driver
and polling overhead. Domain edit timings and HTTP preview timings are distinct
from end-to-end browser construction latency.

Every scene has a sixty-second paused window and a sixty-second running window at
the slowest supported rate (0.125, nominal 400ms/simulated minute). Each starts
from the same accepted save and explicitly centers the camera on deployed stock.
Paused bytes remain exact. Running must move trains and produce a real passenger
unload fare; station counters and WebSocket traffic are captured. Browser frame
intervals use requestAnimationFrame; actual paint-attribute changes supply paint
samples. Idle frame cadence does not imply continuous map repainting. The runtime
caps catch-up and clamps long wall-clock gaps, so observed versus nominal clock
progress is reported directly.

Browser/server RSS is sampled every five seconds and is a sampled lower bound.
CDP supplies heap metrics and a final post-GC retained sample. Browser RSS includes
utility processes and excludes the later peer join. Distinct decoded image URLs'
width×height×4 is an RGBA estimate, not texture/GPU allocation. Raw distributions,
traffic timestamps, memory samples, screenshots and full reports remain under
`tmp/p6-26-capacity/`; maintained hashes make these local artifacts identifiable.
A named save, fresh peer join through connected/assets-ready state at the default
camera, and actual backend restart are timed. The restarted
world must match the final paused canonical bytes exactly. Sixty-second windows
provide bounded sustained-use evidence; 24-hour/seven-day, other-engine/native,
physical-device, optional-content and dense full-city work remain P11.

Provisional existing targets are frame interval p95 ≤33.33ms, complete local
feedback p95 <100ms and feedback p95 <250ms at injected 100ms RTT. Progress below
90% of the slowest nominal clock is reported as a simulation miss. Existing
count/byte/account limits remain active; resource measurements have no invented
RSS or bandwidth pass threshold. A finite 8,192-entry account journal still
bounds uninterrupted use.

## Reproduction

From the repository root, run the relevant station/save regressions before browser
measurement, then keep other capacity workloads idle:

```sh
FREETRAIN_STATION_CAPACITY_FIXTURES="$PWD/tmp/p6-26-capacity" \
  dotnet test tests/FreeTrain.Online.Tests -c Release --no-restore -m:1 \
  -p:UseSharedCompilation=false \
  --filter 'FullyQualifiedName~Station|FullyQualifiedName~Platform|FullyQualifiedName~Save|FullyQualifiedName~CapacityReservation'
cd web
deno task build
deno test -A --no-check tests/station-state.test.ts tests/station-renderer.test.ts tests/station-information.test.ts tests/state-transfer.test.ts
deno run -A npm:@playwright/test@1.64.1 test --config playwright.station-capacity.config.ts
cd ..
python3 scripts/summarize-station-capacity.py
```

The Release probe CLI also accepts
`--station-capacity small|medium|large|extent directory`. Domain samples come from
the isolated capacity run; the manifest records exact raw-report hashes. All four
fresh fixture reports and all four browser reports are required by the summarizer;
missing scenes, identity mismatches and functional failures reject summarization.

## Recorded results

The detected host is MacBookPro18,2 / Apple M1 Max / 64 GiB / 10 logical CPUs,
macOS 26.7.1 Arm64, .NET SDK 10.0.402 and Deno 2.9.8. Chromium version,
exact build/content/fixture hashes, camera/canvas settings and raw-report hashes
are recorded in the manifest. These are English UI measurements on one host.

| Scene | Save MiB | CLI tick p50 / p95 ms | Midnight tick ms | Churn / domain preview / edit p95 ms | HTTP preview p95 ms |
| --- | ---: | ---: | ---: | ---: | ---: |
| small | 0.16 | 0.1 / 2.6 | 8.8 | 1.5 / 1.0 / 2.0 | 36 |
| medium | 0.63 | 0.7 / 39.4 | 196.6 | 5.4 / 2.7 / 13.5 | 69 |
| large | 2.51 | 0.9 / 604.0 | 2331.3 | 23.8 / 17.8 / 46.2 | 155 |
| extent | 4.58 | 0.9 / 511.9 | 2183.7 | 26.7 / 6.3 / 45.2 | 153 |

| Scene | Running frame / paint p95 ms | Successful local / RTT100 feedback p95 ms | Advanced / nominal minutes | Received MiB/s | Cold / join / save / restart ms |
| --- | ---: | ---: | ---: | ---: | ---: |
| small | 16.7 / 3.8 | 133 / 246 | 151 / 152 | 0.28 | 1642 / 1791 / 207 / 2592 |
| medium | 16.8 / 6.2 | 169 / 519 | 151 / 152 | 1.13 | 1894 / 2325 / 206 / 3069 |
| large | 16.8 / 6.2 | 259 / 2493 | 117 / 152 | 3.54 | 2372 / 2185 / 208 / 3576 |
| extent | 16.8 / 6.4 | 265 / 254 | 115 / 152 | 3.65 | 2375 / 2664 / 220 / 3653 |

Feedback p95 is conditional on successful samples, **not** an overall service
p95 for an incomplete phase. Small/medium complete all 30 samples per network.
Large completes 27 local / 19 RTT samples with one failed pause per phase and
2 / 10 requests unmeasured. Extent completes 25 / 21, also with one failed pause
per phase and 4 / 8 unmeasured. Each failed assertion lasts about 15 seconds;
its actual successful completion time is unknown. The original large/extent
60-second unresolved-command diagnostics also remain credited as misses.

| Scene | Sampled server / browser peak RSS MiB | Post-GC owner heap MiB | Decoded-image RGBA estimate KiB |
| --- | ---: | ---: | ---: |
| small | 619 / 1134 | 46.4 | 629 |
| medium | 512 / 1248 | 73.3 | 629 |
| large | 586 / 1331 | 61.5 | 704 |
| extent | 688 / 1289 | 69.2 | 849 |

All four paused windows preserve their accepted canonical bytes. All active
windows move real trains, generate passenger unload fare entries, admit a peer,
write a named save and restart to exactly the final paused canonical state.
Inspected captures show real buildings, both platforms and deployed stock.
The large domain timing report was refreshed with the CLI after a brief build
overlap in its earlier sample; accepted browser timings run after all domain
measurements. Small/medium reports come from the initial browser run; large/extent
reports come from the final run with explicit failed-phase recovery.

Frame p95 meets the provisional target in all eight windows. Performance
acceptance remains open, with these concrete P11 owners:

- **STATION-CAP-001 — P11 simulation/resource performance:** the two 128-station
  scenes advance 115–117 minutes against about 152 nominal, even at the slowest
  rate. Their single sampled midnight bursts take 2.18–2.33 seconds.
- **STATION-CAP-002 — P11 command/publication performance:** successful local
  feedback p95 exceeds 100ms in every scene; medium and both 128-station scenes
  exceed 250ms at injected 100ms RTT. Partial successful distributions do not
  discount the separately recorded failed requests.
- **STATION-CAP-004 — P11 command/publication reliability under load:** normal-rate
  pause commands become unresolved in both 128-station scenes, initially and in
  both final network phases. Preserve receipt/transport fences while investigating
  callback/publication cost and responsiveness; no automatic replay or timeout
  relaxation receives gameplay credit here.
- Existing **P4-CAP-001/002** and **FLEET-CAP-001/002** remain open with their
  existing P11 owners. No production admission budget is changed from these
  one-host measurements.

Fresh validation: **769/769 existing station/platform/save/admission Release
.NET cases**, and separately **4/4 new capacity cases** (the capacity run occurred
before browser measurement and the final existing-case regression afterward);
**26/26 selected client tests**; **four completed Chromium resource journeys**
(two initial small/medium, two final large/extent). The completed large journeys
include explicit feedback failures, not complete feedback acceptance. No cases
skip in the accepted test runs. Production client/generated contracts/content/
locales/Svelte checks pass, with the existing bundle advisory. The standalone
Release probe builds with zero warnings/errors; project boundaries and diff
whitespace checks pass. Test logs, diagnostic failures, complete raw samples and
oracles remain under `tmp/p6-26-*`; maintained hashes/counts are in the manifest.
The complete suite, Linux/container packaging, optional packs, other engines,
native acceptance and full-city/long-soak gates were not executed for this step.
P6-27 distribution/localization/evidence reconciliation is next.
