# P3-14 — rectangular terrain editing and repair

30 October 2026. This slice ports Mountain2 into **Game → Modify terrain →
Rectangle and repair**. It supplies Raise rectangle, Excavate rectangle and Repair
corners, with keyboard/coordinate and two-point map selection in English/Japanese.
Terraces/cliffs and map construction mode remain P3-15/16. Overall P3, final
capacity and the existing P2/native/deferred-browser/full-city/import gates remain open.

## Source and reviewed portable policy

Authority is ExAV 2.1.1.12, archive Git
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`,
`plugins/foobarbazer.Mountain2/src/Mountain2Controller.cs` (pinned raw blob SHA-256
`2f7504e8551cddf290135419e11340709dad7b80cace77b2f247562581fc0992`),
RectSelectorController, MountainVoxel, Direction and World. The existing
[terrain contract](web-parity-terrain-contract.md#corner-and-rectangle-editing),
P3-TERRAIN-004/010/012 and source review bind the behavior. This is source-derived
verification, with no original Windows runtime comparison.

X/Y endpoints normalize at the anchor Z. The service requires both submitted Zs
to agree, and validates every selected cell before any work, including skew-grid
rectangles whose endpoints exist but intermediate cells do not. The bounded
portable review admits at most **64 inclusive selected cells**. Raise/excavate
visit exclusive shared-corner sites (`x<maxX,y<maxY`) in x-major/y-minor order;
either zero span is a no-op. Repair visits selected cells inclusively, including
a single cell. An 8×8 selection has 49 raise/excavate sites or 64 repair sites.

The source SE lookup is `(0,0,SE),(0,1,NE),(1,0,SW),(1,1,NW)`. Mountain2's outer
matching ring starts one cell south of the base, unlike ordinary corner editing.
The edited shared vertex must agree too. Default raise sets offset **4**, rather
than incrementing it. Shift's upper profile uses the current base's number of
corners >=2: fewer than two selects 2, otherwise 4. East/south low ground,
obstructions or the last selection site in either axis force 2. Raise preserves
source selected-ground, occupancy, saturation and roof guards. Excavation rejects
Z=0 and a base already occupied by a Mountain, checks the lower working level and
one above for missing slopes, then sets shared corners to **0**. Missing slopes
start at all-four offsets after lowering ground. Flat/saturated geometry removes
the Mountain and saturation raises ground. Invalid canonical cliff/terrace-backed
transitions are rejected instead of publishing invalid state. Water stays global
and does not exclude an edit.

Repair inspects NE/SE/SW/NW at each cell's actual ground. Empty or Mountain targets
can match an incorrect corner only when all present neighbors agree and the exact
fine-height difference `reference - 4*ground` is in 0..4. All-absent neighbors,
inconsistent neighbors, unrepresentable heights, obstructions and roof violations
cannot produce a repair. This deliberately replaces source's modulo fallback,
which could leave the corner mismatched. Flat/saturated results normalize as above.
Cut/terrace classification is retained; this is not a cut-removal tool.

The partial-failure policy follows the reviewed detached-candidate contract:
source-local failures skip a site and are listed; accepted sites see preceding
accepted changes in source order. **One repair cell is a local atomic unit**:
if a later corner cannot be repaired, none of that cell's corners is written.
This is an explicit adaptation of desktop per-corner partial live mutations.
The valid result publishes once after Confirm. Invalid request, authority,
epoch/revision, final canonical graph or durable admission publishes nothing.
Zero-change sites never enable Confirm. There is no generic terrain charge:
source Mountain2 does not book money. Retained entities keep ID, forest, ownership
and value, with one revision increment per command. New Mountains are unowned;
removed forests/ownership are explicitly reported, and IDs are never reused.
No gameplay random stream is consumed and shared time is not paused.

## Integration and lifecycle

`RectangleTerrain` uses the existing sparse `CornerTerrainDraft` admission and
replacement path. The additive optional `rectangle:{end,soft}` selector on
`CornerTerrainQuery` shares authenticated read-only
`POST /api/terrain/corner/preview`, `editCorner` receipts and FIFO Owner/Operator
commit validation. It cannot mix with screen offsets or nonempty corner strokes.
Observers can query preview but cannot mutate. The optional field is omitted
when absent, preserving existing command fixture round-trips. Save7/protocol5,
world state, content/pack identities and outgoing terrain representation do not
change; the schema and generated client types are synchronized.

Explicit actions work without modifiers. Two clicks pick the endpoints; Ctrl on
the second chooses repair and Shift enables the upper profile. Rectangle mode
does not collect ordinary Shift strokes. The coordinate form provides both ends
and Z, plus an explicit profile checkbox. The map retains its canvas/camera and
shows the selected rectangle. The scrolling card retains sticky Confirm/Cancel,
localized diagnostics, costs, geometry, losses and skips. Escape clears a review
before leaving the tool. Panels/tool changes clear drafts; release/blur cancel
held gestures; connection/epoch/role changes fence drafts. Geometry changes also
invalidate an unfinished anchor. Versioned responses and connection/epoch/geometry
checks prevent late previews or receipts from replacing newer work; ambiguous
commands retain the existing no-replay policy. Reversed preview endpoints are
validated against the normalized request rather than mistaken for stale results.

## Verification and capacity

Fresh commands, results and tested-file hashes are in
[the review record](parity/rectangle-terrain-review.json). Local logs and the
Japanese 390px DPR2 screenshot are linked there. No commit is created by this step.

Final checks pass **685 .NET /138 client /142 Python cases** (one existing Python
skip), **106/106 Chromium**, optional source **4/4** and prepared **4/4**,
Release/probe builds, Svelte/locale/generated/content/source/boundary and whitespace
checks. The 24 focused rectangle cases are included in the .NET total.

The focused tests use literal SE corner arrays, quarter-height targets and repair
references independently of production transition helpers. Cases cover reversed
endpoints, source visit order, saturation, Shift interior/edge decisions,
excavation and existing slopes, floor/roof, degenerate spans, outer-ring mismatch,
single-cell/inclusive repair, inconsistent/unrepresentable neighbors, creek
obstruction, skipped sites, water, retained/lost forest/ownership, allocator
exhaustion, skew-grid boundaries, malformed mixed requests, FIFO Operator/Observer
roles, clock independence, stale/disconnected/epoch fences and exact Save7 reopen.
Client schema cases cover the rectangle command and bounded repair diagnostics.
Five new Chromium journeys cover read-only exact confirmation, reversed endpoints,
canvas/cash/save/reopen, two-point Ctrl/Shift, Escape/panels, actual mismatched-cut
repair with skips, malformed/oversized/stale queries, delayed results, independent
peer conflicts and Japanese narrow keyboard excavation/repair.

The full-size CLI probe runs three independent **1500×1090×45** workflows using
64-cell selections. Raise and excavation each evaluate 49 sites / 196 touching
changes. Repair fixes one mismatched cut and lists 63 unchanged cells. All commit
once and reopen byte-identical Save7 with literal fine-height/revision oracles.
Measured preview/commit/save+reopen milliseconds:

| Action | Preview | Commit | Save + reopen | Save bytes |
| --- | ---: | ---: | ---: | ---: |
| Raise | 11.33 | 1,197.51 | 550.97 | 2,189,139 |
| Excavate | 1.01 | 912.32 | 434.59 | 2,189,139 |
| Repair | 1.55 | 717.12 | 726.90 | 2,187,412 |

Managed memory after collection is about 52 MB. These are functional continuation
measurements, **not interaction-budget passes**. Whole-world validation, save
admission and replacement still exceed the provisional 250ms authoritative target.
Keep rectangle/repair editing latency and dense edited-world publication in final
P3-16a consumer capacity acceptance before P3-17. Existing generation/rendering
measurements do not stand in for these editing measurements.
