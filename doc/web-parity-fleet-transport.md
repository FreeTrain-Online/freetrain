# Fleet persistence and coherent transport — 3 November 2026

P5-05 supplies the detached fleet save/transport foundation. Save9 and protocol7
replace Save8/protocol6 under the [current-only policy](web-parity-runtime-policy.md).
Exact ruleset/content/pack hashes and stock limits are unchanged. Save1–8 reject
as `incompatibleSave`; malformed current graphs reject as `invalidSave` before
world, epoch, queue or file replacement. There is no converter. Retired Save8
fixtures and schema are retained [unchanged](../prototype/protocol/historical/save-v8/README.md).

## Source and requirement bindings

Authority remains ExAV2.1.1.12, Git `883cf7d81cf49db25ff38cd3a25d0f140afe31a0`,
`trunk/`. [P5-01 pinned review](parity/train-review.json),
[P5-02 independent literals](parity/train-fixtures.json),
[P5-03 exact registrations](parity/train-stock-registration.json) and
[P5-04 graph/callback bindings](web-parity-fleet-state.md#source-and-requirement-bindings)
are consumed without changing their oracles.

| Pinned source | SHA-256 | Consumer |
| --- | --- | --- |
| `core/world/CarState.cs` | `c68ad252be432207df5f59b551b5dced074fc80d07ea9cd526e30f5c3787999e` | Explicit Unplaced/Inside/Outside codec, nullable placed boarding origin |
| `core/world/rail/Train.cs` | `0130739ef685f252a8e1cd639fcf6d0908d3e3da6ef028e0418f3cb72cdfd404` | Original/current car identity, reversal, operation/passengers/forwarding/counters and nullable purchase minute |
| `core/world/Clock.cs` | `f9b34df78bb4e3d0530303568962af38413df406c45a11c4ff248b6b1d3517f4` | Explicit one-shot identities, due minutes, registration/order and FIFO restoration |
| `plugins/org.kohsuke.freetrain.tc.tat/src/TATTrainController.cs` | `fe78353f887c684dd135fe67036e1409cb866471a0b1f3e756217a0bc9e7fcb9` | TRAIN-D012 ordered Location-keyed harbor data, including absent locations |

TRAIN-D010 rejects every nonnull reserved `RailComponent.controllerId`, including
signals without a fleet; signals obtain the passing train's controller later.
TRAIN-D002/007/008/009 retain P5-04 graph/fallback, speed/width and referenced
controller-deletion policies. Reciprocal [action](web-parity-actions.md#p5-05-fleet-persistence-foundation)
and [manual/developer](web-parity-manual.md#p5-05-fleet-persistence-foundation) rows
bind MAN-T06–08, MAN-W07–10, MAN-A10–13/17, MAN-O08, DEV-P14/18–20/24 to this
foundation; their feature controls retain their numbered P5 implementing owners.
All six stock/eight car identities and the two controller providers retain the
P5-03 contribution ledger. No new EN/JA player text ships in this slice.

## Durable state and restore

[`FleetWire`](../src/FreeTrain.Online.Persistence/FleetWire.cs),
[`MixedWire`](../src/FreeTrain.Online.Persistence/MixedWire.cs) and
[`SaveCodec`](../src/FreeTrain.Online.Persistence/SaveCodec.cs) use explicit DTOs
shared with the [wire schema](../prototype/protocol/schema.json),
[Save9 schema](../prototype/protocol/save-v9.schema.json) and generated TypeScript.
`RailwayProfile.FleetProfile` supplies exact trusted stock and provider bindings.
No input installs handlers, assemblies, CLR types or serialized delegates.

Fleet arrays preserve train order, current car order/original indices, ordered
group membership and controller order. All stable IDs/revisions, purchase minutes
and queue integers use canonical decimal Int64 strings. All source pose/operation
fields survive, including packed passengers, nullable purchase time, placed
boarding origin, Outside countdown and maximum Int32 counters. Duplicate IDs,
allocator/spatial collisions, wrong formations/recipes/packs/providers, cycles,
dangling or nonreciprocal membership, wrong occupancy and future purchases reject.

Each timetable controller can retain ordered basic harbor data `(Location,
Pass/Fixed/Daily, Minutes, Reverse)`; duplicates and malformed modes reject.
Coordinates remain signed Int32 values and need not resolve to a current harbor.
Source same-location reuse is retained. This supplies location/data storage;
P5-19/20 extend it with executable basic/advanced rules and junction configuration.
No advanced data is silently accepted or dropped today.

A fleet requires `mixed.queue`, whose registration/order/due/handler/interval
records exactly match the top-level save `clock`. Canonical due/FIFO order,
unique future one-shot actions, one per deployed train and pending construction
cell, no action for Unplaced stock, and existing construction119-minute bounds
validate before replacement. Queue capacity remains65,536. Harbor entries have
an8,192-per-controller structural guard; existing mixed8MiB and save256MiB byte
admission remain in force. These are portable guards, not source maxima or
P5-28 moving-resource acceptance.

Restore reconstructs train bindings from canonical `train/<TrainId>` identities
and construction bindings from their explicit owner/location payloads. Deployed
fleets require a **trusted caller-supplied `TrainCallback`** in `ReadWorld`;
`SessionHost.PrepareSave` retains its host's dispatcher. Inventory without placed
trains needs none. The ordinary server has no movement dispatcher until P5-08,
so it accepts inventory and fails closed on deployed saves. Controlled-dispatcher
tests verify continuation; no placeholder movement is installed.

## Coherent publication

Snapshots, joins and packed terrain chunks keep the full fleet and queue in the
mixed header beside reciprocal Traffic ownership. A delta carries a complete
replacement fleet/queue component, so missing inventory/controllers and revised
group links are adopted atomically with occupancy and entity changes. The legacy
prototype `trains` array stays empty for canonical fleets. New IDs obey the shared
allocator; car ownership/type/index, train formations, provider kind, groups and
built-ins retain successor revision/identity guards in both domain and client.

Queue-only rescheduling may change a snapshot without changing geometry. Mixed
publication caches immutable construction data, then captures the **current**
queue at every boundary. The client permits that successor without a geometry
revision and validates its future callbacks against the new tick; graph/occupancy
changes still require their appropriate revisions. Partial/corrupt chunks,
unknown payloads and invalid deltas never become a readable state.

## Verification

On .NET SDK10.0.402, Deno2.9.8 and Playwright1.64.1:

- Focused backend tests: **293/293** in
  `tmp/p5-05-focused-dotnet-final.log`. Includes literal save/snapshot/delta,
  all five operation states, malformed graphs/queues and rejected replacement,
  a real two-peer `NetworkSession`, exact queue continuation, construction/train
  FIFO after codec restore, and128-train/1,024-car inventory round trips.
  Generated browser stock metadata is reconciled against registered formations,
  capacities, recipes and exact provider identities. Oversized harbor graphs reject
  symmetrically at save writing and live adoption without replacing the world.
- Selected client tests: **64/64** in `tmp/p5-05-selected-client.log`, plus
  **4/4** filtered current-version schema cases in `tmp/p5-05-version-client.log`.
  Covers schema examples, reversed stock, shared references, ordered/absent harbor
  locations, exact integers above2^53, atomic two-peer removals, queue-only
  successors, expired unchanged queues, clock rewind, checked border-coordinate
  overflow and out-of-order chunk publication. No exhaustive web run.
- Two selected Chromium workflows: **2/2**,6.0seconds, in
  `tmp/p5-05-selected-browser.log`. Two peers load, step, reject a bad upload,
  reject protocol6 and reconnect with exact shared data and retained canvas.
  The prior mixed-save compatibility case also passes. These emit no screenshots
  and grant no new visual/native acceptance.
- Web build/type/generated/content/locale checks pass (`tmp/p5-05-web-build.log`),
  with the existing bundle-size advisory. Source/fixture/stock/handoff and project
  boundary checks pass (`tmp/p5-05-{source-review,fixtures,stock-review,handoff,project-boundaries}.log`).

Literal fixture SHA-256 values (independently authored expected data, not captured
from the production codec):

| Fixture | SHA-256 |
| --- | --- |
| `saves/fleet-v9.json` | `4826e8d0d56b0d6a02d9b72bf9ca221f46959eacab236583b35c415ad13028e9` |
| `fixtures/snapshot-fleet.json` | `1ee83e11b6d4026391acc599b77911cd99250b3580275c3c361d443257db8168` |
| `fixtures/delta-fleet.json` | `2bb27514bac50b2e7ffaefd6d0780fd8a7d29fc4656628180a699f6eb1df7851` |

The delta literal represents one atomic graph/queue successor at revision7; the
backend's deliberately separate graph mutation and minute step assert the same
payload at revision8. Array/numeric/source expectations are unchanged.

Reproduce from the repository root:

```sh
dotnet test tests/FreeTrain.Online.Tests/FreeTrain.Online.Tests.csproj --no-restore --filter 'FullyQualifiedName~FleetPersistenceTests|FullyQualifiedName~FleetTests|FullyQualifiedName~MixedPersistenceTests|FullyQualifiedName~MixedContinuationTests|FullyQualifiedName~ContractTests|FullyQualifiedName~SaveRejectionTests|FullyQualifiedName~StateTransferTests|FullyQualifiedName~PersistenceTests|FullyQualifiedName~NetworkTests|FullyQualifiedName~StationaryConstructionTests|FullyQualifiedName~RollingStockTests' -m:1 -nodeReuse:false -p:UseSharedCompilation=false
python3 scripts/review-trains.py --check
python3 scripts/train-fixtures.py --check
python3 scripts/review-stock-registration.py
python3 scripts/check-railway-handoff.py
python3 scripts/check-project-boundaries.py
```

From `web/`:

```sh
deno task build
deno test -A --no-check tests/contracts.test.mjs tests/fleet-state.test.ts tests/mixed-state.test.ts tests/state-transfer.test.ts tests/remote-client.test.ts tests/save-transfer.test.ts
deno test -A --no-check --filter '/current save|Save 9|terrain wire and save schemas/' tests/spatial.test.ts tests/terrain.test.ts
FREETRAIN_ONLINE_TEST_SERVER_CONFIGURATION=Debug FREETRAIN_ONLINE_BROWSER_EVIDENCE=../tmp/p5-05-browser deno task test:browser:run fleet-persistence.spec.ts reference-world.spec.ts --project=canonical --grep 'fleet Save9|mixed Save9' --workers=1
```

Trade/deployment/motion commands and controls remain P5-06 onward. Full fleet
checkpoint/recovery acceptance remains P5-27, moving capacity P5-28, station/
passenger integration P6 and desktop/historical conversion P10. Existing P2,
optional-pack, native and P11 qualifications retain their actual scopes.
