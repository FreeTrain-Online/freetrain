# Initial protocol and save contracts

`schema.json` is the canonical draft-07 schema for initial wire messages and
save v1. `$defs` are referenced explicitly; custom formats `int64` and `uint63`
mean canonical decimal strings within signed Int64 or nonnegative Int64 bounds.
Validators must register these formats (see `web/tests/contracts.test.mjs`).
Strings forbid leading zeros, plus signs, whitespace, and negative zero. World
coordinates and counts are bounded JSON integers; money, ticks, epochs,
revisions, input order, and event order are decimal strings. Consumers compare
those using `BigInt` or `long`, never lexicographic or floating-point ordering.

Versions are independent: protocol **2**, content `prototype-content-1`, save `1`.
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

Protocol 1 is rejected with HTTP 426; deploy matching static and server artifacts.

The versioned save contains explicit tracks, complete train/controller/body/stop
state, station dwell state, buildings, clock, cash, deterministic random state and
ordered scheduled events. It supports validation and continuation. It excludes
credentials, connections, control leases, held input, UI, delegates and assembly names.

Run from the repo root:

```sh
deno task --cwd web generate:contracts
deno task --cwd web test
dotnet test FreeTrain.slnx -c Release
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
