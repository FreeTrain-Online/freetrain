# P6-20 departure events and local playback — 10 November 2026

Departure bells now travel from a successfully validated station minute to each
eligible live peer. This completes STATION-D007's network adaptation in the
[station source contract](web-parity-station-contract.md#committed-departure-events).
The current wire contract is **protocol9**; canonical saves remain **Save10**.
Deploy the browser and server together. Old protocol versions receive HTTP426.

The source authority remains ExAV 2.1.1.12, archive Git
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`. Yard call predicates, controller
ordering, boarding/forwarding and selected-platform identity retain
[P6-19's source ordering](web-parity-station-yards.md). The intent additionally
captures the **post-handler Inside head**, before central reversal and movement,
as the source sound location. First calls, positive waits, mismatched stop indices
and disconnected hosts cannot produce a bell; later −999/−1000 forwarding calls
ring without boarding. TURNING/reversal witnesses verify the captured head.

`SessionHost` assigns one committed publication sequence per successful minute
and an ordinal per bell in callback order. A fresh random live epoch is created
for each process/host and world epoch, including replacement and reopen. Only
successfully committed minutes enter the bounded transport outbox (at most
256 minutes ×128 trains); snapshot/save/checkpoint history contains no bells.
Arithmetic/validation failures reject Step with `operationUnavailable`; realtime
failures pause safely. Retrying a receipt never reruns its callback or emits an
event. The prior successful minute's detached intents cannot leak on failure.

Protocol9 adds `departureBell` with world/live epochs, publication sequence,
ordinal, train/platform IDs, exact selected definition and source location.
Network joins, replacement snapshots and resync snapshots carry only
`effectsBaseline`, the current live epoch and sequence. State is queued before
effects. Joining/resynchronizing peers exclude events at or before that baseline;
peers transferring state miss current audio instead of building a backlog.
The live outbox is drained once, independently of command replies. Existing
bounded peer queues still disconnect slow consumers instead of blocking the world.

The remote client maintains a constant-space sequence/ordinal high-water mark.
It rejects old world/live epochs, pre-baseline events, old sequences and duplicate
ordinals, and consumes suppressed events too. Reconnect clears the subscription
and obtains a new baseline. No receipt, snapshot or save reconstructs effects.

The local Enable sound/Disable sound setting also gates real departures. Each
peer independently needs a successful user gesture, a visible document and the
source location inside its current viewport/height cut. The selected contribution
resolves through the hash-checked bundled catalog and its exact WAV checksum.
Mute, disconnect, replacement, hidden-document changes and disposal cancel pending
loads. Suppressed events never queue for later; playback failure cannot change
simulation, receipts or save bytes. The six separately gesture-gated WebAudio
previews retain their existing controls and EN/JA credits. BGM remains deferred.
EN/JA platform help now describes actual departure playback.

## Verification and scope

Fresh verification passes **1,554 relevant Release .NET regressions** with no
skips, including **70/70 focused yard/departure/persistence cases** (ten new
P6-20 cases and forty retained yard cases), **378 web unit/retention tests**,
**nine distinct Chromium scenarios without skips**, **28 station Python tests**,
all **821 independent fixture cases**, raw pinned-source review, generated
contracts/content, EN/JA locale checks, Svelte/TypeScript (zero errors/warnings),
production build, project boundaries and whitespace checks.

An early regression run exposed a stale protocol8 assertion, now updated. A
subsequent transport test timed out under competing workloads; the isolated
transport suite passed18/18 and the final full sweep passed1,554/1,554. Initial
harness imports, a test publication-slot assumption and refreshed evidence
self-digests were corrected; final runs resolve them. The runtime-policy
prerequisite and downstream review/fixture digests changed only metadata;
original source pins, decisions, fixture inputs and expected vectors are unchanged.
Recorded event/playback evidence is in `tmp/p6-20-browser-accepted/*.json`.

The focused tests cover first/nonpositive/wait/disconnected silence, both forwarding
sentinels, selected contributions, two peers, state-before-event ordering, successful
and rejected receipt retries, reconnect/resync/reopen baselines, same-publication
ordinals from **two actual constructed yards and purchased/deployed trains**, and
post-handler TURNING locations. Client tests cover >128 subsequent identities
without forgetting old departures, epoch/baseline resets, invalid envelopes,
gesture/hidden/offscreen/mute/disposal and pending-load cancellation.

The Chromium forwarding scenario uses a clearly authored later-call boundary in
a unit-generated current-format save, with actual constructed yards, purchased
stock, a real inherited TAT forwarding rule and the platform's selected sixth
bell. EN desktop and JA narrow touch Observer peers call the real media `play`,
receive intentionally duplicated network events, and compare exact generated
successors. It verifies gesture gating, live playback, receipt retry, reconnect,
old-event delivery after replacement, offscreen/mute suppression and process
checkpoint restart without replay. A separate real departure with checked cash
underflow verifies rejected callback and repeated receipt silence without changing
saved state. This negative fixture explicitly supplies boundary cash and call count.
The existing Simple-yard browser scenario verifies first wait silence and exactly
one packed boarding departure event across reopen and checkpoint restart.

Automated Chromium media invocation is not a human listening or original-runtime
audio-fidelity certificate. Complete two-station browser construction/service is
P6-21; native/original-runtime, other-engine, optional-content, capacity/soak,
Linux/container, deployment and full P6 acceptance remain with their named owners.

Reproduction (use the repository `.tools/dotnet` SDK on PATH):

```sh
FREETRAIN_YARD_FLEET_OUTPUT="$PWD/tmp/p6-20-yard.json" FREETRAIN_BELL_FLEET_OUTPUT="$PWD/tmp/p6-20-forwarding.json" dotnet test tests/FreeTrain.Online.Tests -c Release --no-restore -m:1 --filter 'FullyQualifiedName~StationYardStopTests|FullyQualifiedName~StationPersistenceTests'
dotnet test tests/FreeTrain.Online.Tests -c Release --no-build --no-restore --filter 'FullyQualifiedName~Station|FullyQualifiedName~Platform|FullyQualifiedName~Passage|FullyQualifiedName~Mixed|FullyQualifiedName~Fleet|FullyQualifiedName~Train|FullyQualifiedName~ContractTests|FullyQualifiedName~NetworkTests|FullyQualifiedName~Multiplayer|FullyQualifiedName~ClockTests|FullyQualifiedName~StateTransfer'
deno task --cwd web test
deno task --cwd web build
FREETRAIN_BELL_FLEET_OUTPUT="$PWD/tmp/p6-20-forwarding.json" FREETRAIN_YARD_FLEET_OUTPUT="$PWD/tmp/p6-20-yard.json" deno task --cwd web test:browser:run departure-bells.spec.ts station-yard.spec.ts --project=canonical
deno task --cwd web test:browser:run reference-world.spec.ts persistence.spec.ts platform-properties.spec.ts --grep='bell|sound|platform properties|platform names'
python3 -m unittest discover -s scripts/tests -p 'test_station*.py'
python3 scripts/station-fixtures.py --check --source-root /Users/dmark/dev/freetrain-exav-archive
python3 scripts/review-stations.py --check --source-root /Users/dmark/dev/freetrain-exav-archive
python3 scripts/check-project-boundaries.py
git diff --check
```

Logs and browser evidence: `tmp/p6-20-*`. Local sockets were enabled for .NET
runner IPC and browser/backend servers after the sandbox rejected binding. The
existing Vite bundle-size advisory remains.
