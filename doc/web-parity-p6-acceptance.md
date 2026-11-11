# P6 integrated acceptance — 11 November 2026

**P6 closes at bundled browser/portable station and passenger scope, with
explicitly controlled population supply.** P7 can consume the station provider,
listener and passenger-effect interfaces; P8 can consume service bookings and
station statistics. [The concrete handoff](web-parity-p7-p8-station-handoff.md)
sets out the existing APIs, atomic publication constraints, independent witnesses
and remaining consumer work. This is not evolving-city, full economic-report,
native fidelity or performance acceptance.

Reviewed implementation base: `a5dd4da7eca63c48d56316ee70f24234cec312d0`.
Behavior authority is stable ExAV 2.1.1.12, archive Git
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, subtree `trunk/`.
This review adds exhaustive bilingual station-family browser witnesses and
acceptance/handoff documentation. Production behavior, Save10/protocol9, content
identity, runtime limits and the current-format-only save policy remain unchanged.

## Criterion-by-criterion exit review

Each row meets its browser/portable criterion at the stated scope. The master
P6 exit is multiple connected stations exchanging passengers and booking
source-derived revenue, platform edits updating reachability, and each station
family/controller configuration working through browser/save/reload. The ordered
checklist adds upkeep, departure bells and explicit P7/P8/native/performance limits.
The rows below reconcile that complete scope; no fixture or prepared recipe alone
grants gameplay credit.

| Criterion / P6 steps | Independent witnesses and runtime evidence | Acceptance scope |
| --- | --- | --- |
| Source, state, booking, transport (01–05) | [Contract](web-parity-station-contract.md), [821 literals / 24 families](web-parity-station-fixtures.md), [state](web-parity-station-state.md), [booking](web-parity-station-booking.md), [transport](web-parity-station-transport.md); separate source hashes and production consumers | Stable owners/IDs, both listener directions, logs, timer handles/FIFO, atomic accounts and versioned strict decoding. Source review remains immutable specification. |
| All constructed station families (06–08) | [Construction](web-parity-station-construction.md), [rendering](web-parity-station-rendering.md), [browser](web-parity-station-browser.md); all35 domain build/remove/save cases, all35 eight-variant literal pixel/alpha-pick gallery; new P6-28 EN/JA construction/reopen loop in `station-building.spec.ts` | Every authored definition crosses actual catalog/placement controls and exact save replacement in both locales. Existing EN keyboard/JA narrow-touch picking/name/removal/peer journeys cover common controls. No native pixel/font identity claim. |
| Thin/fat platforms and lanes (09–10, 13) | [Thin](web-parity-thin-platforms.md), [wide](web-parity-wide-platforms.md), [lanes](web-parity-platform-lanes.md); literal orientation/roof/lane recipes, real Traffic ownership, browser placement/lane/removal | Roofed/unroofed slim platforms, wide centers and one/two yard lanes retain exact ownership, price/timers and safe demolition. |
| Host edits and reachability (11–12) | [Hosts](web-parity-station-hosts.md), [properties](web-parity-platform-properties.md), `StationHostConnectionTests`, EN/JA `platform-properties.spec.ts`; actual service disconnection/reconnection in `passenger-service.spec.ts` | Range8 full-column membership; explicit direct/intermediate hosts, descendant disconnection, cycle/stale rejection and removed/rebuilt host identity. Disconnected yard passes without service; explicit reconnection restores actual stops. |
| Cosmetic passages and stairs (14–15) | [Passages](web-parity-passages.md), [stairs](web-parity-stairs.md); all48 passage/all96 stair appearances across eight variants, literal pixels/picks and EN/JA construction/reopen | Free cosmetic changes, source neighbor precedence, no host/service connection; EXAV-GAL-001 portable correction is provenance-bound, with raw invalid witnesses retained. |
| Providers, demand and upkeep (16–17) | [Demand](web-parity-station-demand.md), [statistics](web-parity-station-statistics.md); all42 tables/1,008 cells,169 bindings, creation-order/cap traces, eight Single vectors, seven-day/FIFO traces, actual timers | Controlled ordinary/special supply only. Live hourly depletion, daily station cost, relative24h platform cost and nonlinear transport logs continue exactly after reopen. |
| Real train exchange and source fares (18–19, 21) | [Exchange](web-parity-station-passengers.md), [yards](web-parity-station-yards.md), [two-station service](web-parity-passenger-service.md); `StationPassengerExchangeTests`, `StationYardStopTests`, four east/west × supplied/zero domain journeys and EN/JA browser journeys | Constructed stations/platforms and purchased/deployed trains unload/board through actual callbacks. Packed594, waiting10000→9406, zero destination supply and `594×400×12×2 = ¥5702400` fare have independent expectations. Complete peers/accounts/queue and exact departure continuation are compared. |
| Simple/TAT/controller configurations (19, 22) | [Controller matrix](web-parity-station-controllers.md);11 literal wait/departure configurations plus Pass; real two-platform/shared-station and rebuild witnesses; EN/JA picked TAT/group/rule-file journeys | Simple30, missing TAT10, Fixed40/1440, Daily equal-arrival1440, Go/Reverse/forwarding/ReverseForwarding/TURNING, stored rule/mask order, current group resolution and station-location keys. Controller-before-yard ordering preserved. Manual remains owner-removed. |
| Committed bells (20) | [Departure delivery](web-parity-departure-bells.md); `StationDepartureBellTests`, real selected-forwarding browser playback on EN/JA peers and rejected-tick witness | Six exact bell registrations including authored silence; gesture/mute/viewport local, post-commit epoch/sequence/ordinal dedup, no replay on retry/join/reopen/restart. Automated playback is not human listening acceptance. |
| Information/list/labels and authority (23–24) | [Information](web-parity-station-information.md), [cross-tool](web-parity-station-cross-tool.md); EN/JA sorted stable-ID list/current-state diagnosis, dirty drafts, peers, authority, lost receipts and cold readiness | Live waiting versus demand and train-call/log meanings; station/group settings, local labels and map preservation; disconnected, Pass, forwarding and zero supply remain distinct. |
| Browser edits, save/reload/checkpoint (25) | [Continuation](web-parity-station-continuation.md); eight domain/storage cases, every-callback cuts, Simple/Reverse/TURNING/midnight uninterrupted oracles and EN/JA backend restart journeys | Exact canonical station/provider/fleet/accounts/random/occupancy/allocator/queue bytes, one train callback each, rejected inputs preserve valid world/named files. Desktop import remains P10. |
| Capacity, content and distribution (26–27) | [Measurements](web-parity-station-capacity.md), [delivery](web-parity-station-delivery.md), [99 declarations/149 selectors/12 operations/78 controls](parity/station-delivery.json) | Legal measured scenes and clean base-only multi-architecture builds/source+prepared synthetic mounts delivered; performance misses and real optional-suite acceptance remain open. |

Exhaustive definition/arithmetic coverage and representative integrated journeys
are separate. This review does not claim a Cartesian product of all35 buildings,
every controller, every platform orientation and every browser engine in one city.
The all-family browser loop builds each definition in an isolated flat world,
reopens its actual save exactly, then restores the empty baseline. Real passenger
service and controller cases use their separately domain-built connected scenes.

## Resolved differences and retained qualifications

This is the current disposition overlay; P6-01's future-tense decisions and earlier
slice reports describe their original stage, not current acceptance.

| Decision | Final P6 disposition / remaining owner |
| --- | --- |
| STATION-D001 | Exact host candidate membership with deterministic stable-ID automatic direct choice and explicit selector; no nearest-host claim. P6-11/12. |
| STATION-D002 | Detached source reparent/disconnect sequence with bounded cycle guards, strict decoding and atomic rejection. P6-03/11/12/24. |
| STATION-D003 / SCN-D01 | Retain creation-order sphere/cube difference and fifth replacement reverse-link omission; save both directions independently. P6-16/25 witnesses; P7 must preserve it. |
| STATION-D004 / EXAV-GAL-001 | Three ordinary stair23 prepared recipes use authored x288; original x388 raw crops/bitmap hashes retained as diagnostics/correction metadata. P6-15/27 portable pixels/picks pass. Original-runtime/native/human fidelity stays P2; canonical asset conversion stays POST-ASSETS-07. |
| STATION-D005 | Checked original Int32 widths and Single grouping/truncation; nonfinite/overflow rejects whole candidate/minute. P6-04/16–19. |
| STATION-D006 | Effective train/group controller receives actual resolved station identity/revision and station-location key, distinct yard location; same-location rebuild retains settings. P6-19/22. |
| STATION-D007 | Committed live departure envelope; transient local gesture-gated audio and no historical replay. P6-20/25. |
| STATION-D008 / MAN-D01–03 / DEV-D01–03 | ExAV range8, cut predicates, toward-zero overlength yard index, .8/.9996 Single decay, configured fare×distance×2 and platform180000×length prevail over historical prose. Rail/slope remains P4; no silent old-formula fallback. |
| STATION-D009 | Immediate construction/registration/booking/timers; no station ConstructionSite. Generic timed construction remains P7/SCN-020 companion. |
| STATION-D010 | Caller-specific categories and zero-booking distinctions, base-ground-only platform quote, station midnight versus relative platform upkeep. P6-04/06/09/10/17. |
| STATION-D011 | Pure stairs preview, source forward-neighbor precedence and atomic outlook replacement. P6-15. |
| STATION-D012 | Stable-ID inspectors/list/Move and local labels; live statistics keep source meanings. Portable panels replace native windows. P6-08/12/23. |
| STATION-D013 | Full population inventory and station-facing controlled supply/effect contracts delivered. Actual producers/listeners/land-value/traffic evolution remain P7; finance histories/reports/stadium remain P8. |
| STATION-D014 | Source legal empty passage operations remain no-effect; no invented standalone rail or logical connection. P6-14. |

The [delivery bindings](parity/station-delivery.json) retain reciprocal MAN/DEV/
scenario obligations, source/donor translation classes, 215 source literals,
90 catalog texts and exact bell/art/notices provenance. SCN-009 diagram/TURNING
keeps its P5/P9/P10 companion scope; SCN-016 legacy timers remains P10; SCN-030
station aggregates do not close P7 development consumers. No unassigned P6
behavior decision remains; the following acceptance qualifications are explicit:

- **P11:** STATION-CAP-001 simulation progress115–117/152 nominal minutes and
  midnight bursts2.18–2.33s in128-station scenes; STATION-CAP-002 local feedback
  p95 above100ms in every scene, RTT100 misses in medium/large/extent;
  STATION-CAP-004 unresolved normal-rate pause commands in both128-station scenes.
  Large/extent feedback distributions are partial successful samples, not overall
  service p95s. P4-CAP-001/002 and FLEET-CAP-001/002 also remain open.
- **P1/P2/UX3/P9:** final foundations review, original-runtime/human full-gallery
  fidelity, native IME/display/zoom/touch/accessibility, other browser engines
  and final manual remain at their recorded gates. Chromium touch emulation and
  literal pixels do not close native/device/human acceptance.
- **P2/P9/P10/P11:** canonical optional V2 resource repairs/older-suite acceptance;
  synthetic source/prepared registrations do not constitute a real optional suite.
- **P7/P8/P10:** real city producer/effect evolution, complete finance/game systems
  and desktop save imports/migrations. Save10-only continuation does not accept
  historical files. P11 retains full-city, long-soak, device and release closure.

## Fresh validation and historical evidence

Fresh 11 November checks and precise commands are recorded below. Slice counts
above remain dated historical evidence unless explicitly listed here. The P6-27
clean arm64/amd64 packaging, synthetic mounts and17 Chromium cases were not
rerun by this review. Neither capacity workloads nor optional/native/original
runtime comparisons were rerun. Host Deno2.9.8 differs from pinned2.9.7; no host
pinned-version check is claimed. Production build retains its bundle advisory.

The new all-family browser tests close a gap between exhaustive domain/gallery
coverage and representative bilingual UI journeys. Their first attempt threw
while polling before the initial mixed-world publication; the corrected poll
waits for that publication without relaxing any acceptance assertion.

| Fresh check | Result | Evidence |
| --- | --- | --- |
| Full Release .NET suite | 2,304 passed, zero failed/skipped | `tmp/p6-28-dotnet-accepted.log` |
| Client unit and retention tests | 384 +2 passed, zero failed | `tmp/p6-28-client.log` |
| Station Python source/fixture/delivery tests | 35 passed | `tmp/p6-28-python.log` |
| Source review / literal fixtures / delivery | Pass;821 cases; raw pinned archive review/fixtures also pass | `tmp/p6-28-{review,fixtures,delivery,review-pinned,fixtures-pinned}.log` |
| Existing integrated Chromium journeys | 15 passed, zero skips;2.1min | `tmp/p6-28-browser.log`, `tmp/p6-28-browser/` |
| New all-family EN/JA Chromium journeys | 2 passed, zero skips;35 definitions each;1.2min | `tmp/p6-28-families-accepted.log`, `tmp/p6-28-families-accepted/` |
| Generated/content/locales/Svelte/production build | Pass; zero Svelte errors/warnings, existing bundle advisory | `tmp/p6-28-web-build.log` |
| Project boundaries / diff whitespace / new document links | Pass | `tmp/p6-28-boundaries.log`, final review |

The initial sandbox .NET invocation built successfully but aborted at VSTest's
local socket before tests executed. The authorized unsandboxed rerun supplies
all2,304 passes. The initial all-family run's two polling failures receive no
success credit; both corrected journeys execute all35 definitions freshly.
The17 successful Chromium cases are15 existing integrated cases plus2 new
all-family journeys, not70 independent test cases. The family evidence retains
70 actual constructed saves and both35-entry identity/timer manifests.

Inspected fresh captures: `tmp/p6-28-browser/service-en-packed.png` shows two
constructed stations/platforms and594 passengers at150% in train information;
`station-controller-ja.png` shows the Japanese narrow shared-station TAT editor
with station key23,13,2 and Fixed40 controls. These demonstrate current browser
presentation, with no native or human fidelity credit. Execution/artifact hashes
are retained in [the results record](parity/station-acceptance-results.json).

Reproduce from the repository root:

```sh
dotnet test FreeTrain.Online.slnx -c Release --no-restore -m:1 -p:UseSharedCompilation=false
DENO_JOBS=4 deno task --cwd web test
deno task --cwd web build
python3 -m unittest discover -s scripts/tests -p 'test_station*.py'
python3 scripts/reconcile-station-delivery.py --check
python3 scripts/review-stations.py --check
python3 scripts/station-fixtures.py --check
python3 scripts/review-stations.py --check --source-root /path/to/freetrain-exav-archive
python3 scripts/station-fixtures.py --check --source-root /path/to/freetrain-exav-archive
python3 scripts/check-project-boundaries.py
git diff --check
```

From `web/` (built Release backend and browser sockets required):

```sh
FREETRAIN_ONLINE_BROWSER_EVIDENCE="$PWD/../tmp/p6-28-browser" deno run -A npm:@playwright/test@1.64.1 test passenger-service.spec.ts station-controller.spec.ts station-information.spec.ts station-building.spec.ts station-rendering.spec.ts platform-properties.spec.ts --project=canonical --grep-invert P6-28 --workers=2
FREETRAIN_ONLINE_BROWSER_EVIDENCE="$PWD/../tmp/p6-28-families-accepted" deno run -A npm:@playwright/test@1.64.1 test station-building.spec.ts --project=canonical --grep P6-28 --workers=2
```

The original15-case run loaded the file before the new family tests were added;
the reproduction excludes them from the first selection explicitly. The second
selection is the exact final successful invocation. The two tests in that file
run sequentially in one worker under the normal Playwright file policy.

P6's qualified exit is met. P7 city/development is next; this report and the
handoff carry unresolved acceptance to its actual phase rather than granting
city, full economy, desktop import or performance parity here.
