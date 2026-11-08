# P5-28 — Legal moving-fleet capacity and resources

8 November 2026. This is fleet/railway evidence, not passenger, growth or
full-city acceptance. Save9/protocol7 and all production resource limits remain
unchanged. P4-CAP-001/002 optimization and the wider resource/device/soak matrix
remain P11 work.

## Legality and budgets

`tools/TerrainTransportProbe/FleetCapacity.cs` reuses P4's construction preview /
apply handlers and reciprocal graph checks. It purchases the bundled head/body/
tail formation `{3983B298-ADB1-4905-94E5-03B7AAE5A221}`, with four cars for the
small scene and eight otherwise. Exact train/car recipe references, stable IDs,
initial poses, catalog and accepted save hashes are recorded in the measurements.
No trusted `CarComponent` insertion supplies the fleet count.

The small/medium/large P4 rings carry 8/32/128 trains and 32/256/1,024 cars.
The second source placement direction is reversed through the production placement
operation so both formations travel around the ring in the same direction.
Every car occupies a unique railway cell and every deployed train has exactly one
future one-shot callback. Validation repeats after each simulated minute. The
three ring scenes complete a 36-move circuit in 72 minutes; reopened and uninterrupted
runs have identical complete canonical save bytes.

The standalone connected infrastructure scene stores 128×160×10, builds the same
junction, signal, ramp, elevated normal/bridge/girder chain, tunnel with terrain
backups and garage branch through P4 handlers, then deploys two eight-car trains
on the elevated chain. Its construction origin is H32,V100,Z2. It provides
network traffic/contending dead-end coverage, not passenger-platform acceptance.
The full stored-map scene is 1500×1090×45 with all 64 constructed rings and the
connected infrastructure. It allocates 126 eight-car trains to 63 rings and two
to the network, preserving the 128-train/1,024-car budget. The unused ring remains
in the accepted railway count. These network scenes compare sixteen minutes of
callbacks and exact reopened continuation; their open branches do not promise a
collision-free circuit. The full-size terrain is flat; both endpoint mountains are consumed through
the tunnel handler and retained in tunnel backups. The accepted scene has zero
remaining terrain occupants. P4's 397,950-entity heavy terrain scene retains its
separate P11 resource qualification.

TRAIN-F001 receives measured qualification for these four/eight-car formations.
The 100-car formation bound remains admission scope rather than a measurement of
100-car movement. No proposed fleet budget was reduced. Purchases split at the existing 100-train
per-request bound. An overlong nineteen-car pair rejects instead of overlapping
on a 36-cell ring and leaves the source bytes unchanged. Existing admission,
movement and reservation unit tests retain fleet, car, queue, transaction and
publication byte bounds. Finite 8,192-entry accounting capacity still limits
uninterrupted operation; these scenes do not promise indefinite running.

## Measurement method and scope

Fresh inputs, raw browser reports, screenshots, fixture reports and environment
metadata are in `tmp/p5-28-capacity/`; the maintained summary is
[the result manifest](parity/fleet-capacity-results.json). The dedicated Playwright
configuration runs one scene at a time against disposable production Release
servers and the built client. The relevant regression run finishes before accepted browser measurements;
final CLI tick samples run separately afterward, with only the capacity class
selected. Diagnostic and interrupted runs do not supply acceptance timings.

Record the actual hardware/OS/toolchain, content/build/test hashes and Chromium
version. The owner uses an emulated 1880×1040 CSS viewport at DPR1 on the same host
as the server. Each scene has three page-cold reload/readiness samples (HTTP cache
may be warm), 30 local and 30 injected-100ms-RTT run/pause feedback samples, then
one sustained sixty-second paused and one sixty-second running window. Control
injection delays commands 50ms outward and deltas/receipts 50ms inward. It does
not simulate limited throughput, jitter or snapshot RTT. Feedback includes
Playwright and polling overhead. Running uses the supported slowest rate, 0.125
(400ms nominal simulated minute), explicitly recorded rather than assuming normal
speed. Every window starts from the accepted scene to preserve equal journal
headroom. Cold reloads use the startup viewport; sustained windows explicitly
recenter on deployed stock after each upload and assert the camera. The report records camera,
zoom and canvas dimensions. The join peer uses the default camera.

Raw WebSocket byte/timestamp/type/tick samples support bandwidth and tick-delivery
analysis. CLI ticks measure the simulation boundary independently of HTTP and
rendering. The current realtime pump caps catch-up work and clamps elapsed gaps
over 100ms; observed clock progress therefore records lost wall time under load,
rather than assuming that an unbounded backlog will eventually catch up.
Browser frame intervals use requestAnimationFrame; paint timings observe
actual canvas paint attribute changes, not idle repetitions of a stale gauge.
An idle frame cadence is not a claim that the map repaints at that rate.
Browser and server RSS are sampled every five seconds alongside CDP heap metrics;
a post-GC sample estimates retained heap. RSS peaks are sampled lower bounds.
Decoded-image memory is a width×height×4 estimate for distinct decoded image URLs,
not a GPU/texture/cache allocation measurement. Aggregate browser RSS includes
browser utility processes; the peer join is outside the sustained-window sampler.

Named save, peer join and actual backend restart timings are recorded;
canonical downloads validate the complete state.
The restarted world must match the final paused save exactly. Captures and browser
errors are checked. This bounded fleet study revises the initial broad P0 procedure
of five 60-second windows, 24-hour/seven-day city workloads and repeated EN/JA
workflow operations: those remain P11/P6–P9 and P5-29. No native, other-engine,
physical-device, dense-terrain fleet or complete-suite acceptance is implied.

## Reproduction

From the repository root:

```sh
FREETRAIN_FLEET_CAPACITY_FIXTURES="$PWD/tmp/p5-28-capacity" \
  dotnet test tests/FreeTrain.Online.Tests -c Release --no-restore \
  --filter 'FullyQualifiedName~FleetCapacityTests|FullyQualifiedName~RollingStockTests|FullyQualifiedName~FleetMovementTests|FullyQualifiedName~CapacityReservationTests'
cd web
deno task build
deno test -A --no-check tests/fleet-state.test.ts tests/train-motion.test.ts tests/train-renderer.test.ts tests/state-transfer.test.ts
deno run -A npm:@playwright/test@1.64.1 test --config playwright.fleet-capacity.config.ts
```

The probe CLI also accepts `--fleet-capacity small|medium|large|network|empty|heavy directory`.
`heavy` is an opt-in extension and has no fresh P5 acceptance credit here.

## Recorded results

The detected host is MacBookPro18,2 / Apple M1 Max / 64 GiB, macOS 26.7.1 Arm64,
.NET SDK 10.0.402 and Deno 2.9.8. Exact build/content/test and accepted save hashes,
Chromium version, camera, canvas and raw-report hashes are in the manifest.

| Scene | Stored H×V×Z | Trains / cars | Rail voxels | Save MiB | CLI tick p50 / p95 ms |
| --- | --- | ---: | ---: | ---: | ---: |
| small | 64×64×16 | 8 / 32 | 144 | 0.14 | 1.1 / 17.0 |
| medium | 128×128×32 | 32 / 256 | 576 | 0.60 | 0.2 / 52.1 |
| large | 256×256×64 | 128 / 1024 | 2,304 | 2.37 | 0.2 / 929.0 |
| network | 128×160×10 | 2 / 16 | 106 | 0.11 | 0.0 / 5.7 |
| empty | 1500×1090×45 | 128 / 1024 | 2,410 | 4.44 | 0.3 / 936.1 |

| Scene | Running frame / paint p95 ms | Local / RTT100 feedback p95 ms | Advanced / nominal minutes | Received MiB/s | Cold / join / save / restart ms |
| --- | ---: | ---: | ---: | ---: | ---: |
| small | 16.7 / 2.6 | 131 / 249 | 151 / 151 | 0.28 | 908 / 1065 / 157 / 1722 |
| medium | 16.8 / 7.3 | 371 / 409 | 151 / 151 | 1.31 | 1406 / 1646 / 161 / 2214 |
| large | 16.8 / 7.6 | 1444 / 1880 | 78 / 152 | 2.57 | 1975 / 2073 / 151 / 2730 |
| network | 16.8 / 3.7 | 121 / 222 | 151 / 152 | 0.13 | 1471 / 1484 / 208 / 2375 |
| empty | 16.8 / 7.7 | 1462 / 1456 | 77 / 152 | 2.63 | 2151 / 2240 / 216 / 3176 |

| Scene | Sampled server / browser peak RSS MiB | Post-GC owner heap MiB | Decoded-image RGBA estimate KiB |
| --- | ---: | ---: | ---: |
| small | 576 / 867 | 33.9 | 537 |
| medium | 463 / 1119 | 58.0 | 537 |
| large | 436 / 1017 | 62.2 | 612 |
| network | 242 / 847 | 38.4 | 757 |
| empty | 528 / 1285 | 68.9 | 757 |

`empty` names the full stored-map **terrain** profile; it contains the accepted
128-train fleet, 64 constructed rings and connected network. All five paused
windows preserve the original complete save bytes, and all final paused states
save, admit a peer and restart exactly. Inspected captures show actual rolling
stock, including the two elevated network trains. Two final large-map captures
required waiting two animation frames and rechecking refinement after the camera
assertion; earlier blank transient captures are diagnostic.

The provisional frame p95 target of 33.33ms is met in these samples, with occasional
longer frames retained in raw distributions. Performance acceptance stays open:

- **FLEET-CAP-001 — P11 simulation/resource performance:** both 128-train scenes
  advance approximately half the nominal minutes even at the slowest rate.
- **FLEET-CAP-002 — P11 command/publication performance:** local complete feedback
  exceeds the provisional 100ms p95 in all five scenes; medium and both 128-train
  scenes also exceed 250ms p95 with injected 100ms RTT. These measurements include
  driver overhead. The maximum fleet also receives about 2.6 MiB/s while running.
- **P4-CAP-001/002 — P11 integrated performance:** retain their existing owners;
  this study adds no latency waiver or full-city release acceptance.

Fresh validation: **381/381 relevant Release .NET cases**, including **6/6 new
capacity cases**, then **6/6 isolated capacity rechecks**; **53/53 selected client
unit tests**; **5/5 Chromium journeys** plus **2/2 final-capture rechecks**. No skips
in these accepted runs. Production client/generated contracts/content/locales and
Svelte checks pass (zero errors/warnings; existing large-bundle advisory). The
capacity CLI Release build also passes with zero errors/warnings. The complete
suite, Linux/container, optional packs and other engines were not run for this
step. P5-29 distribution/bilingual reconciliation remains next.
