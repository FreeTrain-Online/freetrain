# P5-15 multi-train operations — 5 November 2026

P5-15 is complete at portable multi-train simulation and browser operational
scope. The existing movement dispatcher already implements source occupancy and
FIFO semantics; this slice adds integrated evidence and completes the inspector's
reverse/withdraw actions using the existing authoritative preview/commit path.
Deployment and inspector now show forwarding alongside operational state, retain
a distinct label for the saved StoppingAtSignal enum, and explain occupied
Outside returns using the first connected source candidate.

Save9/protocol7, commands, schemas, stock catalogs/packs and capacity limits are
unchanged. There are no reservations, dispatch pathfinding or automatic block
signals. Simple still ignores non-Station harbors. Real manual/TAT signal waits,
controller configuration and station gameplay retain P5-16–24/P6 ownership;
positive stop results still publish StoppingAtStation under GEO-005. The distinct
saved-enum label does not change that rule. Full information/framing/tracking and
color workflows remain P5-25; full recovery, capacity, native/human and optional
acceptance retain their numbered owners.

## Source and presentation bindings

Authority is stable ExAV 2.1.1.12/V2, archive Git
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, `trunk/`. Raw pinned
`core/world/rail/Train.cs`, `core/world/Clock.cs` and
`core/controllers/rail/TrainPlacementController.cs` were reviewed alongside the
[P5 contract](web-parity-train-contract.md),
[independent source fixtures](parity/train-fixtures.json),
[P1 clock contract](web-parity-clock.md) and
[P5-14 return implementation](web-parity-fleet-outside.md).

| Source rule | Runtime/evidence binding |
| --- | --- |
| `clockHandler` computes a candidate and checks `isBlocked` before moving | A train blocks on any occupied destination, including its own tail. Blocking retains direction, poses, MoveCount and operating accounts, with one retry callback. It never initiates a reservation or automatic routing decision. |
| Clock callbacks observe earlier same-minute mutations | The current `(dueMinute, order)` queue exposes the complete preceding callback's occupancy before dispatching the next. Placement registration order, rather than TrainId, decides who enters a contested cell. Reopen retains exact order. |
| `JunctionRailRoad.guide` / Simple straight decision | A gameplay-built pattern14 junction merges west and southwest approaches. Both callback orders preserve straight choice from the west and forced east guidance from the branch. |
| `calcReturnPoint` selects connectivity before `isBlocked` | Backend and browser feedback retain the first connected candidate even when it is occupied and a later candidate is free. No free-route search is added. |
| `TrainPlacementController.onClick` reverses the selected placed train; removal calls `Train.remove` | Inspector and owned-stock actions share `FleetDeploymentControls` and the existing guarded command. Reversal retains the callback/accounts; withdrawal clears all Inside slots and cancels only its own timer while retaining stock. Sale remains the unplaced-only workflow. |
| `Train.stateDisplayText` adds forwarding to the base state | `fleetForwarding` ships EN “Forwarding” / JA “回送”, binding TRAIN-TEXT-0252 (`Train.cs:253`). The browser separates it with a middle dot for readability. |
| `StoppingAtSignal` has distinct source text | `fleetSignalWaiting` ships EN “Stopped at signal” / JA “信号で停止中”, disambiguating TRAIN-TEXT-0256 (`Train.cs:259`, “停止中”). Positive-stop behavior remains unchanged. |

`fleetOperations` is a portable section heading. `fleetOccupiedReturn` and
`fleetMissingReturn` are portable diagnostic descriptions derived from the return
rule, not claims of verbatim source labels. All five new strings ship in English
and Japanese. No historical English donor is asserted. Recovery feedback describes
the current validated snapshot: if a later callback has already cleared the
obstruction, an emergency-stopped train correctly shows the ordinary retry
message until its next callback.

## Fresh verification

On 5 November 2026:

- **148/148 selected .NET tests** pass: FleetMovement (including recovery,
  timing, Outside and the new operations partial), FleetSession,
  FleetDeployment and FleetPersistence. The new partial adds **12 cases** with
  handwritten source expectations and reciprocal occupancy/one-timer checks.
  Traces compare exact saved bytes with save/reopen companions at movement
  boundaries, without using production route results as the expected oracle.
- Opposing trains on real constructed rail, with/without a real three-aspect
  signal, meet at minutes102/104 and remain blocked through108 without repeated
  charges. Operator reversal retains the schedule and lets both leave at110.
  Simple traverses the signal without a fabricated wait or block reservation.
- Adjacent same-direction trains exercise both registration orders. Follower-
  first yields MoveCount `[0,1]` at102, then `[1,2]` at104. Leader-first yields
  `[1,1]`, then `[2,2]`, even when the leading train has the larger identity.
  Callback orders become2/3 then4/5; each train incurs its first running charge
  only when it actually moves. Both junction-entry orders likewise serialize
  complete consists and retain straight/forced-branch guidance.
- A purchased/deployed **32-car** formation fills all32 distinct voxels of the
  gameplay-built octagon. Its head remains blocked by its own tail through four
  callbacks while another train's controlled Outside countdown continues.
  Withdrawing the full formation retains exactly the neighbor's callback.
- Two purchased/deployed trains enter the opposite ends of a real constructed
  ramp. At102 they occupy `(25,20,2)` and `(26,20,3)`; both block across the
  height transition through106. Withdrawal releases the other train, which
  crosses to Z3, reaches the approach's dead end and reverses with exact costs.
- Controlled Outside0 poses on registered rail test both equal-minute return/
  blocker orders. Returning-first waits while the blocker vacates; blocker-first
  permits immediate entry. A free alternate remains unused. The browser companion
  delays the blocker to250 and captures the actual blocked callback at200.
  These controlled return inputs are distinguished from the real purchase/deploy
  line/ramp/ring journeys; P5-14 supplies the continuous boundary-exit trip.
- Two real hosts preserve paused idle state, Step/Run/Pause order and independent
  clocks/accounts, then produce byte-identical saves under matching commands.
  A separate trusted dispatcher consumes one world-local random value before
  each actual production callback: sixteen draws follow exact train FIFO order,
  leave the other world untouched, and reproduce through a midway save/reopen.
  Production movement itself continues to leave the random bank unchanged.
- **51/51 selected client tests** pass: fleet-state, train-motion, mixed-state
  and state-transfer. New recovery checks include foreign/own occupancy, a free
  alternate after an occupied first return, and all **56** independent cardinal/
  diagonal return-choice/blocked/missing literals. Feedback leaves inputs intact.
- **4/4 selected Chromium cases** pass: EN/JA reversal recovery plus the new
  EN/JA two-train inspector journeys. Both sockets agree on complete fleet,
  occupancy, queues and accounts. Checks include equal-minute literal positions/
  orders, stale reversal rejection, Cancel focus, inspector reversal/withdrawal,
  one surviving neighbor timer, peer reload, actual checkpoint restart, forwarding
  plus occupied-return feedback, and disabled reverse/withdraw controls for an
  Observer inspecting a real car. Japanese inspector controls remain usable at
  390×844 and200% text with no horizontal overflow. Canvas instances survive;
  no page errors. Existing recovery tests also retain rail-repair recovery.
- Production build/generated contracts/prepared content/locales pass with zero
  Svelte errors/warnings and the existing bundle-size advisory. Pinned source
  review, all744 source-fixture checks, project boundaries and whitespace pass.
  The existing recovery cases emit five captures; desktop English and narrow
  Japanese captures were inspected for layout. No original-runtime, human pixel
  fidelity, new native-platform or complete-suite acceptance is claimed.

The full web suite was deliberately not run. The initial .NET invocation required
local socket access for its test runner; the focused runs passed with that access.
Early test-only account equality and diagonal-rail setup expectations were corrected
before the final passing runs.

Logs: `tmp/p5-15-{selected-dotnet,client,browser,web-build,source-review,fixtures,boundaries}.log`.
Inputs: `tmp/p5-15-operations-fleet.json` and its `.return` companion. These are
captured inputs; expectations remain literal traces in the tests. Captures are
under `tmp/p5-15-browser/`.

Reproduce from the repository root:

```sh
FREETRAIN_OPERATIONS_FLEET_OUTPUT="$PWD/tmp/p5-15-operations-fleet.json" dotnet test tests/FreeTrain.Online.Tests/FreeTrain.Online.Tests.csproj --no-restore --filter 'FullyQualifiedName~FleetMovementTests|FullyQualifiedName~FleetSessionTests|FullyQualifiedName~FleetDeploymentTests|FullyQualifiedName~FleetPersistenceTests' -m:1 -nodeReuse:false -p:UseSharedCompilation=false
python3 scripts/review-trains.py --source-root ../freetrain-exav-archive --check
python3 scripts/train-fixtures.py --source-root ../freetrain-exav-archive --check
python3 scripts/check-project-boundaries.py
```

From `web/`, using the Debug server produced by that run:

```sh
deno task build
deno test -A --no-check tests/fleet-state.test.ts tests/train-motion.test.ts tests/mixed-state.test.ts tests/state-transfer.test.ts
FREETRAIN_ONLINE_TEST_SERVER_CONFIGURATION=Debug FREETRAIN_OPERATIONS_FLEET_OUTPUT="$PWD/../tmp/p5-15-operations-fleet.json" FREETRAIN_ONLINE_BROWSER_EVIDENCE="$PWD/../tmp/p5-15-browser" deno task test:browser:run --project=canonical fleet-deployment.spec.ts --grep 'P5-15|reversal recovery' --workers=1
```
