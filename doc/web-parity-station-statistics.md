# P6-17 hourly/daily station state and live upkeep — 10 November 2026

Complete at station statistics/callback scope. The production dispatcher now
applies passenger decay and transport-log rotation, including after ordinary
save/checkpoint restoration. Actual yard exchange, boarding, unloading and fares
remain P6-18/19; information/list presentation remains P6-23. This step adds no
passenger injection or population editor to gameplay.

Authority is stable ExAV 2.1.1.12, Git
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`,
`trunk/core/world/rail/Station.cs`: `clockHandlerHourJust`,
`clockHandlerDayJust`, `averageLoadedPassengers`, `averageUnloadedPassengers`
and `TransportLog`. The [source contract](web-parity-station-contract.md) and
[independent fixtures](web-parity-station-fixtures.md) retain their original
source hashes, arithmetic expectations and scope qualifications.

## Live behavior

[`StationStatistics`](../src/FreeTrain.Online.Simulation/StationStatistics.cs)
is the default trusted dispatcher in
[`MixedSessionState`](../src/FreeTrain.Online.Simulation/MixedSessionState.cs).
Every aligned station hour performs Single multiplication and truncation toward
zero: gone × `0.8f`, accumulated loaded/unloaded × `0.9996f`. The daily-average
queries use the independently rounded Single constant
`24.0f * (1.0f - 0.9996f)` = `0.009600162506103516`. Accumulators stay Int32;
no double substitution, rounding-to-nearest integer, or exact decimal factor is
used. Average queries do not alter counters or add serialized fields.

Demand continues to query retained controlled providers using the current
`GameTime` through `StationCatchment.Demand`. Waiting remains
`max(0, demand - gone)`. An hourly demand decrease can leave gone above demand;
the callback decays gone without clamping it to the new population or resetting
it. No cached population or new city source is introduced. P7 retains
constructible producers and city/land-value evolution.

At station midnight, upkeep is booked before the pure effects. Import, export
and train logs each add `Math.Pow(today, 1.0 / 5.0)` to this week's accumulator,
copy today to yesterday and clear today. If the callback's current day is
Saturday (`DayOfWeek == 6`), their fifth-power weekly mean becomes last week's
per-day value and this week's accumulator clears. The calculation uses the
new day at midnight. The current-week per-day query retains the source divisor
`DayOfWeek + 1`. Nonfinite results reject the detached minute.

[`StationLifecycle`](../src/FreeTrain.Online.Simulation/StationLifecycle.cs)
continues to own booking, revision publication and repeated timers. A station
created at 08:37 first decays at 09:00 and pays at the next midnight. Platforms
pay `180,000 × length` at creation plus 24 hours, and repeat from that due time;
station midnight does not shift or duplicate either thin or wide platform
charges. Removal cancels only the removed owners' handles.

Logical/spatial revisions, counters, logs, accounts and the exact next FIFO
queue publish in one detached successor. Registrations remain stable while
repeat orders advance. Equal-time hour/day/train callbacks keep their actual
queue order. Late failures discard earlier effects and charges in the same
minute. Freeze, first fleet creation, ordinary reopen and process checkpoint
restart retain the default dispatcher without registering new handles.
Save10/protocol8, catalog/content identity and runtime limits are unchanged.

## Verification

- **1,428 relevant Release .NET tests pass** across station/platform/passage,
  mixed worlds, fleet/train, persistence, contracts and networking. Final focused
  **54/54** also pass against the completed source and refreshed fixture metadata.
- The 19 new cases execute all **8 independent float32 vectors**, **2 seven-day
  log traces** and **6 equal-time FIFO traces**, plus actual demand-drop/recovery,
  week-long uninterrupted versus repeatedly reopened continuation, and atomic
  rejection of a nonfinite Saturday result. The FIFO witnesses use real
  scheduled station callbacks and controlled train effects; actual yard exchange
  retains P6-19 ownership. They preserve the literal 600,000 station and 720,000
  platform costs. Existing construction/removal tests verify timer cancellation
  and live thin/wide upkeep.
- **375 web unit/retention tests pass** (373 ordinary plus two retention).
  Generated contracts/content, EN/JA locale checks, Svelte/TypeScript and the
  production web build pass with zero check errors/warnings. The existing Vite
  bundle-size advisory remains.
- **9 distinct selected Chromium scenarios pass**, without skips: the broad
  run's eight existing station construction/host/transport scenarios and the
  final two-case transport run, including the new P6-17 witness. EN desktop and
  JA narrow touch peers receive identical counter/log/account/queue updates at
  aligned-hour, midnight and relative platform boundaries. Duplicate command
  receipts, exact reopen, no repeat effects on the next minute and actual
  checkpoint process restart pass. Boundary states are explicitly authored;
  the .NET tests supply the uninterrupted multi-day oracle. Existing scenarios
  retain role, draft, stale, removal and replacement coverage.
- **28 station Python tests**, **821 independent cases / 38 pinned fixture
  sources**, full pinned station source review, project boundaries and whitespace
  checks pass. A stale P6-16 runtime-policy prerequisite hash was refreshed in
  `station-review.json` and propagated to the fixture's prerequisite/internal
  digest. Original source hashes, decisions, matrix inputs and expected outputs
  are unchanged.

Logs: `tmp/p6-17-dotnet.log`, `tmp/p6-17-focused-final.log`,
`tmp/p6-17-web-unit.log`, `tmp/p6-17-web-build.log`,
`tmp/p6-17-browser.log`, `tmp/p6-17-browser-accepted.log`,
`tmp/p6-17-python-final.log`, `tmp/p6-17-{fixtures,review,boundaries}.log`.
Browser evidence: `tmp/p6-17-browser/` and `tmp/p6-17-browser-accepted/`.
Initial new-browser runs corrected authored queue schema/order, replacement
readiness and the test cost lookup; no runtime validation was relaxed.

```sh
dotnet test tests/FreeTrain.Online.Tests -c Release --no-restore -m:1 --filter 'FullyQualifiedName~StationStatisticsTests|FullyQualifiedName~StationPersistenceTests|FullyQualifiedName~StationLifecycleTests'
dotnet test tests/FreeTrain.Online.Tests -c Release --no-build --no-restore -m:1 --filter 'FullyQualifiedName~Station|FullyQualifiedName~Platform|FullyQualifiedName~Passage|FullyQualifiedName~Mixed|FullyQualifiedName~Fleet|FullyQualifiedName~Train|FullyQualifiedName~ContractTests|FullyQualifiedName~NetworkTests'
DENO_JOBS=4 deno task --cwd web test
deno task --cwd web build
deno task --cwd web test:browser:run station-transport.spec.ts station-building.spec.ts station-building-domain.spec.ts station-hosts.spec.ts
python3 -m unittest discover -s scripts/tests -p 'test_station*.py'
python3 scripts/station-fixtures.py --check --source-root /Users/dmark/dev/freetrain-exav-archive
python3 scripts/review-stations.py --check --source-root /Users/dmark/dev/freetrain-exav-archive
python3 scripts/check-project-boundaries.py
git diff --check
```

Native/original-runtime fidelity, other browser engines, optional content,
capacity/soak, Linux/container, deployment and full P6 acceptance remain open.
Next: P6-18 boarding, unloading, fare and station side effects.
