# P6-18 boarding, unloading, fare and station side effects — 10 November 2026

Delivered at detached passenger-exchange scope. Actual yard gating, controller
dispatch and movement integration remain P6-19; departure-bell delivery remains
P6-20 and the first complete service browser scenario remains P6-21.

Authority is stable ExAV 2.1.1.12, Git
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`,
`trunk/core/world/rail/{Station,Train,GlobalTrafficMonitor}.cs`, under
STATION-D005/008 and TRAIN-D008. See the
[source contract](web-parity-station-contract.md#passenger-arithmetic-and-yard-order)
and [independent fixtures](web-parity-station-fixtures.md).

## Implementation

[`StationPassengerExchange`](../src/FreeTrain.Online.Simulation/StationPassengerExchange.cs)
provides pure station/train exchange operations and adapters that resolve real
current-time catchment demand and registered stock from `MixedSessionState`.
The adapters return a complete immutable construction candidate; they do not
mutate the source session or clock. A caller can publish through the existing
validated successor boundary. No network command, passenger injection tool or
new UI string is introduced.

Boarding preserves the initial checked Int32 `available * amenity` product,
then Single multiplication by `0.01f` and `0.3f`, truncation, and the packed cap.
Normal/seated/packed stock admission is unchanged. Export, gone and accumulated
loaded counters advance with checked addition. Origin is the current placed
head, independently of station location. Every departure records a train event.
Zero population returns after that event and station notification, retaining
the train's previous passengers/origin and omitting its loader notification.
Positive population with depleted waiting supply still invokes loading with
zero and records the current head.

Unloading clears passengers and origin, computes truncated 3D current-head
distance and checks each Int32 fare multiplication. Missing origin omits the
earn call; an existing origin still earns a zero receipt for zero passengers
or zero distance. Import, train-event and accumulated unloaded counters update.
The land-value amount retains the source cast to Single *before* taking
`min(import.LastWeekPerDay / 24, unloaded)`.

Notifications are ordered immutable `StationPassengerEffect` values. Boarding
stages land-value, train-change and station-change effects; zero population
stages only station change. Unloading stages train change, land-value, nonzero
traffic transport and station change. They are returned only with a fully
validated candidate, without invoking live listeners. P7 owns application to
city/land-value evolution; P6-19 owns collecting exchange effects during real
yard dispatch. These values are neither save history nor browser audio events.

Publication advances station/spatial and changed train revisions once, keeps
cars/occupancy and the exact queue unchanged, and books RailService revenue at
the candidate revision/current minute. Overflow, accounting admission or later
callback failure discards the detached candidate. No save/protocol/content
version or runtime limit changes.

[`FleetMovement`](../src/FreeTrain.Online.Simulation/FleetMovement.cs) now shares
the train-only unload/fare operation. Outside still reloads
`min(100, normalCapacity)`, respects Forwarding and records the pre-shift head.
Fare remains before same-move operating expense, with post-exchange passengers
used for the expense. It does not emit station effects.

## Verification

- **37 new .NET cases** consume all 10 independent boarding traces, seven fare
  vectors and seven stock-capacity vectors, including raw diagnostic amenity
  inputs outside normal stock admission. They cover packed occupancy, repeated
  departures/hour decay, zero/depleted demand, origin clearing, missing origin,
  zero fare receipts and checked source-width rejection.
- Actual session/catchment/stock adapters verify station/spatial/train revisions,
  unchanged occupancy/queue, ordinary Save10 reopen and the controlled
  `39 × 3 × 5 × 2 = 1,170` fare. Counter/product/cash overflows and a later failed
  station callback preserve the entire serialized session. The 1,170 witness
  uses an explicit source-legal fare3 stock registration, independently of the
  bundled stock fare; it is not claimed as a yard-service workflow.
- The final focused station-exchange/fleet-movement run passes **380 tests**;
  the broader station/platform/passage, mixed-world, fleet/train, persistence,
  contract and network run passes **1,465 tests**, without skips.
- **10 Chromium scenarios pass**, without skips: nine existing station building,
  host and transport workflows plus the generated gameplay Outside exchange.
  EN desktop/JA narrow station peers and EN/JA Outside peers verify shared
  state, reopen and actual process checkpoint restart. Outside verifies ordered
  fare/cost receipts and normal-capacity reload. These regressions protect the
  current station boundary and shared fare path; real yard service is P6-19/21.
- **375 web unit/retention tests**, generated/content/EN/JA locale checks,
  Svelte/TypeScript and the production build pass. Svelte reports zero errors
  and warnings; the existing Vite bundle-size advisory remains.
- **28 station Python tests**, pinned source review, all **821 independent
  fixtures**, project boundaries and whitespace checks pass. A pre-existing stale
  runtime-policy prerequisite hash and downstream digests were refreshed without
  altering original source hashes, decisions, case inputs or expectations.

Logs: `tmp/p6-18-{focused-final,dotnet,web-unit,web-build,python-final,fixtures-final,review-final,boundaries}.log`,
`tmp/p6-18-browser-accepted.log` and `tmp/p6-18-outside-browser.log`.
The initial sandboxed .NET/browser attempts were blocked by local socket
permissions before test execution; the accepted runs enable those sockets.
The first executing .NET run corrected two test-harness issues: collection
identity versus element comparison, and the custom profile's initial revision.

```sh
FREETRAIN_OUTSIDE_FLEET_OUTPUT="$PWD/tmp/p6-18-outside-v10.json" dotnet test tests/FreeTrain.Online.Tests -c Release --no-restore -m:1 --filter 'FullyQualifiedName~StationPassengerExchangeTests|FullyQualifiedName~FleetMovementTests'
dotnet test tests/FreeTrain.Online.Tests -c Release --no-build --no-restore -m:1 --filter 'FullyQualifiedName~Station|FullyQualifiedName~Platform|FullyQualifiedName~Passage|FullyQualifiedName~Mixed|FullyQualifiedName~Fleet|FullyQualifiedName~Train|FullyQualifiedName~ContractTests|FullyQualifiedName~NetworkTests'
DENO_JOBS=4 deno task --cwd web test
deno task --cwd web build
deno task --cwd web test:browser:run station-transport.spec.ts station-building.spec.ts station-building-domain.spec.ts station-hosts.spec.ts
FREETRAIN_OUTSIDE_FLEET_OUTPUT="$PWD/tmp/p6-18-outside-v10.json" deno task --cwd web test:browser:run fleet-persistence.spec.ts --grep 'P5-14 Outside' --workers 1
python3 -m unittest discover -s scripts/tests -p 'test_station*.py'
python3 scripts/station-fixtures.py --check --source-root /Users/dmark/dev/freetrain-exav-archive
python3 scripts/review-stations.py --check --source-root /Users/dmark/dev/freetrain-exav-archive
python3 scripts/check-project-boundaries.py
git diff --check
```

Native/original-runtime fidelity, other engines, optional content, capacity/soak,
Linux/container, deployment and full P6 acceptance remain open. Next: P6-19.
