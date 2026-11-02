# P4-23 — legal railway capacity and release verification

2 November 2026. This is the railway-only stage of the
[P0-06 city recipes](web-parity-scenarios.md#legal-city-and-full-content-fixture-specifications).
Save8/protocol6 remain current. P4-24 integrated acceptance and P5–P11 gameplay,
import, optional packs, native/device and human fidelity gates remain separate.

## Instantiated legality and supported limits

`tools/TerrainTransportProbe/RailwayCapacity.cs` constructs every rail, ramp,
support and special rail through the delivered P4 preview/apply handlers. It
never fabricates railway payloads or inserts future trains/platforms/buildings.
The exact bundled definitions are recorded in each fixture's `bindings`; the
catalog SHA-256 is `316dc169c56d3096bb9e49031ab10d47a2d9015c960b730ecb394f8371dc8b6e`.

The source-specified octagon vertices, inclusive segments, H/V-to-X/Y module
anchors and 36 distinct cells per ring are retained. Small/medium/large place
4/16/64 rings respectively. Every ring port must have a reciprocal neighbor;
components, IDs, footprints, actual terrain occupancy, handler dispatch and
slope/support members validate before the artifact is accepted. Independent
P4-02 literal geometry/source oracles remain separate from this integration check.
The proposed 8/32/128 trains, 32/256/1,024 cars, station and population budgets
receive **no playable capacity credit**.

The full-size scenes add 64 rings and a separate connected EXAV-CITY-NET railway
stage. A three-way junction, signal holding location, complete eight-voxel ramp,
elevated normal/bridge/girder chain, tunnel with exact terrain backups, garage
branch and four open terminal ports form one connected component. Traversal checks
both directions of every edge, including slope Z changes. Together with the rings
there are 65 components. Elevated platforms, trains, contention, passenger stops,
world exit/return behavior and controllers retain P5/P6 ownership.

Both full-size scenes store **1500×1090×45**. The heavy scene retains **397,950**
terrain entities from the accepted terrace/six-tree-forest resource recipe. Its
144×300 H/V construction reservation is flat and clear before construction;
10,800 original decoration entities are excluded explicitly. All other realized
terrain resources and stable IDs remain. Two partial mountain endpoints are
consumed through the tunnel handler and preserved as tunnel backups. This is a
legal constructed scene, not arbitrary railway overlap on the dense terrain.

| Enforced bound | Meaning and evidence |
| --- | --- |
| 8,192 railway voxels | Includes traffic, invisible ramp members, supports, piers and stationary footprints. The limit scene constructs 227 rings (8,172 cells) plus a 20-cell line through real handlers. Exactly 8,192 saves/reopens; another voxel rejects with `railwayCapacityLimit`. |
| At most 8,192 railway owners | Shared allocator/unique spatial IDs; multi-voxel owners consume multiple voxel slots. A separate 2048×830×45 fixture builds a 2,048-cell straight route through one handler call. The limit scene has 8,192 single-cell owners; the mixed network has 2,403 owners / 2,410 voxels. |
| 8,192 accounting entries | Durable journal, not a fresh allowance per command. Pending stationary completions reserve entries. Existing booking/construction boundary tests verify rejection without partial charges or IDs. No journal compaction is advertised. |
| 8,192 route cells | Inclusive construction work bound, additionally constrained by actual H/V/Z bounds, placement legality, occupancy, available voxels, journal and bytes. This is not a promise of an 8,192-cell straight route inside a supported map. |
| At most 1,024 complete ramps | Eight voxels each, only if every other constraint fits; not 1,024 ramps plus 8,192 other voxels. |
| 8 MiB mixed payload / 16 MiB delta / 256 MiB canonical state | Admission and publication byte bounds apply in addition to counts. Speed lists, account records and support combinations can exhaust bytes before a count bound. |

These are current durable P4 limits and measured scenes, not an inferred maximum
of ExAV or a full-city simulation guarantee. The existing P3 dense-edit miss is
still owner-accepted at its own scope; P4 inherits no latency waiver.

## Measurement method

Fresh environment/artifact metadata and raw fixture, browser and Linux evidence
are under `tmp/p4-23-capacity/` and `tmp/p4-23-*.log`. The accepted browser directory
is `tmp/p4-23-capacity/browser-final/`; earlier diagnostic directories/logs retain
failed harness runs and are not passing evidence. `environment-final.json` identifies
the implementation commit, dirty-diff hash, catalog/scenario/toolchain hashes and
freshly detected hardware. The maintained result manifest is
[railway-capacity-results.json](parity/railway-capacity-results.json).

Native host: MacBookPro18,2, Apple M1 Max, 10 cores (8 performance/2 efficiency),
64 GiB, macOS26.7.1 Arm64, .NET SDK10.0.402, Deno2.9.8. Browser version and exact
canvas/camera/zoom are recorded per case: headless Chromium, emulated1880×1040 CSS
viewport, DPR1. No physical display/device acceptance is implied. Server and
clients share the host; an Owner and Observer verify rejoin and restart.

Each scene records three page-cold reload/decode samples in default English
(HTTP cache may be warm),
30 pan/paint, 30 settled picks and 30 discrete-wheel feedback samples. The two
full-size scenes additionally run English and Japanese and 30 **real geometry
extensions**, separately recording immediate checking feedback, complete HTTP
preview, command confirmation and command-to-settled-scene time. First15 commits
use local loopback controls; last15 inject50ms outbound and50ms inbound delay.
Raw WebSocket command/delta/receipt timestamps and sizes remain in each report;
receive timestamps are before the injected inbound delay. Snapshots use loopback
throughput, not a bandwidth-limited WAN. This bounded P4 authoring study does not
claim the proposed full-city five60-second simulation windows, transport/growth
soak or P11 hardware matrix.

Pan paint samples measure cached viewport painting; separate settled-refinement
samples retain the asynchronous resource-field catch-up cost. End-to-end scene
completion also includes test-driver polling/IPC overhead.

Scene readiness distinguishes retained old pixels from completed replacement
presentation. Settled cached/fresh pixels must match byte-for-byte after resize.
The main canvas stays mounted. Every case downloads exact state, reloads, admits
an Observer, restarts its disposable server, reauthenticates both participants
and compares the checkpoint bytes. A250ms RSS sampler reports observed server and
aggregate browser process peaks (shared pages can be counted more than once),
with separate owner-page current/post-GC JS heap. These samples are not absolute
memory proofs. An isolated heavy-artifact reopen/graph-validation process also
records `/usr/bin/time -l` peak RSS.

## Results

| Scene | Rail voxels / owners | Terrain entities | Save / packet bytes | Chunks | CLI save / reopen (ms) |
| --- | ---: | ---: | ---: | ---: | ---: |
| small | 144 / 144 | 0 | 125,160 / 118,831 | 3 | 2.1 / 28.3 |
| medium | 576 / 576 | 0 | 479,734 / 473,405 | 10 | 7.6 / 46.8 |
| large | 2,304 / 2,304 | 0 | 1,903,166 / 1,896,835 | 39 | 17.3 / 108.2 |
| limit | 8,192 / 8,192 | 0 | 8,626,942 / 8,620,611 | 176 | 39.0 / 457.0 |
| route | 2,048 / 2,048 | 0 | 3,792,573 / 3,786,242 | 78 | 17.8 / 215.9 |
| empty | 2,410 / 2,403 | 0 | 4,071,353 / 4,065,022 | 83 | 16.2 / 220.9 |
| heavy | 2,410 / 2,403 | 397,950 | 31,674,842 / 31,668,509 | 645 | 114.1 / 889.0 |

Heavy offline fixture assembly takes480.6s for524 sequential handler operations;
this is separate from world generation or browser startup. The 8,192-voxel
fixture assembly takes32.7s. Isolated heavy reopen/graph validation peaks at
877,707,264 bytes RSS (1.82s elapsed).

All times below are milliseconds. Cold lists are individual reload/decode
samples. Commit columns report p95; first15 local and last15 at100ms injected RTT.

| Scene / locale | Cold reloads | Upload / rejoin / restart | Paint / pick / zoom p95 | Immediate / preview p95 | Commit local /100ms RTT p95 |
| --- | --- | --- | --- | --- | --- |
| small-en | 569 / 567 / 558 | 76 / 559 / 1827 | 1.8 / 6.0 / 16.1 | — / — | — / — |
| medium-en | 647 / 642 / 668 | 50 / 638 / 1896 | 4.2 / 7.0 / 17.1 | — / — | — / — |
| large-en | 1424 / 1395 / 1410 | 137 / 1476 / 2759 | 1.0 / 5.0 / 16.9 | — / — | — / — |
| limit-en | 2080 / 1989 / 2008 | 526 / 1922 / 4349 | 1.0 / 9.0 / 17.1 | — / — | — / — |
| route-en | 1758 / 1621 / 1694 | 293 / 1724 / 2918 | 1.0 / 7.0 / 17.1 | — / — | — / — |
| empty-en | 1622 / 1752 / 1762 | 360 / 1758 / 3083 | 1.0 / 7.0 / 17.8 | 0.6 / 60.0 | 247.8 / 280.8 |
| empty-ja | 1739 / 1750 / 1638 | 317 / 1790 / 3026 | 1.1 / 8.0 / 19.1 | 0.6 / 81.0 | 257.9 / 273.3 |
| heavy-en | 9643 / 9507 / 9509 | 1840 / 7626 / 13256 | 1.0 / 13.0 / 17.4 | 0.6 / 876.0 | 4697.6 / 3867.5 |
| heavy-ja | 7412 / 7861 / 9048 | 1310 / 7489 / 12758 | 1.1 / 9.0 / 16.5 | 0.5 / 823.0 | 4441.5 / 3949.3 |

| Scene / locale | Peak server / browser RSS (GiB) | Retained owner JS (MiB) | Command-to-settled-scene p95 local /100ms RTT (ms) |
| --- | ---: | ---: | ---: |
| small-en | 0.24 / 1.25 | 22.8 | — |
| medium-en | 0.20 / 1.38 | 36.2 | — |
| large-en | 0.27 / 1.57 | 49.1 | — |
| limit-en | 0.62 / 1.67 | 75.3 | — |
| route-en | 0.44 / 2.02 | 66.5 | — |
| empty-en | 0.82 / 3.03 | 67.9 | 1084 / 1121 |
| empty-ja | 0.75 / 2.95 | 67.9 | 1109 / 1156 |
| heavy-en | 2.43 / 5.36 | 304.2 | 6093 / 4717 |
| heavy-ja | 2.88 / 5.55 | 304.0 | 5280 / 4778 |

The unreserved resource-field check runs the shared clock and preserves exact terrain/rail/account state. Thirty picks of the exact signal owner have p95 9.0 ms; thirty forest/terrace pans have paint p95 1.0 ms. Resource-field zoom feedback / settled refinement p95 is 17.7 / 763.5 ms, recorded separately.

**P4-CAP-001 — authoritative construction latency remains unmet.** All four
full-size EN/JA profiles exceed250ms p95 at100ms injected RTT. Immediate local
checking, sampled picking, pan paint and wheel feedback meet their respective
100ms/33.4ms targets; that does not make complete previews or commits fast.
**P4-CAP-002 — visible construction completion remains slow.** Complete
replacement presentation takes roughly1s in the empty scene and several seconds
in the heavy scene, beyond command confirmation. These measured misses are not
covered by the P3 terrain waiver. The owner subsequently deferred both to P11
on 2 November; see the [P4-24 disposition](web-parity-p4-acceptance.md#performance-disposition).
No whole-P4 performance acceptance is claimed. The maintained manifest retains
p50/p95/max, byte sizes, fixture/graph/random/artifact hashes and raw-report hashes.

## Release and regression verification

The packaged Linux Arm64 image is
`sha256:b9cdfe7bce89a043aaea515e73f4d57869291460847efbb06d16959aef55502a`
(`freetrain-online:p4-23`), built with the pinned Dockerfile. A disposable,
loopback-only container with4 CPUs/6 GiB and an isolated test volume verifies
chunked mixed publication, exact packed terrain **and railway payload/owners**,
download/reopen and retained checkpoint restart. The heavy scene has645 chunks;
its exact canonical SHA-256 is
`fa8ea7869a7da87670d90613645a35f95f101a4cfc1d140483124421ece9aa74`.
Observed final Linux heavy upload/reopen memory peak was1,842,270,208 bytes;
heavy checkpoint restore/download peaked at1,162,215,424 bytes.
Heavy checkpoint restart is byte-exact. Distribution verification checks238
assets,505 unique media, four notices and exact catalog hashes. The stale release
checker now verifies the actual generated-terrain startup offer; capacity clients
advertise mixed-world compatibility and retain railway owners beside packed terrain.

Release regression: **1,114/1,114 .NET**, **221/221 client**, **163 Python cases
(one skip)**, production build/generated contracts/prepared content/locales/Svelte
(zero Svelte errors/warnings),66 pinned source files/1,484 independent railway
fixtures/accessory ledger/project boundaries and whitespace pass. Opt-in capacity
Chromium is **10/10** (primary8/8 plus long-route/resource-field2/2); fresh ordinary
construction/cross-tool/mixed-continuation/rail-art Chromium is **14/14**. The
existing Vite bundle-size advisory remains informational. The final inspected
captures include the EN/JA network, source rings and actual forest/terrace field.

P4-23 verification is complete with the two explicit performance findings above;
P4-24 records the owner's explicit deferral to P11 integrated performance work,
alongside later bottlenecks and possible map-size-limit reassessment. The measured
misses remain open; they no longer block P4 infrastructure acceptance. Native,
optional-suite, future train/city budgets and long-form full-city simulation
acceptance are not closed by these authoring measurements.

## Upper-limit user feedback — owner follow-up — 2 November 2026

Both limits are exercised through the actual Track form, in English and Japanese.
A fixture with8,191 railway voxels accepts one extension to8,192; a separate
fixture with8,191 real booked construction entries accepts its final entry.
The journal fixture uses charged normal-rail no-ops through the gameplay handler,
not fabricated accounting records. The next extension in each world is illegal,
quotes zero cost and disables confirmation. Exact save bytes prove no change to
rails, terrain, cash, journal, allocator, revisions, clock or random state.

The former combined limit message is now two explicit browser diagnostics:

- Railway space: **“The railway limit (8,192 occupied cells, including supports)
  has been reached.”** The message recommends removing railway objects or reducing
  the route and confirms that no changes were made.
- Accounting history: **“The construction history is full (8,192 entries).”** The
  message explains that this change was not applied, the world can still be saved,
  and saving/reopening does not reset the history. Further construction requiring
  an entry needs a new world; no journal compaction/reset is implemented.

These are owner-requested browser explanations of runtime limits, delivered in
EN/JA together; they are not translations attributed to ExAV source text.
All tools using the shared railway-reason mapping receive the distinct messages.

A raw WebSocket command deliberately bypassing the disabled button also rejects
with the existing `invalidPlacement` receipt and leaves the entire canonical save
unchanged. It does not terminate the server or disconnect the game. Both full
worlds save/reopen exactly and still accept a clock step afterward. Narrow430×850
captures verify readable wrapped messages and zero horizontal overflow.

Fresh checks: **4/4 Chromium boundary workflows**, **8/8 .NET capacity tests**,
**221/221 client tests**, production/generated/content/locale/Svelte checks and
whitespace pass. No browser page errors occur. Results and current source/browser
artifact hashes are in [railway-limit-results.json](parity/railway-limit-results.json);
raw evidence is `tmp/p4-limit-followup/browser-final/`, with logs
`tmp/p4-limit-*.log`. The initial performance/Linux artifacts above keep their
original recorded hashes; this follow-up changes diagnostics and test tooling.

## Reproduction

From the repository root:

```sh
dotnet build tools/TerrainTransportProbe -c Release --no-restore
# Each construction mode owns a fresh output directory; heavy is deliberately slow.
dotnet tools/TerrainTransportProbe/bin/Release/net10.0/TerrainTransportProbe.dll --railway-capacity heavy tmp/new-rail-capacity/heavy
dotnet tools/TerrainTransportProbe/bin/Release/net10.0/TerrainTransportProbe.dll --railway-verify tmp/new-rail-capacity/heavy
```

Generate `small`, `medium`, `large`, `limit`, `route`, `empty`, `heavy`,
`voxel-edge`, `journal` with the same command.
From `web`, after a production build, run:

```sh
FREETRAIN_RAILWAY_CAPACITY_FIXTURES=/absolute/path/to/tmp/new-rail-capacity \
FREETRAIN_ONLINE_PROFILE_EVIDENCE=/absolute/path/to/fresh-browser-evidence \
deno run -A node_modules/@playwright/test/cli.js test --config playwright.railway-capacity.config.ts
```

The opt-in suite owns its servers on5420/4493 and disposable saves. Linux uses
`scripts/check-bundled-deployment.py --image freetrain-online:p4-23 --platform linux/arm64`
and `scripts/check-map-capacity.py --save <fixture>/world.json`, followed by the
latter's `--restart` mode after restarting the isolated container. No user save,
existing game or slot is replaced by these checks.
