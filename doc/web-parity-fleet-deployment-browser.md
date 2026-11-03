# P5-11 deployment and first playable slice — 3 November 2026

P5-11 connects owned stock to actual P4 infrastructure and the production train
callback through **Game → Trains → Owned stock / ゲーム → 列車 → 保有車両**.
This is the initial browser/portable playable scope; full P5 acceptance remains
with the subsequent numbered steps.

## Source and presentation

The [deployment domain](web-parity-fleet-deployment.md),
[train contract](web-parity-train-contract.md) and ExAV
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0` remain authoritative.
`Train.place/remove/reverse`, `TrainPlacementController.onClick` and the actual
rail subtype guides supply placement, withdrawal and direction semantics.
MAN-T06/W08/A11, DEV-P14/P19/P24 and the deployment portion of bilingual
workflow01 now reach real browser commands. The existing purchase/resale bindings
remain [P5-10](web-parity-fleet-browser.md).

Select an owned train, pick **Pick tail on map / 地図で最後尾を選択** or enter
canonical X/Y/Z, and request **Preview deployment / 配置プレビュー**. The preview
lists every stable CarId, position and localized heading in current head-to-tail
order. Numbered translucent cell diamonds and direction arrows show that exact
footprint on the retained map canvas. These are placement overlays; placed cars
continue to use P5-09's source-art renderer. Map targeting preserves the camera,
active world and shared clock; keyboard coordinate entry uses the same preview.

**Reverse placement direction / 配置方向を反転** composes source reversal in place
with placement. It changes the facing/current car order on the same footprint,
relative to the inventory's retained orientation. Running to a dead end may
already have reversed that inventory orientation. No arbitrary heading or new
rail walk is invented. GEO-008 remains fixed-Z placement; the real motion walker
alone traverses ramp height changes. Missing track across a ramp height boundary,
occupied/duplicate cells, invalid heading, stored-world border and unimplemented
junction controller decisions have distinct EN/JA feedback.

**Withdraw train / 列車を撤去** previews and confirms a separate action. It clears
all Inside slots and the selected timer while retaining owned stock, identities,
orientation, controller/group references and accounting. It books no sale revenue.
The existing sale action continues to require Unplaced stock.

The panel resolves the train's actual effective shared controller with the source
explicit-delegation/null/root fallback and names it. Manual/timetable junction
and harbor behavior and Outside movement retain their later P5 owners. The
panel states those current limitations; unsupported previews diagnose the actual
junction, and the existing callback boundary pauses shared time atomically when
an unsupported callback is reached. Run/Pause/Step remain the shared clock
controls, with no private playback or preview timer.

## Authority and lifecycle

`POST /api/fleet/deployment/preview` accepts authenticated compatible bounded
requests, including Observer read access. The data-only query binds TrainId,
action, tail/reversal, world epoch, complete session revision, shared minute and
content identity. The response carries actual detached complete-consist poses.
No local legality calculation grants placement permission.

`deployFleet` reconstructs the same plan at the FIFO boundary and validates the
whole persistence/transport transition before publication. The existing role,
participant generation, receipt deduplication/retry and unresolved-outcome rules
apply. A peer edit, clock/pause change, competing placement, sale or world
replacement invalidates the old query. Placement and withdrawal publish fleet,
occupancy, revisions and queue together. Preview/cancel/rejection changes no save
bytes, cash or allocator. Replaying an acknowledged request returns its receipt;
a different request carrying the old query rejects.

Changing coordinates or direction cancels the old preview; a stale world clears
the map overlay and disables confirmation. Abort/generation/connection fences
reject late HTTP responses. Retargeting, panel destruction, world replacement,
role changes and rename drafts reuse shell lifecycle guards. Cancel restores the
preview launcher; Escape clears a review or pending map pick before closing the
panel. Default Return uses a scoped deployment action, preserving native form
validation and composition/repeat guards. The existing name draft remains
protected during navigation and locale changes.

Save9/protocol7, saved-state schema, catalogs, pack identities and resource limits
are unchanged. The preview/command contract is additive.

## Verification

Fresh selected checks on 3 November 2026:

- **91/91 .NET**: session trading/deployment, domain deployment/movement,
  fleet persistence and contracts. New session cases cover exact poses in both
  directions, no-sale withdrawal, redeployment, unchanged preview bytes,
  observer/stale/peer/replacement rejection and identical production movement
  after canonical reopen.
- **75/75 client**: stock/preview parsing, fleet/deltas, exact IDs above2^53,
  malformed/duplicate footprints, effective-controller fallbacks, remote lifecycle,
  shell and shared contracts.
- **7/7 selected Chromium**: the three EN/JA deployment/narrow cases and
  four adjacent trading cases. Final deployment-only **3/3** also verifies
  keyboard Return and ready-preview captures. Both playable journeys cross the real ramp,
  inspect actual cars, synchronize a peer and restore exact save bytes after
  HTTP reopen and an actual backend checkpoint restart. They step the restored
  native callback. Cancelled/stale/duplicate/replayed commands, JA390×844/200%
  text, delayed-response cancellation, draft/focus and Observer guards pass.
- Production build, generated/content/locales with zero Svelte errors/warnings
  (existing bundle-size advisory), raw pinned source/48 placement poses,
  744 independent train fixtures, stock registration and project boundaries.

The deployment journeys create real terrain through the retained
creation API and build ramp/approaches, buy, preview/map-pick/deploy, Run/Pause,
inspect actual cars, withdraw/redeploy, HTTP reopen and restart the actual backend
with a connected peer. Creation UI itself retains its earlier P3 acceptance.
The 390×844 Japanese case uses 200% text, name-draft guards, cancelled/late
responses and Observer controls. Adjacent P5-10 trade regressions are selected.
No complete web suite or new native/human/optional acceptance is claimed.

Logs: `tmp/p5-11-{web-build,selected-dotnet,selected-client,selected-browser,placement-review,source-review,fixtures,stock-review,boundaries}.log`.
Final deployment log: `tmp/p5-11-browser-final.log`. Visually inspected
EN/JA ready footprint and JA narrow captures: `tmp/p5-11-browser/deployment-{en,ja,ja-narrow}.png`.
Initial test failures and corrections include source movement-border coordinates,
array-value assertions, toolbar selectors, retained orientation after a running
dead-end reversal, explicit reauthentication after restart and waiting for the
asynchronous Step acknowledgement.

Reproduce selected checks from the repository root:

```sh
dotnet test tests/FreeTrain.Online.Tests/FreeTrain.Online.Tests.csproj --no-restore --filter 'FullyQualifiedName~FleetSessionTests|FullyQualifiedName~FleetDeploymentTests|FullyQualifiedName~FleetMovementTests|FullyQualifiedName~FleetPersistenceTests|FullyQualifiedName~ContractsTests' -m:1 -nodeReuse:false -p:UseSharedCompilation=false
```

From `web/`, after building the current Debug server:

```sh
deno task build
deno test -A --no-check tests/stock-catalog.test.ts tests/fleet-state.test.ts tests/remote-client.test.ts tests/shell-model.test.ts tests/contracts.test.mjs
FREETRAIN_ONLINE_TEST_SERVER_CONFIGURATION=Debug FREETRAIN_ONLINE_BROWSER_EVIDENCE=../tmp/p5-11-browser deno task test:browser:run fleet-deployment.spec.ts fleet-trading.spec.ts --project=canonical --workers=1
```
