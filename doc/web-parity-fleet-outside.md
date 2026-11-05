# P5-14 Outside exit, exchange and return — 5 November 2026

P5-14 is complete at portable Outside movement/accounting and browser transport
scope. Border exits now continue through countdown, passenger exchange and return
in the default dispatcher. Missing return track retries at10; an occupied first
connected return waits without searching later candidates. Mixed reversal and
an Inside handler/dead-end reversal reaching an Outside head use that same path.

Save9/protocol7, saved shape, stock catalogs/packs and capacity limits are
unchanged. Existing EN/JA controls and state presentation carry the result;
this slice adds no new UI copy. Passenger inputs are controlled P5 fixtures.
Station populations, platforms, yard boarding and integrated station fares/bells
retain P6 ownership. Advanced controller selection/configuration retains P5-16
onward; integrated multi-train operations retain P5-15. Native/human visual,
optional-suite and capacity acceptance remain with their recorded owners.

## Source bindings

Authority is stable ExAV 2.1.1.12, archive Git
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, `trunk/`. Raw pinned
`core/world/rail/Train.cs` was read for clockHandler/move, both state visitors,
returnPointMatrixes, unload/loadPassengers and capacity calculation;
`core/world/Location.cs` supplies truncated 3D distance. The
[train contract](web-parity-train-contract.md),
[independent P5-02 literals](parity/train-fixtures.json) and
[SCN-007](parity/exav-scenarios.json) remain the expected behavior.

| Source rule | Runtime binding |
| --- | --- |
| Visible-border test before destination lookup | Existing `Next` Inside branch retains the source H/V/Z border predicate and installs Outside100. No destination rail is required. |
| `CalcNextTrainCarState.onOutside` | A nonzero counter decrements; zero attempts return on the following callback. No connected rail yields Outside10, a successful shift/count/possible charge, then ten countdown callbacks before the next return attempt. |
| `calcReturnPoint` / matrices | Shared `FleetReversal.ReturnReference` retains the exact five cardinal/diagonal transforms and truncating integer division. Reciprocal connectivity alone selects the first candidate; occupancy is checked later in `CompleteMove`. |
| Occupied return | Remain at Outside0, EmergencyStopping, no shift/count/charge/reversal, exactly one native-speed retry. A free later candidate is ignored. |
| `Train.move` exchange at resulting50 | Before shifting, unload old passengers and clear their origin; reload `min(100, normalCapacity)` unless forwarding. Store the current head's pre-shift placed state, including countdown51. Forwarding survives Outside callbacks and clears at every later nonpositive Inside stop result. |
| `unloadPassengers` | Compute truncated3D distance to the current pre-shift head, then checked Int32 `passengers * fare * distance * 2`. A nonnull origin triggers an earn call even for a zero receipt; null origin triggers none. |
| Earn before operating spend | `ConstructionAccounts.EarnAndBook` records separate ordered source calls under one world revision/minute/operation, preserving intermediate cash/top-ups. Expense uses post-exchange passengers. Existing save and client validators already admit these ordered records. |
| Mixed/reversed consist | Stable car identity/type/index and current array order survive; the old complete consist shifts once. Inside stop handling can reverse to an Outside head, which decrements or returns immediately. Return searches the source order anew, independently of the earlier reversal reference. |
| Timing/atomic publication | Outside countdown, failed return and first returning callback use native speed. Existing detached-minute validation publishes occupancy, passengers, accounts and one future callback together, or rolls everything back on failure. |

The obsolete Outside-unavailable host catch has been removed. Advanced
junction/harbor handlers retain their existing unavailable boundaries.

## Fresh verification

On 5 November 2026, focused checks pass:

- **149/149 .NET tests** selected from FleetMovement (including recovery, timing
  and Outside partials), FleetTrading, FleetPersistence and FleetDeployment.
  Eight literal exits continue into Outside99 with native timing and exact
  save continuation. All 67 Outside advance rows run actual scheduled callbacks:
  65 preserve every input; the two zero-capacity rows' initial39 passengers are
  explicitly rejected by admission, then their legal zero-passenger companions
  verify the same exchange outputs. Isolated stock registrations supply normal
  capacities0/40/160 and fare3 without modifying bundled content.
- All 40 individual return choices run for eight headings. Another 40 ordered
  suffix/incorrect-port combinations verify precedence and save continuation.
  Eight own-consist blocked literals and a real second-train occupied-first/free-
  alternate case retain Outside0 without charges; withdrawing the blocking train
  recovers on the next native callback. Three missing-track retry points retain the
  exact10…0 cadence; actual rail construction recovers through save/reopen.
- Existing 16 found/missing Outside reversal literals remain covered. New legal
  mixed reversal and reentry traces preserve complemented counters, current
  order and original car identity. Three Inside stop outcomes (0/-1/-1000)
  reverse to an Outside head and continue with native timing, correct forwarding,
  one shift/charge and one timer, despite Low on the original Inside head.
- A gameplay-built, purchased/deployed three-car formation runs 105 exact-due
  moves. Exit is102/Outside100; exchange is202/Outside50; zero is302; return is304.
  All followers shift their predecessor's old state; every callback agrees with
  save/reopen, with extra reloads around exchange/zero/return boundaries. The
  controlled39 passengers, fare400 and distance6 earn187,200 once. Reload uses100;
  operating charges remain122,000 before exchange and130,000 afterwards on the
  established cadence. A separate exchange on a charged move verifies earn then
  spend under one revision, including durable restore and forwarding suppression.
  Distance, receipt-cash, move-count and car-revision overflow each roll back the
  complete minute and original callback.
- **49/49 selected client tests**: fleet-state, train-motion, mixed-state and
  state-transfer. **1/1 selected Chromium EN/JA two-peer case** checks literal
  exit/exchange/return boundaries plus simultaneous receipt/operating expense,
  reciprocal occupancy, full fleet/socket agreement, no movement between due
  times, one timer, HTTP reopen at each boundary, reload and checkpoint restart
  with continued callbacks. Canvas identity survives HTTP replacements. No page
  errors; no screenshots or new visual/native acceptance are claimed.
- Production web build/generated contracts/content/locales pass, with zero
  Svelte errors/warnings and the existing bundle-size advisory. Raw pinned source
  review, all 744 independent fixtures, project boundaries and whitespace pass.
  **No complete web suite ran.**

Initial test expectations/setup were corrected before the final passing runs:
fixture member revisions, a retained test-only throwing handler after reentry,
the mixed reversal's earlier return candidate and omitted-zero browser revenue
fields. The sandbox initially denied the test runner's local socket; focused
verification passed with local socket access.

Logs: `tmp/p5-14-{selected-dotnet,selected-client,selected-browser,web-build,source-review,fixtures,boundaries}.log`.
Browser inputs `tmp/p5-14-outside-fleet.json` and its `.50`, `.101`, `.charged`
companions are captured gameplay/controlled inputs, not expected-output oracles.
Backend and browser expectations use independent source literals and explicit
time/position/account traces.

Reproduce from the repository root:

```sh
FREETRAIN_OUTSIDE_FLEET_OUTPUT="$PWD/tmp/p5-14-outside-fleet.json" dotnet test tests/FreeTrain.Online.Tests/FreeTrain.Online.Tests.csproj --no-restore --filter 'FullyQualifiedName~FleetMovementTests|FullyQualifiedName~FleetTradingTests|FullyQualifiedName~FleetPersistenceTests|FullyQualifiedName~FleetDeploymentTests' -m:1 -nodeReuse:false -p:UseSharedCompilation=false
python3 scripts/review-trains.py --source-root ../freetrain-exav-archive --check
python3 scripts/train-fixtures.py --source-root ../freetrain-exav-archive --check
python3 scripts/check-project-boundaries.py
```

From `web/`, using the Debug server built by that focused run:

```sh
deno task build
deno test -A --no-check tests/fleet-state.test.ts tests/train-motion.test.ts tests/mixed-state.test.ts tests/state-transfer.test.ts
FREETRAIN_ONLINE_TEST_SERVER_CONFIGURATION=Debug FREETRAIN_OUTSIDE_FLEET_OUTPUT="$PWD/../tmp/p5-14-outside-fleet.json" FREETRAIN_ONLINE_BROWSER_EVIDENCE="$PWD/../tmp/p5-14-browser" deno task test:browser:run --project=canonical fleet-persistence.spec.ts --grep 'P5-14' --workers=1
```
