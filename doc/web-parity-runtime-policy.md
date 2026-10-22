# Runtime save policy — 22 October 2026

**Owner direction supersedes the earlier prototype compatibility decision:**
Online prototype Save 1 is unsupported. Do not implement import, conversion,
continuation or a writer for that format. Do not relabel its payload as a new
format. The source-only P0 specifications and earlier delivery records retain
their provenance; their Save 1 compatibility requirement is no longer current.
The [machine-readable decision](parity/runtime-decisions.json) records the override.
Historical desktop ExAV import requirements remain separate P10 work.

The retired codec, DTOs and continuation/recovery tests are archived under
[`attic/online-prototype-save-v1`](../attic/online-prototype-save-v1/README.md).
The old reference file is under
[`prototype/protocol/historical/save-v1`](../prototype/protocol/historical/save-v1/README.md).
They are excluded from active application/contract generation. The old game
runtime temporarily remains, identified as an obsolete reference world; this
does not provide save compatibility.

## Current behavior

- Uploading or loading a well-formed unsupported version returns `incompatibleSave`.
  This includes actual Save 1 files, unknown versions and a prototype payload
  merely relabeled as version 2. The current world, epoch, leases and queued work
  are preserved. Invalid JSON/headers return `invalidSave`; the existing 1 MiB
  input limit remains enforced.
- Startup catches checkpoint errors, reports the reason in the server log and
  `/api/saves/status`, and starts a fresh reference world. It does not delete,
  rewrite or overwrite the rejected file. The save panel displays the error in
  English/Japanese.
- No canonical-world save codec is implemented yet. Save/download controls are
  disabled with an explanation; direct write requests return `savingUnavailable`.
  Automatic and shutdown checkpoint writes are disabled. Selecting an unsupported
  input reports its error without a replacement confirmation or world mutation.

The wire remains protocol 3; current live geometry/profile remains the obsolete
reference world. The save model is removed from the current schema and generated
TypeScript. File storage remains a bounded opaque-byte utility, with no prototype
serialization rules. Old process/deployment recovery checks are retired or updated
to require rejection/fresh startup rather than prototype continuation.

## Next geometry slice

Updating the live reference world now is the appropriate next vertical slice,
before adding more systems to the obsolete runtime. Rebuild a small, independent
source-valid H/V scenario: source rail patterns, eight headings, canonical bounds,
stable identities, actual server/client reads, picking and targeting. The existing
loop's cardinal right-angle curves, three-way junction and buffer have no direct
canonical rail-pattern conversion. A source-valid eight-direction loop is a better
fixture than transforming those old shapes.

Introduce the new world's explicit identity and a genuinely new save schema with
its own validation and round-trip tests. Keep unsupported operations unavailable
until their source behavior is implemented; do not carry prototype clock, cost or
train rules into the canonical runtime by renaming them. Source clock/scheduler,
random streams and full train/content behavior retain their P1–P6 owners.

The canonical reference-world change has **not** been implemented by this policy
update. P1 remains open, and prototype migration is excluded rather than pending.

## Verification boundary

The active tests now cover rejection, disabled writing, actual retired input,
startup checkpoint preservation, roles/epochs and English/Japanese feedback.
Verification passed: 147 .NET tests, 74 client tests, 105 Python tests and 54
browser tests, plus the production client build and project-boundary check.
The .NET total excludes the archived prototype continuation suite. Current
bilingual save feedback is recorded in the
[presentation review](parity/save-policy-translation-review.json).
Old Save 1 positive-continuation tests are historical evidence only. Browser epoch
replacement/modal and sound tests use explicitly labeled transport/UI fixtures
where they previously used Save 1 to reset a world; they do not establish an
implemented canonical-world replacement or persistence path. Existing .NET tests
still exercise real station-arrival sound generation.

The Docker deployment check has been updated for rejection and fresh in-memory
restart, but was not executed in this update. Physical/native UX acceptance remains
open independently of these changes.
