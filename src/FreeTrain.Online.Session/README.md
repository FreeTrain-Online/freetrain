# Session boundary

`SessionHost` privately creates and owns one `World`. Network adapters must submit
commands with a trusted `ParticipantContext`; commands do not select authority.
`Enqueue` is bounded (256 by default) and returns a task completed only after
`Pump` commits or rejects it. Unauthorized submissions cannot fill the queue.

The adapter calls `Pump(tickBudget, boundaryTime)`; there is no background timer or wall-clock
catch-up inside this library. All callers serialize through one lock. A pump
drains up to 64 FIFO commands before each automatic tick; `Pump(0)` also drains
one prefix while paused. Arrivals beyond that prefix wait for the next boundary.
A paused single-step is an explicit command that advances one tick, and therefore
can advance time even with a zero automatic tick budget. No pump advances more
than 256 ticks, including steps. Step while running is rejected. Completions run
asynchronously, outside simulation callback execution.

Commands validate epoch, participant and relevant entity/track revisions before
mutation. Successful commands receive an application sequence; rejected commands
leave world state and revisions unchanged. Participant-scoped request receipt
deduplication lives in NetworkSession. Do not automatically retry based only on a
request ID in this headless API.

`ReadState` returns immutable domain values including train bodies, held input,
clock/scheduler counters and pending value actions. `Snapshot` returns detached
wire DTO arrays, including complete track state; editing them cannot edit the
world. Events are returned per pump and are not retained in a growing log.
Per-host controller factories instantiate the explicitly registered strategies;
mutable input, random, clock, money and entity state belong to the world.

Trusted adapters register a `ParticipantContext` with owner/operator/observer role
and a connection generation. Enqueue and application validate the registered context;
replacing or disconnecting it fences queued commands. Observer commands cannot mutate
the world. Save mutations require owner authority; service checkpoint operations are
trusted internal calls.

Train leases live here, independently of human/agent implementation and of simulation
controllers. Acquiring a free train gives a five-second fenced lease. Controller/input
commands require its ID; accepted input renews it. Disconnect/release/expiry/load clear
held input, and a new lease resets input sequence ordering. Reset/expiry affect only the
relevant participant/train. Lease changes advance train/world revisions. Wall-time
maintenance runs at each pump, including `Pump(0)` while paused. Boundary time can be
injected for deterministic tests and is clamped against backwards movement. Simulation
also expires input after five ticks, before due events.

Future agents use the same contexts, commands, reads and leases. No decision engine or
provider is part of this library.

The default canonical `ReferenceWorld` uses the same bounded FIFO and snapshot /
spatial query boundary. Run/Pause/Step advance its world-local source-minute clock;
other gameplay commands return `operationUnavailable`. Economy/train behavior is pending.
Capture writes canonical Save 7, including the entity/voxel graph, allocator, clock and random streams. Load
accepts only that current format and validates a complete replacement before
changing the epoch; later commands/loads carrying the old epoch are fenced.
Earlier save formats are rejected without mutation or conversion. Explicit
prototype regression hosts cannot write saves.
See [P1-01c](../../doc/web-parity-reference-world.md), [P1-02](../../doc/web-parity-clock.md) and [P1-04](../../doc/web-parity-spatial-contracts.md).

Terrain hosts share this canonical FIFO and clock boundary. Save 7/protocol 5 carry
actual ground/occupants, realized forests, authoring mode, provenance and revision/
allocator continuation, with exact trusted profile resolution. Replacement snapshots
and edited cell/occupant/removal deltas preserve epoch and stale-request fencing.
The terrain adapter hook does not expose a gameplay editing command.
See [P3-04 scope and limits](../../doc/web-parity-terrain-transport.md).
