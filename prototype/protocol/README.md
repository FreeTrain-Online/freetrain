# Wire protocol 9

`schema.json` is the canonical draft-07 schema for current wire messages and spatial queries. `$defs` are referenced explicitly; custom formats `int64` and `uint63`
mean canonical decimal strings within signed Int64 or nonnegative Int64 bounds.
Validators must register these formats (see `web/tests/contracts.test.mjs`).
Strings forbid leading zeros, plus signs, whitespace, and negative zero. World
coordinates and counts are bounded JSON integers; money, ticks, epochs,
revisions, input order, and event order are decimal strings. Consumers compare
those using `BigInt` or `long`, never lexicographic or floating-point ordering.

Versions are independent: protocol **9**, default content `reference-spatial-content-2`, canonical **Save 10**. Online prototype Save 1 is retired. The explicit historical regression harness still identifies `prototype-content-1`.
Join conveys server incarnation, participant/session identity, server-assigned role,
epoch and initial snapshot. A `metadataOnly=true` HTTP bootstrap returns `sessionOffer` without world geometry; large-world bootstrap always uses this bounded offer. Commands cannot choose participant identity or authority.

Commands carry request ID and expected epoch; rename/controller/removal carry
expected revision. `accepted` means committed and includes application sequence,
tick and revision. `rejected` carries a stable code and string arguments. Deltas
include base/new revision and explicit removals, independently tracked per peer.
Request receipts are scoped to participant and server incarnation; connection loss
never causes automatic replay. Reusing a request ID for another payload is rejected.

Train DTOs include nullable `controlLease` with participant ID, lease ID and server
UTC Unix-millisecond expiry (decimal string). `acquireControl` acquires/renews a
five-second train lease; `releaseControl` relinquishes it. `setController` and
`controllerInput` require the current lease ID. Other holders receive `controlBusy`;
stale/unowned tokens receive `leaseRequired`. Input renews a lease. Disconnect,
release, expiry or load clear input; a new lease resets input sequence ordering.
`inputReset` clears only the sender's inputs and carries no global pause flag.

Protocols 1 and 2 are rejected with HTTP 426; deploy matching static and server artifacts.

Online prototype Save 1 is unsupported. The current schema and generated types
contain no Save 1 model. Historical data is kept only under `historical/save-v1`
and the archived implementation. The runtime rejects unsupported input with
`incompatibleSave`. Canonical Save 10 uses `save-v10.schema.json` and `saves/reference-v10.json`; the codec additionally validates topology, clock ranges, bound scheduler actions and the complete versioned random bank. Only the current format is supported during parity iteration; Saves 1/2/3/4/5/6/7/8/9 return `incompatibleSave` without conversion. Retired schemas and rejection fixtures live under `historical/save-v1`, `historical/save-v2` `historical/save-v3` and `historical/save-v4`. Prototype writes remain unavailable. See the
[owner-directed policy](../../doc/web-parity-runtime-policy.md).

Run from the repo root:

```sh
deno task --cwd web generate:contracts
deno task --cwd web test
dotnet test FreeTrain.Online.slnx -c Release
```

Commit generated TypeScript alongside schema changes. C# records in Contracts
and Persistence must round-trip the same fixtures without dropping/adding fields
or changing numeric values. The C# JSON reader enforces required constructor
arguments and disallows unmapped fields; the server additionally validates command kinds, counters and input at its boundary.
Complete saves are validated before replacement. TypeScript
compile-time types alone do not validate messages from a server.

Existing bounded queues, receipt retention, resync,
compatibility/access checks and persistence behavior remain the foundation.

`POST /api/leave` revokes the caller's login credential and disconnects its socket.
It is idempotent and uses the same origin protections as other API operations.
The configured access key remains usable through `POST /api/access`. Bootstrap
returns HTTP 401 after leaving, including in local mode; the response indicates
whether an empty key can deliberately rejoin as a local Owner. Login credentials
are opaque random values retained in a bounded, eight-hour server registry.

The default world identity is `reference-spatial-4` / `reference-spatial-content-2`
with the exact required-pack descriptor in `canonical-profile.json`. Join, snapshots
and deltas carry this identity. The map uses `hv` H/V storage dimensions and track
shapes `normal0`..`normal11`, matching source `RailPattern.singlePatterns` indices.
The asset-free reference pack is not the complete game catalog. Tick is absolute source minutes (initially 130080); revision advances for time and
pause changes. Cash stays zero; each rail has a stable entity ID and an explicit
occupied footprint. There are no trains, stations or buildings yet. The world starts
paused. See [P1-02](../../doc/web-parity-clock.md).

`prototype-profile.json` / `prototypeXY` remain explicit historical regression data.
Unknown identities block reveal. Clients can advertise multiple installed content
versions with repeated `contentVersion` query parameters; the server requires its
actual version to occur in that list and always publishes its exact identity.

Wire train `heading` and each `carHeadings` entry are integer indices 0..7
(N, NE, E, SE, S, SW, W, NW). Prototype adapters explicitly map old cardinal values
onto 0/2/4/6. Save 1 is rejected rather than adapted to the new representation. The
browser validates car counts/headings and the selected installed profile before
applying reads. Source canonical geometry has shared fixtures separate from that
compatibility world. Protocol-2 originals are preserved in `historical/v2`.

`POST /api/spatial/preview?protocolVersion=9&contentVersion=reference-spatial-content-2`
accepts a `SpatialQuery` and returns a `SpatialPreview` (both defined in the schema).
Any authenticated role can use it. Expected epoch, position, nullable anchor and
snap 0/4/8 select a checked read-only calculation. The result carries world
identity/revision, snapped position, bounds, projection, optional storage,
heading and distance. It never reserves cells, changes state or approves a later
construction commit. Invalid/overflow input is 400; stale epoch is 409.

Canonical snapshots include `entities`; tracks refer to their `entityId` (positive
Int64 decimal strings). Entity definitions are exact `{packId, contributionId,
recipeId}` references; anchors plus footprint offsets identify occupied voxels,
including invisible ones. Entity value, ownership and silent reclamation are
separate fields. Canonical deltas include changed `entities` and
`removed.entityIds`, with base/new revision and identity checks before application.
The historical prototype omits these fields. Optional schema fields preserve the
common protocol-3 envelope; exact ruleset dispatch enforces their presence for
canonical reads. Unknown or inconsistent graphs block reveal.

Save 10 also stores `spatial.nextEntityId`; removed IDs are never allocated again.
The allocator is private durable state and is absent from public reads. See
[P1-04](../../doc/web-parity-spatial-contracts.md) for source mappings and limits.

## P3-04 terrain transport

`terrain-state-1` / `terrain-content-1` selects the exact bundled pack/hash in
`web/src/generated/terrain-profile.json`, generated from the prepared catalog alongside TypeScript contracts.
Reference and prototype snapshots omit `terrain`; terrain snapshots require it and
`entities`, and have zero trains/stations/buildings/tracks. Cells are H/V row-major;
quarter corners use explicit NE/SE/SW/NW fields. Forests carry exact definitions and
realized ordered tree positions/sprite indices. Spatial ownership/reusability/value
and allocator identity remain explicit. Snapshot terrain metadata includes the
original canonical request/provenance and authoring mode; actual edited state is loaded.

Terrain deltas carry indexed changed cells, upserted occupants and current water/mode/
geometry revision/allocator; `removed.entityIds` removes both entity and terrain payload.
The client validates the merged graph before exposing it. Name, dimensions and
provenance changes require an epoch replacement snapshot. Coalesced entity revision
advances are supported, allocator rewind and removed-ID reuse are rejected.

Save 10 supports reference, terrain or mixed railway profiles; Saves 1–9 are rejected without conversion.
Save/upload/assembled state have a 256 MiB limit; ordinary incoming commands remain 16 KiB. Large terrain snapshots and joins use `stateChunk` messages, with at most 48 KiB of payload per chunk, one transfer ID/epoch/revision, declared total/count/lengths and SHA-256. The payload has `message` (the scalar snapshot/join header) and `packedTerrain`. A client checks shape, structure, integrity, exact profile and complete geometry before atomic reveal. Duplicate/conflicting/stale/corrupt transfers fail closed; out-of-order chunks are assembled by index. Missing chunks expire after 30 seconds. One assembly per socket and two server state writers bound transient transfers; live changes coalesce until the snapshot writer finishes, then resume from the last queued revision.

Packed terrain uses `terrain-packed-1`: one byte per row-major ground cell, an interned definition table, 43-byte little-endian occupant headers and three bytes per realized tree. All Int64 values remain exact. The [capacity report](../../doc/web-parity-map-capacity.md) specifies the binary layout, resource bounds, current tested scenes and the later full-city qualification. The logical in-process snapshot and delta contracts retain explicit cells, occupants, ownership and geometry. Source algorithms and content identities are unchanged.

P4-05 adds `mixed-railway-1` / `mixed-railway-content-1`, the exact bundled railway
handler profile and `mixed` snapshot/delta component. Save10 `spatial.entities`
contains railway owners while packed terrain retains terrain owners; full snapshots
combine both. The mixed component is a complete bounded replacement including
booking journal, not an implicit patch. Snapshot chunks retain railway header
entities while packing only terrain. See the [maintained contract and initial
admission limits](../../doc/web-parity-railway-persistence.md). Save7 is retired
under `historical/save-v7`; protocol5 peers are incompatible.

## P4-09 normal-track query and command

The additive protocol8 `RailwayQuery` / `RailwayPreview` schemas and
`editRailway` command support [the Track task](../../doc/web-parity-normal-rail-tool.md).
Preview uses `/api/railway/preview`, permits read-only Observer access and echoes
the exact epoch/geometry/content-fenced query. Commands carry that query, never a
price or graph; FIFO revalidation and participant receipts govern confirmation.

First construction may publish a complete same-epoch snapshot changing an exact
terrain profile to its installed mixed railway profile, retaining map, packs and
provenance. The client accepts this specific handoff without reconnecting; other
profile changes still bootstrap. No durable schema or save version changes.

P4-11 adds `SlopeQuery` / `SlopePreview`, read-only
`POST /api/railway/slope/preview` and FIFO `editSlope` for the distinct
[Slope task](../../doc/web-parity-slope-tool.md). Query inputs contain only
expected epoch/geometry/content, selected position, cardinal climb and Lay/Remove;
prices and candidate IDs are authoritative outputs. Legal previews carry all eight
unique rail/filler/support/wall members, four ordered transition positions, elected
piers and explicit pier losses. Removal resolves any member to its owning base.
The existing role/participant/receipt and terrain-to-mixed promotion rules apply.
Save10/protocol8 mixed state and chunks retain every invisible occupied cell.

P4-18 extends the current mixed schema with `kind: stationary`, exact stationary
bindings and an optional `construction` cell payload (stage 0–4, ground 0–6,
door flag). The existing clock state carries one bound one-shot per unfinished
cell, preserving order and the world-local construction random stream. Completion
replaces the site owner and charges the source price; cancellation removes its
pending actions. `RailwayQuery.infrastructure: stationary` carries a bounded,
opaque `stationaryType` ID and identical point endpoints. Removal resolves the
actual owner independently of that selected ID. Existing Save10/protocol8 documents
without sites retain their shape. See [the lifecycle and contribution ledger](../../doc/web-parity-stationary-tool.md).

P4-19 adds optional `mixed.speedLimits` with ordered `low`, `medium`, `fast`
XYZ arrays (8,192 entries each). Empty membership is omitted. Restore validates
TrafficVoxel existence, preserving duplicate order and cross-list state. All
infrastructure replacement paths prune absent Traffic only. The shared railway
query adds `infrastructure: speed`, `speed: low|medium|fast`, `speedSlope` and
cardinal `speedClimb`; slope input endpoints agree. Preview and WebSocket commit
share role/revision/content/input validation. No booking or train movement is
introduced. See [authoring and P5 handoff](../../doc/web-parity-speed-limits.md).


## P5-05 fleet boundary

Current Save10/protocol8 add `mixed.fleet` and its required `mixed.queue` as an
atomic replacement component in snapshots and deltas. Arrays preserve train/car,
group-member, controller and location-keyed harbor order. Absent members encode
fleet removals together with references and Traffic occupancy. Scalar queue changes
can occur without a geometry revision. The save queue must exactly match `clock`.
`RailComponent.controllerId` is reserved-null for every signal (TRAIN-D010).

[`fleet-v10.json`](saves/fleet-v10.json), [snapshot](fixtures/snapshot-fleet.json)
and [delta](fixtures/delta-fleet.json) are literal data-only witnesses. The fleet
fixture requires a trusted dispatcher supplied to `SaveCodec.ReadWorld`; the
P5-08 movement implementation is not activated here. Unplaced inventory needs
no dispatcher. Unknown definitions/providers/actions or malformed graphs reject
before replacement. [Full contract and checks](../../doc/web-parity-fleet-transport.md).
Save8 is retired under [historical/save-v8](historical/save-v8/README.md);
protocol6 peers are rejected. Exact content hashes remain unchanged.

Normal railway build queries optionally include `via`, an ordered array of up to
eight intermediate positions (10 nodes including `from` and `to`). The server
validates the continuous, coplanar route including bends at intermediate nodes,
and previews and commits it as one atomic construction edit. Other railway
infrastructure and removal queries do not accept nonempty `via`.


Canonical manual driving was removed on 7 November 2026. Snapshot/Delta no longer
include `fleetControl`, and the canonical fleet controller kinds are Simple,
Delegation and Timetable. Save10 and wire schemas reject Manual fleet records.
The older acquire/release/setController/controllerInput contracts above remain
for the explicit prototype regression harness only; canonical sessions return
`operationUnavailable`. See [the removal decision](../../doc/web-parity-fleet-manual.md).


## P6-05 station boundary

Save10/protocol8 add optional `mixed.stationGraph`, with a required `mixed.queue`
even when no fleet exists. The full graph replaces atomically with geometry,
spatial owners, fleet, accounts and callbacks. Explicit buildings, platforms,
yards, outlooks, passages, passenger counters, three transport logs and timers
preserve qualified definitions and all array order. `listeners` retains each
provider's local station order; `listenerMemberships` separately retains ordered
station-side provider IDs. STATION-D003 permits asymmetric edges; loaders never
rediscover or repair them. Provider references resolve actual spatial owners.

P6-16 adds optional `listeners[].provider`: data-only population expressions,
land value, XY/height scale, special classification and `input: "controlled"`.
Absent metadata remains unbound and is omitted by writers. Expressions have
depth16/node256 bounds and checked source arithmetic. Metadata edits require
the provider owner's revision. The source fifth-station asymmetry remains;
removal clears dangling local IDs without rediscovery. Save10/protocol8 and
catalog identities are unchanged. [P7 handoff and evidence](../../doc/web-parity-station-demand.md).

Station callbacks use intervals60/1440, retain registration cookies and advance
FIFO order; construction/train callbacks remain one-shot. The duplicated save
queue must exactly match `clock`. Version7 peers and Save9 documents are rejected.
The shared [snapshot](fixtures/snapshot-stations.json) and
[save](saves/stations-v10.json) are literal transport witnesses, not constructed
station gameplay. [Implementation, reproduction and scope](../../doc/web-parity-station-transport.md).

## Platform construction queries

`ThinPlatformQuery` and `WidePlatformQuery` carry expected epoch, geometry
revision, minute and content version, Place/Remove, position, cardinal direction,
length1–100 and target platform ID/revision. Thin adds `roof`; wide adds
`secondLane`, with lane0 mandatory at creation. Preview endpoints are
`/api/station/platform/preview` and `/api/station/wide-platform/preview`.
`editThinPlatform` / `editWidePlatform` commit the echoed query and exact
`expectedCost` through the existing authority/receipt boundary. Previews are
read-only and validate transition admission; clock or geometry changes invalidate
the quote. Wide placement positions are ordered center, lane0, optional lane1.
Removal resolves the full platform from a center/yard cell and retains normal
track. Both use current Save10/protocol8 state; host/service and subsequent lane
editing retain their later P6 slices. See [wide-platform evidence](../../doc/web-parity-wide-platforms.md)
and [slim-platform evidence](../../doc/web-parity-thin-platforms.md).


## Platform properties and logical connections

`POST /api/station/platform/properties` echoes a `PlatformPropertiesQuery` with
expected epoch, geometry revision, content version and platform ID/revision. Its
`PlatformProperties` response lists authoritative `PlatformHostCandidate` rows:
direct ID/revision/location/type and resolved station ID/revision/location.
This authenticated read-only operation adds no state or sound event.

`editPlatformProperties` commits that query, independent `name`, exact qualified
`bell`, nullable `hostId`, `expectedHostRevision` and `expectedStationRevision`.
Null host requires zero host/root revisions. Commit revalidates candidate membership
and all fences through the existing command role/epoch/receipt boundary, then
publishes name, host, child disconnects, bell and matching spatial revisions
atomically. No-ops preserve state; property edits preserve accounts, queues and
RNG. Dirty browser drafts retain the query/candidate revisions until Reset.
Save10/protocol8 already retain these fields. Bell preview is local; departure
events remain P6-20. See [implementation and verification](../../doc/web-parity-platform-properties.md).

Wide-platform `addLane` / `removeLane` actions use the same preview and command
boundary. For these actions `secondLane` selects lane1 (left); false selects lane0
(right), relative to the platform heading. The position, direction and length are
the existing center geometry; platform identity/revision must be current. Legal
lane previews contain exactly that lane's ordered positions and cost zero. Commit
revalidates the epoch, geometry, minute, content, platform revision and occupancy.
Save10/protocol8 remain unchanged; slim platforms reject lane actions.


## P6-20 committed departure effects — protocol9

`departureBell` is live only: world/live epochs, committed publication sequence,
effect ordinal, train/platform IDs, selected bell definition and source location.
Join snapshots, replacements and resync snapshots include `effectsBaseline`;
there is no effect history in snapshots, receipts or Save10. State precedes events.
Clients discard old epochs, events at/before the baseline and duplicate ordered
identities. Reconnect obtains a new baseline. Local gesture/sound/visibility/
viewport controls cannot affect simulation. See [implementation and verification](../../doc/web-parity-departure-bells.md).
