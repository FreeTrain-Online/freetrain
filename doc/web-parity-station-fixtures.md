# P6-02 independent station fixtures — 8 November 2026

The [literal fixture set](parity/station-fixtures.json) supplies **821 cases in
24 families**, bound to **38 pinned source/manifest files** from stable ExAV
2.1.1.12/V2, Git `883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, `trunk/`.
The [P6-01 contract and decisions](web-parity-station-contract.md) and
[P5 train/harbor handoff](web-parity-p6-train-handoff.md) remain its inputs.
This completes fixture scope. Station gameplay, commands, presentation and
save/transport consumers belong to P6-03 onward. Save9/protocol7, content and
runtime limits are unchanged.

Expectations were authored from pinned source formulas and declaration data,
independently of production code. The [read-only oracle](../scripts/station-fixtures.py)
imports no simulation, renderer, content compiler or other fixture evaluator;
it generates no expectations and executes no legacy assemblies. Ordinary checking
uses repository source evidence. `--source-root` additionally checks original
Git blobs, hashes and bound line excerpts, never a corrected archive working tree.
These are source calculations, not original-runtime captures or visual approval.

| Family | Cases | Expected boundary and later consumer |
| --- | ---: | --- |
| Thin geometry | 32 | Four headings × two roof styles × lengths1/3/4/8; floor-quarter roof bounds, traffic cells, normal-rail restoration, draw pass. P6-09. |
| Wide geometry | 16 | Four headings × either/both/no lanes; right90 lane0, left90 lane1, separate center strip, no roof, source chip rectangle/anchor. P6-10/13. |
| Placement predicates | 16 | Flat ground/reclaimability/bounds, aligned SingleRailRoad conversion; diagonal, car, curve, junction, accessory-only Traffic and occupied center rejection. P6-06/09/10. |
| Station definitions | 35 | Every bundled station declaration: exact height/footprint, Int64 purchase, daily operation, subsidiary demolition, ownership/value, Gray cut color and immediate creation. P6-03/06/07. |
| Removal/lane predicates | 40 | Four headings, occupied/unoccupied thin and every wide lane mask; independent lane removal, rail restoration, center removal and whole-platform timer cancellation. P6-09/10/13. |
| Host range | 4 | Four headings with fourteen probes each: inclusive range8 rectangle around `otherEnd(L)`, z±1, only L actual full-height columns, world-height edge, any structure voxel, dedup and stable direct Station choice. P6-11/12. |
| Host graph | 8 | Reparent, descendant target, recursive child disconnect, same-host no-op, unresolved target, self/dangling/cycle rejection. P6-03/11. |
| Costs | 19 | Base-ground/water/elevated/underground platform quotes, Int32 product boundaries, relative daily upkeep, demolition genre, Int64 station price and no-booking lane/passage/stair/removal companions. P6-04. |
| Drawing | 296 | 192 stair +96 passage +8 thin selectors/crops/anchors/pass orders. All three materials, day/night, cardinal headings, stair up/down/roof/width and passage bridge/width. P6-07/09/15. |
| Passage/stair lifecycle | 24 | Three materials: legal empty path with no fabricated floating rail, orthogonal endpoint flags, parallel/single-cell rejection, forward stair precedence, pure portable preview versus source invalid cast. P6-14/15. |
| Stop positions | 8 | L4/N3–6 in both directions, including toward-zero negative division. P6-19. |
| Yard calls | 14 | Exact P5 `yards` IDs, inputs and expectations, independently recalculated here: host/index gates, first/later/wait/pass/forward/reverse/TURNING order. P6-19/22. |
| Capacity | 7 | Per-car seated fallback, mixed aggregate, packed/normal/Outside distinction, zero and checked overflow. P6-18. |
| Boarding | 10 | Packed cap, amenity0/50/100/200, repeated departures/decay, zero/depleted supply, placed-head origin, float32/truncation and checked initial Int32 product. P6-18. |
| Fare | 7 | 2D/3D truncated distance, 500,000 and1,170 receipts, absent/zero-distance origin, repeat unload, checked Int32 boundary and side-effect order. P6-18/19. |
| Hourly decay/averages | 8 | Float32×0.8/0.9996 and daily factor24×(1−0.9996), including values beyond float32's exact integer range. P6-17. |
| Demand tables | 42 | All42 source arrays/**1,008 cells**, raw percentages and base125 truncation. P6-16/P7. |
| Provider calendar | 200 | Ten providers × weekday/Saturday/holiday/vacation/Sunday ×07/17 × entering/outgoing; school/leisure residents and vacation division, BusinessInn shared table, Bar Sunday holiday mapping. P6-16/P7. |
| Nested providers | 4 | Child truncation before multiplication/addition, persistent wrapper and overflow; controlled provider boundary, not constructible city gameplay. P6-16/P7. |
| Listener lifecycle | 14 | Both creation orders, strict sphere/inclusive cube, diagonal/z/ordinary/special edges, fifth replacement, tie and removal asymmetry. P6-16/25/P7. |
| Listener demand | 5 | Station split, land/XY/height cap, special uncapped demand and checked overflow. P6-16/P7. |
| Timer lifecycle | 4 | Immediate registration, aligned station hour/midnight versus platform relative24h, separate removal cancellation. P6-04/17. |
| Equal-time FIFO | 6 | Explicit due/order with waiting/departing trains, hour/day ordering, today/yesterday assignment and600,000+720,000 costs. P6-17/19/25. |
| Transport logs | 2 | Uniform and nonuniform seven-day fifth-root accumulation/Saturday fifth-power result. P6-17/23. |

The artifact binds EXAV-SCN-008–020/030/031 and EXAV-SCN-D01 without changing their
original inputs or expectations. Each link names concrete fixture IDs. SCN009's
rule-file/turning implementation, SCN014's moving expense, SCN020's calendar/P7
ConstructionSite and SCN031's complete bridge renderer remain cross-phase
companions; the links credit only the yard/timer/fare/draw boundaries represented
here. They do not claim that a P6 fixture implements those whole scenarios.
P6 station creation is immediate, with no ConstructionSite.

Per-car negative or excessive seated capacity falls back to that car's normal
capacity **before** the aggregate packing clamp. Thus SCN010's normal100/seated−1
means seated100/packed100, while seated0 means packed200. The handoff's aggregate
clamp description does not authorize clamping an invalid individual car to zero.
At L4, N3/4/5/6 gives same-heading indexes2/3/3/4 and opposite0/0/0/−1. Five cars
can stop in both directions; six cannot. The fourteen yard witnesses retain
controller/handler changes before station effects and forwarding boarding
suppression before the central sentinel reset.

SCN-D01's fifth closer station appears in the listener's ordered local list but
receives no reverse registration. The source-only removal diagnostic also shows
that removing this unregistered newcomer cannot notify that listener, leaving a
stale local reference. This is explicitly labeled `danglingAfterSourceRemoval`,
not an admissible saved graph. P6-16/25 must reconcile actual removal with
STATION-D003's preserved asymmetry and its prohibition on dangling references;
these literals do not normalize the graph or decide a new runtime policy.

The [P6-16 implementation](web-parity-station-demand.md#catchment-and-source-anomaly)
now records the runtime disposition: preserve the missing fifth reverse edge,
but clear the removed station's local ID without finding a replacement. Original
source fixtures and their dangling diagnostic stay unchanged.

All six material/lighting variants of stair index23 retain the authored
`(388,4,32,36)` crop outside336×153. EXAV-GAL-001 remains with P6-15/27; no guessed
replacement, corrected asset or native fidelity is approved by these fixtures.

The checker validates explicit matrix IDs, independently recomputed outputs,
source hashes/excerpts, prerequisite hashes, exact yard reuse, scenario/difference
bindings and evaluator input immutability. Separate reviewed input/mapping
fingerprints prevent changing a matrix input, family source/owner or scenario
link and merely recomputing its expected result. Expected outputs are excluded
from those input fingerprints so semantic corruption must be detected by the
oracle. Double-precision nonlinear logs allow1e−12 relative/absolute error;
integer and float32 arithmetic witnesses retain exact values.

Verification on 8 November 2026 (.NET SDK10.0.402, Deno2.9.8, Playwright1.64.1):

- Default and raw-Git fixture checks: **821 cases/38 files pass**;
  `tmp/p6-02-fixtures.log`, `tmp/p6-02-source.log`.
- **16 oracle regressions pass**, including every family's rehashed output and
  missing-case corruption, changed inputs/sources/owners/scenario links, six
  invalid crops, float32 and FIFO contrasts, and input immutability;
  `tmp/p6-02-oracle-tests.log`.
- Full Python suite: **221 run,220 pass,one existing skip**;
  `tmp/p6-02-python.log`. P6-01 source review and project boundaries also pass;
  `tmp/p6-02-review.log`, `tmp/p6-02-boundaries.log`.
- Full Release backend suite: **1,667 passed,zero failed/skipped**;
  `tmp/p6-02-dotnet-final.log`.
- Client unit/retention tests: **315 passed** (313 ordinary plus two retention);
  `tmp/p6-02-web-unit.log`. Generated contracts, content, locales,
  Svelte/TypeScript and production build pass; `tmp/p6-02-web-build.log`.
  The existing large-chunk advisory remains.
- Focused Chromium: **19/19 passed**; `tmp/p6-02-browser.log`. These regress
  historical-harness station draft/focus/conflict fences and canonical shared
  Simple/TAT/signal/TURNING continuation in EN/JA. They do not execute real
  P6 station/passenger gameplay. Initial sandbox MSBuild IPC failure is recorded
  in `tmp/p6-02-dotnet.log`; successful backend/browser runs used local IPC and
  loopback permission. No other-engine/native/device, container, optional-suite
  or P6 capacity acceptance is claimed.

Reproduce from the repository root:

```sh
python3 scripts/station-fixtures.py --check
python3 scripts/station-fixtures.py --check --source-root /path/to/freetrain-exav-archive
python3 -m unittest discover -s scripts/tests -p test_station_fixtures.py
python3 -m unittest discover -s scripts/tests
python3 scripts/review-stations.py --check
python3 scripts/check-project-boundaries.py
dotnet test FreeTrain.Online.slnx -c Release --no-restore -m:1 -nodeReuse:false -p:UseSharedCompilation=false
DENO_JOBS=4 deno task --cwd web test
deno task --cwd web build
deno task --cwd web test:browser:run station-editing.spec.ts fleet-controller-manager.spec.ts signal-integration.spec.ts
```

Fixture file SHA-256:
`c6d99550567d4343f11800b00bdd8b74973839b555c092ebb33cfe26713a3566`.
Internal canonical-JSON digest:
`77a19a6a050f007009d03481154926079bcd7dd047a01d423ed7a713b683581b`.
Production consumers must load these literals in their numbered implementing
slices; production output must never generate their expected values.
