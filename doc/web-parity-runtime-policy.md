# Runtime save policy — updated 12 November 2026

**Owner direction:** Online prototype Save 1 is unsupported. Do not import,
convert, continue or write it, or relabel its payload as a new format. Historical
P0 compatibility requirements are superseded. Desktop ExAV imports remain P10 work.
**Owner direction, current-format iteration:** support only the current canonical
save format while rapidly implementing slices without a save-game corpus.
Saves 2/3/4/5/6/7/8/9/10 are unsupported; do not parse, upgrade or convert them. Preserve version
checks and rejected-file/current-world protection. Add migrations when saves need
to be retained. The [decision record](parity/runtime-decisions.json) preserves these policies.

The [canonical reference world](web-parity-reference-world.md) is the default
runtime. [P1-02](web-parity-clock.md) adds the source-minute clock/calendar/scheduler
and [P1-03](web-parity-random.md) adds versioned random streams.
[P1-04](web-parity-spatial-contracts.md) added stable entities/footprints.
[P3-04](web-parity-terrain-transport.md) adds actual terrain, realized vegetation,
persisted authoring mode/provenance.
[P3-16a capacity](web-parity-map-capacity.md) packs the actual state and introduces **Save 7**.
[P5-05 fleet transport](web-parity-fleet-transport.md) advances the current contract to **Save9/protocol7**. P6-05 subsequently advances the current contract. Full gameplay and complete-world persistence remain with P2–P10.
[P5-06 trading](web-parity-fleet-trading.md) adds optional zero-default transaction
revenue to both current schemas and decoders; existing expenditure-only entries
and format/protocol numbers remain unchanged.

[P5-19 basic TAT](web-parity-basic-tat.md) adds optional location-keyed junction
settings to Save9/protocol7. An absent array means empty; writers omit empty
arrays, preserving existing no-junction payloads. Format/protocol numbers remain unchanged.

[P6-05 station transport](web-parity-station-transport.md) advances the current contract to **Save10/protocol8**. This was the current format at P6-05. Exact station/platform hosts, listener edges, logs and repeated timers are durable; [P6-06 construction](web-parity-station-construction.md) adds immediate station-building preview/commit and removal using this state. Presentation, controls and passenger slices remain open.

[P6-16 demand providers](web-parity-station-demand.md) adds optional data-only
`listeners[].provider` metadata to Save10/protocol8. Absent inputs stay unbound;
writers omit them. Expression shape/depth/node limits and owner revisions are
validated before publication. Source asymmetric edges remain durable; station
removal clears dangling local references without rebuilding catchment.

[P7-05 city transport](web-parity-city-transport.md) advances the current contract
to **Save11/protocol10**, with City format1, typed producer provenance, both listener
directions, heat/search/settings/cache state and exact callback/random continuation.
Only Save11 is read and written. Save10 files/schema are retained solely as
rejection fixtures under `historical/save-v10`; no conversion is installed.

## Current behavior

- Prototype, unknown-version and incompatible identity inputs return `incompatibleSave`.
  Invalid structure/topology/JSON returns `invalidSave`; inputs over 256 MiB return
  `saveTooLarge`. Validation completes before world replacement. Failure preserves
  the world, epoch and queued work.
- Startup reports rejected checkpoints in the log and `/api/saves/status`, then
  discards the transient checkpoint and starts a fresh canonical world. Automatic,
  explicit and shutdown checkpoint writes remain enabled. Named slots and rejected
  uploads are preserved. If the checkpoint cannot be deleted, startup fails rather
  than continuing with stale recovery data.
- Valid Save11 uploads are checked before the browser offers replacement confirmation.
  Confirmed load joins the authority FIFO and rechecks Owner role and expected
  epoch. Success advances the epoch and publishes a full coherent snapshot (bounded chunks for large state); queued requests
  for the old epoch become stale. Earlier formats return `incompatibleSave`; no upgrade parser is retained.
- Save slot/download, automatic checkpoint and shutdown checkpoint writes now use
  the canonical codec. The explicit historical regression harness cannot write
  saves or accept a canonical replacement into a prototype host.

The wire uses protocol10. Default identity and exact pack hash are in
[`canonical-profile.json`](../prototype/protocol/canonical-profile.json).
The [Save11 schema](../prototype/protocol/save-v11.schema.json) is structural;
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


[P6-20 departure events](web-parity-departure-bells.md) advances only the wire to
**protocol9**. Save10 remains unchanged. Fresh live epochs and subscription
baselines fence committed bell events across retries, reconnect and replacement;
no save, snapshot or receipt replays effect history. Matching static/server
artifacts are required; earlier wire versions receive HTTP426.
