# P5-16 typed controllers and harbors — 5 November 2026

P5-16 completes the portable controller/harbor contract and simple defaults.
The production movement dispatcher consumes typed stop decisions and junction
routes. Signals resolve the passing train's shared controller; ordinary rails
have no harbor callback. The simple controller ignores signals, chooses straight
at a choice junction and waits30 minutes on a first Station call, then departs.
Station adapters in this slice are explicitly test harbors. Real yards, stop
positions, platforms, passenger exchange and bells remain P6.

Authority is stable ExAV2.1.1.12, archive Git
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, raw `trunk/` objects.
The [source contract](web-parity-train-contract.md),
[review](parity/train-review.json) and
[independent P5-02 fixtures](parity/train-fixtures.json) retain their original
hashes and scopes. This slice adds handwritten source-derived controller traces;
none are original-runtime captures.

## Source bindings and implementation

| Source | Portable behavior |
| --- | --- |
| `core/world/rail/TrainController.cs:26,49` | Typed junction choice and harbor callback with explicit callCount, passing train and shared controller snapshot. |
| `core/world/rail/SimpleTrainControllerImpl.cs:23–32` | Straight; non-Station proceeds; first Station wait30 then proceed. The source's one-hour comment does not control behavior. |
| `core/world/rail/Train.cs:329–399` | Handler changes precede central action interpretation; waits increment the counter, set StoppingAtStation and return; nonpositive actions reset forwarding and then the counter; movement timing uses the captured original head. |
| `core/world/rail/TrainHarbor.cs:12–17` | Harbor settings are keyed by the harbor's Location. A future yard supplies its host Station, independently of the current rail cell. |
| `core/world/rail/JunctionRailRoad.cs:15–47` | Ask the head controller only when straight and curve are both available; followers retain predecessor routing and forced paths bypass the controller. |
| `plugins/org.kohsuke.freetrain.rail.signal/src/SignalRailRoad.cs` | Direction gate delegates to the passing train's controller with the signal itself as harbor; TRAIN-D010 prohibits a per-signal controller override. |

`FleetControllers.cs` defines `FleetStopDecision`: Proceed, positive Wait,
Reverse, Forwarding and ReverseForwarding. Wait factories reject zero and
negative values. The zero/default struct is Proceed. `FromSourceMinutes`
preserves -999 as forwarding and -1000 as reverse-forwarding; every other
negative means Reverse. `SourceMinutes` emits the canonical -1 for generic
reverse, retaining the exact two forwarding sentinels. The integer
`FleetStopResult` constructor is a source-bound adapter used by earlier literal
fixtures; production movement branches on the typed action.

`FleetStopResult` carries the handler's detached train and decision together.
The caller applies central effects to that train, preserving handler-side
reversal/passenger/forwarding mutations. Positive waits preserve forwarding and
car changes, increment stopCallCount and register exactly one explicit wait.
They do not move or book an operating expense. All positive waits, including
signal waits, keep the source's StoppingAtStation operational state. Departure
resets forwarding before interpreting the action and resets stopCallCount before
movement. Reversal plus movement normalizes each stable car revision once.
Outside callbacks continue to bypass Inside stop handling.

`FleetHarborCall.Controller` is the immutable shared `FleetController` snapshot:
Id is assignment identity and Revision is its configuration/member revision.
The existing graph owns both; no second controller identity/configuration store
is introduced. Every callback resolves effective assignment through the existing
delegation/fallback policy. Confirmed graph changes must advance the instance
revision, and the next call observes those settings for every assigned train.
Location-keyed settings remain on the shared controller instance.

`FleetHarborReference.Signal` carries its spatial owner identity/revision and
location. `FleetHarborReference.Station` defines a future resolved host Station
identity/revision/location. A call carries RailLocation separately from the
harbor's configuration key. Ordinary rails supply Proceed without invoking a
harbor handler; directional signals create their own reference and invoke the
passing train's controller. The older `SignalControllerReference` inspection
shape retains its reserved null controller field, with corrected documentation.

`FleetJunctionRoute` replaces the guide's boolean curve flag. Junction calls
carry the shared instance snapshot, actual rail location/pattern and incoming
heading. Deployment supplies its current temporary head location explicitly,
including iterations that will become followers. Reversal and movement supply
their actual head location. Unbound manual/TAT providers still trigger the
existing guarded pause/rejection rather than acquiring invented defaults.

Runtime handler delegates remain trusted bindings, outside durable state.
`AtHarbor` permits explicit test/future station/provider adapters and validates
the reference's identity/revision and Inside call boundary. These adapters do
not register live stations or implement platform legality, TURNING placement,
boarding or departure-bell effects. Saved test continuation rebinds the same
trusted dispatcher explicitly; production saves restore the production dispatcher.

## Verification and limits

Twenty new executable cases in `FleetControllerTests.cs` cover:

- Source action mapping, Int32 boundaries and invalid wait durations.
- Literal first wait200→230, departure using old-head Low timing230→234,
  an ordinary rail callback, and a fresh first wait236→266 at a second test stop.
  Cars, forwarding, passengers, booking, counters and the sole callback are
  checked before/after save and explicit adapter rebinding.
- Eight combinations of handler-side reversal and central proceed/reverse/
  forwarding/reverse-forwarding, including exact stable car order/poses,
  one revision increment, original-head timing and post-handler operating cost.
- A positive typed wait retaining handler reversal/forwarding before departure.
- All nine existing independent signal fixtures, including two controllers at
  one signal and diagonal direction-gate probes. Probes do not claim admitted
  diagonal saved signal poses. Simple ignores nonstation harbors; ordinary rail
  never calls the adapter.
- Two trains sharing one configured controller and one test Station key from
  distinct rail cells. Changing wait7 to11 under the same instance Id and a new
  revision affects both next callbacks; unchanged-revision mutation rejects.
  Repeated waits/departure and exact save/reopen preserve settings and schedule.

The selected regression set also retains all17 source guide vectors, the
existing literal movement/sentinel/timing/Outside traces, gameplay-built ring
and signal runs, fleet graph/persistence/session guards and detached rollback.
One old signal construction test still expected the reserved controller field
to survive saving. It now checks existing TRAIN-D010 rejection and a valid null
field round trip, retaining its full-journal zero-booking check.

Verification results:

- **217/217 selected .NET tests** pass across FleetMovementTests (including
  timing/Outside/operations/controller partials), FleetDeploymentTests,
  FleetPersistenceTests, FleetTests, FleetSessionTests and SignalConstructionTests.
- **37/37 selected client tests** pass: fleet state, train fixtures and train motion.
- **4/4 selected Chromium tests** pass: EN/JA two-peer fleet operations and
  signal construction/reopen cases.
- Production build, generated contracts, content, locales and Svelte checks pass
  with zero Svelte errors/warnings; the existing bundle-size advisory remains.
- Pinned70-file source review,744-fixture checker, project boundaries and
  whitespace checks pass.

No complete web suite ran. No new browser controls or locale strings are added.
Save9/protocol7, content packs and admission limits are unchanged. Manual leased
input, controller management, TAT, TURNING and P6 gameplay retain their numbered
owners. Native/human pixel-fidelity and later phase gates remain open.

Logs are `tmp/p5-16-{selected-dotnet,client,browser,web-build,source-review,fixtures}.log`.
Browser inputs are `tmp/p5-16-operations-fleet.json` and its `.return` companion;
artifacts are under `tmp/p5-16-browser/`.

Reproduce from the repository root:

```sh
FREETRAIN_OPERATIONS_FLEET_OUTPUT="$PWD/tmp/p5-16-operations-fleet.json" dotnet test tests/FreeTrain.Online.Tests/FreeTrain.Online.Tests.csproj --no-restore --filter 'FullyQualifiedName~FleetMovementTests|FullyQualifiedName~FleetDeploymentTests|FullyQualifiedName~FleetPersistenceTests|FullyQualifiedName~FleetTests|FullyQualifiedName~FleetSessionTests|FullyQualifiedName~SignalConstructionTests' -m:1 -nodeReuse:false -p:UseSharedCompilation=false
python3 scripts/review-trains.py --source-root ../freetrain-exav-archive --check
python3 scripts/train-fixtures.py --source-root ../freetrain-exav-archive --check
python3 scripts/check-project-boundaries.py
```

From `web/`, after that Debug build:

```sh
deno task build
deno test -A --no-check tests/fleet-state.test.ts tests/train-fixtures.test.ts tests/train-motion.test.ts
FREETRAIN_ONLINE_TEST_SERVER_CONFIGURATION=Debug FREETRAIN_OPERATIONS_FLEET_OUTPUT="$PWD/../tmp/p5-16-operations-fleet.json" FREETRAIN_ONLINE_BROWSER_EVIDENCE="$PWD/../tmp/p5-16-browser" deno task test:browser:run --project=canonical fleet-deployment.spec.ts signal-construction.spec.ts --grep 'P5-15|signal types, directions' --workers=1
```
