# Wire protocol 3

`schema.json` is the canonical draft-07 schema for current wire messages and spatial queries. `$defs` are referenced explicitly; custom formats `int64` and `uint63`
mean canonical decimal strings within signed Int64 or nonnegative Int64 bounds.
Validators must register these formats (see `web/tests/contracts.test.mjs`).
Strings forbid leading zeros, plus signs, whitespace, and negative zero. World
coordinates and counts are bounded JSON integers; money, ticks, epochs,
revisions, input order, and event order are decimal strings. Consumers compare
those using `BigInt` or `long`, never lexicographic or floating-point ordering.

Versions are independent: protocol **3**, default content `reference-spatial-content-1`, canonical **Save 3**. Online prototype Save 1 is retired. The explicit historical regression harness still identifies `prototype-content-1`.
Join conveys server incarnation, participant/session identity, server-assigned role,
epoch and initial snapshot. Commands cannot choose participant identity or authority.

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
`incompatibleSave`. Canonical Save 3 uses `save-v3.schema.json` and `saves/reference-v3.json`; the codec additionally validates topology, clock ranges and bound scheduler actions. Save 2 upgrades to the source start time, paused. Prototype writes remain unavailable. See the
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

The default world identity is `reference-spatial-2` / `reference-spatial-content-1`
with the exact required-pack descriptor in `canonical-profile.json`. Join, snapshots
and deltas carry this identity. The map uses `hv` H/V storage dimensions and track
shapes `normal0`..`normal11`, matching source `RailPattern.singlePatterns` indices.
The asset-free reference pack is not the complete game catalog. Tick is absolute source minutes (initially 130080); revision advances for time and
pause changes. Cash stays zero and there are no game entities yet. The world starts
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

`POST /api/spatial/preview?protocolVersion=3&contentVersion=reference-spatial-content-1`
accepts a `SpatialQuery` and returns a `SpatialPreview` (both defined in the schema).
Any authenticated role can use it. Expected epoch, position, nullable anchor and
snap 0/4/8 select a checked read-only calculation. The result carries world
identity/revision, snapped position, bounds, projection, optional storage,
heading and distance. It never reserves cells, changes state or approves a later
construction commit. Invalid/overflow input is 400; stale epoch is 409.
