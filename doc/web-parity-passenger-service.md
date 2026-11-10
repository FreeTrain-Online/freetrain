# P6-21 complete passenger-service browser scenario — 10 November 2026

The first complete two-station service workflow now exercises actual station and
platform construction, stock purchase/deployment and production Simple yard
callbacks. English desktop/eastbound and Japanese narrow touch/westbound journeys
use the existing game controls. Save10/protocol9 and content identity are unchanged.
This closes this scenario slice; the controller matrix, full station information
and list, broader recovery/capacity and integrated P6 acceptance retain P6-22–28.

## Scene and behavior

The committed `web/tests/fixtures/passenger-service-{east,west}.json` baselines
contain only flat terrain, a legally constructed straight railway and a provider
attached through the P7 producer interface. They contain **no stations, platforms,
trains, passengers, boarding origins or test harbors**. Each provider is labeled
`input: "controlled"`: source `ConstantPopulation(10000)`, land value 10000,
ordinary catchment and unit XY/height scales. The provider's spatial owner is a
rail cell, explicitly a controlled interface witness rather than a city producer.
Constructible and evolving city population remains P7.

The browser constructs single-cell stations at (23,13,2) and (36,13,2), attaches
length-four roofed slim platforms at (25,15,2) and (37,15,2), and buys/deploys a
three-car bundled formation. Source catchment advertising reaches only the origin
station; the destination has **zero supply**. The host graph and memberships are
checked on both peers. Station/platform IDs and timers come from construction;
no passenger or stop-call operands are edited.

Both approach directions demonstrate:

- Disconnect the source platform through its picked Properties controls. The
  actual deployed train passes the yard without stopping, boarding, a station
  call or a departure event. Withdraw, reconnect to the real host and redeploy
  through the UI; the same yard now performs the production Simple stop.
- First arrival waits 30 minutes and publishes no departure bell. Departure boards
  **594 passengers against 396 normal capacity (150%)**, updates the station's
  exports/Gone and reduces waiting from **10,000 to 9,406**. The controlled demand
  itself remains 10,000. The existing train Information display shows the packed
  count and percentage; this is not clamped to 100%.
- Travel between actual yards unloads those passengers at the second station,
  clears the boarding origin, updates imports and accumulated unloading, and books
  **594 × 400 × 12 × 2 = ¥5,702,400** from the source pre-movement boarding head
  to the destination arrival head. Account transactions reconcile to current cash,
  and the visible funds display follows the publication.
- The destination stops despite zero supply, then departs empty with one selected
  platform departure event per peer. Zero supply and disconnection remain distinct.
- Operator and opposite-language Observer receive identical complete mixed state
  after every stepped minute, including cars/occupancy, accounts, station state
  and callback queue. The Japanese narrow workflow uses the visible Game menu's
  Step control and touch platform picking.

Station demand/waiting/log assertions inspect the committed graph and known
controlled input; the full Station Information/StationList UI remains P6-23.
Existing train Information and funds controls are inspected through rendered UI.
Departure events are observed at both live sockets; actual gesture-gated audio
playback remains covered by the separate P6-20 evidence, not inferred from an
event or screenshot here.

## Persistence and independent witnesses

At the first connected wait, the browser downloads the actual constructed world,
executes uninterrupted to departure, restores the waiting save and independently
restarts the server from its checkpoint. Reopen/restart reproduce the waiting save
bytes exactly and publish no historical bell. Repeating the same minutes produces
**the exact uninterrupted departure save bytes**, including accounts, random
state, queue, station counters, passengers, origins, fleet and occupancy. Both
peers see the same successor and one fresh departure event each.

`StationPassengerServiceTests` adds four domain cases: east/west × 10,000/zero
supply. They use gameplay construction/trading/deployment, actual production
controllers and clock dispatch throughout; no injected harbor or passenger count
is used. They independently query source demand/waiting, verify disconnection and
reconnection, packed or empty boarding, exact 12-voxel fare and empty destination
departure. Reopened and uninterrupted saves compare at departure, destination
arrival and the next departure, retaining exactly one train callback. Generated
baseline bytes are checked against the committed browser inputs. Metadata selects
the same station/stock definition and capacities.

The existing pinned [station contract](web-parity-station-contract.md),
[821 literal fixtures](web-parity-station-fixtures.md),
[passenger arithmetic](web-parity-station-passengers.md),
[yard dispatch](web-parity-station-yards.md) and
[departure delivery](web-parity-departure-bells.md) remain the source-bound oracles.
EN/JA building/platform help now describes delivered service, the 30-minute Simple
wait and zero-supply versus disconnected behavior instead of calling service
pending.

## Verification

Fresh verification passes **1,519 relevant Release .NET tests with no skips**,
including the four new service cases, plus a final **4/4** focused run that checks
both committed baseline bytes and stock/station metadata. **378 web unit/retention
tests**, **10 Chromium scenarios with no skips** (the two service journeys and
eight construction/properties/information regressions), **28 station Python tests**,
all **821 independent fixtures**, pinned-source review, generated contracts/content,
EN/JA locale checks, Svelte/TypeScript (zero errors/warnings), production build,
project boundaries and whitespace checks pass. The existing Vite bundle-size
advisory remains.

Initial test-harness issues in storage dimensions, pre-movement fare origin,
map-paint readiness, enum spelling, optional transaction fields and narrow-screen
clock selection were corrected. Final scenarios use visible controls and explicitly
scroll passenger information into view for screenshot verification.

Logs use `tmp/p6-21-*`; accepted service captures and full waiting/departure/
arrival/zero-supply saves are under `tmp/p6-21-browser-accepted/`. The committed
inputs make this browser scenario runnable without ignored temporary fixtures.

```sh
dotnet test tests/FreeTrain.Online.Tests -c Release --no-restore -m:1 -p:UseSharedCompilation=false --filter FullyQualifiedName~StationPassengerServiceTests
dotnet test tests/FreeTrain.Online.Tests -c Release --no-build --no-restore -m:1 --filter 'FullyQualifiedName~Station|FullyQualifiedName~Platform|FullyQualifiedName~Passage|FullyQualifiedName~Mixed|FullyQualifiedName~Fleet|FullyQualifiedName~Train|FullyQualifiedName~ContractTests|FullyQualifiedName~NetworkTests'
DENO_JOBS=4 deno task --cwd web test
deno task --cwd web build
deno task --cwd web test:browser:run passenger-service.spec.ts --project=canonical --workers=1
deno task --cwd web test:browser:run station-building.spec.ts platform-properties.spec.ts train-information.spec.ts --project=canonical --grep-invert 'color|Outside' --workers=2
python3 -m unittest discover -s scripts/tests -p 'test_station*.py'
python3 scripts/station-fixtures.py --check --source-root /Users/dmark/dev/freetrain-exav-archive
python3 scripts/review-stations.py --check --source-root /Users/dmark/dev/freetrain-exav-archive
python3 scripts/check-project-boundaries.py
git diff --check
```

No native/original-runtime, human listening, other-engine, optional-content,
capacity/soak, Linux/container, deployment, city simulation or full P6 acceptance
is credited by these portable Chromium/domain scenarios.
