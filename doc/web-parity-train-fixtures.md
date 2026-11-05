# P5-02 independent train/controller fixtures — 3 November 2026

The [literal fixture set](parity/train-fixtures.json) and
[read-only source checker](../scripts/train-fixtures.py) supply **744 cases in
17 families**, bound to **20 pinned source files** from stable ExAV 2.1.1.12/V2,
Git `883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, `trunk/`.
They expand the P5 portions of SCN-004–009/014/026/031 before implementation.
The [P5-01 contract](web-parity-train-contract.md) remains authoritative for
portable defect dispositions. The local checklist remains the ordered progress
record; the next implementing input is P5-03 stock registration.

Expectations were authored from pinned source formulas independently of production
code. The checker imports no simulation, renderer or content compiler, writes no
expectations and executes no legacy assemblies. Default mode checks repository
provenance; `--source-root` additionally verifies raw pinned Git blobs and the
P5-01 bound line excerpts, never a corrected archive working tree. Literal output,
source derivation, modern API checks and native/human fidelity remain distinct.

## Cases and source boundaries

| Family | Cases | Literal expectations |
| --- | ---: | --- |
| Guides | 17 | Exact P4 `guide-*`, head/follower and forced-curve IDs; reciprocal incoming ports, controller versus predecessor choice |
| Ramps | 16 | Every P4 `ramp-*` ID: four climb directions × surface/underground × up/down; three two-car shifts, current-rail zdiff, identities, charges and due/order |
| Reversals | 24 | Eight headings × straight/left/right; guide-based heading, original indices/current order, unchanged locations/accounts/queue |
| Callbacks | 20 | Eight straight moves, curve/junction followers, foreign occupancy, legal eight-car closed loop blocked by its own tail, dead end, both-null, handler reversal, positive stop and all central sentinels |
| Outside | 83 | Eight headings × all five return candidates; occupied first candidate, missing return, reversal with/without return, 100/51/1 countdowns, capacity0/40/160, forwarding, exchange/fare/boarding-origin order |
| Timetables | 20 | Strict daily departure479/480/481, all five wildcard/mismatch components, stored first-match order, initial pass/stop and later go/reverse/forwarding/ten-minute waits |
| TURNING | 36 | Eight headings × one/three cars × -1/-1000; curve/junction and Outside early returns; handler pose, central-sentinel pose, identities/reversal flag, unchanged pending timers |
| Rendering | 448 | 192 planar and256 slope cases: symmetric/asymmetric/color/reverse-tail, eight headings, both curve senses, all four ramp levels/directions, independent formation/wrapper reversal, offsets and exact crops |
| Running costs | 1 | SCN014 full17-move trace: charges1/5/9/13/17,122,000 each,610,000 expense,500,000 receipt,9,890,000 cash |
| Resale | 5 | SCN026 ages0/364/365/730 and absent purchase time; retained car identities become Unplaced, inventory/listener removal and train-only timer cancellation |
| Signals | 9 | Eight P4 `signal-heading-*` IDs plus two distinct effective controllers passing one shared signal; reserved-null signal ControllerId |
| Numeric speed | 33 | All32 P4 rows plus overlap precedence; native<=0 remains source diagnostic and is inadmissible under TRAIN-D007 |
| Timing | 7 | Exact named P4 prior-head, destination, Outside and positive-wait timing boundaries |
| Queue | 2 | P1 FIFO equal-time retry and executing-entry cancellation preserving the neighbor |
| Drawing | 1 | SCN031 road-before/accessory-before/rail-before/car/rail-after/accessory-after |
| Controlled yards | 14 | Integer stop-position truncation, five-car train/four-cell platform contrasts, no-host/wrong-index rejection, ordered handler/unload/load/bell calls |
| Visible exits | 8 | Source HV/Z visible border before destination reciprocity; Outside100 and native-speed registration in every heading |

The artifact hashes P5-01, P0 geometry/scenarios and the P4 railway/guide/speed
inputs. Source and scenario bindings retain their original inputs/expectations;
expanded cases have stable IDs. The checker independently recomputes arithmetic,
checks source ports, distinct car identities/occupancy, contiguous legal consists,
mask domains and return-candidate domains. Matrix coverage is required in checker
code, so removing a fixture and rehashing its document does not hide a missing cell.
The thirteen oracle regressions test rehashed evidence corruption, every family's
outputs/coverage, source bindings, illegal poses and evaluator immutability.

Both-null fixtures distinguish the native null dereference from TRAIN-D001's
portable result: retain performed reversals, EmergencyStopping, unchanged positions,
move count and cash, and exactly one retry after the queued neighbor using the
captured prior head's speed limit. Positive signal waits retain StoppingAtStation.
An occupied first connected Outside candidate blocks without trying the next.
Countdown1 becomes0 before return is attempted on the following callback; a failed
return restarts10 and still counts as a successful Outside shift.

TURNING traces retain its initial reversal on early return. A single nonsharp
(diagonal) car can turn requested -1 into -1000 and enable forwarding. Successful
re-placement can exchange named cars' positions; it is not equivalent to plain
reversal. `cars` is the pose after the handler, `afterCentral` is after the central
negative sentinel, before the subsequent movement attempt. The source's timer
remains untouched by the handler. Actual `.ftdd` decoding/validated append remains
P5-23; SCN009's diagram metadata is retained without claiming that parser exists.

Controlled yard/passenger inputs establish call order and arithmetic only. At
Outside51→50, unload uses the pre-shift location; service boarding records the
pre-shift Outside51 state, then the consist shifts. The 3D distance in the fare
case truncates sqrt(29) to 5: 39×3×5×2 = 1,170. Forwarding earns the old fare, unloads
and omits reboarding while remaining forwarding. P6 still owns actual stations,
passenger populations, host/catchment integration and departure bells.

## Production consumers and discovered recipe correction

[Four .NET boundary tests](../tests/FreeTrain.Online.Tests/TrainFixtureBoundaryTests.cs)
consume ramp, signal, visible-border and queue literals through the existing
P1/P3/P4 APIs. They do not implement or mock a production train state machine.
[Two client tests](../web/tests/train-fixtures.test.ts) and the
[content-compiler regression](../scripts/tests/test_content_recipes.py) consume all
448 literal crops using exact bundled definition IDs. Future P5 slices must load
the same literal data for live movement, controllers and rendering offsets;
production output must never generate their expected results.

P5-08's [production movement consumer](../tests/FreeTrain.Online.Tests/FleetMovementTests.cs)
now executes 13 zero-stop/simple callback literals, all16 real surface/underground
ramp traces (48 shifts), all eight visible exits and both predecessor route
cases. [Runtime evidence and later-handler limits](web-parity-fleet-movement.md)
keep the remaining fixture families with their numbered implementing slices.

[P5-13 timing/accounting](web-parity-fleet-timing.md) extends that consumer to18
callback literals, including positive stops and central sentinels. All32 numeric
handoff rows plus overlap and all seven timing rows now have runtime consumers;
Outside timing uses controlled candidates entering the shared continuation,
without granting P5-14 route/exchange/return acceptance. SCN-014's17 actual ring
moves and controlled receipt verify the exact9,890,000 companion balance.

The new independent crops exposed a P2 preparation defect in reverse-tail slope
selectors. `ReverseTrainCarImpl.drawSlope` flips direction by four **and** flips
climbing. The asymmetric frame index is `direction + !climbing`; reversing its
selector therefore XORs **5**, rather than the previous1. Symmetric reversed
wrappers retain the masked planar frame and the climbing-axis slope frame. The
generic compiler now applies those source rules; a controlled symmetric-wrapper
regression also covers the unbundled generic path. The two bundled asymmetric tail
recipes and matching server/browser catalogs were regenerated.

The corrected catalog fingerprint is
`9e85cf889fd8bd49c24c6173c971bf7fe935a5d3a36438f4e242e84bee3c35b9`.
Generated terrain/railway profiles and seven active canonical save/protocol
fixtures were refreshed to match it. Save8/protocol6 and runtime limits are
unchanged. Earlier evidence retains its historical catalog hash; exact-pack
rejection remains in force for saves from a different catalog. This step adds no
migration, new stock registration, moving-train gameplay, browser fleet controls
or native-pixel acceptance.

## Verification

On 3 November 2026 with .NET SDK 10.0.402, Deno 2.9.8 and Playwright 1.64.1:

- Default and explicit pinned-source fixture checks pass 744 cases/20 files.
  Logs: `tmp/p5-02-fixtures.log`, `tmp/p5-02-source.log`.
- Full Python evidence regression: 187 run, 186 pass, one existing skip;
  includes 13 oracle checks and two added compiler regressions.
  Log: `tmp/p5-02-python.log`.
- Focused .NET train-boundary/content/mixed-persistence/contract checks: 84/84.
  Log: `tmp/p5-02-dotnet-final.log`.
- Selected client suites (`train-fixtures`, `content-recipes`, `mixed-state`,
  `state-transfer`): 24/24. Log: `tmp/p5-02-web.log`.
- Web build, generated/content/locale checks, P5 source review, P4 handoff and
  project boundaries pass. The build retains its existing bundle-size advisory.
  Logs: `tmp/p5-02-web-build.log`, `tmp/p5-02-content-check.log`,
  `tmp/p5-02-train-review.log`, `tmp/p5-02-handoff.log`, `tmp/p5-02-boundaries.log`.
- Only one Chromium workflow ran: the representative bundled recipe gallery across
  seasonal/light variants, 1/1, 5.6 seconds. Log: `tmp/p5-02-browser.log`.
  No exhaustive browser, capacity, container, native or human fidelity run is credited.

Reproduction commands from the repository root (the browser command uses the
fresh Debug server built by the focused .NET run):

```sh
python3 scripts/train-fixtures.py --check
python3 scripts/train-fixtures.py --source-root /path/to/freetrain-exav-archive --check
python3 -m unittest discover -s scripts/tests
python3 scripts/review-trains.py --check
python3 scripts/check-railway-handoff.py
python3 scripts/check-project-boundaries.py
python3 scripts/prepare-bundled-content.py --check
dotnet test tests/FreeTrain.Online.Tests/FreeTrain.Online.Tests.csproj --no-restore --filter 'FullyQualifiedName~TrainFixtureBoundaryTests|FullyQualifiedName~PreparedContentTests|FullyQualifiedName~MixedPersistenceTests|FullyQualifiedName~ContractTests' -m:1 -nodeReuse:false -p:UseSharedCompilation=false
```

From `web/`:

```sh
deno task build
deno test -A --no-check tests/train-fixtures.test.ts tests/content-recipes.test.ts tests/mixed-state.test.ts tests/state-transfer.test.ts
FREETRAIN_ONLINE_TEST_SERVER_CONFIGURATION=Debug deno task test:browser:run reference-world.spec.ts --project=canonical --grep 'bundled recipe gallery representative' --workers=1
```

Fixture file SHA-256:
`3367d352414b4e688855649ba13d5044a350e16b89114b56448f1129f901e7dc`.
The internal canonical-JSON digest is independently checked by the oracle.
