# Wire protocol 6

`schema.json` is the canonical draft-07 schema for current wire messages and spatial queries. `$defs` are referenced explicitly; custom formats `int64` and `uint63`
mean canonical decimal strings within signed Int64 or nonnegative Int64 bounds.
Validators must register these formats (see `web/tests/contracts.test.mjs`).
Strings forbid leading zeros, plus signs, whitespace, and negative zero. World
coordinates and counts are bounded JSON integers; money, ticks, epochs,
revisions, input order, and event order are decimal strings. Consumers compare
those using `BigInt` or `long`, never lexicographic or floating-point ordering.

Versions are independent: protocol **6**, default content `reference-spatial-content-2`, canonical **Save 8**. Online prototype Save 1 is retired. The explicit historical regression harness still identifies `prototype-content-1`.
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
`incompatibleSave`. Canonical Save 8 uses `save-v8.schema.json` and `saves/reference-v8.json`; the codec additionally validates topology, clock ranges, bound scheduler actions and the complete versioned random bank. Only the current format is supported during parity iteration; Saves 1/2/3/4/5/6/7 return `incompatibleSave` without conversion. Retired schemas and rejection fixtures live under `historical/save-v1`, `historical/save-v2` `historical/save-v3` and `historical/save-v4`. Prototype writes remain unavailable. See the
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

`POST /api/spatial/preview?protocolVersion=6&contentVersion=reference-spatial-content-2`
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

Save 8 also stores `spatial.nextEntityId`; removed IDs are never allocated again.
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

Save 8 supports reference, terrain or mixed railway profiles; Saves 1–7 are rejected without conversion.
Save/upload/assembled state have a 256 MiB limit; ordinary incoming commands remain 16 KiB. Large terrain snapshots and joins use `stateChunk` messages, with at most 48 KiB of payload per chunk, one transfer ID/epoch/revision, declared total/count/lengths and SHA-256. The payload has `message` (the scalar snapshot/join header) and `packedTerrain`. A client checks shape, structure, integrity, exact profile and complete geometry before atomic reveal. Duplicate/conflicting/stale/corrupt transfers fail closed; out-of-order chunks are assembled by index. Missing chunks expire after 30 seconds. One assembly per socket and two server state writers bound transient transfers; live changes coalesce until the snapshot writer finishes, then resume from the last queued revision.

Packed terrain uses `terrain-packed-1`: one byte per row-major ground cell, an interned definition table, 43-byte little-endian occupant headers and three bytes per realized tree. All Int64 values remain exact. The [capacity report](../../doc/web-parity-map-capacity.md) specifies the binary layout, resource bounds, current tested scenes and the later full-city qualification. The logical in-process snapshot and delta contracts retain explicit cells, occupants, ownership and geometry. Source algorithms and content identities are unchanged.

P4-05 adds `mixed-railway-1` / `mixed-railway-content-1`, the exact bundled railway
handler profile and `mixed` snapshot/delta component. Save8 `spatial.entities`
contains railway owners while packed terrain retains terrain owners; full snapshots
combine both. The mixed component is a complete bounded replacement including
booking journal, not an implicit patch. Snapshot chunks retain railway header
entities while packing only terrain. See the [maintained contract and initial
admission limits](../../doc/web-parity-railway-persistence.md). Save7 is retired
under `historical/save-v7`; protocol5 peers are incompatible.

## P4-09 normal-track query and command

The additive protocol6 `RailwayQuery` / `RailwayPreview` schemas and
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
Save8/protocol6 mixed state and chunks retain every invisible occupied cell.

P4-18 extends the current mixed schema with `kind: stationary`, exact stationary
bindings and an optional `construction` cell payload (stage 0–4, ground 0–6,
door flag). The existing clock state carries one bound one-shot per unfinished
cell, preserving order and the world-local construction random stream. Completion
replaces the site owner and charges the source price; cancellation removes its
pending actions. `RailwayQuery.infrastructure: stationary` carries a bounded,
opaque `stationaryType` ID and identical point endpoints. Removal resolves the
actual owner independently of that selected ID. Existing Save8/protocol6 documents
without sites retain their shape. See [the lifecycle and contribution ledger](../../doc/web-parity-stationary-tool.md).
