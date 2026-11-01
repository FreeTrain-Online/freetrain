# P3-13 — corner terrain editing

30 October 2026. This slice implements the ordinary MountainController. Mountain2
rectangle editing/repair, terrace/cut operations and map authoring mode remain
P3-14–16. Overall P3 and final capacity acceptance remain open.

## Initial work reviewed

The inherited working tree contained only `CornerTerrainSelection.cs` and seven
selection tests. The helper matches the pinned slope-dependent threshold,
x<8/x>=24 boundaries and C# integer division. It was unconnected to the map,
commands, preview, authority and persistence. Those two files are retained.

## Source contract and portable boundary

Authority is ExAV 2.1.1.12, archive revision
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, specifically
`core/controllers/terrain/MountainController.cs`,
`core/controllers/Disambiguators.cs`, `core/views/QuarterViewDrawer.cs`,
`core/world/terrain/MountainVoxel.cs`, and the ground methods in
`core/world/World.cs`. Existing hashes and decisions are in
[the terrain review](parity/terrain-review.json) and
[terrain contract](web-parity-terrain-contract.md#corner-and-rectangle-editing).
Evidence is source-derived, not an original Windows runtime comparison.

`pickTerrainGround` ports the descending ground-level ray independently of
sprite/forest alpha and the visible water plane, bounded by the current height
cut. The service applies the reviewed slope-aware selection formula using actual
NE/SW corners; submitted screen offsets cannot supply geometry. Direct X/Y/Z
entry instead identifies the cell whose NE corner is edited.

Raise/lower changes one quarter at the shared NE point, visiting the touching
cells in source x-major/y-minor order. Source's `isFourAdjacentCornersMatched`
actually tests four surrounding **outer** vertices: NW of the base, NE of its
north neighbor, SE of its northeast neighbor, and SW of its east neighbor. These
checks are retained. The edited shared vertex must agree too, so cuts cannot
produce a canonically mismatched result. Raise checks same ground, ground-level
selection, Mountain/empty occupancy, saturation and roof. Outside neighbors are
skipped for raise and block lower. Lower preserves the distinct existing-Mountain
and missing-slope branches and checks occupancy at the working level and above.
A missing slope lowers ground and starts with all-four corners. Saturated raise
raises ground/removes Mountain; flattened lower removes Mountain. Water is not
an edit exclusion and its global level is unchanged.

Retained Mountains keep stable ID, definition, realized forest, value and ownership;
a whole atomic stroke increments retained entity revisions once. Newly created
Mountains are unowned and undecorated. Removed IDs remain consumed. Removal can
erase forest treatment/trees and ownership; preview names both losses. **Cost is
zero**: these source methods do not book money. Construction mode is unchanged,
not a price or legality exemption. Geometry, tree/entity resource admission,
allocator/revision exhaustion and terrace backing validity are checked before
publication. Invalid source cliff/lower states are rejected rather than written.

1 November 2026 interaction update: the default web tool is now **Paint terrain**, incorporating
intersection selection and following freetrain-modern's nearest projected shared-corner
selection. A circular marker follows the selected vertex's actual quarter
height. A click selects an intersection without changing terrain; holding or dragging
on the map paints between intersections, with Shift reversing the action.
Raise/Lower buttons apply one step on click and repeat while held with
pointer, Enter or Space. Each step previews and commits against fresh geometry;
release, Escape, blur, pointer cancellation and tool/panel changes stop repetition.
The selected X/Y stays fixed between steps. Existing corner limits, occupancy
checks and authority remain in effect. Changes already committed remain.

X/Y/Z Preview and Confirm remain available as an alternative. Ctrl reverses the
pointer preview action. The former Shift stroke collection and Add coordinate
controls have been replaced by the persistent intersection interaction. The
bounded stroke protocol remains supported for existing clients; no server,
save or terrain geometry contract changes are required.

## Service, transport and UI

- `CornerTerrain` implements legality and literal transitions;
  `CornerTerrainDraft` maintains sparse detached changes while evaluating an
  ordered stroke. Preview does not construct or publish a replacement world.
- `POST /api/terrain/corner/preview` is an authenticated, compatible-content,
  bounded, read-only query, also available to observers. Its response includes
  epoch/geometry revision, actual target/action, per-cell before/after geometry,
  removals/losses, skipped sites and zero cost.
- `editCorner` goes through existing request receipts and the session FIFO.
  Owner/Operator authority, current connection, outer and query epochs and
  geometry revision are checked again at application. The entire candidate is
  validated and admitted to Save7 before replacement. Clock/run/pause changes
  do not invalidate an unchanged geometry preview. No train lease is invented.
- Schema and generated client contracts are synchronized. These are additive
  protocol5 operations with no changed snapshot/save representation; Save7,
  rules/content versions and exact pack identities remain unchanged. Older
  clients already understand the resulting terrain/entity deltas.
- **Game → Modify terrain** and the map toolbar expose the same delivered tool.
  EN/JA controls reuse the existing scrolling tool card, sticky Confirm/Cancel,
  stable canvas, modal/panel/input lifecycle and connection/pending guards.
  Late preview responses are fenced by request, connection, epoch and geometry;
  late acknowledgements cannot clear a newer draft. Uncertain commands use the
  existing no-replay policy.

## Verification and capacity qualification

Fresh evidence and file hashes are recorded in
[the corner terrain review](parity/corner-terrain-review.json). Local logs,
full-size save and screenshots are linked there. No commit was created.

Validation runs pass660 .NET tests, including25 focused corner/selection cases,
136 client tests,142 Python cases (one existing skip), locale/generated/content
checks and a Release build with zero warnings/errors. The raw pinned-source check
passes all40 file hashes,77 literal fixtures and59 source text bindings. Optional
source and prepared-content browser suites each pass4/4. Chromium covers101 unique scenarios: the full run passed100/101, then the final
affected spatial file passed6/6 after correcting its locale-fixture scope.
Application source was unchanged between those runs. Artifact hashes and logs
in the review retain that distinction.

The .NET tests include literal four-cell quarter geometry, saturation/flattening,
flat lowering, asymmetric border behavior, floor/roof/ground/mismatch/outer-ring
checks, submerged edits, non-reclaimable creek obstruction, retained and lost
forest/ownership, source selection from actual slopes, FIFO Operator/Observer
roles, stale revision/epoch/disconnected participant, ordered local skips,
malformed late-site atomic rejection, maximum stroke size, exhausted identities,
allocator monotonicity and exact Save7 continuation. The inherited seven helper
boundary cases remain included. Client tests cover bounded/consistent response
validation, source ground-ray selection through water, height cuts and stale
connection/geometry responses. Four new Chromium journeys cover pointer Ctrl,
Shift stroke release/blur, read-only preview, exact single commit, coordinate
keyboard input, Escape/panels, late responses, independent participant conflicts,
observer read-only access, Japanese 390px DPR2 layout and save/reload/lower.

A fresh CLI 1500×1090×45 empty-world fixture evaluates 64 sites / 256 touching
changes, commits them once and verifies byte-identical Save7 reopen. Final
measurements were **11.6 ms preview / 1,193.7 ms commit / 525.5 ms save+reopen**,
with a 2,192,169-byte save, about87 MB managed memory after collection,
and233.7 MB maximum RSS for the `dotnet run` probe command.
This is a functional full-size continuation check, **not** an interaction-budget
pass. Commit rebuilds/validates the canonical world, validates its save envelope,
then replaces it at the FIFO boundary; dense edited-world publication also needs
new capacity evidence. Large-map commit responsiveness exceeds the provisional
250 ms authoritative target. Keep this specific optimization/measurement in the
P3-16a final consumer regression before P3-17; no existing generation/transfer/
rendering capacity result is relabeled as an editing measurement.

Five existing Japanese browser assertions had retained older polite wording
while the checked-in locale already uses shorter wording. They now read the
current locale fixture; UI copy was not changed to satisfy those assertions.
The individual commands ran under installed Deno2.9.8 (.deno-version declares
2.9.7), .NET SDK10.0.402 and installed Playwright Chromium. The aggregate shell
script with its exact-Deno-version assertion was not used.

Native/device/deferred-browser, optional real-suite, P2 and full-city/import
acceptance retain their prior scope. This slice does not close those gates.
