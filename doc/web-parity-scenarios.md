# Stable ExAV parity scenarios

**P3-06 runtime slice — 29 October 2026:** [Empty-world creation evidence](web-parity-empty-world.md) now covers source-default and custom flat worlds, zero rails, candidate cancellation/Owner confirmation, roles, stale fences and Save6 reopening. This is empty-world runtime evidence; the broader source-scenario/city/generated/editing matrix remains open.

**P0-06 source-scenario specification complete — 21 October 2026.**
[The maintained scenario set](parity/exav-scenarios.json) contains **31 scenarios,
38 hashed source files, six bilingual workflows, three proposed city profiles,
two integration companions and a full-content gallery specification**. It reads
stable ExAV **2.1.1.12 / V2**, archive Git
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, SVN r115, subtree `trunk/`.
The [baseline policy](web-feature-parity.md) and owner-approved source corrections
remain in force. This review uses pinned Git objects, preserving the archive's
original bytes even where its working tree has approved corrections.

This closes scenario design and source reassessment. **Fixture implementation,
legal instantiated city validation, simulation/browser execution, original-runtime
comparison, measured capacity and native/device acceptance remain open.** P0-07
[coverage/estimates reporting](web-parity-report.md) and P0-08
[optional source/specification](web-parity-optional-plugins.md) are now complete;
P0-09 [source integration](web-parity-exav-integration.md) is complete; overall P0 remains open. No game, protocol, content, save version,
service limit or distribution input changes in this batch.

## P3-01 terrain fixture handoff

**27 October 2026:** [terrain contracts](web-parity-terrain-contract.md) add
[77 literal source/adaptation/generation vectors](parity/terrain-fixtures.json),
bound through [40 pinned files and decisions](parity/terrain-review.json).
They use `TERRAIN-*` IDs and retain classification; they do not alter the existing
31 `EXAV-SCN-*` denominator or count new generation heuristics as source parity.
The read-only `scripts/review-terrain.py` checks specification arithmetic and
provenance; whole-world production import, editing/rendering/save/browser and
generated-topology execution remain P3-03–17. Water remains the simple single
global plane per owner direction. P3-01 source specification can close while
the phase's runtime and original-runtime acceptance stay open.

## Independent expectations

Each `EXAV-SCN-*` record has inputs, expected outcomes, a derivation, phase owner,
source symbols/hashes and `specified` status. Expected values are maintained
separately from the modern implementation. The checker verifies provenance,
references, selected arithmetic and ring geometry; its passing result is not
simulation parity. P0-04's expanded tables remain the detailed geometry matrices,
and P0-05 retains the serialization/version/authority contract. Their review IDs
are reused through explicit artifact links rather than copied with new counts.

| Scenarios | Source-derived acceptance and significant changes |
| --- | --- |
| 001–003 | Eight directions, unsupported cardinal corner, inclusive three-cell rail route, preview/commit booking and ground-relative cost. Three empty ground-level cells cost **18,000,000**; preview spends nothing. Raised/buried/water cases distinguish cost lookup from actual placement legality. |
| 004–007 | Stable car IDs through reversal, occupied-cell emergency stop, free reverse path at dead end, prior-location speed-limit dispatch and outside-world exchange/retry. Restrictions use explicit 4/3/2-minute branches; base speed 5 can accelerate under these source branches. Placement and outside steps use base speed. |
| 008–009 | Fixed/daily/advanced dwell, forwarding sentinels, TURNING and `.ftdd` boundaries. Arrival exactly at a daily departure waits **1,440 minutes**. Forwarding departures ring the bell without boarding. Diagram import appends ordered `RuleCollection` entries using binary source layout; commented SOAP is not active behavior. TURNING has its own spatial/identity/timer matrix. |
| 010–012 | Per-car seated fallback, packed capacity, amenity and hourly recovery. Normal 100 / seated 40 permits **160 packed passengers**. Three departures around an hourly tick yield gone counts **160 → 320 → 256 → 416** and 480 total exported passengers under the specified population/capacity. |
| 013–015 | Configured fare, movement cost and separate daily station/platform upkeep. 100 passengers × fare 500 × integer distance 5 × 2 yields **500,000**. Three cars / 39 passengers cost **122,000 on moves 1, 5, 9, 13 and 17**. Starting cash 10,000,000 plus that fare less five charges yields **9,890,000**. |
| 016–018 | Aligned hourly/midnight timers and old-handle migration/removal; ordinary/special reach, integer distance and footprint/height caps. Station creation at 08:37 first ticks at 09:00. Ordinary population is divided among attached stations then capped by land value × footprint × max(1, height/4) + 10; special population bypasses that cap. |
| 019–021 | Residential weekday/weekend/holiday and weekly-table routing; 365-day calendar, construction working hours, insertion-ordered equal-time callbacks and returned-wrapper cancellation. Source fixed holiday dates are game rules, independent of today's host calendar. Construction's time gate permits weekday holidays and 17:59, rejects weekends and 18:00. |
| 022–024 | ULV ownership, entity deduplication, development strength/iteration budget, V2 price window and controlled construction progression. Imported 100 / exported 200 yields strength **24**; land value 20 yields price range **200,000–350,000**. Explicit random draws give a one-voxel completion trace; no unsaved static RNG continuation guarantee is invented. |
| 025–026 | Debt due, daily/monthly history arrays, **April 1 fiscal year** rollover and age-dependent train resale. Price 1,000,001 resells for **500,000 / 500,000 / 333,333 / 250,000** at ages 0/364/365/730 days; missing purchase time yields no receipt. |
| 027–030 | Bank term truncation, deposit due versus renewal, anomalous bank totals, stock fees/price impact and transport reports feeding growth. A 364-day bank term has zero whole years; it must not silently receive fractional-year interest. Buying 10 then selling 4 units in the stated stock case leaves cash **938,996**, six owned units and price **10,060**. Transport's weekly mean is a fifth-power mean, not an arithmetic passenger average or distinct-train count. |
| 031 | Traffic draw ordering through before/car/after passes; expanded cut, picking, seasonal/color/alpha and direction cases reference the P0-04 gallery. |

All money values above are raw source amounts. Display currencies/units need the
slice's reviewed localization mapping; historical prototype prices and units are
not defaults. Integer truncation and float32 boarding/decay matter. Numerical
vectors stay in ordinary non-overflow ranges; source integer multiplication before
conversion to `long` remains a separate boundary decision.

## Source anomalies and explicit decisions

These observations do not authorize corrections. The scenario JSON carries
`EXAV-SCN-D01`–`D04` with phase owners and acceptance follow-through:

- **D01, P6/P7:** listeners attaching to existing stations use integer spherical
  reach `<10` / `<20`; newly created stations advertise an inclusive axis-aligned
  cube and `advertiseStation` does not recheck reach. Replacing the farthest of four
  stations removes its listener but does not register that listener on the new
  station. Test creation order, boundary/corner/z positions, a fifth station and
  save/reload before choosing portable behavior.
- **D02, P8:** bank asset recalculation enumerates debts for both debt and deposit
  totals. Debt cancellation recalculates before removal. Keep source outputs and
  any approved fix distinguishable in reports, cancellation and continuation.
- **D03, P5:** when neither train direction connects, `clockHandler` can leave the
  next state null and then dereference it during scheduling. The successful
  free-reverse trace does not establish disconnected-track continuation.
- **D04, P1/P8:** fare/trading/cost integer intermediates and bank whole-year
  division require explicit boundary decisions. Do not silently substitute wider
  arithmetic, fractional interest or translated units for source behavior.

Other open differences, including `EXAV-GAL-001`'s invalid stair crop and existing
geometry/action/persistence deltas, retain their owners. A source exception or
invalid resource is a diagnostic case, not a passing playable fixture. TURNING
spatial vectors, full dependency/control detail, and expanded subsystem traces
must ship with the relevant feature before it is called verified.

## Legal city and full-content fixture specifications

The previous budgets remain **proposals**, re-evaluated with an explicit ExAV
recipe. `EXAV-CITY-S/M/L` specify 64×64×16 / 128×128×32 / 256×256×64 worlds,
8/32/128 trains, 32/256/1,024 cars and target structure budgets of
200/2,000/10,000. They are not supported limits or generated save files.

A 16×32 H,V storage-grid module contains a 36-cell octagonal rail ring at
ground z=2, water=0,
with cardinal/diagonal segments and 45-degree turns. The JSON fixes every vertex,
construction order and formation budget. Two formations start 18 cells apart
on each occupied ring. Anchors use `World.toXYZ(16*column, 16+32*row, 2)`;
vertices are local X,Y offsets, not H,V positions. Independent checking confirms
unique rail cells, turn angles, transformed storage bounds, non-overlapping modules,
source train-border bounds and room for both formations. This geometric check does not validate a selected contribution's
footprint, source placement predicate, platform host or terrain occupancy.

Before collecting capacity evidence, P2/P4/P5/P6/P7 must bind exact bundled
contribution IDs/compositions and hashes, create actual terrain, place stations
and platforms with valid stop positions from both directions, and place population
structures without rail/support/road overlap. Every target count must reconcile;
an inability to fit the structure budget requires a recorded budget revision,
never silent overlap or dropped items. The legality gate checks reciprocal
connections, bounds, slope/support/hole occupancy, unique IDs, train occupancy,
platform hosts and listener/catchment order. Record the accepted fixture hash.

`EXAV-CITY-NET` adds a connected shared network with junction/signal, eight-voxel
slope/support chain, elevated platform, bridge/tunnel, dead end, exit/return and
multiple trains. `EXAV-CITY-GROW` adds free development parcels, controlled random
draws, construction and finance/report due events. Both require implementing
slices to validate instantiated legality. Use paired uninterrupted and save/reload
traces before and after scheduled events, with due times, registration order,
entity/contribution IDs, random draws, passenger counts and accounting entries.
The old 0.4/0.6.2 saves remain structural samples, not stable runtime fixtures.

`EXAV-GALLERY-BASE` expands the P0-04 offline gallery into a runtime catalog fixture.
Account for all **483 declaration/generated-child records, 56 manifests and 238
media** through the content review. These are accounting denominators, not 483
placeable pictures. Enumerate reachable visual recipe/frame/direction/height/color/
season/night/alpha keys; give factories/support wrappers/audio explicit outcomes.
Paginate isolated scenes using actual footprints. Preserve invalid-crop diagnostics
and inspect native-size composition, picking and occlusion. Measure full-catalog
cold decode separately from active-city rendering. Optional pack identities,
outputs, notices and fixture results remain separate under the
[P0-08 selected profile and acceptance contract](web-parity-optional-plugins.md).
Optional runtime gallery, legality and save-continuation fixtures remain P2/P10/P11 work.

## English/Japanese workflows and measurement

`EXAV-LOCALE-01`–`06` cover purchase/deploy/resale, station/platform passengers,
TAT/group/diagram editing, finance/history, authority/save recovery, and catalog/
settings/chapter help. Each links relevant `MAN-A*` requirements, scenario IDs and
browser destinations. Run both `en` and `ja`, then switch language with an open
draft. Cases include long Japanese/English names, zero/one/many interpolation,
large cash/negative balances, raw fare/capacity/rate units, wildcard/time fields,
source errors and missing-content recovery. Check 390×844 coarse and 1920×1080
keyboard layouts, 200% text, focus/Escape/Cancel, held-input reset, role/stale-draft
behavior, stable selection and canvas identity. Local navigation must keep shared
time running under the existing modern UX contract.

These are representative acceptance designs, not a completed translation ledger.
Each implementing slice still records Japanese text/hash and source identity,
English donor or new wording, meaning changes and review status for every delivered
string. Help must describe delivered behavior in both languages.

The named historical reference remains **MacBookPro18,2 / M1 Max / 64 GiB /
macOS 26.7.1 arm64**. This review did not re-detect hardware or run a browser.
Every new run must record actual hardware/OS, browser and tool versions, physical
or emulated display/scaling, viewport/DPR/canvas/camera, server/client allocation,
participants, RTT, implementation/fixture/content/random hashes and raw samples
in a fresh `FREETRAIN_ONLINE_PROFILE_EVIDENCE` directory. Keep UX3's production
1880×1040 canvas and matched 1106×470 comparison distinct.

Initial procedure: three cold-load/decode runs, five warmed 60-second samples per
profile/locale, and 30 pan/preview/panel/authoritative operations per sample;
measure paused and active simulation. Separate frame paint/interval distributions,
input/authority latency, simulation tick/backlog, bandwidth, decoded memory,
client/server RSS and save/reload time. Add 24 simulated hours, seven simulated
days for weekly reports/growth, dedicated fiscal/year events and P11 extended soak.
Provisional targets remain **≥30 fps**, local feedback p95 **<100 ms**, authority
feedback p95 **<250 ms at injected 100 ms RTT**. P2/P4 measurements must refine
limits and P11 must freeze release targets. UX3's historical **29.67 fps** remains
borderline/unmet; renderer stress is not ExAV legal-city capacity. Native Linux,
other engines and physical-device acceptance need their own evidence.

## Reproduction and handoff

From the repository root, with an explicitly supplied archive repository:

```sh
python3 scripts/review-exav-scenarios.py --source-root /path/to/freetrain-exav-archive --check
python3 -m unittest discover -s scripts/tests
```

The first command reads pinned Git objects only, checks all 38 file hashes and
symbols, IDs/manual/artifact references, selected numeric traces and octagon
geometry. It does not execute legacy assemblies or discover sibling checkouts.
The JSON is a maintained specification; the checker does not regenerate expected
values from modern code. Existing Python checks cover the previously delivered
inventory/review tooling and remain separate from the new gameplay obligations.

Validation on 21 October: the scenario checker and **93 Python tests** pass;
`git diff --check` passes. These are source/specification/tooling checks.

P1–P10 implement and execute these fixture designs with their feature slices;
P11 reconciles every scenario, expanded matrix, difference and locale workflow to
evidence. The completed [P0-07 report](web-parity-report.md) publishes
coverage/estimates using this distinction; P0-08 now specifies optional source
identities/profile and acceptance, with runtime fixture binding in P2/P10/P11.
P0-09 [integrates authority/scope](web-parity-exav-integration.md) and
cross-document decisions. The discovery ledger retains its inventoried statuses.
[Historical scenarios](parity/historical/scenarios.json) and
[prototype traces](../prototype/scenarios/README.md) keep their original provenance;
passing them cannot close these ExAV cases.
