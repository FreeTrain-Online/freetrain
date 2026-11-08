# P6-01 station/platform/passenger source contract — 8 November 2026

**Complete at source-specification scope.** This review implements no station
commands, gameplay, UI or save/protocol changes. Entry remains Save9/protocol7.
[P5's train/harbor handoff](web-parity-p6-train-handoff.md) is consumed as an
integration requirement, not evidence of working passenger stations. P6-02 owns
independent executable fixtures; P6-03–28 own implementation and acceptance.

Authority is stable ExAV **2.1.1.12/V2**, archive Git
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, `trunk/`. The
[hashed source contract](parity/station-review.json) binds original Git blobs,
line/symbol excerpts, operation semantics, dependency chains, decisions, controls,
contributions and a source-derived acceptance matrix. The
[text inventory](parity/station-localization-review.json) classifies EN/JA scope
and historical donor provenance without claiming wording shipped. Archive working
tree corrections and historical FreeTrain formulas do not supersede these blobs.

## Inventory and ownership

The review pins **106 source/manifest files and 102 binary/form resources**,
**12 operations, 14 decisions, four cross-system dependency chains, 78 source
control/action records and 26 acceptance descriptions**. All source hashes match
the P0 ledger. Assets include source/day/night picture and bell dependencies;
form resources remain inert bytes, never activated CLR objects. Exact native
rendering, hearing or historical desktop execution is not established.

All **99 P6-assigned declarations** retain an explicit disposition: **35 station
buildings, six departure bells, one StationList consumer, 54 P4 decorative
railStationary structures and three owner-deferred BGM entries**. Exact station
IDs, names, price, size, operationCost, class references and declaration XML are
retained individually. A decorative station-like accessory cannot become a
Station/PlatformHost. Optional non-free assets are outside this bundled review.

Demand closure includes all **17 core population classes**, **42 distinct
24-entry distribution/entering arrays across ten concrete providers**, and
**169 manifest population nodes**, including nested combinations, across the
bundled catalog. This exceeds the five historical weekday-demand examples in
the planning prose. Constants, multipliers, persistent references, weekly/holiday
and vacation branches are separate contracts. Stadium mutable demand and its
special listener are explicit P7 producer dependencies. P6 consumes source-bound,
labeled controlled providers/land values until P7 supplies constructible city
sources; there is no fixture population editor in the gameplay contract.

## Placement, ownership, costs and time

Station placement calls `StationContribution.create` directly, which executes
`new Station`. **There is no ConstructionSite or deferred station completion.**
The general `ConstructionSite` implementation is pinned as a negative dependency
and EXAV-SCN-020 remains a calendar/P7 construction companion. P6-04/06/25 must
verify immediate station registration and callbacks, not introduce a generic
construction delay. Station footprints require reclaimable occupancy and flat
ground at the base z; the station remains owned, non-reclaimable and value zero.

| Caller | Source booking or scheduling contract |
| --- | --- |
| Station place | Manifest Int64 price to RAIL_SERVICE, after creation; preview/commit must quote the same terrain/content state. |
| Station remove | Inherited PThreeDimStructure removal spends `500000 * size.x * size.y` to **SUBSIDIARIES**; no sale refund. |
| Platform place | `500000 * length * multiplier`, with Int32 intermediates before long return; base-cell ground only. If height<0, multiplier is height×−2, otherwise height+1. When ground≤z≤water and ground<water, price is zero. |
| Platform remove / lane add/remove | Restore normal rails and remove center voxels where applicable; no separate source spend/earn call. |
| Passage/stair place/remove | Cosmetic outlook changes; no spend/earn call. A zero quote does not mean illegal. |
| Station callbacks | First hour at `60-currentMinutes`, then hourly; daily at next midnight, then every24h. Definition operationCost to RAIL_SERVICE before daily log rotation. |
| Platform callback | First callback **24h after creation**, then every24h; `180000 * length` Int32 cost to RAIL_SERVICE. It is not aligned to midnight. |

Station and platform removal cancel their respective timers. Hour/day/train
equality uses the existing P1 FIFO scheduler and executing-entry cancellation
policy. Station legacy repeat-handler conversion is historical P10 import scope,
not a migration to add to current-format canonical saves.

Thin platforms replace aligned, unoccupied SingleRailRoads or empty Traffic cells;
curves, junctions, other special rails and cars reject. They own yard rail state
on the same traffic voxel. Roof cells satisfy `L/4 <= index < L-L/4`. Wide
platforms own a separate empty center strip plus initial right90 lane0 and optional
left90 lane1. Wide has no roof. Removal restores normal rails and disconnects
children; the service must enforce caller occupancy predicates atomically.

## Host selection and listener graph decisions

`STATION-D001/002` preserve range8 membership: inclusive rectangle expanded around
base and `otherEnd`, at z±1, plus full-height actual platform columns. `otherEnd`
uses L, not L−1. At `(10,10,2)`, east, L4, candidates cover x2…22/y2…18/z1…3
plus columns x10…13/y10 across all valid z. Any station structure voxel can
supply a candidate. Automatic construction selects a **direct Station**, while
properties may explicitly select an already connected Platform.

Source Set enumeration is Hashtable-unordered. The portable automatic choice is
stable station-ID order over the exact direct candidate set; this is a declared
deterministic adaptation, not a source nearest-host guarantee. Save the chosen
host. Reparenting detaches the old parent, clears self, disconnects a copied child
list recursively, then tests whether the requested host still resolves a station.
Children stay disconnected. A descendant target may lose reachability during that
sequence. Detached validation rejects self/cycles/dangling IDs and bounds walks;
it must preserve valid source child-disconnect effects without publishing partial
mutations. Removal does not automatically search for a replacement host.

`STATION-D003` retains **EXAV-SCN-D01**. Listener-first creation checks truncated
3D spherical reach `<10` ordinary / `<20` special; station-first advertising uses
inclusive ±10/20 cubes with clipped z and no spherical recheck. For a fifth,
closer station, the source removes the farthest station's reverse listener
membership and replaces the listener's local slot, but fails to register the new
reverse membership. Preserve that asymmetry and creation-order behavior. Persist
local ordered station references and station-side memberships separately; do not
rebuild nearest-four or demand reciprocal equality on load. Duplicate/dangling
references remain invalid. P6-16/P6-25 and P7 own consuming tests/handoff.

Ordinary contribution is `min(rawPopulation / stationCount,
landValue * XYSize * max(1,ZSize/4) + 10)` with source integer division/grouping;
special listeners divide raw demand by station count without that land cap.
Residents, current outgoing demand, entering demand and available waiting
passengers remain different values. P7 owns growth, land-value evolution and
constructible provider lifecycle; P6 owns these query and effect boundaries.

## Passenger arithmetic and yard order

`STATION-D005/008` consume TRAIN-D008 and supersede historical DEV-D01/02/03
formulas. Clamp seated into `[0,normal]`; packed is seated+2×(normal−seated).
Use `max(0,population-gone)` and then
`min(packed, trunc(available * amenity * 0.01f * 0.3f))`. Preserve the initial
Int32 multiplication, each float32 intermediate and toward-zero conversion.
Capacity is packed here; Outside uses `min(100,normal)`. Normal occupancy may
exceed100%; zero-capacity stock remains valid. Zero population increments the
train-event log and notifies before returning without invoking the train loader.

Boarding updates export/gone/loaded and landQ, then records the **current placed
head** as origin. Unloading clears passengers/origin and books
`passengers * configuredFare * truncated3DDistance * 2` when an origin exists.
The controlled P5 witness is `39 * 3 * 5 * 2 = 1170`. It then updates import,
train-event count, landQ using `min(import.LastWeekPerDay/24,r)`, unloaded
accumulator, nonzero global traffic notification, and station notification.
Missing origin earns zero. Use checked original-width integer operations and
reject invalid/nonfinite candidates with no train/station/account/queue changes;
Int64 account storage does not authorize widening source Int32 intermediates.

Hourly gone decay is float32×0.8, accumulated loaded/unloaded decay ×0.9996,
then truncation. Average factor is float32 `24.0f*(1.0f-0.9996f)`. TransportLog
is nonlinear: daily totals contribute a fifth root; Saturday raises their mean
to the fifth power. Preserve the actual daily/hour/train callback FIFO order and
all accumulator/log fields across save/reopen. No memo's0.9, fare600/div5000,
16-move cadence or18-unit platform upkeep is an ExAV default.

`STATION-D006` consumes TRAIN-D012: controller settings use **station location**,
with actual station ID/revision for host validity and yard rail location separately.
Resolve the effective shared train/group controller every call. Preserve absent
settings and same-location reuse, Simple's first30-minute wait, TAT's first10,
and daily-equality1440. Signals remain passenger-free. Manual stays owner-removed.

Yard gates precede controller invocation: same-heading stop index `(L+N)/2-1`,
opposite `(L-N)/2`, integer division toward zero, then index-range/actual-rail-index/
resolved-host checks. L4/N3,4,5,6 yields same `[2,3,3,4]`, opposite `[0,0,0,-1]`;
there is no blanket N>L rejection. Controller/handler mutation, including TURNING,
occurs before station effects. First positive result unloads once; first zero/
negative and later positive do nothing. Later −999/−1000 rings only; other later
nonpositive results board then ring. Central sentinel/reset/reversal follows
those effects; positive waits schedule without movement cost. Timing uses the
callback-entry head. Consume all14 existing `yards` witnesses through real
constructed hosts in P6-19, not substitute test harbors for gameplay evidence.

## Cosmetic passages, safety and open art case

Passages change thin-platform outlooks at right angles; logical host links are
independent. Source legality admits empty cells, but its builder has TODOs there
and creates no open-ended floating rail. Preserve that no-effect case
(`STATION-D014`); do not manufacture service infrastructure from sprite helpers.
Stairs prefer a forward passage neighbor, then backward. Concrete, brown wood
and green wood retain roof/up/down/width and before/car/after dependencies.

`STATION-D011` adapts `createStair(test=true)`: source skips replacement creation
but then casts/writes the old outlook as a StairPlatform. Preview must validate
without this cast or mutation; commit stages the new outlook only after all
predicates succeed. This is a portable safety adaptation with source neighbor
precedence intact.

**EXAV-GAL-001 remains unresolved with P6-15/P6-27.** Index23 (south, down,
double-width, roofed) asks for `(388,4,32,36)` in336×153 sheets, all three materials
and both day/night. Keep original selection, hashes and P2 diagnostics. This
review does not guess a replacement coordinate, approve corrected art or claim
native output. P6-02 pins the invalid witness; P6-15 must supply an evidence-backed
visual disposition before visual acceptance. This named follow-up does not block
specification closure and is not a new permission gate.

## Committed departure events

`STATION-D007` specifies P6-20's network adaptation. Source sound dispatch uses
the post-handler current Inside head, filters visible maps and queues at turn end
(max3,200ms spacing). The server instead stages bell intent inside the detached
successor and emits it only **after successful world publication**. The envelope
uses `(liveEpoch, committedPublicationSequence, effectOrdinal)` with train,
platform and bell IDs and source location. Distinct departures in one publication
have distinct ordinals; rejected callbacks and receipt retries emit nothing.

Create a fresh live epoch on process start/world replacement/reopen. Live
subscription starts with a baseline sequence at join/reconnect; drop old epochs,
pre-baseline events and duplicates. Snapshots, checkpoints and command receipts
do not replay effect history. Local viewport/gesture/mute/effects settings gate
playback independently on each peer; suppressed sounds never queue for later.
Audio cannot change the simulation. Six bell previews remain independent, and BGM
remains owner-deferred. P6-20/P6-25 own duplicate/retry/reconnect/replacement tests.

## Operation and MAN/DEV bindings

Each row is reciprocal with the [action review](web-parity-actions.md#p6-01-station-workflow-bindings)
and [manual reconciliation](web-parity-manual.md#p6-01-station-source-reconciliation).
Developer IDs refer to the maintained
[developer review](../tmp/web-feature-parity-developer-docs.md); detailed source
control records and owning operation IDs are in `station-review.json.actions`.
The original P0 inventories remain immutable inputs.

| Operation | Manual/developer obligations | Implementing owner | Controls and acceptance |
| --- | --- | --- | --- |
| `station-building` | MAN-T04, MAN-W03, MAN-A05, DEV-P16, DEV-P24, DEV-A06 | P6-03/P6-06/P6-07/P6-08 | station family; orientation previous/next; preview; Place/Remove; name; cost. All 35 station declarations, flat/reclaimable/bounds, immediate timers/registration, removal genre/footprint, exact name draft and peer revision checks. |
| `thin-platform` | MAN-T04, MAN-W04, MAN-A05, MAN-D02, MAN-E06, EXAV-10 | P6-09 | N/E/S/W; roofed/unroofed; length >=1 (native default Maximum100); Place/Remove. Each heading/roof/length, both draw passes and car overlap, retained traffic/accessories, occupied/stale rejection, normal rail restoration. |
| `wide-platform` | MAN-W04, MAN-W05, MAN-A05, MAN-A08, EXAV-10 | P6-10/P6-13 | wide style; length; two lane add/remove controls; Place/Remove. All headings, lane0 mandatory creation, either/both/no lanes after edits, occupied lane rejection, exact saved ownership and independent stop indexes. |
| `host-graph` | MAN-W05, MAN-A06, MAN-A08, MAN-D01, MAN-E06, EXAV-10 | P6-03/P6-11/P6-12/P6-13 | host selector; disconnect warning; name; bell selector; preview bell; Apply/Cancel. Literal rectangle (10,10,2), east,L4 => x2..22/y2..18/z1..3 plus x10..13 columns; reparent child disconnect, descendant selection, host removal/rebuild, stale host IDs and bounded cycle-safe validation. |
| `passage-stairs` | MAN-T05, MAN-W06, MAN-A09, MAN-E06 | P6-14/P6-15 | concrete/brown wood/green wood; Passage/Stairs; Place/Remove; line preview. All materials/headings/up/down/roof/width; legal empty path no fabricated floating rails; pure preview, adjacent passage removal, precise invalid index23 handling and no service/booking changes. |
| `providers` | MAN-W04, MAN-A16, DEV-P16, DEV-P22, DEV-P29, DEV-P30, DEV-P31, DEV-A05, EXAV-06 | P6-16/P7 | provider identity/location; resident versus demand/entering queries; controlled test inputs only. Independent table cells/calendar and nested truncation cases; actual provider bindings, residents distinct from demand, height/footprint scaling; no advertised fixture population editor. |
| `listeners` | MAN-A06, MAN-A16, MAN-E10, DEV-P29, DEV-P30, DEV-P31, DEV-A05 | P6-16/P6-25/P7 | current demand; waiting value; provider lifecycle. SCN-D01 creation-order corners/z/equal-distance/fifth/removal, preserve separate forward/reverse membership and load continuation; controlled land values labeled until P7. |
| `timers-logs` | MAN-A13, MAN-A14, DEV-P24, EXAV-05, DEV-P29, DEV-P31, DEV-A05, DEV-A06 | P6-04/P6-17/P6-25 | today/yesterday; averages; clock step/run; upkeep. 08:37=>09:00 station/next00:00 daily versus platform next08:37; FIFO hour/day/train equality, source 600000+720000=1320000, nonlinear weekly logs and exact timer cancel/reopen; P10 old timer conversion excluded. |
| `passenger-fare` | MAN-A07, MAN-A12, MAN-A16, MAN-E10, EXAV-04, DEV-P19, DEV-P24, DEV-D01, DEV-D02, DEV-A05 | P6-18/P6-19 | packed/normal occupancy; waiting/demand; board/alight logs; cash. EXAV-SCN010..014, 39*3*5*2=1170; above100% occupancy, zero/missing origin, repeated/drop supply, checked overflow rollback and fare before move cost. |
| `yard-controller` | MAN-W05, MAN-A07, MAN-A12, MAN-D03, MAN-E07 | P6-19/P6-22 | pick Station/platform harbor; existing Simple/TAT manager; shared group settings. Eight L4/N3..6 indices and14 yards witnesses through real hosts, first/later/pass/forward/reverse/TURNING, shared multi-platform key, signals passenger-free and Manual absent. |
| `departure-events` | MAN-M06, MAN-W05, MAN-O07, MAN-A08, MAN-A12, MAN-A15 | P6-20 | per-platform bell choice; preview; local effects/mute; gesture enable. Epoch+committed publication sequence+effect ordinal dedup; no snapshot/save history replay, join/reconnect subscription baseline, duplicate receipts/delivery/rejected callback silent, two peers hear independently eligible live events. |
| `information-list` | MAN-M02, MAN-M03, MAN-W10, MAN-A05, MAN-A13, MAN-O16, MAN-A15, MAN-E03, DEV-P16, EXAV-14 | P6-08/P6-23 | name Apply/Cancel; Information; actual waiting/demand; today/yesterday board/alight/train; sorted list; Move; station labels. EN/JA duplicate names/rename/removal/empty list, exact logs/averages, peer stale edits, narrow layout and retained selection/focus/canvas. |

## Verification and reproduction

The [checker](../scripts/review-stations.py) validates prerequisite hashes,
contribution/provider/resource completeness, exact action records, reciprocal
requirements, decision ownership, table excerpts/values, text provenance and
specification-only claims. Archive mode additionally reads all original Git blobs,
line excerpts, declarations, provider nodes, array values and resource bytes.
No desktop assembly is loaded. Default verification needs no external archive.

```sh
python3 scripts/review-stations.py --check
python3 scripts/review-stations.py --source-root /path/to/freetrain-exav-archive
python3 -m unittest discover -s scripts/tests -p test_station_review.py
python3 -m unittest discover -s scripts/tests
dotnet test FreeTrain.Online.slnx -c Release --no-restore
DENO_JOBS=4 deno task --cwd web test
deno task --cwd web build
deno task --cwd web test:browser:run station-editing.spec.ts fleet-controller-manager.spec.ts signal-integration.spec.ts
```

Fresh verification on 8 November 2026:

- Repository and raw-archive checks pass: 106 source/manifest files, 102 resources,
  all42 tables and169 provider bindings. Evidence: `tmp/p6-01-review.log` and
  `tmp/p6-01-source-verification.log`.
- All **12 evidence-corruption tests pass**, including changed policies, lost
  declarations/providers/actions/resources/requirements, altered table values or
  donors, and false runtime/shipped claims: `tmp/p6-01-corruption.log`.
- Full Python suite: **205 run, 204 passed, one existing skip**:
  `tmp/p6-01-python.log`. Existing train/railway source reviews,744 train fixtures
  and project-boundary checks pass in their corresponding `tmp/p6-01-*.log` files.
- Full Release backend suite: **1,667 passed, zero failed/skipped**:
  `tmp/p6-01-dotnet-final.log`. Release build succeeded. Initial sandbox runs
  could not create MSBuild IPC pipes or bind browser loopback servers; successful
  runs used the authorized local test execution outside that sandbox.
- Client unit/retention tests: **315 passed** (313 ordinary plus two retention),
  `tmp/p6-01-web-unit.log`. Generated contracts, bundled content, EN/JA checks,
  Svelte/TypeScript (zero errors/warnings) and production build pass:
  `tmp/p6-01-web-build.log`. Vite retains its existing large-chunk advisory.
- Focused Chromium: **19/19 passed**, covering historical-harness station draft/
  focus/conflict fences and real shared controller/signal/TURNING continuation
  in EN/JA: `tmp/p6-01-browser.log`. These are regressions of existing behavior,
  not tests of real P6 passenger stations. No other-browser/native/device,
  container, optional-suite or P6 capacity acceptance is claimed.

[P6-02 independent literal fixtures](web-parity-station-fixtures.md) now supply
the source-derived geometry/lifecycle/arithmetic handoff. Production consumers
remain with P6-03 onward. EXAV-GAL-001 visual correction, P7 city providers/evolution,
P8 full financial reporting, P10 historical imports and P11 integrated capacity
retain their named owners and acceptance boundaries.
