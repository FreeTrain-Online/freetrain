# P7-02 independent city fixtures — 11 November 2026

The [literal fixture set](parity/city-fixtures.json) supplies **703 cases in
29 families**, bound to **197 pinned source/manifest/project files** from stable
ExAV 2.1.1.12/V2, Git `883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, `trunk/`.
It consumes the [P7-01 decisions](web-parity-city-contract.md) without changing
them. This completes independent fixture/specification scope. City gameplay,
persistence/transport consumers, galleries and browser acceptance remain with
P7-03 onward. Save10/protocol9, content identity, limits and shipped EN/JA text
are unchanged.

Expectations are source-derived literals, authored separately from production.
The [read-only oracle](../scripts/city-fixtures.py) imports no simulation,
renderer, content compiler or other fixture evaluator. It generates no expected
outputs, executes no legacy assemblies and writes no files. Ordinary checking
uses pinned review evidence and raw bundled BMP bytes; `--source-root` additionally
checks original Git source/resource objects and exact line excerpts. It never
uses corrected archive checkout bytes as authority. These checks establish source
calculations and artifact integrity, without original-runtime observations.

| Family | Cases | Boundary / later consumer |
| --- | ---: | --- |
| Definitions | 225 | All 66 commercial definitions and distinct minimum/midpoint/maximum heights of all 55 variable definitions; exclusive bounds, Int64 prices, integral price-per-area, demolition, multiplier and computer eligibility. P7-03/07/10/12. |
| H/V coordinates | 8 | Origin shift, negative arithmetic shift, parity and the public read's +1/+1 versus deposit/rho's unshifted indexes. P7-06. |
| Heat traces | 11 | Initial zero buffers/interior-only bare rho, even/odd impulses, road/shoulder/sea/mountain, negative clamp, decay and border values alternating between retained buffers; cast-before-times10 public values. P7-06. |
| Conductivity | 9 | Ascending-column overwrite by every Traffic, including null road; road > sea > mountain priority and source float32 shoulder average. Notification-path acceptance remains explicit. P7-06/21. |
| Land trading | 6 | Empty/shared/protected/already-owned cells, nested demolition, native preview discrepancy, ordered receipts and sale at current value. P7-15. |
| ULV / cache | 5 + 2 | Ground/water/ownership rejection, unique entity sum; non-null cache stays stale, null retries. P7-25/31. |
| Road masks/art | 45 | All 15 masks × three bundled styles, attach/detach, Standard15 and A3 crop aliases/anchors, retained style, before-car pass. P7-21/22. |
| Road routes | 10 | New endpoint bits, existing masks/zero new-road charge, Traffic crossing, car rejection, train versus other-car removal, water/elevated/underground quotes, same-cell rejection and retained rail. P7-21/23. |
| Fixed footprints | 32 | Full exclusive occupancy, invisible middle/interior cells, cuts, flatness/bounds/reclaimability including an invisible-cell collision; original inverted generic-controller diagnostic versus positive port legality. P7-03/07/08. |
| Variable recipes | 18 | Top-first precedence, descending top indexes, bottom/middle seams, overlay, short/overlapping/tall heights and base-only cut. P7-10. |
| Half structures | 27 | Side/axis compatibility, no second spend, initial listener snapshot, partial versus final demolition, combined structure price and strict cut/back/highlight/fore order. P7-11/13. |
| Ground costs | 25 | Raw/wrapper callers, fence side overload, skipped cells, rice/vinyl per-success plus area spend, reversed endpoints and grid-difference snapping. P7-16–20. |
| Construction gates | 16 | Weekday hours9..17 inclusive, weekend rejection, weekday holiday allowed and below/above floor dependencies. P7-04/27. |
| Construction traces | 5 | One-voxel four-stage completion, constructor door, decorative machine branch, denied-gate retry and abandonment/cancellation; exact stream/bound/value order. P7-04/27/31. |
| FIFO | 4 | Equal-time station reset/development/heat/construction/departure order and cancellation. P7-04/14/25/31. |
| Growth arithmetic | 3 | Source strength24/budget7, land20 price200000–350000; zero/large companions and search amplitude. P7-25/26. |
| Candidate indexes | 8 | Source halving searches, surprising upper endpoint, duplicate prices, one-per-pool/no-price End without draw and explicit empty-array invalid state. P7-25. |
| Replacement | 8 | Owned/site protection, strict subsidiary threshold or alternate land-price condition, inclusive ordinary-land threshold. P7-25/26. |
| Valuation | 3 | Piecewise profit and cumulative market-price updates; distinct variable-height entity value. P7 valuation prerequisites/P8 markets. |
| Arithmetic | 9 | Int32 product boundaries and source unchecked diagnostics versus atomic checked port rejection. P7-03/04/06/13/28. |
| Settings | 20 | Cadence/registered-interval distinction, zero/negative/fractional bounds, reciprocal power, float32 range, zero-base power, NaN/Infinity text admission and context-dependent finite values. P7-28. |
| Completed providers | 27 | Residential06/07/08 weekday/Saturday/weekday-holiday, child truncation before variable-height multiplication, residents/outgoing/incoming and catchment cap; no provider during construction. P7-07/10/13/14. |
| Weekly station scores | 2 | Uniform32 and nonuniform243 fifth-root/fifth-power reports, reset fields and development-budget truncation. P7-14/25. |
| Raw art/alpha | 145 | All 137 reviewed BMP surfaces plus eight road/construction crops: source hash, extent, key, RGBA digest, opaque/transparent counts and probes. P7-08/10/11/17/20/22/33. |
| Fixed sprite loader | 2 | Exact 2×2×3 top-hat/front-edge crops/anchors/invisible cells and clipped single-cell top. P7-08. |
| Controlled random flows | 15 | Forest count/coordinate/pattern draws and coordinate-only sort, search/retries/zero amplitude, land size/selection, conditional markets/timing and 2×2×2 constructor z/x/y door-delay order. P7-04/16/17/25/26/31. |
| Automatic plans | 6 | Land plan value versus final price, immediate land versus timed structures, unique replacement cost, minimum/maximum variable height and unowned/no upfront booking. P7-25/26. |
| Station land-value effects | 7 | Boarded population deposit, capped unloading deposit, changed frequency, empty-total loading, zero unloading, train counts, unload-only global event and absence of active bundled traffic subscribers. P7-06/13/14. |

The source list carries P7-01's 193 exact records plus four pinned P1/P2
dependencies: default 3D sprite loader, Clock, Time and PriorityQueue. An additional
**263 immutable P6 cases** are bound by individual input/output hashes: all42
demand tables,200 provider calendar cases,14 listener lifecycle cases,five demand
caps and two weekly logs. They are inherited companions, excluded from the703
new-case count. Run the P6 oracle alongside this one; neither imports the other.

All EXAV-SCN-017–024/030/031 retain their original expected hashes. Concrete P7
fixture links and seven acceptance companions specify real constructed providers,
SCN-D01 creation order/removal, EXAV-CITY-GROW, search phases, vertical/FIFO
construction, settings, actual booking callers and the future inspected gallery.
These are independent acceptance descriptions, not executed city scenarios.
The eight random contracts name saved streams and branch order for construction,
development, forest, land selection, subsidiary timing/markets, local catalog
randomization and coordinate-seeded creeks. They distinguish durable streams
from local previews/UI and do not assert seeded equivalence to unsaved desktop
`System.Random` state.

Several literals prevent plausible but unauthorized corrections:

- A shared reclaimable entity of value50, removal fee7 and land10/20/30 has native
  preview80, ordered commit spends50,7,10,20,30 and authoritative quote117.
  CITY-D004 stages that actual traversal; resale uses live value, not acquisition.
- `FindLastIndexOf([100,200,300,400],350)` returns3, pointing at400. The ordinary
  selection work array is `[1,3,1,3]`; `last-first` still excludes its last index.
  Single-entry pools produce n0 and End without a selection draw. Empty arrays
  are explicit invalid states; these fixtures approve no fallback candidate pool.
- The fixed-structure class summary's base-only drawing description is outdated.
  The pinned default loader actually supplies top hats and bottom front edges;
  middle/interior invisible cells still occupy the entity's full volume.
- The one-voxel09:00 site, controlled delay draws0 and ground-branch draw1,
  reaches bone1/bone2/bone3/walled at09:40/10:20/11:00/11:40, once. Southwest
  constructors omit the door draw; other cells draw door before delay. Fixed
  commercial provider attachment precedes subsidiary timing in its base
  constructor; variable/half constructors do timing before provider attachment.
- LandPlan.value is `price*4`, while its strength-reduction final price is
  `price*selectedArea`. Compatible second-half addition keeps the initial
  population listener and does not refresh market valuation or spend again.
- Construction ignores weekday holidays but rejects weekends. A frequency
  change10→4 keeps the captured600-minute callback interval and changes deposit
  multiplier2.5→1. Settings cases check branch-local arithmetic hazards;
  `portAccept` means that local guard passes, not approval of a complete settings
  candidate. P7-28 must check every reachable power/cast/product against admitted
  world/content values; no balancing range or blanket finite-value approval is
  introduced here.

Source outcomes, source diagnostics and the existing CITY-D004/008/010/012/013
atomic/deterministic/legality adaptations are labeled separately. SCN-D01's
source dangling diagnostic stays in the inherited fixture: P6's delivered
remove-ID-without-replacement adaptation remains the admissible saved graph.
Ordinary float32 traces are exact; only nonlinear weekly doubles permit1e−12
relative/absolute comparison. No legacy CLR out-of-range floating cast is
promoted to a portable numeric guarantee.

The raw BMP fixtures describe original pixels and top-left keyed alpha before
hue/night transformations. Explicit night override files are independent raw
surfaces. The later gallery must additionally check transformed keys, authored
crop/anchor mappings, seasonal variants, cuts, alpha picking and composed layer
order. Raw hashes supply no inspected-gallery, native pixel-identity or optional
suite acceptance. Stadium gameplay remains P8; P3 creek/forest behavior and
P6 station/provider qualifications retain their recorded scopes.

The outer canonical JSON digest detects accidental changes. A separately pinned
matrix digest binds every case ID/input/classification, source/excerpt, family
owner/derivation, scenario link, random contract and inherited/acceptance mapping.
Expected outputs are excluded from that pin: re-signing a wrong expected result
must still fail source recomputation. The integrity tests exercise that failure
for every family, missing cases, mapping changes, exact float32 contrasts,
read-only imports and evaluator input immutability.

Reproduce from the repository root (Python standard library only):

```sh
python3 scripts/city-fixtures.py --check
python3 scripts/city-fixtures.py --check --source-root /path/to/freetrain-exav-archive
python3 -m unittest discover -s scripts/tests -p test_city_fixtures.py -v
python3 -m unittest discover -s scripts/tests
python3 scripts/review-city.py --check
python3 scripts/station-fixtures.py --check
python3 scripts/check-project-boundaries.py
deno task --cwd web check:generated
deno task --cwd web check:content
deno task --cwd web check:locales
```

Fresh verification and artifact hashes are recorded in the checklist and
`tmp/p7-02-validation.json`. Production consumers must load these literals in
their numbered implementing slices; production output must never generate the
expected values. This specification-only change requires no save/protocol
version and receives no .NET city-runtime, browser/native, capacity, optional-pack
or phase-exit credit.

Verification on 11 November 2026 (Python3.15.2, Deno2.9.8): offline and
original-Git checks pass for all703 cases/197 files; all20 new oracle regressions
pass. The full Python suite ran259 tests: **258 passed, one existing owner-supplied
historical-sample skip**. P7-01 source review, P6's821-case source oracle, project
boundaries, generated contracts, bundled content and both locale checks pass.
New-file whitespace, local report/index links and `git diff --check` pass.
No .NET or browser execution is claimed for this specification-only change.
Logs: `tmp/p7-02-{fixtures,source,oracle-tests,python,review,station-fixtures,boundaries,generated,content,locales}.log`.

Fixture file SHA-256:
`5a52c89b264a896465005f58a1522dd54f4d47fcb2f3d80856be28040e85933d`.
Internal canonical JSON digest:
`26d1b6a124a054e33d15721c8224b6c95dcaff7322b74c780f396d0983b86e83`.
