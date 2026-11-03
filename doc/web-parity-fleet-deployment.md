# P5-07 placement, direction and withdrawal domain — 3 November 2026

P5-07 is complete at detached domain scope.
[FleetDeployment](../src/FreeTrain.Online.Simulation/FleetDeployment.cs) previews
and commits actual owned rolling stock against canonical mixed rail/fleet state.
It exposes the complete planned train, including stable car identities, current
order, locations, headings and orientation. Browser controls remain P5-11;
movement/production dispatch remains P5-08. No new player-facing text ships;
English/Japanese deployment controls retain that later owner.

## Source bindings

Authority is ExAV 2.1.1.12/V2, Git
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, `trunk/`. Raw pinned objects were
read directly, independently of the archive working tree. The
[P5 source contract](web-parity-train-contract.md),
[P4 railway handoff](web-parity-p5-railway-handoff.md) and
[source review](parity/train-review.json) remain the behavior authority.
The [placement literals](parity/train-placement-fixtures.json) bind seven source
files and their complete SHA-256 hashes:

| Source | Implemented behavior |
| --- | --- |
| `core/world/rail/Train.cs` | `place`109–158 tail-first temporary head; duplicate rejection; `remove`163–172 clears cars/callback; `registerTimer`276–320 native first delay; `reverse`463–476 guide reversal then array swap |
| `core/world/rail/RailRoad.cs` | `dir1`111–114 selects the lowest-index rail port |
| `core/world/rail/SingleRailRoad.cs` | `guide`: keep heading when present, else left, else right |
| `core/world/rail/JunctionRailRoad.cs` | `guide`: temporary head asks controller; real followers reuse predecessor location |
| `core/world/rail/SlopeRailRoad.cs` | `guide`: keep heading; deployment never calls motion's `zdiff` |
| `core/world/rail/SpecialPurposeRailRoad.cs` | straight heading across bridge, girder, tunnel, garage and signal infrastructure |
| `core/world/rail/SimpleTrainControllerImpl.cs` | `onJunction`: straight |

`TrainPlacementController.onClick`475–499 supplies the source direction-choice
equivalent: placement followed by clicking the selected train reverses it at the
same positions. The domain's `reverse` preview option composes those actions into
one confirmed candidate. It does not choose an arbitrary initial heading or a
different rail walk. Already placed reversal/recovery remains P5-12.
Reciprocal [action](web-parity-actions.md#p5-07-deployment-domain) and
[manual](web-parity-manual.md#p5-07-deployment-domain) bindings retain MAN-T07,
MAN-W07 and DEV-P19/P24.

## Placement and direction

The clicked location anchors the current tail. Starting at `rr.dir1`, the planner
walks tail to head through each actual rail subtype. Every iteration uses the
temporary **head** semantics from the source, even for a location that ultimately
belongs to a follower. Simple-controller junctions choose straight; forced turns
need no controller decision. An unresolved manual/timetable junction decision
rejects with `unboundJunctionController`, keeping those later controller slices
explicit rather than silently applying simple control to them.

Only the detached candidate receives temporary/planned states. Missing track,
occupied track (including a different owned train), repeated cells, impossible
guidance and stored-world boundaries reject before publication. The final guide
is evaluated, but a neighboring rail beyond the final head is not required.
Placement uses stored voxel bounds; the distinct motion visible-border predicate
does not change deployment or create Outside states.

**GEO-008 / TRAIN-D003:** deployment changes XY only. A real four-cell ramp
can accept two cars on its same-height pair; a three-car placement across its
height transition finds a filler/missing rail at the unchanged level and rejects.
It never substitutes the motion walker to make that deployment succeed.

Optional reversal visits the planned cars in current head-to-tail order. The
head uses its effective simple controller; junction followers use their actual
predecessor's location. Each heading becomes `guide().opposite`, then current
car order reverses and `IsReversed` toggles. Curves therefore do not simply add
four to every stored heading. Original car IDs/indices/types remain fixed,
including explicit mixed patterns and the source single-car tail precedence.

**Heading validation correction:** P5-04/05 had required an opposite rail port
for every Inside heading. Source placement initializes `rr.dir1` even on curves
such as ports `[0,5]`, where initial heading0 has no opposite4. Backend world/save
validation and the browser decoder now agree on subtype guidance: normal and
junction rails accept a heading that is present or can turn left/right one
eighth; slope/special rails require the heading itself. Reciprocal fleet/Traffic
identity/location/definition checks remain. Impossible guidance rejects instead
of retaining a source Debug.Assert-only malformed pose. Motion's destination
reciprocity remains P5-08 and is a separate predicate.

## Atomic state and callbacks

Preview produces and validates one immutable construction/fleet/queue successor.
Confirm requires the same session/construction identity, session revision,
absolute minute, queue high-water and ordered pending entries. Replacement,
competing edits, pause/time/queue changes and replay against a successor reject.
Discarding a preview changes no save bytes, IDs, cash, random state or callbacks.

Commit installs all Inside car slots and the complete fleet pose together. Each
changed car/train revision advances once; each affected spatial owner advances
once, including a shared slope owner. Terrain, subtype recipes/backups,
road/accessory/platform/support data, speed lists, groups, shared controllers,
purchase time and allocator high-water survive. Placement/withdrawal book nothing.

Placement sets Moving and resets **StopCallCount** to zero. The source does
**not** reset MoveCount in `place` or `remove`: running-cost cadence survives
withdrawal/redeployment, together with passengers, boarding origin and forwarding.
This qualifies the checklist's original generic “reset counters” wording against
the pinned source. The first one-shot uses the stock's native minutes per voxel,
independently of limits at the placed head. Registration uses the existing FIFO
high-water and is sorted alongside neighboring train/construction callbacks.
Checked member/owner/session/order/due arithmetic rejects before publication.

Deployment requires an explicit trusted train dispatcher, as P5-04/05 already
require for deployed saves. This step installs no placeholder movement. Tests use
a controlled dispatcher to verify save/reload scheduling and construction
coexistence; no runnable-train or production motion credit is claimed.

Withdrawal clears **every** Inside slot, converts Inside and Outside cars to
Unplaced, sets the train Unplaced and cancels exactly its callback. Owned stock,
current/original order, group membership and shared controllers remain. Neighbor
and construction callbacks keep their registration/order/due values; no sale or
revenue occurs. Repeated withdrawal and placement of already placed stock reject.

Save9/protocol7/schema/catalog/pack hashes and stock limits are unchanged. Both
current decoders consume the same pose semantics. Coordinated builds remain
required; historical save migration/import remains P10.

## Verification

Fresh checks on3 November2026:

- **218/218 focused .NET tests**, including32 deployment cases plus affected
  fleet/trading, save/transport, mixed continuation/persistence, stock and train
  boundary regressions. The48 literal poses cover eight final headings, both
  initial formation orientations, both direction choices and1/3/9 cars.
  Additional cases consume all17 P5-02 guide literals; cover real bends, temporary
  head junctions, all four actual slopes, missing/occupied/loop/border/impossible
  heading rejection, explicit mixed stock, all six bundled families' native
  delays, Inside/Outside withdrawal, construction/neighbor preservation, stale
  quotes/replay, numeric exhaustion and exact durable continuation.
  Log: `tmp/p5-07-focused-dotnet.log`.
- **25/25 selected client tests**: fleet-state, mixed-state and state-transfer.
  Two immutable peers apply independently authored complete placement/withdrawal
  deltas; invalid occupancy/queue/heading changes reject. Curve `dir1` and
  impossible normal/special guidance have explicit decoder regressions.
  Log: `tmp/p5-07-selected-client.log`.
- **1/1 selected Chromium case**: two-peer inventory/revenue save load, shared
  Step, rejected uploads, reconnect and retained canvas. It verifies regression
  compatibility, not browser deployment or moving trains. No screenshots or new
  visual/native acceptance. Log: `tmp/p5-07-selected-browser.log`.
- Clean web build/generated/content/locale checks, zero Svelte errors/warnings
  (existing bundle-size advisory): `tmp/p5-07-web-build.log`. Raw pinned-source
  placement/source/fixture checks, stock registration, railway handoff and project
  boundaries pass: `tmp/p5-07-{placement-fixtures,source-review,fixtures,stock-review,handoff,project-boundaries}.log`.

The unchanged P5-05 fleet fixture supplies the independent delta inputs. Train20
is one E231 car, initially Unplaced. Source placement at the existing tunnel
`(3,2,2)` sets heading0, train/car revision1 and owner9 revision1; world/session
revisions become6/7. Native delay2 at minute130081 appends order9007199254740994,
due130083, after train17's unchanged due130082. Withdrawal advances train/car/
owner/world/session once, clears only that slot/callback and keeps queue
high-water9007199254740995. All accounts, tunnel backup, other fleet/occupancy,
groups/controllers and IDs remain. These
[placement](../prototype/protocol/fixtures/delta-fleet-placement.json) and
[withdrawal](../prototype/protocol/fixtures/delta-fleet-withdrawal.json) literals
were authored from the source and existing inputs, not captured from production.
Tests compare actual domain results against each complete delta structure.

Reproduce from the repository root:

```sh
dotnet test tests/FreeTrain.Online.Tests/FreeTrain.Online.Tests.csproj --no-restore --filter 'FullyQualifiedName~FleetDeploymentTests|FullyQualifiedName~FleetTests|FullyQualifiedName~FleetTradingTests|FullyQualifiedName~FleetPersistenceTests|FullyQualifiedName~TrainFixtureBoundaryTests|FullyQualifiedName~MixedContinuationTests|FullyQualifiedName~MixedPersistenceTests|FullyQualifiedName~RollingStockTests' -m:1 -nodeReuse:false -p:UseSharedCompilation=false
python3 scripts/check-train-placement.py --source-root ../freetrain-exav-archive
python3 scripts/review-trains.py --source-root ../freetrain-exav-archive --check
python3 scripts/train-fixtures.py --source-root ../freetrain-exav-archive --check
python3 scripts/review-stock-registration.py
python3 scripts/check-railway-handoff.py
python3 scripts/check-project-boundaries.py
```

From `web/`:

```sh
deno task build
deno test -A --no-check tests/fleet-state.test.ts tests/mixed-state.test.ts tests/state-transfer.test.ts
FREETRAIN_ONLINE_TEST_SERVER_CONFIGURATION=Debug FREETRAIN_ONLINE_BROWSER_EVIDENCE=../tmp/p5-07-browser deno task test:browser:run fleet-persistence.spec.ts --project=canonical --workers=1
```

No complete web suite ran. P5-08 movement, P5-09 art, P5-10/11 controls and network
receipts, advanced controllers/recovery/capacity and native/human/optional
acceptance keep their numbered owners.
