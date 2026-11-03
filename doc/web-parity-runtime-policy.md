# Runtime save policy — updated 3 November 2026

**Owner direction:** Online prototype Save 1 is unsupported. Do not import,
convert, continue or write it, or relabel its payload as a new format. Historical
P0 compatibility requirements are superseded. Desktop ExAV imports remain P10 work.
**Owner direction, current-format iteration:** support only the current canonical
save format while rapidly implementing slices without a save-game corpus.
Saves 2/3/4/5/6/7/8 are unsupported; do not parse, upgrade or convert them. Preserve version
checks and rejected-file/current-world protection. Add migrations when saves need
to be retained. The [decision record](parity/runtime-decisions.json) preserves these policies.

The [canonical reference world](web-parity-reference-world.md) is the default
runtime. [P1-02](web-parity-clock.md) adds the source-minute clock/calendar/scheduler
and [P1-03](web-parity-random.md) adds versioned random streams.
[P1-04](web-parity-spatial-contracts.md) added stable entities/footprints.
[P3-04](web-parity-terrain-transport.md) adds actual terrain, realized vegetation,
persisted authoring mode/provenance.
[P3-16a capacity](web-parity-map-capacity.md) packs the actual state and introduces **Save 7**.
[P5-05 fleet transport](web-parity-fleet-transport.md) advances the current contract to **Save9/protocol7**. Only Save9 is read and written. Full gameplay and complete-world persistence remain with P2–P10.
[P5-06 trading](web-parity-fleet-trading.md) adds optional zero-default transaction
revenue to both current schemas and decoders; existing expenditure-only entries
and format/protocol numbers remain unchanged.

## Current behavior

- Prototype, unknown-version and incompatible identity inputs return `incompatibleSave`.
  Invalid structure/topology/JSON returns `invalidSave`; inputs over 256 MiB return
  `saveTooLarge`. Validation completes before world replacement. Failure preserves
  the world, epoch and queued work.
- Startup reports rejected checkpoints in the log and `/api/saves/status`, then
  starts a fresh canonical world. Rejected files are never rewritten: automatic
  and shutdown checkpoint attempts are disabled for that process, and explicit
  checkpoint writes fail. Named slots and downloads can save the fresh world.
- Valid Save9 uploads are checked before the browser offers replacement confirmation.
  Confirmed load joins the authority FIFO and rechecks Owner role and expected
  epoch. Success advances the epoch and publishes a full coherent snapshot (bounded chunks for large state); queued requests
  for the old epoch become stale. Earlier formats return `incompatibleSave`; no upgrade parser is retained.
- Save slot/download, automatic checkpoint and shutdown checkpoint writes now use
  the canonical codec. The explicit historical regression harness cannot write
  saves or accept a canonical replacement into a prototype host.

The wire uses protocol7. Default identity and exact pack hash are in
[`canonical-profile.json`](../prototype/protocol/canonical-profile.json).
The [Save9 schema](../prototype/protocol/save-v9.schema.json) is structural;
the server additionally verifies the exact selected profile: the reference loop and rail ports,
or the complete terrain grid/corners/occupancy/forest/terrace/creek graph and provenance,
plus the complete stock/controller/group/occupancy graph, location-keyed harbor data, allocator, definitions and explicit callback queue. Deployed fleets require an installed trusted dispatcher; saves carry no executable code. Unknown random algorithms/versions are incompatible; malformed, missing,
duplicate or unknown random streams are invalid. Rejected input preserves the world and files.

The retired prototype codec/DTOs are under
[`attic/online-prototype-save-v1`](../attic/online-prototype-save-v1/README.md).
Its inputs remain under
[`historical/save-v1`](../prototype/protocol/historical/save-v1/README.md).
Historical evidence remains labeled; it does not establish canonical continuation.

Verification and remaining acceptance limits are recorded with [P1-01c](web-parity-reference-world.md), [P1-02](web-parity-clock.md), [P1-03](web-parity-random.md) and [P1-04](web-parity-spatial-contracts.md) and [P3-04](web-parity-terrain-transport.md).
