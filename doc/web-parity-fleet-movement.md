# P5-08 Inside movement — 3 November 2026

P5-08 is complete at Inside simulation and live transport scope.
[FleetMovement](../src/FreeTrain.Online.Simulation/FleetMovement.cs) is the default
trusted dispatcher for new mixed sessions and Save9 restoration. Real registered,
purchased/deployed stock runs on gameplay-built track; callers can still supply
an explicit controlled dispatcher. Save9/protocol7, schemas, stock limits, packs
and catalog hashes are unchanged. This step adds no purchase/deployment control
and grants no moving-art or native acceptance.

## Source and behavior

Authority remains ExAV 2.1.1.12/V2, Git
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, `trunk/`. Pinned raw Git objects were
read independently of the archive working tree. The [P5 contract](web-parity-train-contract.md),
[hashed source review](parity/train-review.json), [P4 handoff](web-parity-p5-railway-handoff.md)
and [independent literals](parity/train-fixtures.json) remain the specification.

| Pinned source | Runtime binding |
| --- | --- |
| `core/world/rail/Train.cs` | `clockHandler` captures the original Inside head, resets zero-stop forwarding/stop count, checks occupancy before shifting, reverses once for a missing candidate and registers one retry; `move` shifts old states and charges successful moves |
| `core/world/rail/RailRoad.cs` | ordinary stop result zero; current pattern's `zdiff` uses the incoming heading |
| `core/world/rail/SingleRailRoad.cs` | straight, otherwise left, otherwise right guidance |
| `core/world/rail/JunctionRailRoad.cs` | head asks its controller only when there is a choice; followers retain their predecessor's actual route |
| `core/world/rail/SlopeRailRoad.cs` / `SpecialPurposeRailRoad.cs` | straight guidance; surface/underground ramp pattern owns its height transition |
| `core/world/rail/SimpleTrainControllerImpl.cs` | choose straight; ignore every non-Station harbor, including signals |
| `core/world/World.cs` | reuse the existing source H/V/Z visible-border predicate before destination lookup |

The walker computes current-rail guide → XY step → current-rail incoming-heading
zdiff → visible border → destination reciprocal port. Stored voxel bounds do not
replace the visible-border test. A visible exit becomes Outside100 without
requiring destination track. A connected occupied destination blocks, including
the train's own tail; it does not initiate reversal.

A successful move shifts the candidate head state and each predecessor's **old**
state through the complete ordered consist. Stable car IDs, original indices,
mixed-stock bindings and formation orientation survive. Followers do not ask the
current head controller to choose their route again. The fixture whose follower
is already taking a curved junction route retains that route even when the
current effective controller is simple.

Missing or nonreciprocal track performs one Inside reversal/retry. Each reversed
heading uses current `guide().opposite`, then the consist order reverses.
TRAIN-D001 retains that reversal on both-null, sets EmergencyStopping, books no
move and schedules exactly one future retry. Connected occupied candidates never
reverse. Complete mixed Inside/Outside reversal, handler-side TURNING effects,
commands, localized reversal feedback and recovery matrices remain P5-12 onward.

Initial deployment still schedules native speed. Inside callbacks consume the
captured prior-head location for Low → Medium → Fast timing; Outside candidates
use native speed. Successful moves increment the checked source-width counter;
moves1/5/9/etc book `(length*20 + passengers/20)*2000` as one RAIL_SERVICE spend.
Waiting/blocked callbacks neither count nor charge. P5-13 retains its full numeric,
handler-reversal/positive-wait and SCN-014 acceptance matrices; these foundational
Inside bindings do not close that later step.

## Publication and unavailable handlers

Placement/withdrawal and movement share one complete-consist occupancy installer.
Changed cars and the train advance their revisions once; affected Traffic/slope
owners advance once even when several car slots change on one owner. Rail
subtypes, backups, road/accessory/support members, terrain, groups/controllers,
allocator and random state survive. Bookings, queue rescheduling and all spatial/
fleet changes publish only after the detached **entire minute** validates.
Late callback failure or car/owner/move/order overflow discards earlier successful
moves, accounts, random draws and pending-action changes from that minute.

Simple ordinary/junction/signal behavior is bound. Advanced junction/harbor
decisions reject explicitly rather than borrowing simple decisions. Outside
countdown/exchange/return is not activated piecemeal: the initial Inside exit is
real, but the next Outside-head callback raises `unboundOutsideMovement` until
P5-14 supplies the complete behavior. Mixed-state reversal has the corresponding
explicit boundary. The host catches only these named unavailable-handler cases:
Step returns `operationUnavailable` with the minute unchanged; Run pauses after
rollback. Save/load remains usable. Existing EN/JA portable diagnostic wording now
says “this world” rather than misidentifying mixed worlds as the spatial
reference world. This is an existing browser diagnostic adaptation, not a new
translation of a pinned source literal.

The renderer deliberately retains its unsupported-car guard until P5-09. A
deployed fleet currently reaches “Map unavailable”; this step does **not** remove
that guard, hide the cars or substitute fixture art. The new browser case sends
ordinary Step commands through the real connected sockets while checking live
transport/decoding and continuation. Actual moving-map art, picks and retained
canvas acceptance remain P5-09/11. The existing inventory browser case still
exercises visible UI, canvas retention and reload.

## Verification

Fresh on3 November2026: .NET SDK10.0.402, Deno2.9.8, Playwright1.64.1, Chromium.

- **232/232 focused .NET** tests, including15 movement cases. Actual registered/
  purchased stock executes13 literal zero-stop/simple callback traces, all16 real
  four-direction surface/underground ramps in both travel directions (48 shifts),
  eight visible exits, both junction-follower routes, foreign occupancy and
  recovery, reciprocal-port rejection and atomic fault/overflow rollback.
  The ramp fixtures translate XY by(+16,+16) to fit legal stored/visible bounds;
  relative geometry, Z, headings, source patterns, costs and queue literals remain
  exact. Each ramp shift agrees with independently restored continuation and
  advances its shared owner once. Callback neighbors are real owned trains.
- Two formations, in opposite initial orientations, each run80 moves around an
  actual32-cell normal-rail octagon built by P4 handlers, with a real junction and
  branch. An independent eight-direction route oracle verifies every head
  position; all80 successful moves, stable identities,20 operating bookings,
  exact save continuation and mid-run restore pass. This32-cell witness is
  separate from P4's36-cell capacity rings and gives no P5-28 capacity credit.
  Production `SessionComposition.CreateSavedHost` drives the same saved fleet
  through real Step commands. A separate host test verifies unavailable Step,
  safe Run pause and usable save capture after exit.
- **30/30 selected client** tests: fleet-state, mixed-state, state-transfer and
  localization. **2/2 selected Chromium** fleet cases pass in7.6seconds: prior
  inventory/revenue/rejected-upload/reconnect behavior and production moving
  transport across two real browser clients. The latter verifies complete
  consist/occupancy/bookings/one future callback, simple junction and curve,
  exact HTTP reopen, resumed movement, no unintended reconnects and peer reload.
  No moving-art screenshot or visual/native credit is claimed.
- Web production build, generated/content/locales and Svelte checks pass (zero
  Svelte errors/warnings; existing bundle-size advisory). Pinned train review,
  744-fixture source check,48-placement-pose source check, stock registration,
  railway handoff and project boundaries pass. No complete web suite ran.

Logs are `tmp/p5-08-focused-dotnet.log`, `tmp/p5-08-selected-client.log`,
`tmp/p5-08-selected-browser.log`, `tmp/p5-08-web-build.log` and
`tmp/p5-08-{source-review,fixtures,placement,stock-review,handoff,project-boundaries}.log`.
An initial moving-browser attempt correctly reached the pending P5-09 render
guard; final transport verification explicitly retains that scope boundary.

The gameplay-built input is emitted only when `FREETRAIN_MOVING_FLEET_OUTPUT` is
set. Its SHA-256 is
`9a112dbd4ac44d8450986dad997dbf0c156e28935f453d4ea9ae0a10f8346a9d`.
It is a captured test input for browser transport, not an independent oracle;
expected poses/costs/routes come from the pinned literals and independent route
above. The unchanged744-case fixture SHA-256 remains
`3367d352414b4e688855649ba13d5044a350e16b89114b56448f1129f901e7dc`.

Reproduce from the repository root:

```sh
FREETRAIN_MOVING_FLEET_OUTPUT="$PWD/tmp/p5-08-moving-fleet.json" dotnet test tests/FreeTrain.Online.Tests/FreeTrain.Online.Tests.csproj --no-restore --filter 'FullyQualifiedName~FleetMovementTests|FullyQualifiedName~FleetDeploymentTests|FullyQualifiedName~FleetTests|FullyQualifiedName~FleetTradingTests|FullyQualifiedName~FleetPersistenceTests|FullyQualifiedName~TrainFixtureBoundaryTests|FullyQualifiedName~MixedContinuationTests|FullyQualifiedName~MixedPersistenceTests|FullyQualifiedName~RollingStockTests' -m:1 -nodeReuse:false -p:UseSharedCompilation=false
python3 scripts/review-trains.py --source-root ../freetrain-exav-archive --check
python3 scripts/train-fixtures.py --source-root ../freetrain-exav-archive --check
python3 scripts/check-train-placement.py --source-root ../freetrain-exav-archive
python3 scripts/review-stock-registration.py
python3 scripts/check-railway-handoff.py
python3 scripts/check-project-boundaries.py
```

From `web/`, after the focused .NET run:

```sh
deno task build
deno test -A --no-check tests/fleet-state.test.ts tests/mixed-state.test.ts tests/state-transfer.test.ts tests/localization.test.ts
FREETRAIN_ONLINE_TEST_SERVER_CONFIGURATION=Debug FREETRAIN_MOVING_FLEET_OUTPUT="$PWD/../tmp/p5-08-moving-fleet.json" deno task test:browser:run fleet-persistence.spec.ts --project=canonical --workers=1
```

The moving browser case explicitly skips when its generated input is absent;
both cases ran in the reported result. The ordered checklist records completion
and the next step. P5-09 art, P5-10/11 controls/receipts, full P5-12–24 behavior,
P5-27 recovery, P5-28 moving capacity and existing native/human/optional phase
qualifications remain open at their stated scopes.
