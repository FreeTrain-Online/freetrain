# Runtime save policy — 22 October 2026

**Owner direction:** Online prototype Save 1 is unsupported. Do not import,
convert, continue or write it, or relabel its payload as a new format. Historical
P0 compatibility requirements are superseded. Desktop ExAV imports remain P10 work.
The [decision record](parity/runtime-decisions.json) preserves this policy.

The [canonical reference world](web-parity-reference-world.md) is the default
runtime. [P1-02](web-parity-clock.md) adds the source-minute clock/calendar/scheduler
and **Save 3**. Canonical Save 2 upgrades explicitly at source start time, paused;
its source-valid H/V rail graph is preserved. Full gameplay, random state and
entity/footprint persistence remain with P1–P10.

## Current behavior

- Prototype, unknown-version and incompatible identity inputs return `incompatibleSave`.
  Invalid structure/topology/JSON returns `invalidSave`; inputs over 1 MiB return
  `saveTooLarge`. Validation completes before world replacement. Failure preserves
  the world, epoch and queued work.
- Startup reports rejected checkpoints in the log and `/api/saves/status`, then
  starts a fresh canonical world. Rejected files are never rewritten: automatic
  and shutdown checkpoint attempts are disabled for that process, and explicit
  checkpoint writes fail. Named slots and downloads can save the fresh world.
- Valid Save 2/3 uploads are checked before the browser offers replacement confirmation.
  Confirmed load joins the authority FIFO and rechecks Owner role and expected
  epoch. Success advances the epoch and publishes a full snapshot; queued requests
  for the old epoch become stale. Only the explicit canonical Save 2 upgrade is supported; no prototype state is involved.
- Save slot/download, automatic checkpoint and shutdown checkpoint writes now use
  the canonical codec. The explicit historical regression harness cannot write
  saves or accept a canonical replacement into a prototype host.

The wire remains protocol 3. Default identity and exact pack hash are in
[`canonical-profile.json`](../prototype/protocol/canonical-profile.json).
The [Save 3 schema](../prototype/protocol/save-v3.schema.json) is structural;
server and browser additionally verify the complete loop and source rail ports.

The retired prototype codec/DTOs are under
[`attic/online-prototype-save-v1`](../attic/online-prototype-save-v1/README.md).
Its inputs remain under
[`historical/save-v1`](../prototype/protocol/historical/save-v1/README.md).
Historical evidence remains labeled; it does not establish canonical continuation.

Verification and remaining acceptance limits are recorded with [P1-01c](web-parity-reference-world.md) and [P1-02](web-parity-clock.md).
