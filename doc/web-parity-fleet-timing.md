# P5-13 movement timing and running costs — 5 November 2026

P5-13 is complete at portable timing/accounting scope. The real Inside dispatcher
and the shared movement continuation use the original callback-entry head for
speed restrictions, even after a rail handler or central reversal. Positive
waits publish handler effects, increment StopCallCount, set StoppingAtStation and
return before route lookup, movement or operating charges. Successful moves
charge on moves 1/5/9/13/17; blocked and waiting attempts preserve MoveCount.

Save9/protocol7, saved shape, packs/catalogs, limits and browser copy are
unchanged. Production Outside route selection/exchange/return remains explicitly
unavailable until P5-14. Its timing and charges are verified here through
controlled candidates entering the same production movement continuation;
this is not Outside gameplay acceptance. Controller selection/configuration,
harbor identities and TURNING remain with P5-16 onward and P6.

## Source bindings

Authority is ExAV 2.1.1.12/V2, Git
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, `trunk/`. Raw pinned
`core/world/rail/Train.cs` was read for clockHandler, registerTimer and move.
The [source contract](web-parity-train-contract.md),
[P4 speed handoff](parity/speed-limit-handoff.json) and
[independent P5 literals](parity/train-fixtures.json) supply the expectations.

| Source rule | Runtime binding |
| --- | --- |
| Capture `ins` before `getStopTimeSpan` and all reversal | `FleetMovement.Tick` passes immutable callback-entry state to its trusted stop handler. `CompleteMove` reads the original train from the unchanged session for timing. Handler changes cannot substitute the new head location. |
| Positive duration returns early | `FleetStopResult` carries the detached train and source duration/sentinel. Positive results preserve handler mutations and forwarding, increment the checked stop counter, publish StoppingAtStation, and register exactly that duration. No route guide, speed-limit calculation, shift or spend occurs. |
| Nonpositive stop processing | Clear forwarding, interpret -999 as forwarding, -1000 as reverse/forwarding, other negative values as reverse, then reset stop count before route lookup. Zero proceeds. These mechanical rules are available to later controller handlers without activating their configuration/UI early. |
| `registerTimer(Location)` | `LimitedDelay` preserves Low → Medium → Fast precedence: Low=4; Medium retains native4, otherwise3; Fast retains native3/4, otherwise2. In particular native5 under Fast becomes2. The pure function retains diagnostic nonpositive arithmetic; stock admission still rejects nonpositive native speed. |
| Native timer boundaries | Deployment remains native regardless of local limits. A next Outside candidate or original Outside head uses native timing, including an occupied return candidate. Positive waits bypass this choice. Both-null retries retain TRAIN-D001's captured-head timing and safe emergency result. |
| `moveCount++` and RAIL_SERVICE spend | Shared `CompleteMove` shifts the old complete consist and increments once only when unblocked. `Publish` charges `(length*20 + passengers/20)*2000` when the old count's low two bits are zero, using post-handler passengers and checked source-width arithmetic. The same continuation accepts Inside, Outside countdown and return candidates. |

Default ordinary/junction/ramp handlers still return zero; Simple ignores
signals. Advanced signal/controller callbacks retain their named unavailable
boundary. A trusted stop handler is a domain dependency like TrainCallback,
never a serialized delegate or client-supplied instruction. Existing complete
graph/occupancy/account validation and whole-minute rollback surround it.

## Fresh verification

On 5 November 2026, .NET SDK10.0.402, Deno2.9.8, Playwright1.64.1/Chromium:

- **123/123 focused .NET tests**: FleetMovement (including recovery/timing),
  FleetDeployment, FleetPersistence and SpeedLimit. All32 numeric handoff rows
  plus overlap run through the source arithmetic; all25 admitted rows also run
  actual scheduled Inside callbacks with registered stock. The eight nonpositive
  rows verify registration rejection. Extra live cases cover all-overlap for
  each native1–5 and Medium-over-Fast without Low. Source-legal native1/5/
  Int32.MaxValue registrations use isolated test profiles, without changing the
  bundled catalog or claiming those speeds belong to bundled stock.
- All **seven literal timing boundaries** run through the shared continuation:
  four actual Inside moves, a positive wait, controlled Outside-next and
  controlled Outside-prior/return candidates. Actual border exit/native timers
  remain covered by all eight existing visible-exit literals. Both deployment
  orientations explicitly schedule native speed under overlapping limits, then
  apply the captured-head Low limit on the first move through save continuation.
  A controlled return blocked by its own consist keeps one native-speed timer,
  unchanged car poses/count/accounts and forwarding through reopen.
- **18 literal callback rows** now include positive wait, reverse, forwarding,
  reverse-forwarding and handler-reverse/both-null, alongside the previous13
  zero-stop rows. Eight additional handler/central reversal combinations retain
  the original Low location while new head/destination locations carry Fast;
  handler-adjusted40 passengers produce124,000 rather than the original122,000.
  Repeated positive waits preserve reversal/forwarding through reopen, book
  nothing, and depart on the third callback. Stop-counter overflow and a fault
  after handler reversal/random consumption roll back exactly; existing late
  callback, move/car/owner/order overflow and blocked/no-charge cases pass.
- **SCN-006** covers exact equality branches, priority and original-location
  timing. **SCN-014** uses purchased/deployed stock on the gameplay-built ring
  with three cars/39 passengers. Seventeen actual moves occur at
  102/106/109/111/113/115/117/119/121/123/125/127/129/131/133/135/137.
  Charges at102/113/121/129/137 total610,000. Save/reopen immediately before each
  later charged move continues byte-identically. The controlled500,000 accounting
  receipt produces **9,890,000**, with no P6 fare-gameplay claim. Seventeen
  controlled Outside countdown continuations independently retain native speed,
  forwarding, exact consist shifts, the same five charges and save continuation;
  none crosses the P5-14 exchange-at50 boundary.
- **49/49 selected client tests**: fleet-state, train-motion, mixed-state and
  state-transfer. **1/1 selected Chromium case** uses English/Japanese peers and
  the gameplay-built timing save. Toolbar Step drives all17 moves and intervening
  minutes; both sockets agree on complete fleet/reciprocal occupancy/accounts/
  one future callback, no intermediate charges, exact due times, HTTP reopen
  before move5 and peer reload. Canvas identity survives, with no page errors.
- Production web build, generated contracts, prepared content and locales pass;
  Svelte reports zero errors/warnings, with the existing bundle-size advisory.
  Pinned source review, all744 independent fixtures, project boundaries and
  whitespace checks pass. **No complete web suite ran.** No screenshots or new
  native/human visual acceptance are claimed.

Initial test-only compilation and synthetic-profile successor mistakes were
corrected before the final passing runs. The sandbox test runner initially could
not bind its communication socket; the focused run passed with local socket
access. No production failure is hidden by these initial attempts.

Logs: `tmp/p5-13-{selected-dotnet,selected-client,selected-browser,web-build,source-review,fixtures,boundaries}.log`.
The generated browser input `tmp/p5-13-timing-fleet.json` has SHA-256
`1ad3b6c0ce7bf878aa02a55d20b2a227a95e75d1d7466421ab585c5b8e9e2fcc`.
It is a captured gameplay-built input, not an expected-output oracle. Expected
poses, due times and amounts are independent literals and test-authored traces.

Reproduce from the repository root:

```sh
FREETRAIN_TIMING_FLEET_OUTPUT="$PWD/tmp/p5-13-timing-fleet.json" dotnet test tests/FreeTrain.Online.Tests/FreeTrain.Online.Tests.csproj --no-restore --filter 'FullyQualifiedName~FleetMovementTests|FullyQualifiedName~FleetDeploymentTests|FullyQualifiedName~FleetPersistenceTests|FullyQualifiedName~SpeedLimit' -m:1 -nodeReuse:false -p:UseSharedCompilation=false
python3 scripts/review-trains.py --source-root ../freetrain-exav-archive --check
python3 scripts/train-fixtures.py --source-root ../freetrain-exav-archive --check
python3 scripts/check-project-boundaries.py
```

From `web/`, with the Debug server built by the focused run:

```sh
deno task build
deno test -A --no-check tests/fleet-state.test.ts tests/train-motion.test.ts tests/mixed-state.test.ts tests/state-transfer.test.ts
FREETRAIN_ONLINE_TEST_SERVER_CONFIGURATION=Debug FREETRAIN_TIMING_FLEET_OUTPUT="$PWD/../tmp/p5-13-timing-fleet.json" FREETRAIN_ONLINE_BROWSER_EVIDENCE="$PWD/../tmp/p5-13-browser" deno task test:browser:run fleet-persistence.spec.ts --project=canonical --workers=1 --grep 'P5-13'
```

The browser case skips when its emitted input is absent; it ran and passed in
the reported result. P5-14 must supply complete Outside selection/exchange/return
effects before calling the shared continuation, including exchanged passenger
counts before charges. P5-16 onward must connect actual typed controllers/harbors
to the tested stop-result path. P5-27 recovery, P5-28 capacity and inherited
native/human/optional qualifications retain their stated owners.
