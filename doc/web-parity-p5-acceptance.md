# P5 integrated acceptance — 8 November 2026

**P5 is closed at bundled browser/portable train scope, with Manual driving
removed by owner decision. P6 may start from real saved trains and shared
controllers.** Performance, native/human fidelity, optional packs and later-phase
gameplay retain the named qualifications below. This review changes evidence and
handoff documentation; it changes no production behavior, assets, admission
limits or Save9/protocol7 contract.

Reviewed implementation: `b5c43eb66cec9027c8abdd258d66f53e2208637c`.
Authority remains ExAV 2.1.1.12, archive Git
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, subtree `trunk/`.
The owner requested moderate relevant web/unit testing after substantial hands-on
integration testing. Accordingly this acceptance combines the fresh selection
below with the explicitly historical slice evidence. It does not claim a fresh
complete regression or one new uninterrupted journey exercising every feature.
The owner's testing is context for that selection, not a fabricated capture or
criterion-by-criterion native acceptance record.

## Scope and trace reconciliation

The [source review](parity/train-review.json) enumerates all 18 operation families,
18 decisions, eight dependencies and 34 contributions. The freshly checked
[delivery inventory](parity/train-delivery.json) joins every contribution and
operation to delivered runtime/UI/test bindings, accounts for 638 source literals,
55 catalog texts and 172 bilingual UI keys, and checks all 325 admitted formation
length choices. Its Manual entry is an explicit removal, not implemented parity.
The [744 independent literals](parity/train-fixtures.json), in 17 families bound
to 20 pinned files, remain independent of production output. Source/fixture
checkers were run both against repository evidence and raw pinned archive blobs.

| P5 steps / operation families | Independent witnesses and delivered evidence |
| --- | --- |
| 01–03: source, fixtures, stock | [Contract](web-parity-train-contract.md), [fixtures](web-parity-train-fixtures.md), [delivery inventory](parity/train-delivery.json); six trains/eight cars/seven color templates, rendering448, numericSpeed33 |
| 04–05: state and transport | [Fleet state](web-parity-fleet-state.md), [transport](web-parity-fleet-transport.md); stable car identities/order, reciprocal occupancy, shared references, exact queue/codec continuation |
| 06–07, 10–11: trade/deploy | [Trading](web-parity-fleet-trading.md), [deployment](web-parity-fleet-deployment.md), [purchase UI](web-parity-fleet-browser.md), [deployment UI](web-parity-fleet-deployment-browser.md); sales5, source placement separate from ramp motion |
| 08–09, 12–15: movement/reversal/timing/Outside | [Movement](web-parity-fleet-movement.md), [moving art](web-parity-moving-stock-art.md), [recovery](web-parity-fleet-recovery.md), [timing](web-parity-fleet-timing.md), [Outside](web-parity-fleet-outside.md), [operations](web-parity-fleet-operations.md); guides17, ramps16, reversals24, callbacks20, outside83, costs1, numericSpeed33, timing7, queue2, drawing1, exits8 |
| 16–18: simple/manual/manager | [Typed controllers](web-parity-fleet-controllers.md), [Manual removal](web-parity-fleet-manual.md), [manager](web-parity-fleet-controller-manager.md); positive waits/sentinels, Simple station adapter, shared identity/copy/reference rejection; Manual retired |
| 19–21: tat-junction/tat-stop/tat-advanced/turning | [Basic TAT](web-parity-basic-tat.md), [advanced TAT](web-parity-advanced-tat.md), [TURNING](web-parity-fleet-turning.md); timetables20, turning36 and ordered handler/central effects |
| 22–23: groups/diagram | [Groups](web-parity-fleet-groups.md), [rule files](web-parity-timetable-rule-files.md); delegation/cycle guards, typed copy, bounded inert read/ordered append, portable export |
| 24–25: infrastructure/information-tracking/color-test | [Signals](web-parity-fleet-signals.md), [information/color](web-parity-train-information.md); signals9, train-owned effective controller, world-local colors and transient tracking |
| 26–27: integration/continuation | [Cross-tool](web-parity-fleet-cross-tool.md), [continuation](web-parity-fleet-continuation.md); gameplay-built trains, peer/role/draft/receipt fences, exact every-minute reopen and process restart |
| 28–29: capacity/distribution/localization | [Measured capacity](web-parity-fleet-capacity.md), [delivery](web-parity-train-delivery.md); legal scenes, explicit resource misses, real bilingual workflows, base/optional distribution separation |
| 30: integrated review/P6 interface | This report and [train/harbor handoff](web-parity-p6-train-handoff.md); controlled yards14 remain source/order inputs, with real station/passenger acceptance assigned to P6 |

SCN-004–009, 014, 026 and 031 are credited only for their P5 portions. The
source and runtime reports above retain reciprocal MAN-T06–08, MAN-M02/03,
MAN-W07–10, MAN-O08/09, MAN-A10–13/17 and DEV-P14/18–20/24/32 bindings.
Prepared declarations, optional synthetic fixture stock and trusted P4 test cars
are not substitutes for actual bundled purchases/deployment.

## Decisions and unresolved differences

This is a current disposition overlay on the immutable P5-01 specification.
Earlier reports describing a later slice as pending are historical scope records.

| Decision | Current disposition / acceptance owner |
| --- | --- |
| TRAIN-D001 | Portable both-null EmergencyStopping/retry retained; source reversal preserved. P5-12/13/27 tests cover pose, timing and continuation. |
| TRAIN-D002 | Explicit Simple root/fallback, delegation and atomic cycle/membership rejection delivered in P5-04/22. |
| TRAIN-D003 | Source deployment walk omits slope zdiff; live movement uses it. P5-07/08/11 retain separate witnesses. |
| TRAIN-D004 | Positive waits retain source StoppingAtStation; UI identifies signal/departure wait without inventing another state. P5-16/24. |
| TRAIN-D005 | Both editors restore actual day instead of month; distinct month/day round trips delivered in P5-20. |
| TRAIN-D006 | Ordered handler effects, -999/-1000 and TURNING early reversals delivered in P5-16/20/21. Actual yard unload/board/bell companion remains P6. |
| TRAIN-D007 | Positive Int32 native speed admission; no clamp of values above four. P5-03/13. |
| TRAIN-D008 | Checked source-width arithmetic and atomic rejection; normal source costs/fares/resale retained. P5-03/06/13/14; station-service integration P6. |
| TRAIN-D009 | Referenced controller deletion rejected with usage diagnostics; typed copy has fresh identities. P5-18/22. |
| TRAIN-D010 | Passing train's effective controller owns signal dispatch; reserved signal ControllerId stays null. P5-18/24. |
| TRAIN-D011 | Stored first-match rule order; branch-specific duplicate handling; no invented sorter/enable flag. P5-20/23. |
| TRAIN-D012 | Location-keyed harbor/junction settings and explicit junction cleanup retained. P5-19/24; real shared Station platforms/host resolution P6. |
| TRAIN-D013 | Typed clone, bounded inert legacy rule read and portable JSON export delivered P5-18/23. Native binary export is not promised; world import remains P10. |
| TRAIN-D014 | Superseded by owner's 7 November Manual removal. No remaining canonical input authority/lease implementation is promised. P5-17 retirement tests enforce rejection/absence. |
| TRAIN-D015 | Detached atomic fleet/occupancy/accounts/queue publication, transient listeners/camera and exact callback continuation delivered P5-04/26/27. P6 extends atomicity to station effects. |
| TRAIN-D016 | Declared bounded formation/fleet admission and legal measurements delivered P5-03/28. No group deletion; source permits moving contents. Resource targets remain P11. |
| TRAIN-D017 | Browser withdrawal before resale; internal sale teardown remains safe; absent purchase time earns no invented refund. P5-06/10/27. |
| TRAIN-D018 | Shared-within-world color-test apply, local preview/export and cross-world isolation delivered P5-25. |

Manual wording in the original P5-30/phase-exit plan is superseded by the linked
owner removal. Inert Manual source metadata and historical prototype code do not
grant a callable canonical provider. P5-MANUAL-001's former input limitation is
retired with the feature, rather than carried as a pending restoration task.

Remaining qualifications have explicit owners:

- **P11:** FLEET-CAP-001 (128-train simulation falls behind nominal time),
  FLEET-CAP-002 (command/publication latency/network load), P4-CAP-001/002,
  extended soak/full-city limits and final integrated release acceptance. P5-28
  measurements remain misses, not performance passes or new latency waivers.
- **P1:** final foundations coverage/phase review remains open; P5 runtime
  consumers do not close that separate review.
- **P2 / UX3:** original-runtime/human gallery fidelity, source-invalid crops,
  native IME/zoom/display/touch/accessibility and owner-deferred other engines
  retain their recorded qualifications. Inspected Chromium emulation is separate.
- **P2/P9/P10/P11:** canonical optional V2 repairs and older-suite compatibility.
  P5-29's synthetic source/prepared registration and distribution checks remain
  historical evidence, not canonical optional acceptance.
- **P6/P7/P8/P10:** actual stations/platforms/passengers/fares/bells, population/
  development, full economic systems and desktop world import. The handoff makes
  yard truncation, host resolution, capacities, forwarding and hourly ordering
  explicit before P6 implementation.
- **Separate stretch scope:** smooth-animation full acceptance and map rotation
  retain their own records; P5 closure does not close those enhancements.

## Fresh validation and inspected captures

Fresh checks on 8 November, .NET SDK10.0.402 / Deno2.9.8 / Playwright1.64.1:

| Selection | Result | Evidence |
| --- | --- | --- |
| Relevant .NET train/controller/stock/trading/continuation unit tests | 369/369, zero skips | `tmp/p5-30-unit.log` |
| Selected client fleet/stock/renderer/motion/information/save tests | 75/75 | `tmp/p5-30-client.log` |
| Source-review, independent-fixture and delivery Python unit tests | 26/26 | `tmp/p5-30-evidence-unit.log` |
| Selected EN/JA Chromium workflows | 16/16, zero skips, 57.7s | `tmp/p5-30-browser.log`, `tmp/p5-30-browser/` |
| Web production/generated/content/locales/Svelte | Pass; zero Svelte errors/warnings, existing bundle advisory | `tmp/p5-30-web-build.log` |
| Source/fixture/delivery checks, including raw pinned source | Pass | `tmp/p5-30-source-review.log`, `tmp/p5-30-source-pinned.log`, `tmp/p5-30-fixtures.log`, `tmp/p5-30-fixtures-pinned.log`, `tmp/p5-30-delivery.log` |

The unit selection freshly covers literal ramps/junctions/reversal/dead ends,
speed/costs, Outside exchange/return, multi-train collisions/equal-time order,
Simple/TAT/advanced masks/TURNING, shared controllers/groups, Manual retirement,
stock/resale and every-minute continuation. Browser runs reuse meaningful
existing cases: real infrastructure build → purchase → deploy → Run across a
ramp → inspect → withdraw/redeploy → peer reopen/checkpoint restart (EN/JA),
two-train exact-oracle recovery (EN/JA), shared manager and nested groups
(EN/JA desktop/coarse), information/Move/Follow (EN/JA, JA narrow) and trade/
draft-protected locale switching (EN/JA desktop).

The ring and manager browser inputs were regenerated by the fresh unit run as
`tmp/p5-30-ring.json` (plus `.expected`) and `tmp/p5-30-manager.json`.
Information tests use the existing gameplay-built P5-25 saved input; that input
was not regenerated here. All browser assertions executed freshly. Inspected
captures: `deployment-en.png`, `controller-manager/ja-coarse-configure.png` and
`information/ja.png` under `tmp/p5-30-browser/`. They show actual rail/deployment
controls, reachable Japanese enlarged configuration and train/view controls;
they add no native pixel-fidelity claim. Other slice-specific captures and their
inspection records remain historical in the linked reports.

The first sandboxed .NET launch could not bind VSTest's local socket and aborted
before executing tests; the authorized rerun produced the reported 369 passes.
No production fix or new behavioral test was necessary. No complete suite,
capacity/soak, Linux/container, optional-pack, deployment, other-engine or native
check was rerun. P5-27 Linux continuation, P5-28 capacity and P5-29 distribution/
bilingual matrices retain their dated evidence and limitations.

Commands and selections are retained in the tool record; the browser log also
includes the exact invocation.
The .NET filter selects `FleetMovementTests` (including its partial timing,
Outside, recovery, operations and continuation cases), `FleetControllerTests`,
`FleetTimetableTests`, `FleetAdvancedTimetableTests`, `FleetTurningTests`,
`FleetControllerManagerTests`, `FleetGroupTests`, `FleetManualRetirementTests`,
`FleetTradingTests` and `TrainFixtureBoundaryTests`. No P6 production feature is
implemented by this review.
