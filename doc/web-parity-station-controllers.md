# P6-22 shared station timetables and controller matrix — 10 November 2026

Picked station buildings and connected slim/wide platforms now expose the existing
shared TAT configuration UI in English and Japanese. The key is the **resolved
station location**, including connections through another platform. Signal
configuration keeps its own location and passenger-free behavior. No save,
protocol or content version changes: Save10/protocol9 remain current.

## Configuration and identity

Create a named TAT in Trains → controller manager, then pick a station or connected
platform, choose that shared instance and configure its stop settings. All
platforms connected to the same station select the same entry. Another station
has its own entry in the same instance. This edits shared configuration; trains
continue to use their actual train/group assignment on every callback.

The new `stationSharedController` EN/JA text is source-derived workflow guidance
from the location-key/host contract, not an original XML title or historical
English donor. Japanese harbor help now says the connected platform’s station
rather than a garage, matching the actual yard/host operation.

The inspector uses the existing cycle-safe `resolvedPlatformStation` query, so a
disconnected or removed host exposes no station configuration. Changing the
resolved identity, platform parent or world epoch destroys the picked editor and
its pending rule/file draft. Unapplied station/platform property drafts disable
configuration mutations. Existing role, pending-command, controller revision and
rule import fences remain active. Removing a station retains its location settings
under TRAIN-D012; rebuilding creates a new identity and explicitly reconnecting
platforms reuses those settings. A nearby rebuild does not automatically rehost
existing disconnected platforms.

## Real controller witnesses

The source inputs remain ExAV `883cf7d` `YardRailRoad`, `StationHandler`,
`AdvancedStationHandler`, `TimeMask` and Simple controller, bound by the
[station contract](web-parity-station-contract.md), [independent station
fixtures](web-parity-station-fixtures.md) and [train handoff](web-parity-p6-train-handoff.md).
The detached controller algorithms already served production yards; this slice
connects the picked workflow and adds real-host acceptance witnesses.

`StationControllerMatrixTests.cs` extends `StationYardStopTests` with production
clock dispatch at constructed yards. Its observed controller calls use the active
callback time and actual station ID/revision, station key and distinct rail
position. Literal expected first/later source durations are:

| Configuration | First call | Later call | Yard departure |
| --- | ---: | ---: | --- |
| Simple | 30 | 0 | Board, bell, move |
| Absent TAT setting | 10 | 0 | Board, bell, move |
| Fixed40 with reverse | 40 | −1 | Board, bell, reverse, move |
| Fixed1440 | 1440 | 0 | Board, bell, move |
| Daily equal to arrival, reverse | 1440 | −1 | Board, bell, reverse, move |
| Advanced Go before Stop | 10 | 0 | Board, bell, move |
| Advanced Reverse before Stop | 10 | −1 | Board, bell, reverse, move |
| Advanced Forwarding | 10 | −999 | Bell and move, no boarding |
| Advanced ReverseForwarding | 10 | −1000 | Bell, reverse and move, no boarding |
| Advanced TURNING reverse | 10 | −1 | Handler turn, board, bell, central reverse and move |
| Advanced TURNING forwarding | 10 | −1000 | Handler turn, bell, central reverse and move, no boarding |

A nonmatching month mask precedes matching wildcard rows. Stored Go/Reverse/etc.
rows are ignored on arrival, where the following Stop wins; they win on the
later call. A separate Pass-before-Stop case moves without station exchange or
bell. Both actual same-host platforms independently stop for the same Fixed40
setting, board and emit the correct platform's bell. Day-long waits retain exact
hour/day rotation. Every wait/departure successor matches a current-format
reopen, with one train callback.

A separate construction/removal witness retains two independent station entries,
rebuilds one station with a different ID, reconnects its two platforms and checks
unchanged location settings and exact save bytes. It also generates and checks
the committed browser baseline `station-controller-v10.json`. The scene contains
only domain-constructed structures and purchased/deployed stock, with zero seeded
passengers/origins/stop calls and no test harbor. Its rail-owned special
`ConstantPopulation(10000)` is explicitly controlled P7 interface supply.

## Browser acceptance

`station-controller.spec.ts` runs EN desktop and JA 430×860 touch workflows:

- Create a fresh shared TAT with no settings; pick two actual platforms and their
  station building. Disabled coordinates show the station key and all three
  entry points see the same Fixed40 setting. A second station retains a separate
  Pass entry; remove/re-add of the first entry preserves that second entry.
- Open Advanced through the other platform, preview a real legacy `.ftdd` without
  mutation, append its nine ordered rules (including duplicate masks), export
  `.ftdd.json` and preserve the exact rule/save bytes through world replacement.
  Existing companion parser, append/capacity and peer-conflict regressions remain
  separate checks; this is no desktop-world import claim.
- Assign the real train's group to that TAT. Its production station arrival
  schedules exactly40 minutes. Reassign the group to Simple during the wait;
  departure resolves the current assignment and boards controlled passengers.
  Save/reopen plus independent process checkpoint restart reproduce the exact
  uninterrupted departure bytes on both socket peers.
- Reassign the moving train's group to TAT. The independent station's Pass entry
  leaves its log unchanged and retains the carried passengers.
- A peer removes the original host while a local Fixed90 draft is open. The
  picked editor disappears and the committed Fixed40 entry remains. Rebuild via
  the construction UI, explicitly reconnect the first platform and connect the
  second through it; the same setting is visible through the new host graph.

Full mixed-state publications are compared at both live sockets after commands
and each stepped minute. Screenshots include the English and Japanese picked
configuration, and the narrow viewport has no document overflow. Manual remains
absent/rejected by the existing canonical controller/protocol regressions.

## Verification

Fresh verification passes:

- **1,534 relevant Release .NET tests, no skips**, including 15 new real-station
  controller cases and the committed baseline byte check.
- **379 web unit/retention tests** (377 ordinary + 2 retention), including the
  added chained/disconnected/removed/cyclic-host location query case.
- **36 distinct Chromium scenarios, no skips**: 2 new final-build EN/JA station
  journeys; 20 controller/group cases; 14 passenger-service, platform properties,
  station-building, legacy/portable companion-file and signal regressions.
  The initial 34-case regression run had 18 fixture-load failures because older
  ignored P5 inputs carried the obsolete bundled catalog hash. Source-built
  fixture regeneration and the complete 20-case controller/group rerun resolve
  all 18; the other 14 scenarios passed in the original run. These are combined
  final passing results, not a claim that the initial 34-case run passed.
- **28 station Python tests**, all **821 independent station fixtures**, pinned
  station-source review, generated contracts/content, EN/JA locale checks,
  Svelte/TypeScript (zero errors/warnings), production build, project boundaries
  and whitespace checks.

The final new journeys also pass after the Japanese wording correction. Initial
new-harness issues in active callback time, midnight log assertions, current
host revisions, exposed platform picking and snapshot clock/label selectors
were corrected. No production controller arithmetic change was necessary.
The existing Vite bundle-size advisory remains.

Reproduce the new scenario and core checks from the repository root:

```sh
dotnet test tests/FreeTrain.Online.Tests -c Release --no-restore -m:1 -p:UseSharedCompilation=false --filter FullyQualifiedName~StationYardStopTests
dotnet test tests/FreeTrain.Online.Tests -c Release --no-build --no-restore -m:1 --filter 'FullyQualifiedName~Station|FullyQualifiedName~Platform|FullyQualifiedName~Passage|FullyQualifiedName~Mixed|FullyQualifiedName~Fleet|FullyQualifiedName~Train|FullyQualifiedName~ContractTests|FullyQualifiedName~NetworkTests'
DENO_JOBS=4 deno task --cwd web test
deno task --cwd web build
deno task --cwd web test:browser:run station-controller.spec.ts --project=canonical --workers=1
deno task --cwd web test:browser:run passenger-service.spec.ts station-building.spec.ts platform-properties.spec.ts fleet-controller-manager.spec.ts fleet-groups.spec.ts fleet-rule-file.spec.ts signal-integration.spec.ts --project=canonical --workers=2
python3 -m unittest discover -s scripts/tests -p 'test_station*.py'
python3 scripts/station-fixtures.py --check --source-root /path/to/freetrain-exav-archive
python3 scripts/review-stations.py --check --source-root /path/to/freetrain-exav-archive
python3 scripts/check-project-boundaries.py
git diff --check
```

Existing P5 browser suites require their domain-generated current-content inputs.
This run refreshed them with `Manager_browser_input`, `TAT_junction_manager`,
`Turning_real_signal`, `Signal_real_shared_harbor` and `Rule_file` method filters,
using `FREETRAIN_CONTROLLER_FLEET_OUTPUT`, `FREETRAIN_TAT_FLEET_OUTPUT`,
`FREETRAIN_TURNING_FLEET_OUTPUT`, `FREETRAIN_SIGNAL_FLEET_OUTPUT` and
`FREETRAIN_RULE_FILE_FLEET_OUTPUT` at their existing `tmp/p5-*` paths. The Advanced
P5-20 input is the same P5-19 TAT baseline. These focused fixture refreshes pass
12 and 18 .NET cases respectively. The new P6-22 browser input is committed and
requires no ignored fixture setup; SHA-256:
`477a5eef3d1d77b939be84273815871de973bd0da2f1e7dbc41b355871442f41`.

Logs use `tmp/p6-22-*`; new accepted EN/JA captures and state evidence are in
`tmp/p6-22-browser-accepted/`. Native/original-runtime, other browser engines,
optional content, capacity/soak, Linux/container, evolving city producers, full
station information/list and overall P6 acceptance retain their later gates.
