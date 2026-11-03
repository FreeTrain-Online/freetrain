# P5-04 canonical fleet and callback ownership — 3 November 2026

P5-04 is complete at detached domain scope. [`Fleet.cs`](../src/FreeTrain.Online.Simulation/Fleet.cs)
adds immutable owned stock, shared controllers and ordered group membership to
[`MixedWorld`](../src/FreeTrain.Online.Simulation/MixedWorld.cs).
[`MixedSessionState`](../src/FreeTrain.Online.Simulation/MixedSessionState.cs)
binds train and construction actions in the same world-local clock. The
[focused tests](../tests/FreeTrain.Online.Tests/FleetTests.cs) exercise these
production types using registered bundled stock and independent fixture poses.

This step supplies state and callback ownership, not movement/controller
algorithms or purchase/deployment commands. Save8/protocol6 remain unchanged.
Until P5-05 adds the complete fleet codec, `CaptureTerrain`, current save writing
and mixed wire publication reject a world with a fleet graph; they cannot silently
drop inventory, shared references or allocated identities. Existing construction
worlds and explicitly trusted P4 test cars retain their previous contracts.

## Source and requirement bindings

Authority is ExAV 2.1.1.12, Git
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, `trunk/`.
The following raw-file hashes and pinned symbol excerpts are verified by the
[P5-01 review/checker](parity/train-review.json). This implementation consumes that
review and the [P5-02 literal fixtures](parity/train-fixtures.json); it does not
rewrite either oracle from production output.

| Pinned file | SHA-256 | Source contract / implementation consumer |
| --- | --- | --- |
| `core/world/Car.cs` | `996a334c9352048c03d53dea36c4ab5b581a86312a4db94637475c405230d03d` | `setState`, lines26/32/41: old-slot clear/new-slot install; bidirectional `FleetGraph.ValidateWorld` |
| `core/world/CarState.cs` | `c68ad252be432207df5f59b551b5dced074fc80d07ea9cd526e30f5c3787999e` | Inside61, Unplaced73, Outside94/countdown99; immutable `FleetCarState` variants |
| `core/world/rail/Train.cs` | `0130739ef685f252a8e1cd639fcf6d0908d3e3da6ef028e0418f3cb72cdfd404` | nullable purchase date82, placement115, stop state342, previous778; `FleetTrain` identities/order/state/counters and one-shot callback ownership |
| `core/world/rail/TrainItem.cs` | `8820be8ce5c188a0137d26a8de1096a2c13b54d42a215293f5f36dca594d9e1b` | shared controller28, moveUnder36/41; train/group controller and reciprocal membership references |
| `core/world/rail/TrainGroup.cs` | `97ad5bee15ea7b535fbdad0665806f070211d719c46cb38ec395624a539098b8` | constructors12/14; ordered group items and delegation identity |
| `core/world/rail/DelegationTrainControllerImpl.cs` | `8f092209729a8b885660a9610e18f05e1654bf6cb537dbfa5d17727d31db0e9f` | traversal38/39, fallback43; `EffectiveController` with TRAIN-D002 null/simple disposition |
| `core/world/Clock.cs` | `f9b34df78bb4e3d0530303568962af38413df406c45a11c4ff248b6b1d3517f4` | registerOneShot62/unregister84; mixed callbacks retain P1 FIFO/GEO-002 cancellation |

Granular reciprocal requirements remain in the
[action bindings](web-parity-actions.md#p5-04-fleet-foundation),
[manual bindings](web-parity-manual.md#p5-04-fleet-foundation) and local master
checklist. MAN-T06/T07/T08, MAN-W07/W08/W09/W10, MAN-A10/A11/A12/A13/A17,
MAN-O08 and DEV-P14/P18/P19/P20/P24 consume this foundation. Their actual controls,
movement, trading, manager configuration, tracking and group operations retain
their numbered P5 implementing owners. No new player text or EN/JA control ships
in this domain step; `train-localization-review.json` remains the source-bound
presentation handoff. Default internal root/built-in names are domain names,
not newly localized browser labels.

## Graph and occupancy contract

- TrainId, CarId, ControllerId and GroupId are stable positive Int64 identities
  in the existing `SpatialState.NextEntityId` namespace. Initialization reserves
  simple/delegation/root IDs from its high-water mark. A car occupies its rail
  owner's Traffic component, with no second whole-voxel spatial footprint.
- Every train references an exact registered stock binding and permitted length.
  Original car index/type/identity is distinct from the current head-to-tail
  array. Reversal flag and array order must agree; `Predecessor` follows that
  array. A successor cannot replace a train's fixed original cars, repurpose a
  live identity or reuse an allocated/deleted ID. Member revisions advance by
  one when changed. Terrain/rail tools retain the graph automatically.
- Preserve Unplaced/Inside/Outside, formation reversal, all five source operation
  states (including the retained StoppingAtSignal enum value), forwarding,
  passengers/placed boarding origin, Int32 move/stop counters and nullable purchase
  time. Packed occupancy can exceed normal capacity. Invalid numeric state and
  purchase dates newer than the clock reject; P5-08/13 supplies checked transitions.
- Only Inside cars occupy slots. Their exact location, TrainId, CarId and car
  definition must match a Traffic rail and incoming rail port. Every slot resolves
  back to exactly one Inside car. Outside references obey the existing source
  visible-border predicate and countdown0…100. Mixed Inside/Outside consists are
  allowed; partially Unplaced trains are rejected.
- Exact selected pack sets must match terrain. P4 fixture cars cannot acquire stock
  identity. Signals retain reserved-null ControllerId in a canonical fleet world;
  effective train controllers own signal behavior (TRAIN-D010). The old codec's
  explicit reserved-field rejection remains P5-05 work.
- Group members preserve their source interleaved train/subgroup order and agree
  with each child's one parent reference. Duplicate/dangling members, cycles,
  multiple roots, root reparenting and missing shared controllers reject. Root
  initializes to simple. Null lookup resolves to simple; delegation walks parents,
  with exhausted all-delegating chains falling back to simple. Shared manual/TAT
  identities bind the exact two registered providers; their behavior/configuration
  follows in P5-16 onward. Referenced controllers cannot disappear. Existing groups
  have no deletion transition, retaining the source no-delete disposition.
- Actual inventory enforces P5-03's formation100/fleet128-train/1,024-car budgets.
  The maximum inventory test creates all128 trains/1,024 cars through the real
  graph/allocator. This is state admission, not P5-28 moving-fleet capacity acceptance.
  New portable structural guards bound groups/controllers to256 each and names
  to256 characters, with profile admission bounded to256 packs/4,096 stock
  definitions/128 providers. These are engineering guards, not asserted source
  maxima or measured moving-resource limits; P5-28 retains capacity acceptance.

## Clock and atomic successor contract

The durable callback identity is invariant `train/<TrainId>`, with existing
`ScheduledAction` registration/order/due minute fields. Each deployed train,
including an entirely Outside consist or a stopping train, requires exactly one
future one-shot action. Unplaced stock requires none. Construction retains its
per-cell one-shot callback and119-minute boundary; that construction-only time
constraint does not apply to train waits/native speeds. Unknown, missing,
duplicate, repeated, past-due and unbound actions reject at restored boundaries.

A deployed fleet requires a trusted `TrainCallback` dispatcher. No default
movement is invented. The dispatcher returns one detached `MixedConstructionState`
and either a positive next delay or null after withdrawal. Its result validates
world/fleet/spatial identity, accounting prefix and clock minute before publication.
Callbacks may use world-local random streams on the detached boundary. The entire
post-dispatch boundary is frozen, validated and rebound before the live session
adopts geometry/fleet/accounts/random/revision/clock together. A fault after an
earlier construction callback discards that whole minute. Equal-time callbacks
retain FIFO; cancellation removes only actions belonging to withdrawn trains or
removed construction cells. Unknown successor payloads reject rather than being
silently filtered. Existing speed-limit state is explicitly passed through
successor validation.

`Freeze` retains the dispatcher and reconstructs bindings to the new session.
This is in-memory continuation only; no delegate, CLR type, input lease or UI
listener becomes durable data. P5-05 reconstructs the dispatcher from explicit
state at the codec boundary. P5-06/07/08 supply actual trade/deploy/withdraw/move
mutations. The controlled dispatcher tests here verify callback infrastructure,
not source movement or controller decisions.

## Verification

With .NET SDK10.0.402, Deno2.9.8 and Playwright1.64.1 on3 November2026:

- Focused backend tests: **180/180**, including43 fleet cases and affected
  stock, mixed-world, stationary construction, clock, speed-limit,
  persistence and continuation regressions. Log: `tmp/p5-04-focused-dotnet-final.log`.
  Independent fixture consumers load24 reversal and36 TURNING cases (120 before/
  after graph poses) and the literal equal-time queue registration/order oracle.
  This credits pose/identity/queue contracts, not executing reversal/TURNING gameplay.
- Selected client suites: **17/17** (`train-fixtures`, `mixed-state`, `state-transfer`).
  Log: `tmp/p5-04-selected-client.log`. No exhaustive web test run.
- Web build/generated/content/locale checks pass with the existing bundle-size
  advisory. Log: `tmp/p5-04-web-build.log`.
- Source/fixture/registration review, P4/P5 handoff and project-boundary checks pass.
  Logs: `tmp/p5-04-source-review.log`, `tmp/p5-04-fixtures.log`,
  `tmp/p5-04-stock-review.log`, `tmp/p5-04-handoff.log`,
  `tmp/p5-04-project-boundaries.log`. Catalog, format/protocol and prior stock
  budgets are unchanged; the new graph structural guards are listed above.
- Only two Chromium construction lifecycle workflows ran: **2/2**,16.3seconds.
  English covers detached preview, real construction callbacks, cancellation,
  two peers and exact reload; Japanese covers narrow keyboard confirmation.
  Log: `tmp/p5-04-selected-browser.log`. The selected cases emit no screenshots;
  no new inspected visual artifact or human/native fidelity is credited.
  Full moving-fleet capacity, P6 passenger/station integration and remaining
  phase acceptance stay open.

Reproduction from the repository root:

```sh
dotnet test tests/FreeTrain.Online.Tests/FreeTrain.Online.Tests.csproj --no-restore --filter 'FullyQualifiedName~FleetTests|FullyQualifiedName~StationaryConstructionTests|FullyQualifiedName~RollingStockTests|FullyQualifiedName~MixedWorldTests|FullyQualifiedName~MixedPersistenceTests|FullyQualifiedName~MixedContinuationTests|FullyQualifiedName~ClockTests|FullyQualifiedName~SpeedLimitTests' -m:1 -nodeReuse:false -p:UseSharedCompilation=false
python3 scripts/review-trains.py --check
python3 scripts/train-fixtures.py --check
python3 scripts/review-stock-registration.py
python3 scripts/check-railway-handoff.py
python3 scripts/check-project-boundaries.py
```

From `web/`:

```sh
deno task build
deno test -A --no-check tests/train-fixtures.test.ts tests/mixed-state.test.ts tests/state-transfer.test.ts
FREETRAIN_ONLINE_TEST_SERVER_CONFIGURATION=Debug FREETRAIN_ONLINE_BROWSER_EVIDENCE=../tmp/p5-04-browser deno task test:browser:run stationary-construction.spec.ts --project=canonical --grep 'stationary source lifecycle' --workers=1
```
