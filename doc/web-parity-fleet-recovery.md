# P5-12 reversal and emergency recovery — 5 November 2026

Subsequent [P5-14 delivery](web-parity-fleet-outside.md) completes production
Outside movement/exchange/return, including an Inside retry reaching an Outside
head. The unavailable boundaries below describe the original P5-12 scope.

P5-12 is complete at browser/portable reversal and Inside recovery scope.
Owned, placed trains now offer an English/Japanese **Reverse train** preview and
confirmation. Missing forward track reverses once and retries; a connected,
occupied destination waits without reversing. Repeated blocked callbacks and
subsequent track repair or withdrawal preserve identities, reciprocal occupancy,
accounts and exactly one train callback through save/reopen and server restart.

Save9/protocol7, saved-state shape, pack/catalog identities and capacity limits
are unchanged. The deployment query adds the `reverse` action. Full Outside-head
movement/exchange/return remains P5-14; station/signal/controller-side reversal
and TURNING remain with their numbered controller slices.

## Source bindings and adaptations

Authority is ExAV 2.1.1.12/V2, archive Git
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, `trunk/`. The
[train contract](web-parity-train-contract.md),
[source review](parity/train-review.json) and
[independent P5-02 literals](parity/train-fixtures.json) remain authoritative.

| Source behavior | Delivered binding |
| --- | --- |
| `Train.reverse` / `ReverseCarState.onInside` | Shared `FleetReversal.Reverse` visits current head-to-tail order, uses each current rail's `guide().opposite`, then swaps order and toggles formation orientation. Original car IDs, indices and definitions survive. Placement and dead-end movement use the same operation. |
| `JunctionRailRoad.guide` | A follower uses its predecessor's placed location, including an Outside predecessor; reversing does not substitute a fresh head-controller decision for the follower's route. |
| `ReverseCarState.onOutside`, `calcReturnPoint`, `Matrix` | First connected reference in the exact five-matrix order, with truncating integer division and no occupancy filtering. A reference produces `reference.location - reference.direction`, the opposite reference heading and `100-countdown`. No reference leaves the car pose unchanged while train order/orientation still reverse. |
| `Train.clockHandler` | Only a missing/nonreciprocal forward candidate starts one reversal/retry. Any connected destination car, including the train's own tail, blocks movement. No blocked attempt increments MoveCount or books running costs. |
| TRAIN-D001 / SCN-D03 | Both-null retains the reversal, publishes EmergencyStopping without a position shift or charge, and schedules one future retry using the captured prior-head timing. The native source null dereference is explicitly not reproduced. |
| `Train.stateDisplayText` / placement reversal | Owned-stock and picked-car panels display the current saved operational state. Browser recovery reasons/help and preview/confirmation are portable UI adaptations. Explicit reversal preserves stop/forwarding/passenger/boarding/purchase/movement history and the exact existing callback; it incurs no booking. |

Reversal plans use the existing detached candidate, stale session/revision/clock
checks, complete-consist occupancy installer and FIFO command/receipt lifecycle.
Car revisions advance once per complete operation; unchanged Outside poses keep
their car revisions. Queries/commands reject unplaced reversal, invalid arguments,
Observers and stale input without partial state. Whole-minute overflow/failure
rollback remains intact.

Recovery reasons are derived from the current immutable snapshot, without
advancing controllers or time. They distinguish missing reciprocal track from
connected occupancy, include slope incoming-heading height changes and visible
border rules, and clear on recovery. A repaired but not yet retried train says it
will be checked at the next callback. No save field is added for presentation.

The shared Outside reversal visitor is delivered independently of Outside
movement. A saved mixed Inside/Outside consist reverses and reopens with its
existing callback. After reversing back to an Inside head, actual movement
continues identically in the original and reopened sessions. An Outside-head
callback, including one reached during a dead-end retry, still returns the named
`unboundOutsideMovement` boundary with whole-minute rollback until P5-14.
This step does not claim countdown/exchange/return gameplay or later controller
stop-handler execution; those handlers must reuse this reversal/safety contract.

## English/Japanese wording

`fleetEmergency` and `fleetWaiting` express the source EmergencyStopping and
generic departure-wait states; existing Moving/Unplaced labels are reused.
`fleetReverse`/`fleetReverseConfirm` describe the source reversal action in the
browser preview workflow. `fleetMissingTrack`, `fleetOccupiedTrack`, `fleetRetry`,
`fleetRecoveryHelp` and `fleetReverseHelp` are explicitly new diagnostic/workflow
copy for the portable retry adaptation and safety rules, not translations of
invented source dialogs. Both locale files ship together. Inspection and owned
stock use the same state/reason mapping.

## Verification

Fresh on 5 November 2026:

- **103/103 focused .NET tests**: FleetMovement (including recovery), FleetSession,
  FleetDeployment and FleetPersistence. All 24 literal straight/curved reversals
  in eight headings, SCN-004, 16 real surface/underground ramp inputs, both
  junction-follower routes and 16 Outside found/missing reversal literals pass.
  The Outside literals are abstract pure-visitor checks; a separate legal border
  mixed-state case supplies actual save/occupancy/callback continuation evidence.
  Mixed head/body/tail stock retains three distinct original definitions through
  reversal, movement and reopen. One- and three-car both-null retries retain
  exact cells and zero charges for four callbacks, then recover on a genuinely
  built approach. Foreign occupancy blocks for three callbacks without reversal
  and recovers after withdrawal, with save continuation. Own-tail blocking,
  nonreciprocal track, stale plans and numeric/late-callback rollback also pass.
- **84/84 selected client tests**: fleet-state, train-fixtures, train-renderer,
  train-motion, remote-client and contracts. Reversal previews accept Inside and
  fully Outside consists; duplicate poses and inconsistent placement/withdrawal
  previews reject. Recovery feedback distinguishes foreign/own occupancy from
  missing reciprocity and leaves its input unchanged. Existing reversal
  discontinuity and retained motion/rendering checks pass.
- **Four distinct selected Chromium scenarios pass**: EN/JA reversal recovery,
  EN playable ramp deployment and Japanese narrow deployment/Observer regression.
  The recovery pair exercises detached preview/cancel/focus, peer-clock staleness,
  repeated confirmation, exact identity/order/timer/account publication, a real
  source-mask car pick after reversal, both-null feedback, HTTP save replacement,
  checkpoint restart and actual rail-tool repair followed by resumed movement.
  Canvas identity and camera are retained across reversal; the car-picking check
  then explicitly pans to the selected train. JA recovery also covers 390×844
  at 200% text with reachable actions and no horizontal panel overflow. The
  Observer regression verifies that placed reversal is disabled. No page errors.
  EN/JA emergency/recovery and JA narrow captures were visually inspected.
- Production build, generated contracts, prepared content, locale registry and
  Svelte checks pass with zero Svelte errors/warnings; the existing bundle-size
  advisory remains. Raw pinned source review, all 744 independent fixtures,
  project boundaries and whitespace checks pass. No complete web suite ran.

The first browser run exposed withdrawal-only preview and command guards; both
now accept reversal. The test initially waited for the preview to disappear,
which happens on submission; it now waits for authoritative state. The map-pick
check uses an actual source-mask roof pixel and explicit pan rather than a track
cell center. Initial test-only constructor/clock-comparison/occupied-build-start
mistakes were corrected before the passing runs. New recovery cases finish 2/2
in `tmp/p5-12-browser-recovery.log`; the two earlier passing regressions are in
`tmp/p5-12-selected-browser.log`. Earlier failed recovery attempts in that log
are superseded, not counted as passes.

Logs: `tmp/p5-12-{selected-dotnet,selected-client,web-build,server-build,source-review,fixtures,boundaries}.log`.
Captures: `tmp/p5-12-browser/`. Native/human, optional-suite, full P5 capacity and
later controller/timing/Outside/phase acceptance retain their existing owners.

Reproduce the focused backend set from the repository root:

```sh
dotnet test tests/FreeTrain.Online.Tests/FreeTrain.Online.Tests.csproj --no-restore --filter 'FullyQualifiedName~FleetMovementTests|FullyQualifiedName~FleetSessionTests|FullyQualifiedName~FleetDeploymentTests|FullyQualifiedName~FleetPersistenceTests' -m:1 -nodeReuse:false -p:UseSharedCompilation=false
```

From `web/`, with the current Debug server built:

```sh
deno task build
deno test -A --no-check tests/fleet-state.test.ts tests/train-fixtures.test.ts tests/train-renderer.test.ts tests/train-motion.test.ts tests/remote-client.test.ts tests/contracts.test.mjs
FREETRAIN_ONLINE_TEST_SERVER_CONFIGURATION=Debug FREETRAIN_ONLINE_BROWSER_EVIDENCE=../tmp/p5-12-browser deno task test:browser:run fleet-deployment.spec.ts --project=canonical --workers=1 --grep 'reversal recovery|Japanese narrow deployment|en playable'
```
