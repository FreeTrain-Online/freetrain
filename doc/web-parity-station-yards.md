# P6-19 real yard stop dispatch and atomic fleet integration — 10 November 2026

Delivered at production yard/movement scope. Actual slim and wide platform
yards now dispatch to their current resolved station and each train's current
effective controller. Save10/protocol8, content identity and runtime limits are
unchanged. Departure-bell delivery/playback remains P6-20, the complete passenger
service browser workflow remains P6-21 and the full controller UI matrix remains
P6-22.

Authority is stable ExAV 2.1.1.12, archive Git
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`,
`trunk/core/world/rail/YardRailRoad.cs` and its Train/Station/controller
dependencies, under STATION-D006/008 and TRAIN-D006/008/012. The original yard
blob was read directly; the [station source contract](web-parity-station-contract.md#passenger-arithmetic-and-yard-order)
and [821 independent fixtures](web-parity-station-fixtures.md) retain their
original source hashes, inputs and expectations.

## Dispatch and publication

[`FleetControllers.StopTime`](../src/FreeTrain.Online.Simulation/FleetControllers.cs)
finds yards through a derived Traffic-owner index in the immutable station graph.
[`StationYardStop`](../src/FreeTrain.Online.Simulation/StationYardStop.cs)
checks current heading, exact rail location/index and resolved host before
invoking the controller. Stop position is `(L+N)/2-1` in the platform direction
and `(L-N)/2` in the opposite direction, with source toward-zero integer
division. Out-of-range positions skip service; trains are not rejected merely
for exceeding platform length. In particular L4/N5 remains serviceable in both
directions; L4/N6 does not.

Controller settings use the actual station location/ID/revision, with the yard
rail location supplied separately. Shared train/group assignments and host
resolution are read again each call. Removal/rebuild cannot retain an old host
object. Signals still supply their own location and exchange no passengers.

The controller executes first, retaining reversal and TURNING re-placement.
The incoming call count then controls exchange: only first-positive results
unload; later positive waits do neither exchange nor bell; later nonpositive
results board then stage the selected platform bell. Later forwarding sentinels
−999/−1000 stage the bell while suppressing boarding. First zero/negative calls
do neither. Boarding/fare use the post-controller head. Central forwarding,
reversal and call-count reset follow exchange; movement timing still uses the
callback-entry head. Positive waits retain handler changes, increment call count,
set StoppingAtStation, schedule one callback and incur no movement charge.

[`FleetMovement`](../src/FreeTrain.Online.Simulation/FleetMovement.cs) merges
exchange and final movement into one candidate. Station/spatial, train and
changed car/Traffic revisions advance once. Occupancy matches the final consist,
station fare is booked before any same-move expense, and expense uses current
post-exchange passengers. Existing Outside fare/reload behavior is preserved.

Ordered passenger effects and `DepartureBellIntent` values are returned with the
callback candidate and collected on the detached minute. `MixedSessionState`
exposes them only after all callbacks, accounting, continuation scheduling and
whole-world validation succeed. Failed minutes preserve the entire prior world,
accounts, queue, random state and effect publication. These ephemeral collections
describe only the last successfully published minute and are absent from Freeze,
saves and snapshots. They are domain intents, with no new network/audio event;
P6-20 owns committed delivery, deduplication and local playback. P7 owns applying
land-value and city traffic effects.

## Verification and reproduction

`StationYardStopTests` supplies **40 new .NET cases**. All eight independent
L4/N3–6 direction cases and all 14 original yard witnesses execute through
production dispatch against stations/platforms created by gameplay construction
operations and stock purchased/deployed by the production domain. Explicit
fixture counts/handler results isolate source callback boundaries. Additional
production Simple/TAT/TURNING cases verify both wide lanes, incoming count after
handler mutation, pre-unload reversal, captured-head speed, current inherited
controller and removal/rebuild. Real travel between two constructed stations
loads from controlled demand and earns exact distance-based fare at the second
station, without passenger injection.

Counter overflow, fare cash overflow, departure expense underflow, queue-order
overflow and a later failed equal-time station callback discard the complete
minute, including passenger and bell intents. Actual waits/departures and
two-station travel continue identically after current-format reopen.

`station-yard.spec.ts` consumes a generated legal scene and unit-generated
continuation oracles from those tests. An EN desktop operator and JA narrow
touch Observer receive identical actual Simple arrival/wait, packed boarding,
movement/occupancy/cost and queue state. HTTP reopen and a real checkpoint process
restart during the wait retain exact successors. A duplicate departure command
receipt has no second effect and the canvas survives ordinary publications.
The browser scene has one real station and controlled supply; the full two-station
UI construction/purchase/service scenario remains P6-21.

Fresh verification passes **1,503 relevant Release .NET regressions** and the
final **40/40 yard matrix**, **375 web unit/retention tests**, **14 distinct
selected Chromium scenarios**, **28 station Python tests**, all **821 independent
fixtures**, raw pinned-source review, generated contracts/content, EN/JA locales,
Svelte/TypeScript (zero errors/warnings), production build, project boundaries
and whitespace checks. The existing Vite bundle-size advisory remains.

The initial browser sweep passed12/14; both signal tests rejected old-content
prerequisite saves at upload. Regenerating those saves from current production
and rerunning passes2/2. The new yard browser and11 other scenarios passed
the initial sweep. Early .NET harness failures in fixture dimensions, revisions,
controller delegation and collection comparison were corrected; final runs
resolve them. Sandboxed compiler startup needed local IPC sockets, so accepted
.NET and browser runs enabled local sockets.

```sh
FREETRAIN_YARD_FLEET_OUTPUT="$PWD/tmp/p6-19-yard-v10.json" dotnet test tests/FreeTrain.Online.Tests -c Release --no-restore -m:1 -p:UseSharedCompilation=false --filter 'FullyQualifiedName~StationYardStopTests'
dotnet test tests/FreeTrain.Online.Tests -c Release --no-build --no-restore -m:1 --filter 'FullyQualifiedName~Station|FullyQualifiedName~Platform|FullyQualifiedName~Passage|FullyQualifiedName~Mixed|FullyQualifiedName~Fleet|FullyQualifiedName~Train|FullyQualifiedName~ContractTests|FullyQualifiedName~NetworkTests'
FREETRAIN_SIGNAL_FLEET_OUTPUT="$PWD/tmp/p5-24-signal-fleet" dotnet test tests/FreeTrain.Online.Tests -c Release --no-build --no-restore -m:1 --filter 'FullyQualifiedName~Signal_real_shared_harbor'
DENO_JOBS=4 deno task --cwd web test
deno task --cwd web build
FREETRAIN_YARD_FLEET_OUTPUT="$PWD/tmp/p6-19-yard-v10.json" deno task --cwd web test:browser:run station-yard.spec.ts station-transport.spec.ts station-hosts.spec.ts thin-platform.spec.ts platform-lanes.spec.ts signal-integration.spec.ts --workers 2
python3 -m unittest discover -s scripts/tests -p 'test_station*.py'
python3 scripts/station-fixtures.py --check --source-root /Users/dmark/dev/freetrain-exav-archive
python3 scripts/review-stations.py --check --source-root /Users/dmark/dev/freetrain-exav-archive
python3 scripts/check-project-boundaries.py
git diff --check
```

Logs and browser evidence: `tmp/p6-19-*`. Portable browser/runtime verification
does not establish native/original-runtime, other-engine, optional-content,
capacity/soak, Linux/container, deployment or full P6 acceptance.
