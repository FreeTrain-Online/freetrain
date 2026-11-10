# P6-05 station persistence and transport — 8 November 2026

Complete at canonical state/authority boundary scope. Save10/protocol8 replace
Save9/protocol7 under the [current-format policy](web-parity-runtime-policy.md).
Catalog bytes, rulesets and content versions are unchanged. Earlier saves and
peers reject without conversion; the retired Save9 schema/fixtures remain under
[`historical/save-v9`](../prototype/protocol/historical/save-v9/README.md).
Construction commands, station/platform presentation, host discovery and passenger
service remain with their later P6 slices.

## Durable state and validation

[`StationWire`](../src/FreeTrain.Online.Persistence/StationWire.cs) converts the
immutable graph into explicit data-only contracts in
[`WireContracts.cs`](../src/FreeTrain.Online.Contracts/WireContracts.cs).
`mixed.stationGraph` retains:

- Building/platform IDs and revisions, qualified definitions, exact geometry,
  names and both automatic-name sequence high-water marks, including empty graphs.
- Direct hosts and ordered reciprocal children, thin/wide identity, lane masks,
  ordered yards with Traffic owner/index/lane, roof, outlook/material/bridge/stair
  choices, departure-bell definitions and standalone cosmetic passages.
- Gone/accumulated passenger counters and exact today/yesterday/weekly values for
  all three transport logs. Double values round-trip without truncation.
- Complete hourly/daily timer registrations, FIFO orders, due times, handler IDs
  and repeat intervals. Registration cookies can differ from current FIFO order.
- Separate ordered provider-local station references (`listeners`) and
  station-side provider memberships (`listenerMemberships`). STATION-D003 source
  asymmetry is accepted; no nearest-four discovery, reciprocal repair or sorting
  occurs on load. Providers must resolve actual spatial owners. These are
  controlled interface witnesses; P6-16/P7 still own provider/demand gameplay.

The state travels in saves/checkpoints, snapshots, complete mixed deltas and
chunk headers alongside spatial ownership, accounts and fleet relationships.
Removing a graph member replaces all its references and timer ownership in one
successor; removed spatial owners accompany that replacement. Names and identity
allocators never rewind. Logical member changes require revisions; provider-local
listener/passage changes require their Traffic/spatial owner's revision, and
station-side membership changes require the station revision.

Portable admission bounds the combined building/platform/passage count, total
yards and host edges to8192, providers to8192, local stations per provider to4,
and total reverse memberships to32768. Save preflight checks nested/aggregate
bounds before typed restoration; encode/restore/browser semantic checks agree.
The existing8MiB mixed,16MiB delta and256MiB complete-state budgets remain.
Graph validation also checks exact content, footprints, host cycles, lane/rail
membership, nonnegative finite logs, identity collisions and queue ownership.

## Scheduler and authority boundary

A station graph requires `mixed.queue` even without a fleet. In saves this queue
must exactly match the canonical clock, including ordering and next-order value.
All construction, deployed-train and station/platform handlers bind before terrain
restoration. Repeat cookies/intervals retain their source semantics; train and
construction callbacks remain one-shot. Session validation rejects missing,
extra, duplicate, misaligned and mismatched actions.

`SaveCodec.ReadWorld` accepts an optional trusted station dispatcher, and
`SessionHost.PrepareSave` retains the host's installed dispatcher just as it does
for trains. No callback delegate/type is serialized or activated by input. The
production default runs repeat continuation and upkeep for buildings/platforms;
[P6-17](web-parity-station-statistics.md) now supplies hourly passenger decay
and daily log rotation. See the
[station callback crash fix](web-parity-station-booking.md#callback-boundary-and-later-consumers).
This does not claim completed station service.

The existing authority FIFO, Owner-only replacement, Operator/Observer checks,
expected epoch/revision fences and duplicate command receipts remain in force.
The graph DTO is a read/validated-save contract, not permission for clients to
submit arbitrary graph replacements as gameplay commands. Later constructing and
property slices must add their exact queries/previews/commands to this boundary.

## Fresh verification

Production tests cover literal and authored detached graphs, exact round trips,
large repeat cookies, source-asymmetric listener order, 924-minute continuation
through midnight, combined purchased fleet/station state, atomic removals,
malformed content/graphs/queues, revision checks, two peers, roles/receipts/stale
epochs and host-dispatcher retention across replacement. The independent shared
[save](../prototype/protocol/saves/stations-v10.json) and
[snapshot](../prototype/protocol/fixtures/snapshot-stations.json) include both
platform kinds, a cosmetic passage, nonzero logs and large timer handles.

Four new client tests consume those literal bytes, reject malformed graphs and
queues before reveal, require logical revisions, remove the complete graph while
retaining name sequences, and assemble chunked state only when coherent.

The new live-map Chromium witness uses a building/log/listener scene derived
explicitly from the literal fixture, without platforms: platform rendering still
fails closed until its implementing slice. It verifies EN desktop/JA narrow peers,
shared Step before a callback is due, corrupt-upload rejection, exact download/
reopen, canvas retention and independent process/checkpoint restart. Full platform
state is verified through .NET and client decoding/chunk tests, not rendered
platform gameplay. Existing station-editor harness, shared Simple/TAT/TURNING,
signal and persistence regressions also run.

Final Release .NET: **1,717/1,717 pass** (`tmp/p6-05-dotnet-final.log`). Client:
**320/320 pass** (318 ordinary plus2 retention; `tmp/p6-05-web-unit.log`).
Selected Chromium: **26/26 pass**, zero skipped in the final explicitly scoped
selector (`tmp/p6-05-browser-final.log`). Python: **221 run/220 pass/one existing
skip** (`tmp/p6-05-python.log`). Source review,821 independent station
cases/24 families/38 source pins, project boundaries, generated/content/locale
checks and Svelte/TypeScript/build checks pass. The existing bundle-size advisory
remains. No native, other-browser, optional-suite, Linux/container or capacity
acceptance is implied.

Reproduce from the repository root:

```sh
dotnet test FreeTrain.Online.slnx -c Release --no-restore -m:1 -nodeReuse:false -p:UseSharedCompilation=false
DENO_JOBS=4 deno task --cwd web test
deno task --cwd web build
python3 -m unittest discover -s scripts/tests
python3 scripts/station-fixtures.py --check
python3 scripts/review-stations.py --check
python3 scripts/check-project-boundaries.py
```

Browser controller/signal tests require fresh current-format production inputs;
regenerate them instead of relabeling previous saves:

```sh
FREETRAIN_CONTROLLER_FLEET_OUTPUT="$PWD/tmp/p5-18-manager-fleet.json" \
FREETRAIN_TAT_FLEET_OUTPUT="$PWD/tmp/p5-19-tat-fleet.json" \
FREETRAIN_TURNING_FLEET_OUTPUT="$PWD/tmp/p5-21-turning-fleet.json" \
FREETRAIN_SIGNAL_FLEET_OUTPUT="$PWD/tmp/p5-24-signal-fleet" \
dotnet test FreeTrain.Online.slnx -c Release --no-build --no-restore -m:1 \
  --filter 'FullyQualifiedName~Manager_browser_input|FullyQualifiedName~TAT_junction_manager_patches|FullyQualifiedName~Turning|FullyQualifiedName~TURNING|FullyQualifiedName~Signal_real_shared'
cp tmp/p5-19-tat-fleet.json tmp/p5-20-tat-fleet.json
deno task --cwd web test:browser:run station-transport.spec.ts station-editing.spec.ts fleet-controller-manager.spec.ts signal-integration.spec.ts persistence.spec.ts \
  --grep-invert 'P5-14|P5-13|production fleet transport|@extended|@performance'
```

The broader persistence selector also discovers three opt-in fleet movement/
Outside/timing scenarios; these were skipped because their separate generated
inputs were not enabled; the final selector excludes them explicitly. The
selected26 workflows pass. The first browser run
exposed old Save9 inputs and the deliberately unavailable platform renderer;
regenerated inputs and the scoped building-only live-map witness resolve those
failures without implementing or claiming later presentation slices.
