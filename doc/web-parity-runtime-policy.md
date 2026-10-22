# Runtime save policy — 22 October 2026

**Owner direction:** Online prototype Save 1 is unsupported. Do not import,
convert, continue or write it, or relabel its payload as a new format. Historical
P0 compatibility requirements are superseded. Desktop ExAV imports remain P10 work.
**Owner direction, current-format iteration:** support only the current canonical
save format while rapidly implementing slices without a save-game corpus.
Saves 2/3 are unsupported; do not parse, upgrade or convert them. Preserve version
checks and rejected-file/current-world protection. Add migrations when saves need
to be retained. The [decision record](parity/runtime-decisions.json) preserves these policies.

The [canonical reference world](web-parity-reference-world.md) is the default
runtime. [P1-02](web-parity-clock.md) adds the source-minute clock/calendar/scheduler
and [P1-03](web-parity-random.md) adds versioned random streams and **Save 4**.
Only Save 4 is read and written. Full gameplay and entity/footprint persistence
remain with P1–P10.

## Current behavior

- Prototype, unknown-version and incompatible identity inputs return `incompatibleSave`.
  Invalid structure/topology/JSON returns `invalidSave`; inputs over 1 MiB return
  `saveTooLarge`. Validation completes before world replacement. Failure preserves
  the world, epoch and queued work.
- Startup reports rejected checkpoints in the log and `/api/saves/status`, then
  starts a fresh canonical world. Rejected files are never rewritten: automatic
  and shutdown checkpoint attempts are disabled for that process, and explicit
  checkpoint writes fail. Named slots and downloads can save the fresh world.
- Valid Save 4 uploads are checked before the browser offers replacement confirmation.
  Confirmed load joins the authority FIFO and rechecks Owner role and expected
  epoch. Success advances the epoch and publishes a full snapshot; queued requests
  for the old epoch become stale. Earlier formats return `incompatibleSave`; no upgrade parser is retained.
- Save slot/download, automatic checkpoint and shutdown checkpoint writes now use
  the canonical codec. The explicit historical regression harness cannot write
  saves or accept a canonical replacement into a prototype host.

The wire remains protocol 3. Default identity and exact pack hash are in
[`canonical-profile.json`](../prototype/protocol/canonical-profile.json).
The [Save 4 schema](../prototype/protocol/save-v4.schema.json) is structural;
the server additionally verifies the complete loop, source rail ports and clock
ranges. Unknown random algorithms/versions are incompatible; malformed, missing,
duplicate or unknown random streams are invalid. Rejected input preserves the world and files.

The retired prototype codec/DTOs are under
[`attic/online-prototype-save-v1`](../attic/online-prototype-save-v1/README.md).
Its inputs remain under
[`historical/save-v1`](../prototype/protocol/historical/save-v1/README.md).
Historical evidence remains labeled; it does not establish canonical continuation.

Verification and remaining acceptance limits are recorded with [P1-01c](web-parity-reference-world.md), [P1-02](web-parity-clock.md) and [P1-03](web-parity-random.md).
