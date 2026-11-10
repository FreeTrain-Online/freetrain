# P6-04 station booking and callback lifecycle — 8 November 2026

Complete at detached contract scope. The [source contract](web-parity-station-contract.md),
[independent fixtures](web-parity-station-fixtures.md) and
[canonical graph](web-parity-station-state.md) supply the inputs. Construction
commands and browser station gameplay remain with P6-06 onward; passenger decay,
demand and transport-log effects remain with P6-17. Save9/protocol7 and their
explicit rejection of station graphs remain unchanged until P6-05.

## Source booking callers

[`StationBooking`](../src/FreeTrain.Online.Simulation/StationBooking.cs) produces
immutable quotes through the existing ConstructionBooking/ConstructionAccounts
mechanism. It does not allocate identities, draw randomness or change geometry,
cash, time or the queue. Each constructing slice must validate geometry and use
the same content and terrain state for its preview and commit.

| Caller | Booking |
| --- | --- |
| Station placement | Definition's Int64 price to RAIL_SERVICE immediately; no ConstructionSite or completion callback. |
| Station demolition | Checked Int32 `500000 * sizeX * sizeY` to SUBSIDIARIES; no refund. |
| Station daily | Definition's operationCost to RAIL_SERVICE, separately from platform upkeep. |
| Platform placement | Checked Int32 `500000 * length * multiplier`, then Int64 booking; only the base ground enters the quote. Height below ground multiplies by `height * -2`, otherwise `height + 1`. The source base-ground/water early return records one zero spend. |
| Platform daily | Checked Int32 `180000 * length` to RAIL_SERVICE. |
| Platform removal, lane changes, passage/stair changes | No spend call, distinct from a zero spend. |

The historical memo's 18-unit platform upkeep is not an ExAV rule. The pinned
Platform daily caller uses **180,000 per length**, so length 4 costs 720,000; a
station definition with operationCost 600,000 brings the combined daily amount
to 1,320,000. Source-width overflows reject instead of wrapping or widening the
intermediate product. All 19 literal cost vectors, including the 4,294/4,295
placement and 11,930/11,931 upkeep boundaries, are production consumers. All 35
bundled station price/operationCost pairs are checked separately.

## Registration, cancellation and atomic publication

[`StationLifecycle.Reconcile`](../src/FreeTrain.Online.Simulation/StationLifecycle.cs)
restores a detached instance of the existing FIFO Clock. New buildings register
hourly then daily timers in graph order; new platforms follow in graph order.
The station first delays are `60 - now % 60` and `1440 - now % 1440`.
A platform's first delay is 1440, relative to creation. Exact hour/midnight creation
schedules the next occurrence, never the current minute. At 08:37, a building's
first callbacks are 09:00 and next 00:00, while the platform's first daily callback
is next 08:37. These match all four independent timer lifecycle vectors.

Removal unregisters only the removed owner's handles. Unrelated train/site
entries and retained station/platform registrations, due times and FIFO orders
survive unchanged. Retained timers cannot be dropped or rewritten. New owners
must enter a session successor with their required timers; directly constructed
inactive P6-03 graphs remain available as detached fixtures. Queue validation
requires exact equality with each graph timer, one registration per handler,
source station alignment and a remaining delay within its period. Unknown,
duplicate, missing, expired or inconsistent entries reject.

`StationLifecycle.Commit` combines the candidate graph and spatial state,
registration/cancellation, ordered booking and session revision in one detached
MixedSessionState successor. It uses the existing whole-world identity and
revision validation. Construction mode does not waive charges. Failed account
arithmetic, queue capacity, order-counter exhaustion or clock-range admission
leaves the original geometry, allocator, names, accounts, clock and RNG intact.
`MixedConstructionState.Replace` also accepts explicit station graph successors.

## Callback boundary and later consumers

MixedSessionState binds active station/platform timers alongside construction
and train callbacks. A trusted StationCallback dispatcher can use
`StationLifecycle.Tick` to book upkeep before applying pure graph effects,
revise the logical/spatial owner and retain the repeated timer's registration.
The next due minute and FIFO order must match the existing Clock's repeat entry.
Freeze/successor copies and first-fleet initialization retain the dispatcher.
Session candidate validation now checks the candidate station graph explicitly.

The default station dispatcher now runs
[`StationStatistics.Tick`](web-parity-station-statistics.md) through
`StationLifecycle.Tick`, retaining timer continuation and upkeep booking while
applying hourly passenger decay and daily transport-log rotation. A trusted
dispatcher can supply effects without changing timer ownership. No delegate is
serialized.

This replaces the earlier contract-stage
`stationDispatchRequiresP6PassengerLifecycle` rejection: once live construction
was enabled, an ordinary station's next hourly callback threw through the server
driver and stopped the host. This could coincide with a train reaching a
platform, but requires no train to reproduce. Regression tests exercise the
default dispatcher across two days, daily station/platform charges and save
restoration, while retaining rejection of malformed callback successors.
Crash-fix verification: Release backend **1,812/1,812 pass**, zero skipped;
`tmp/station-timer-crash-full-tests.log`. Station source-contract review, project
boundaries and whitespace checks also pass.

The existing detached minute batch publishes only after every callback and the
final boundary validate. Tests run hourly/daily/platform repeats through the
actual scheduler, including day/platform/hour equality after a full day of
hourly reinsertion, and shared station/site/train equality. A late hourly fault
after both daily charges discards those charges, prior graph changes, consumed
randomness and every queue change; a retry starts from the same boundary.
These checks grant lifecycle acceptance, not boarding, fares or passenger-log
acceptance. The 6 independent passenger FIFO witnesses retain P6-17/19/25 ownership.

## Verification

[`StationLifecycleTests`](../tests/FreeTrain.Online.Tests/StationLifecycleTests.cs)
adds 14 production cases, consuming the 19 cost, 35 definition and 4 timer literals.
It verifies pure quotes, one-successor publication in both construction modes,
independent removal/restored rails, repeated-cookie/FIFO continuation, relative
upkeep, shared construction/train callbacks, malformed schedules and atomic
late rejection. Existing station-state helpers are shared by these tests.

Fresh verification on 8 November 2026:

- Release backend: **1,695/1,695 pass**, zero failed/skipped;
  `tmp/p6-04-dotnet-final.log`. Focused final lifecycle cases: **14/14 pass**;
  `tmp/p6-04-dotnet-lifecycle.log`.
- Client: **315/315 pass**, including two retention cases;
  `tmp/p6-04-web-unit.log`.
- Web build, generated contracts, content, locales and Svelte/TypeScript checks
  pass with zero Svelte errors/warnings; `tmp/p6-04-web-build.log`.
  The existing large-bundle advisory remains.
- Chromium: **22/22 pass**; `tmp/p6-04-browser.log`.
- Python: **221 run, 220 pass, one existing skip**; `tmp/p6-04-python.log`.
- Independent fixture validation: **821 cases / 24 families / 38 pinned sources**;
  `tmp/p6-04-fixtures.log`. Source review and project boundaries pass;
  `tmp/p6-04-review.log`, `tmp/p6-04-boundaries.log`. Whitespace checks pass.

Reproduce from the repository root:

```sh
dotnet test FreeTrain.Online.slnx -c Release --no-restore -m:1 -nodeReuse:false -p:UseSharedCompilation=false
DENO_JOBS=4 deno task --cwd web test
deno task --cwd web build
deno task --cwd web test:browser:run station-editing.spec.ts stationary-construction.spec.ts fleet-controller-manager.spec.ts signal-integration.spec.ts
python3 scripts/station-fixtures.py --check
python3 scripts/review-stations.py --check
python3 scripts/check-project-boundaries.py
python3 -m unittest discover -s scripts/tests
```

Evidence is retained under `tmp/p6-04-*`. The .NET test runner and Chromium
require local IPC/loopback permission. Browser checks regress existing EN/JA
station-editor, decorative construction, controller and signal workflows,
including peers, save/reopen and checkpoint restart; they do not construct P6
service stations. Native, other engines/devices, optional-suite, container and
capacity acceptance remain outside this step.
