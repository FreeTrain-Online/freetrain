# P5-24 real signals and shared controllers — 7 November 2026

Both bundled signal types now have integrated browser/portable evidence with
purchased, deployed trains. Signal properties configure the selected shared
controller at that signal's location; the controller manager edits the same data.
Each passing train resolves its own effective controller. The reserved
`RailComponent.ControllerId` remains null (TRAIN-D010).

Manual driving was removed from canonical gameplay on 7 November; the
[removal decision](web-parity-fleet-manual.md) supersedes this step's older
manual/TAT wording. This slice verifies two independent timetable controllers
and Simple. It neither restores Manual nor credits the historical steering tests
as current functionality.

## Source and behavior

Authority is stable ExAV 2.1.1.12, archive Git
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, raw `trunk/` objects. The
[train contract](web-parity-train-contract.md),
[pinned review](parity/train-review.json),
[localization decisions](parity/train-localization-review.json), and
[independent fixtures](parity/train-fixtures.json) remain the source bindings.
No native executable was run.

| Source | Delivered contract |
| --- | --- |
| `plugins/org.kohsuke.freetrain.rail.signal/src/SignalRailRoad.cs`, `getStopTimeSpan` | Delegate through the passing train's controller only when the heading is the signal direction or either adjacent eighth-turn; all other headings proceed. Both bundled graphics use this behavior. |
| Same file, `remove` | An occupied signal rejects removal. An empty signal restores ordinary track with its pattern. No signal booking or reservation system. |
| `core/world/rail/Train.cs`, `clockHandler` | A positive wait increments `stopCallCount`, retains cars/passengers/forwarding, publishes `StoppingAtStation` and schedules one callback. Later departure resets the counter and moves under the ordinary collision rules. |
| `core/world/rail/SimpleTrainControllerImpl.cs` | A signal is a non-Station harbor; Simple passes without waiting. |
| TAT `StationHandler` and location stores | Each controller owns its own location-keyed settings. A missing setting waits ten minutes on a first call, then proceeds. Removing/rebuilding a signal preserves those settings for reuse at that location. |
| Train-garage rail behavior | Cars traverse the garage's rail behavior; no maintenance, repair or passenger exchange is introduced. P6 owns actual yards/stations. |

The direction probes cover all eight arrival headings against all four legal
cardinal signal directions, separately for each graphic. Diagonal arrivals are
explicit callback probes, not admitted diagonal saved signal poses. Cardinal
placement and rejection of diagonal signal directions are checked separately.
Optional automatic-block/pass-monitor plugins retain pack-qualified behavior;
bundled signals receive no implied block-aspect or passenger functionality.

## Browser integration

Signal inspection allows a local choice of which shared timetable to configure.
Its coordinate fields and stored-position selector are now locked to the picked
signal; opening the controller manager retains general location editing. Opening,
Cancel and selecting a controller do not assign that controller to the signal or
change any train. Existing controller revision, role, epoch, capacity and receipt
fences continue to apply.

EN/JA construction help now describes delivered controller editing. Signal help
explains the direction gate, effective-controller dispatch, Simple behavior and
absence of passenger exchange. Positive waits still display the generic
**Waiting for departure / 発車待ち** meaning, with separate **Stopped at signal /
信号で停止中** context. The durable and wire state remains `stoppingatstation`;
this is the GEO-005/TRAIN-WORD-002 disposition, not a new signal state.

The context lookup reads the current head and registered rail recipe without
calling a controller, advancing time or changing the snapshot. Client tests check
both signal bindings, ordinary rail, moving/Outside states and read-only behavior.

## Independent journey and recovery evidence

`FleetSignalIntegrationTests.cs` builds ordinary rail through the construction
API, replaces one cell with the selected signal, purchases three one-car bundled
trains, and deploys them through the actual placement API. Construction and
purchase accounting is retained. Two separately identified TAT instances use ten
and twenty minutes at the same location; the third train uses Simple. Expected
positions/times/counters are handwritten source traces:

| Game minute | Literal observation |
| --- | --- |
| 102 | First train waits at X22, moveCount0/callCount1, callback due112; following trains reach X21/X19. |
| 104 | Second train is blocked at X21 while third reaches X20. |
| 112 | First departs to X23, second enters X22, third enters X21. |
| 114 | Second waits at X22, moveCount2/callCount1, callback due134; third is blocked at X21. |
| 134 | Second departs to X23 and Simple enters X22. |
| 136 | Second reaches X24 and Simple passes to X23 with callCount0/moveCount5. |

Each boundary reopens the prior save and compares the complete canonical save
bytes after advancing to the next boundary. Checks include exact occupancy,
one callback per placed train, reserved-null signal controller, zero passengers
and absent boarding origin. The fixture writer emits both browser input saves
only from this gameplay-built setup.

Selected session checks reject occupied signal removal without mutation and
reject deletion of a referenced controller. Removing a harbor setting while
waiting preserves trains and pending due times; the existing later callback
proceeds. Withdrawing followers permits signal removal/rebuild; the second
controller retains both location entries. Redeploying its train at the rebuilt
signal performs its twenty-minute wait, including exact reopen continuation.

The EN three-aspect and JA shunting browser journeys edit through the manager
and actual signal-art picking, verify coordinate locking and Cancel, and confirm
independent per-controller settings. They compare an Observer peer's full
mixed snapshot during the first wait, upload/reopen the waiting save, and restart
the actual backend process from its checkpoint before continuing to the second
wait and Simple passage. Removing the second harbor setting during its pending
wait leaves the callback intact. Both runs check source signal pixels and capture
settings and both waits. The pixel helper now accepts the actual map height;
its default remains the existing 42-row construction fixture projection.

## Fresh verification

- **55/55 selected .NET tests**, including six new integration cases, signal
  construction/session/fixture tests, controller sharing/referenced deletion,
  SCN-008 daily waits and canonical Manual-removal guards.
- **32/32 selected client tests**: fleet state, shared controllers and localization.
- **3/3 selected Chromium tests**: the two new journeys plus existing signal
  inspector configure/remove coverage. Production web, Debug backend. JA runs at
  390×844 with 200% root text and canvas keyboard stepping. EN/JA settings/wait
  captures are under `tmp/p5-24-browser-evidence/signals/` and were visually
  inspected. A final **2/2** journey repeat verifies wait-context visibility and
  restores 200% text after reopen/restart.
- Production build, generated contracts/content, EN/JA locale validation and
  Svelte checks pass (zero errors/warnings; existing bundle-size advisory).
- Pinned 70-file source review, 744 fixture specifications, project boundaries
  and whitespace checks pass. Logs and gameplay inputs: `tmp/p5-24-*`.

Only selected web/backend tests were run. This slice adds no native/IME/human
fidelity, Linux/container, optional-suite or capacity acceptance. P5-25–30 and
P6 passenger/yard integration retain their scopes; the Manual removal remains
explicit in this step's acceptance.

To regenerate the gameplay browser inputs, run from the repository root:

```sh
FREETRAIN_SIGNAL_FLEET_OUTPUT="$PWD/tmp/p5-24-signal-fleet" dotnet test tests/FreeTrain.Online.Tests/FreeTrain.Online.Tests.csproj --no-restore --filter 'FullyQualifiedName~Signal_real_shared_harbor' -m:1 -nodeReuse:false -p:UseSharedCompilation=false
```

After `cd web` and `deno task build`, run only the selected browser cases:

```sh
FREETRAIN_ONLINE_TEST_SERVER_CONFIGURATION=Debug deno task test:browser:run --project=canonical signal-integration.spec.ts fleet-controller-manager.spec.ts --grep 'real shared signal|signal inspector' --workers=1
```
