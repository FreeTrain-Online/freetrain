# P3-04 saves and transport — 28 October 2026

P3-04 integrates actual canonical terrain with **Save 6 and protocol 4**. A terrain
world can be saved, loaded through the authority FIFO, published to independent
clients and restored from a checkpoint after restart. Invalid data or unavailable
pack identities preserve the current world/epoch and rejected files. **Next: P3-05
rendering and picking.** The default application still uses its spatial reference
world; browser terrain rendering/creation, preset capacity and overall P3 acceptance
remain open.

## Delivered contract

[TerrainSessionState](../src/FreeTrain.Online.Simulation/TerrainSessionState.cs)
composes immutable terrain with source-minute clock/scheduler, paused state,
versioned world-owned random streams, an explicit persisted construction-mode flag
and creation provenance. Provenance retains the validated original Empty/Heightmap/
Generated request, generation profile and canonical request hash. It contains image
identity/metadata where applicable, never input pixels or machine paths. Loading
uses actual ground/corners/water/occupants and realized tree positions/sprite indices;
it never regenerates a map or consults an input image. Geometry revision and total
session revision are distinct so clock advances do not masquerade as terrain edits.
The mode flag grants no legality or economy bypass; its user controls remain P3-16.

[TerrainWire](../src/FreeTrain.Online.Persistence/TerrainWire.cs) supplies explicit
portable DTOs and complete restoration through P3-03's graph/continuity validator.
Names and dimensions, row-major H/V ground/surface cells, global water, Mountain
quarters, forests, Terrace backups, Creek masks, stable entity identities/revisions,
footprints, ownership, reusability, values and next allocator are all persisted.
Only the exact bundled terrain profile has trusted runtime dispatch. Unknown,
missing, extra, duplicate, version/hash/case-mismatched packs have no fallback;
optional terrain handlers are not registered by inventory or by browser input.

[SaveCodec](../src/FreeTrain.Online.Persistence/SaveCodec.cs) supports the reference
and terrain profiles in [Save 6](../prototype/protocol/save-v6.schema.json).
Saves 1–5 are rejected without conversion under the owner's current-format policy.
The retired Save 5 schema/fixture remain under
[historical/save-v5](../prototype/protocol/historical/save-v5/README.md).
Duplicate/unknown JSON properties, numeric/noncanonical Int64 values, malformed
state, metadata and invalid clock/random state fail before replacement.
`SaveCodec.Digest` hashes the canonical UTF-8 actual-state save encoding, including
mode/provenance/clock/random; it is not a hash of a request alone. Save input property
order/whitespace do not affect the digest after validated restoration. No credentials,
control leases, participants, epochs, UI drafts or input image bytes enter saves.

[SessionHost](../src/FreeTrain.Online.Session/SessionHost.cs) queues captures,
loads and its trusted terrain-adapter hook on the same bounded FIFO as commands.
Load rechecks Owner/epoch, validates completely and then advances the epoch once.
Queued commands for the old epoch fail. Terrain Run/Pause/Step use the source clock;
prototype gameplay commands remain unavailable. Read-only spatial geometry queries
use the selected validated H/V profile. The adapter hook additionally fences
participant connection, world revision and allocator/entity lineage, and checks the
candidate's encoded save limit before publication. It exposes no new editing endpoint
or authoring permission; gameplay legality/booking stays with P3-13–16.

[Protocol DTOs](../src/FreeTrain.Online.Contracts/WireContracts.cs) and
[shared schema](../prototype/protocol/schema.json) add optional profile-specific
terrain payloads. Terrain snapshots contain the complete grid and occupancy graph,
with zero rails legal. Deltas contain changed cell indices, upserted occupants,
water/mode/geometry revision/allocator, and shared entity removals that also remove
the corresponding terrain payload. Name, dimensions, provenance and profile changes
require a full epoch snapshot. The network publishes replacement snapshots to every
peer and reconnects start from a complete snapshot. Missing dependent output closes
that peer instead of silently dropping a delta.

[Client validation](../web/src/lib/terrain-state.ts) checks the exact generated
profile, dimensions, water, shared corners, forest definitions/sprites, Terrace
backing, transparent occupancy, values and matching entity/payload graph before
accepting a snapshot or merged delta. [Delta application](../web/src/lib/protocol.ts)
keeps immutable old reads, rejects stale/out-of-order bases, duplicate or out-of-range
cell changes, allocator rewind/reused IDs and unversioned entity/forest changes.
Coalesced revision advances are allowed. Shared readable fixtures bind the .NET
codec and client independently. The compact terrain handler metadata is generated
from the checked-in prepared catalog/hash by `deno task generate:contracts`.

The browser currently advertises only the reference/prototype rendering profiles.
A terrain replacement is therefore transport-valid but blocks the existing live
renderer at its identity guard; a terrain checkpoint produces HTTP 426 for that
browser's bootstrap capabilities. P3-05/06 must add rendering/readiness and then
advertise terrain. No flat fallback or premature playable terrain claim is intended.

## Bounds and measured limits

Save input/output, storage, HTTP upload and outgoing state frames are limited to
**16 MiB**; the client checks the matching UTF-8 message/upload bound and localized
EN/JA limit feedback. Ordinary incoming command frames remain **16 KiB**. Existing
queue-count, peer-count, slow-consumer disconnect and checkpoint writer/atomic rename
bounds remain in force. A candidate over the actual save cap fails before publication;
an oversized file is rejected before JSON parsing. These are limits, not guarantees
that every dimension/decoration combination fits or that all peers fit in memory.

[TerrainTransportProbe](../tools/TerrainTransportProbe/Program.cs) constructs real
P3-03 graphs with two occupants per cell (Terrace plus Mountain), six realized trees,
full exact definition bindings and actual production serialization/restoration.
[Raw measurements](parity/terrain-transport-budget.json) record the environment and
allocation churn. This is an intentionally dense serialization stress fixture,
not a generated legal-city/browser capacity result.

| Grid / Z32 | Entities | Snapshot bytes | Save bytes | P3-02 transfer reservation |
| --- | ---: | ---: | ---: | ---: |
| 30² | 1,800 | 1,082,846 | 1,089,198 | 526,336 |
| 64² | 8,192 | 4,934,026 | 4,940,378 | 2,162,688 |
| 128² | 32,768 | 19,817,201 | Rejected: `saveTooLarge` | 8,454,144 |

The real shape exceeds P3-02's provisional 512-byte/cell transfer allowance.
Construction, serialization and successful restoration allocate about 45 MB/171 MB
in aggregate for 30²/64². Aggregate allocation is churn, **not peak RSS or concurrent
working memory**. All measured dense cases exceed the provisional allocation
allowance; the 128² case does not restore because its save is rejected. Initial
64²/128² presets and creation readiness remain disabled. Before enabling them,
P3-06/17 must rework/measure actual representation, allocations, retained/peak
memory, worker/decoder copies, network queues and browser rendering. Keep the
P3-02 synthetic probe separate from these observations.

## Verification and handoff

[TerrainPersistenceTests](../tests/FreeTrain.Online.Tests/TerrainPersistenceTests.cs)
cover actual edited round trips/digest/random and clock continuation, empty worlds,
heightmap provenance without input images, stable allocator continuation, cell/
forest/Creek upserts and removals, invalid graph/pack/mode/provenance failures,
Owner/epoch/revision fencing, two independent transport peers, real HTTP uploads/
validation and WebSocket replacement, checkpoint restart and rejected startup-file
preservation. [Client tests](../web/tests/terrain.test.ts) use the shared server
fixtures and corrupt their graph/metadata/deltas before reveal.

Reproduce from the repository root:

```sh
dotnet build FreeTrain.Online.slnx -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test FreeTrain.Online.slnx -c Release --no-build --no-restore -m:1 -nr:false
python3 scripts/check-project-boundaries.py
python3 scripts/review-terrain.py --check
python3 -m unittest discover -s scripts/tests
cd web
deno task build
deno task test
deno run -A npm:@playwright/test@1.64.1 test
```

The .NET runner, HTTP/WebSocket tests and Chromium require local socket/process
access. The probe's first run needs restore; then build with the same single-process
flags and run `dotnet tools/TerrainTransportProbe/bin/Release/net10.0/TerrainTransportProbe.dll`.
`--write-fixtures /path/to/repo` explicitly refreshes the terrain fixtures from the
production DTOs. Test assertions also bind the fixture values to the actual state.

Validation on 28 October: Release solution/probe builds pass with zero warnings/errors;
**414 .NET, 108 client and 141 Python cases pass**. Generated contracts/terrain profile,
prepared content, locale/type checks, offline and explicit-archive P3-01 checks,
project boundaries, new/changed tracked documentation links and whitespace checks pass.
Chromium covers **18 canonical + 54 historical cases**: the full run passed 71 and
exposed one obsolete 1 MiB oversized-upload test; correcting its fixture/expectation
to 16 MiB and rerunning all four persistence cases passes. The earlier standalone
canonical run also passed all 18. Both synthetic optional-content configurations
pass **3 source + 3 prepared-directory cases**. These browser checks exercise existing
reference/prototype/gallery/optional workflows, not terrain rendering.

Logs are under `tmp/p3-04-{build,dotnet,client,python,web-build,web-check}.log`,
`tmp/p3-04-browser-final.log`, `tmp/p3-04-browser-persistence.log` and
`tmp/p3-04-browser-optional-{source,prepared}.log`. The first sandboxed .NET attempt
could not bind its runner socket; the successful suites used local socket access.
The probe uses single-process build flags because the sandboxed default multi-node
restore/build stalled; the replacement build and measurement completed. There was
no deployment/container rebuild in this slice.
No native/device, original-runtime capture or new browser terrain gameplay
acceptance is claimed. Existing P2 crop/optional/native deferrals remain open;
P3-05/06 own rendering/reveal and playable creation, with further save/transport
coverage required in every subsequent terrain slice.
