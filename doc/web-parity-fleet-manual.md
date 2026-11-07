# Manual driving removed — 7 November 2026

Manual train driving was removed on 7 November 2026. Rendering trails the
server, making immediate intervention cause position jumps and invalid-packet
recovery/disconnects. The fleet UI no longer takes control, captures driving
keys or offers hold buttons; canonical sessions no longer lease trains or consume
manual input. Shared Simple, Delegation and timetable controllers, controller/group
management, deployment/reversal and automatic timetable actions remain.

Manual is no longer a canonical controller kind or registered fleet provider.
The current save and wire schemas reject Manual controller records; there is no
conversion or compatibility fallback. Worlds without Manual records retain their
existing behavior. Historical prototype regression code is separate from the
canonical game and has no driving UI.

Independent improvements from commit `2625613690cb357c60fc24a713ff9d68964fa34c`
are retained: browser-valid WebSocket close codes and bounded UTF-8 reasons,
recovery/schema diagnostics that omit payload values, pure junction drawing and
motion history derived from committed destinations, and canonical train camera
centering. These also apply to automatic/timetable operations.

## Removal verification

The release build and all 1,595 .NET tests pass. Client tests pass 293/293 plus
one rendering-retention check; generated contracts/content/locales and Svelte
checks pass, and the production web build succeeds. Chromium passes 16 canonical
controller/group/timetable/turning cases and 38 affected prototype inspector,
map, keyboard, multiplayer, connection and station-editing cases. New regressions
verify rejection of Manual saves and canonical input commands without state
mutation, and the absence of driving UI/keyboard packets. Project boundaries and
whitespace checks pass.

## Historical implementation and evidence

The remainder records the removed P5-17 implementation, not current functionality.

# P5-17 manual driving — 5 November 2026

P5-17 delivers canonical manual driving through exclusive participant/train
leases, map keyboard input and EN/JA hold buttons. The adaptation preserves
ExAV's controller decisions while replacing native process-global keyboard
polling with authoritative per-world, per-train intent (TRAIN-D014). Ordinary
rail still has no harbor callback. Signals use the passing train's effective
shared controller. Station references remain labeled test adapters until P6.

## Source comparison and online design

Authority is stable ExAV 2.1.1.12, archive Git
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, raw `trunk/` objects.
The pinned [P5-01 contract](web-parity-train-contract.md) and
[P5-16 typed interface](web-parity-fleet-controllers.md) remain the inputs.
These are source-derived tests, not desktop-runtime captures.

| Pinned source | Canonical decision |
| --- | --- |
| `plugins/org.kohsuke.freetrain.tc.manual/src/ManualTrainController.cs`, `onJunction` | Valid Left before valid Right, then Straight. Only a choice junction invokes the controller; forced paths and followers keep source guidance. |
| Same file, `getStopTimeSpan` | Initial Shift → Proceed; otherwise Wait5, even with Up/Down held. Later Up → Proceed before Down → Reverse; otherwise Wait5. Shift has no later-call effect. Signal and Station harbor callbacks use the same rules. |
| Same file, `config` | Native Japanese instructions describe held Left/Right at points, Shift on arrival, then Up/Down after stopping. English/Japanese browser labels and Help preserve those meanings and add lease/focus/shared-time instructions. |
| Same file, `GetAsyncKeyState` | Replaced by explicit leased intent; no native keyboard polling, mutable global controller input or client simulation. |
| `core/world/rail/Train.cs`, `JunctionRailRoad.cs` | Existing typed stop interpretation, counter/reset order, source reversal, captured-head timing, follower routing, occupancy, charge and callback ownership remain authoritative. |

The old `FreeTrain.Online.Plugins.ManualController` was audited against this
source. Its prototype key priorities agree, but its four-heading exits,
`StopAction`, scenario-tick waits and historical contribution binding are not the
canonical domain. It stays confined to the explicit prototype regression host.
The reusable browser key capture, lease commands, FIFO, participant generations,
wall-time expiry and receipt infrastructure are retained and connected to the
canonical graph. The current registered provider comes from the exact content
profile, rather than that assembly's historical constant.

The online game deliberately leases each **train**, even when several trains
share one Manual controller instance. One person's keys never control another
train just because both use the same provider. No driver means neutral input:
straight at a choice junction, initial Wait5 and subsequent Wait5 at a harbor.
This does not apply a new emergency brake between harbors or pause shared time.

Held intent is sampled on the existing authoritative callback. Depart/reverse
therefore waits for the next five-game-minute poll. A button does not reschedule
the train or move it immediately. This preserves calendar, movement and economy
relationships at every playback rate; 0.125× makes five minutes take about two
wall seconds under normal pacing.

## Runtime and transport

`FleetControllers` implements the manual typed decisions. `FleetMovement` passes
one immutable intent value through harbor handling, junction movement and source
reversal. `MixedSessionState.AdvanceOneMinute` accepts a train-keyed input map
only for its detached executing minute. Frozen successors, saves and reopened
states have no retained input; no runtime closure is stored in the graph.

`FleetManualSession` owns transient leases, independently from controller
configuration. The existing commands accept canonical decimal-string train IDs:

- Owner/Operator may acquire a train; Observer cannot. Participant and connection
  generation must match the trusted active context. Other participants, including
  Owners, cannot override an active lease.
- Leases expire after five wall seconds without accepted input. Explicit Take
  control starts a fresh token/generation, resets intent and restarts sequence
  ordering. Accepted input and successful controller changes renew expiry; input
  sequences must increase.
- Held input expires after 500 ms by default; the browser renews it every150 ms.
  `NetworkOptions.InputExpiry` applies to generated and checkpoint-restored hosts.
  Maintenance runs while paused and at every playback rate.
- Blur/reset and connection replacement/disconnect neutralize input. Queued
  pre-reset input is invalidated. World epochs, lease generations and sequence
  ordering reject stale work. No key press implicitly reacquires a lost lease.
- Withdrawal and confirmed reversal discard the affected lease/input. Sale,
  world replacement and effective-controller identity/configuration changes
  invalidate affected control. Other trains and their callbacks remain intact.
- Existing network receipts deduplicate identical requests. Retries on the same
  connection return the recorded result; reconnect never replays a command.

The optional protocol7 `fleetControl` Snapshot/Delta field is a complete bounded
list of train IDs and existing `ControlLease` reads; an empty list clears
ownership. It contains no held keys. Lease publications advance the session
revision, preserving the unchanged immutable geometry and existing coherent
delta path. Clients validate unique IDs and live train references, including
when applying a delta without rebuilding the fleet graph.

Save9, the saved schema, content/catalog/pack identities and admission limits
are unchanged. Only controller assignment/configuration persists. Save/load and
process restart retain exact consists, occupancy and callbacks, with no leases,
participant credentials, input sequences or held buttons in the saved world.

## Browser workflow and P5-18 boundary

Open Trains → Owned stock, select a placed train and choose Manual. The existing
assignment flow obtains a lease first. Take/Release control, current holder,
confirmed controller and five hold buttons are also available in the actual
picked-car inspector. Buttons support pointer capture, touch, held Space/Enter
and the source keys. Map driving requires a picked train, Inspect, owned lease
and map focus; text/IME and modified shortcuts are not captured.

The two views share the existing effective-controller resolver, including the
source distinction between null assignment (Simple fallback) and explicit
Delegation. Selection, epoch, controller/lease generation, focus, visibility,
tool/panel/menu and connection changes clear held input. Returning focus or
reacquiring control requires a fresh intentional press. The canvas/camera remain
mounted and none of these local interactions pauses shared time.

For this slice the assignment select offers Simple and the exact registered
Manual provider. It creates/reuses one shared Manual instance lazily and stores
its ordinary graph reference. It does not implement independent names,
create/copy/delete management, arbitrary instance selection or TAT configuration;
those remain P5-18 onward. Unplaced stock assignment also remains with that
manager. Existing unsupported Timetable assignments are displayed and retain
the guarded unavailable boundary. Real yards/platforms/passengers/bells stay P6.

The browser labels/help are source-bound where noted in the table; lease,
contention, reset and accessibility wording are online additions, not claims of
literal native UI reproduction. The obsolete Manual/Outside-unavailable message
was corrected. Driving buttons precede operations in the owned detail column;
long instructions use Help. Narrow/200% text retains scrolling access.


### Idle-control expiry fix — 5 November owner playtest

The initial implementation renewed leases only while keys were held, and a
controller change retained the original acquisition deadline. Spending four
seconds choosing Manual could therefore leave only one second of control.
Temporarily disabling the selector for confirmation also dropped its focus.

Successful controller changes now grant a fresh five-second lease duration,
retaining the token and clearing held input. While a visible, focused driving
control or picked-train map owns control, the client sends neutral reset input
once per second when no driving key is held. This renews ownership without
extending or replaying a departure/reverse command. Only the focused surface
renews, so the panel cannot clear keys being held on the map. Blur, hidden state,
menus/tools, selection changes and destruction stop idle renewal; the existing
five-second expiry and no-automatic-reacquisition policy remain.

After Take control/controller confirmation, focus lost by temporary disabling is
restored only if the same train/world/component remains active and focus has
fallen to the document body. Explicit focus changes and stale selections are
preserved. English/Japanese Help explains focused ownership renewal.


Fresh fix verification: **143/143 selected .NET**, **23/23 selected client**,
**3/3 canonical Chromium** (idle-control regression plus existing EN/JA journeys)
and **2/2 prototype lifecycle Chromium** pass. The new browser case uses real
server wall time: acquire, wait4.1s before choosing Manual, idle6.2s on the
controls and6.2s on the map without holding keys, then blur and verify expiry
without reacquisition. A backend boundary case proves a mode change at4.5s
extends the same lease to9.5s. Build/generated/content/locales and Svelte checks
pass with zero errors/warnings; project boundaries and whitespace checks pass.
Logs are `tmp/manual-lease-fix-{dotnet,client,browser,prototype,build}.log`.
Earlier P5-17 counts below remain the original implementation runs.

### Junction disconnect fix — 5 November owner playtest

Holding Left approaching a selectable junction exposed two unported presentation
assumptions. The discrete car renderer threw `Car junction controller unavailable`
for a Manual head on the junction. Independently, committed animation history
used the head's default straight guide for `from.outgoing`, even when the actual
move turned. That contradicted the destination heading/displacement. The strict
wire validator rejected the packet and closed the socket; a concurrent reconnect
could receive409 while the old peer was still being disconnected. The supplied
server abort/reconnect log does not contain a simulation exception.

Heads now draw a neutral pose at an undecided choice junction, independent of
controller type and local/remote held keys. Followers still use predecessor
positions. Committed motion records every car's actual exit from its destination
heading, with displacement and45-degree-turn validation. Unrepresentable moves
retain explicit discontinuity markers. The client keeps strict motion checks.
When a newly chosen exit changes a buffered endpoint's tangent, the existing
player may re-anchor that train instead of inventing an uncommitted route;
seamless interpolation across that decision is not credited by this fix.

Both defects were reproduced before the fix: the renderer test threw, and live
left/right host traces published6 instead of the independently expected5/7.
Fresh verification passes **171/171 selected .NET**, **283/283 complete client
plus retention1/1**, production/generated/content/locales/Svelte (zero errors or
warnings), boundaries and whitespace. **4/4 new canonical Chromium cases** hold
Left/Right through gameplay-built junctions at0.125× with smoothing enabled and
with `?smoothTrains=0`. Driver and Observer both retain one connection, a ready
rendered scene, valid motion packets, stable lease and actual post-turn car picks.
The fixture is P4-built track/branches with purchased, deployed two-car stock.

Logs: `tmp/manual-junction-{red-dotnet,red-client}.log` and
`tmp/manual-junction-fix-{dotnet,client,all-client,browser,build}.log`.
Generate browser inputs with `FREETRAIN_MANUAL_JUNCTION_OUTPUT` set to the path
`tmp/manual-junction-fix.json` while running the selected FleetMovementTests;
these write the left save and its `.right` companion. Then run
`canonical-manual-driving.spec.ts --grep 'crosses a manual junction'` with the
Debug backend. No new native, container, full-backend or full-browser acceptance
is implied.

### P5-MANUAL-001 — Known issue, owner-deferred 6 November

**Open; troubleshooting deferred at the owner's request.** Manual steering can
intermittently cause a packet error, disconnect and game reload. The owner
reports Chrome at Normal1×, including holding Left on straight track and near
junctions. Earlier lease and junction corrections have not established closure
of this remaining report. Continue the execution plan with P5-18; resume this
investigation separately rather than treating it as fixed.

The latest investigation found and corrected an additional recovery defect:
browser JavaScript rejects `WebSocket.close(1002)`, which threw inside the receive
pipeline. Application close codes4001/4002 now recover correctly. Client warnings
identify packet/schema reasons, timeouts and receive-queue overflow; the backend
logs application close reasons as `Client requested session recovery`. These
improvements do not identify the original intermittent steering trigger.

An isolated copy of the owner's400×600×24, five-train world (including an18-car
Manual train) stayed connected in sustained Normal-speed Chrome runs, including
map focus, Vite development mode and Retina resolution. Explicit malformed-packet
and receive-queue-overflow browser tests now recover. Those successful runs do
not supersede the owner's failing playtest. Handoff evidence is under
`tmp/manual-straight-*`, `tmp/manual-steering-diagnostics-*` and
`tmp/manual-packet-recovery-*`.

For future reproduction, `web/tests/browser/manual-driving-soak.spec.ts` accepts
`FREETRAIN_MANUAL_STEERING_SAVE`, optional `FREETRAIN_MANUAL_STEERING_TRAIN` and
`FREETRAIN_MANUAL_STEERING_SECONDS` (default90). Its fixture server receives a
copy of the supplied save; it does not drive or replace the running user world.
`FREETRAIN_ONLINE_TEST_DEV_CLIENT=1` selects the development client. Capture the
first `[FreeTrain] Session packet rejected`/`Session reconnecting` reason, or the
corresponding backend recovery log, when investigation resumes. No further
troubleshooting is requested by this deferral.

## Verification

Fresh verification on 5 November:

- **285/285 selected .NET tests** pass: fleet movement/controllers/deployment,
  persistence/state/session, signals, multiplayer, network and playback rates.
  Manual-specific coverage includes all32 key masks at first/later Station and
  Signal calls; all16 choice-junction patterns in eight headings; gameplay-built
  branch movement with two-car followers; real signal Wait5/Depart/Reverse and
  initial Shift; two trains sharing a Manual instance with isolated input;
  roles/contention, sequences/generations, paused/slow-rate expiry, queued resets,
  reconnect/epoch/withdrawal, durable assignment and exact callback ownership.
  Saving during a wait with Up+Down held reopens into another Wait5, while the
  uninterrupted controlled train departs. Station calls are explicitly adapters.
- **282/282 complete client tests plus1/1 renderer-retention test** pass, including
  three new tests for shared delegation/null semantics, immutable lease-only
  deltas and no acquisition/replay from held input.
- **Six distinct selected canonical Chromium scenarios** pass: two new EN/JA
  manual journeys and four fleet-trading regressions. Manual journeys use actual
  P4-built signals/track and purchased/deployed stock exported by the backend
  test. They cover held pointer input through callbacks, picked-car/map keyboard,
  modifiers/blur, two-participant contention/handoff, shared-instance input
  isolation, duplicate receipts, HTTP reopen and actual checkpoint restart.
  Final manual-only2/2 additionally checks Observer controls, late lease/selection
  fences, modified-key releases, corrected help and
  visible EN/JA captures. JA uses390×844, touch capability and200% text.
- **2/2 prototype Chromium lifecycle regressions** pass for reused input handling.
- Production build, generated contracts/content/locales and Svelte checks pass
  with zero Svelte errors/warnings; the existing bundle-size advisory remains.
  Raw pinned70-file source review,744 fixtures, project boundaries and whitespace
  checks pass.

Logs: `tmp/p5-17-{selected-dotnet,all-client,web-build,source-review,fixtures}.log`,
`tmp/p5-17-browser-final.log`, `tmp/p5-17-browser-manual-final.log` and
`tmp/p5-17-prototype-browser.log`. Inspected captures are
`tmp/p5-17-browser/manual-{en,ja}.png`. Generated gameplay input is
`tmp/p5-17-manual-fleet.json`; it is not a substitute stock definition or P6 world.
No complete backend/browser suite, Linux-container run, new native/IME/human
pixel-fidelity, optional-suite or capacity acceptance is claimed.

Reproduce from the root, with the pinned toolchain:

```sh
FREETRAIN_MANUAL_FLEET_OUTPUT="$PWD/tmp/p5-17-manual-fleet.json" dotnet test tests/FreeTrain.Online.Tests/FreeTrain.Online.Tests.csproj --no-restore --filter 'FullyQualifiedName~FleetMovementTests|FullyQualifiedName~FleetDeploymentTests|FullyQualifiedName~FleetPersistenceTests|FullyQualifiedName~FleetTests|FullyQualifiedName~FleetSessionTests|FullyQualifiedName~SignalConstructionTests|FullyQualifiedName~MultiplayerTests|FullyQualifiedName~PlaybackRateTests|FullyQualifiedName~NetworkTests' -m:1 -nodeReuse:false -p:UseSharedCompilation=false
deno task --cwd web build
deno task --cwd web test
python3 scripts/review-trains.py --source-root ../freetrain-exav-archive --check
python3 scripts/train-fixtures.py --source-root ../freetrain-exav-archive --check
python3 scripts/check-project-boundaries.py
```

From `web/`:

```sh
FREETRAIN_ONLINE_TEST_SERVER_CONFIGURATION=Debug FREETRAIN_ONLINE_BROWSER_EVIDENCE="$PWD/../tmp/p5-17-browser" deno task test:browser:run --project=canonical canonical-manual-driving.spec.ts fleet-trading.spec.ts --workers=1
FREETRAIN_ONLINE_TEST_SERVER_CONFIGURATION=Debug deno task test:browser:run --project=prototype-regression manual-driving.spec.ts --workers=1
```
