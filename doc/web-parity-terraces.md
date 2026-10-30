# P3-15 — terraces and cliffs

30 October 2026. **Game → Modify terrain → Terraces and cuts** adds Place terrace,
Remove terrace, Make cut and Restore cut in English/Japanese. Each operation has
map or coordinate targeting, an authoritative read-only geometry/loss/cost preview
and explicit Confirm/Cancel. P3-16 map authoring mode, final editing capacity and
P3-17 acceptance remain open, alongside existing P2/native/deferred-browser,
full-city and historical-import gates.

## Source rules and portable validation

Authority is ExAV 2.1.1.12, archive Git
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`,
`plugins/org.kohsuke.freetrain.terrain.terrace/src/`:
`TerracePlacementStrategy`, `TerraceRemovalStrategy`, `CliffPlacementStrategy`,
`CliffRemovalStrategy`, `MountainDisambiguator` and `TerraceVoxel`. The pinned
[terrain source review](parity/terrain-review.json),
[terrain contract](web-parity-terrain-contract.md#terraces-cuts-and-construction-mode)
and P3-TERRAIN-009/010/012 bind these behaviors. This is source-derived evidence;
no original Windows runtime or native GDI comparison is claimed.

* Source terrace placement requires a Mountain at the selected ground. The
  browser also accepts bare ground under the owner-requested extension below.
  Existing Mountain placement removes that entity, backs up only its four
  NE/SE/SW/NW offsets, raises ground by one and
  installs a fresh unowned, non-reclaimable, zero-value Terrace at the old Z.
  Forest decoration and ownership loss are explicit in preview. Roof and
  obstruction guards run before any mutation.
* Terrace removal selects the backed-up voxel below raised ground and requires
  the voxel above to be vacant. It lowers ground by one and creates a fresh
  unowned, undecorated Mountain with the exact saved offsets. Flat and all-four
  backups remain exact; they are not normalized into a different ground height.
* Make cut requires Mountain. Source `Direction.get(i*2)` checks the four N/E/S/W
  neighbors in canonical X/Y; any higher neighbor ground rejects the operation.
  The Mountain is removed without changing ground. Ground remains explicitly Cut.
* Restore cut requires vacant selected ground. For each corner it takes the
  maximum over the source's four touching **same-Z** samples: Mountain offset at
  that Z, otherwise zero, plus `4*selectedZ`. Neighboring ground at another Z
  does not enter this calculation. All-zero offsets are a no-op. Nonzero results
  create an unowned, undecorated Mountain; an all-four result stays at its Z.
  Signed/corner/top bounds and occupancy are checked before publication.

The source does not back up the original surface classification. Removal and
restoration retain explicit Cut classification so that intentional discontinuities
remain legal without disguising unrelated malformed natural terrain. Water stays
the global plane and excludes none of these operations. No generic charge or RNG
consumption is introduced: these source methods book no money. Replaced entities
get fresh monotonically allocated IDs; removed IDs are never reused.

`TerraceTerrain` implements four separate strategies through the sparse
`CornerTerrainDraft`. The existing `CornerTerrainQuery.action` gains four values. Placement also
accepts the bounded rectangle described in the follow-up below. Screen-corner
offsets and nonempty strokes cannot be mixed with terrace/cut strategies;
removal and cuts remain single-site operations. State, Save7/protocol5 and content identity do
not change. The strict schema, generated client types and network command filter
agree on the actions and diagnostics. Owner/Operator commits revalidate current
connection/role, epoch, geometry revision, complete candidate and durable Save7
admission before one atomic publication. Compatible Observer read-only previews
remain available. Clock changes alone do not invalidate geometry or pause time.

## Picking, art and lifecycle

Mountain placement/cutting and Terrace removal use their own source voxel rays,
independent of sea/decorations/sprite alpha. Restore cut uses the ground ray.
The height cut bounds all rays. A cached two-byte-per-column index avoids repeated
entity scans during picking; a new immutable entity array invalidates the index.
Coordinates support every legal stored border. Explicit actions ignore corner
Ctrl/Shift modifiers and never accumulate a stroke. Existing prepared Terrace,
quarter/cliff recipes, clipping, occlusion and inspection targets render the
result; no placeholder art or new recipe identity is introduced.

Preview displays before/after ground and backed-up/current corners, zero cost and
forest/ownership loss. The existing scrolling tool card keeps Confirm/Cancel
reachable on narrow layouts. Escape clears a review; panel/tool changes discard
it and reset targeting. Connection, role, epoch and geometry changes fence drafts;
versioned requests and the existing remote-client checks reject stale results.
Ambiguous command outcomes retain the existing no-replay behavior. Canvas/camera
survive tool/panel workflows. Source-only terminology is localized as Terraces and
cuts / 雛壇と切り通し; Inspector's existing Terrace label remains 擁壁.

## Verification and measured capacity

Reproducible commands, logs, source/tested-file hashes and the Japanese narrow
screenshot are in [the review record](parity/terrace-terrain-review.json).

The final .NET suite passes **700 cases**, including 15 focused cases for literal
`[1,2,3,4]` terrace backup/restoration, exact source sample coordinates, maximum
corner reconstruction, unowned identities/value/decoration loss, natural terrain,
four stored borders, water, roof, allocator exhaustion, higher ground, occupancy,
malformed mixed modes, FIFO Operator/Observer permissions, peer conflicts,
clock independence and exact Save7 continuation. The client suite passes **140
cases**, including strategy-ray/cut targeting and strict action/diagnostic checks.
Python passes **142 cases** (one existing skip); the pinned 40-source-file,
77-fixture/59-text review and project boundaries pass. Release/probe and web builds,
Svelte, locales, generated contracts and bundled content checks pass.

The browser matrix exercises all four actions, read-only previews, exact
save/reopen, map selection of the backed-up Terrace, Escape/panels, peer conflict,
canvas preservation and Japanese 390px DPR2 keyboard confirmation. Existing
terrain renderer cases independently verify Terrace front pixels, entity targets,
quarter/cliff recipes, water, clipping and all seasonal/light variants. Full
Chromium passes **109/109**; optional source and prepared configurations each pass
**4/4**.
Development failures (a test continuing after panel reset without reselecting the
strategy and a Japanese expected-text mismatch) were corrected before the final run.

A **1500×1090×45** CLI workflow starts with an actual source rectangle slope,
then places/removes a Terrace and makes/restores a cut. Every result commits once
and reopens byte-identical Save7, with literal ground/offset/kind oracles.

| Action | Preview ms | Commit ms | Save + reopen ms | Save bytes |
| --- | ---: | ---: | ---: | ---: |
| Place terrace | 3.67 | 1,068.06 | 520.79 | 2,187,899 |
| Remove terrace | 0.11 | 965.06 | 584.84 | 2,187,756 |
| Make cut | 0.05 | 1,040.07 | 612.19 | 2,187,696 |
| Restore cut | 0.26 | 1,071.50 | 611.51 | 2,187,756 |

Collected managed memory is approximately 52–53 MB. This extends functional
large-map continuation evidence; it is **not** a latency/dense-publication budget
pass. Whole-world validation/durable admission/replacement still exceed the
provisional 250ms authoritative target. Retain terrace/cut editing alongside
corner/rectangle editing and dense edited-world publication in final P3-16a
capacity acceptance before P3-17. No native/device/deferred-browser acceptance or
full-city/import capability is inferred from these measurements.


## Rectangle terrace placement follow-up — 30 October 2026

Owner request extends **Game → Modify terrain → Terraces and cuts → Place
terraces in a rectangle**. The optional checkbox retains existing single-cell
placement/removal/cut controls. Select two opposite map corners or enter start
X/Y/Z and end X/Y, then preview and confirm once. Inclusive normalized rectangles
admit up to 64 cells at the first selected Z; reversing endpoints produces the
same x-major/y-minor plan. Ctrl/Shift do not turn terrace selection into repair
or upper-profile terrain editing.

This batching is browser workflow policy layered over the original per-voxel
strategy. Every cell must independently satisfy eligible ground/roof/occupancy
and allocation rules. The later bare-ground extension below removes the need for
a preparatory Mountain stroke. Invalid cells, including outside cells within a skew-grid
bounding rectangle, are shown as skips. Source corner backups, forest/ownership
loss, fresh unowned identities and zero booking are unchanged. Eligible cells
appear as highlighted map diamonds and a terrace count; skipped cells have
separate highlights/reasons. If every cell is skipped, Confirm stays disabled.
All eligible sites publish in one revalidated atomic edit and one geometry
revision, preserving existing roles, clock, mode and no-replay handling. The
saved world contains the actual backed-up terraces; reopening needs no rectangle
request. This extends existing Save7/protocol5 contracts without new fields.

Backend cases cover four-cell exact backup/loss/identity/save restoration,
reversed normalization, partial/all skips, roof rejection, 64-cell admission,
oversized/mixed selections and role/revision contention. Browser cases cover
coordinate and two-map-point rectangles, one confirmation, read-only preview,
exact save/reopen, Escape and peer invalidation, plus Japanese narrow keyboard
confirmation. The shared corner/rectangle/forest/creek regressions also run.
Validation passes **719 .NET cases, 142 client cases and 19 targeted Chromium
journeys** (corner, rectangle, terrace and scenery controls). Release solution
and production web builds pass with zero Svelte errors/warnings. The desktop
batch screenshot was inspected. Fresh logs and file hashes are in
[the batch review](parity/terrace-batch-review.json); the earlier P3-15 evidence
above retains its original slice scope. Full-size batch latency/publication
acceptance remains in final P3-16a; the single-cell measurements above are not
new batch capacity evidence.


## Direct bare-ground terraces — 30 October 2026

Owner request removes the preparatory corner-stroke requirement from **Place
terrace**, both for single cells and the optional rectangle. Natural or Cut
bare ground at the selected Z is directly eligible. Preview raises the ground
one level and backs up four zero corner offsets; existing slopes retain their
actual offsets. This is an explicit browser authoring extension to the pinned
source's Mountain-only eligibility, not a change to the source fixture claims.

Roof, bounds, occupancy, allocation/resource admission, roles and stale-preview
checks still apply. Creek cells and already backed-up terrace columns are
occupied; no stacking or overwriting unrelated occupants is introduced. Make
cut still requires Mountain. Placement uses the ground ray so a neighboring
slope cannot take precedence over the bare ground under the pointer. EN/JA help
now states that no corner stroke is needed.

Confirmation remains one atomic edit. The ordinary Save7 Terrace record already
stores all-zero backups, so no format/content version changes are needed.
Removing a bare-ground terrace after save/reopen lowers it to its original level
and restores a flat, unowned, undecorated Mountain with zero offsets, using the
existing removal representation. Shared clock, mode and zero booking remain
unchanged. Tests cover Natural/Cut direct placement and exact saved restoration,
64 initially bare cells, roof/creek/stacking/allocation rejection, pointer picking,
read-only preview, direct rectangle creation and Japanese narrow confirmation.
Validation passes **722 .NET cases, 142 client cases and all seven terrace
Chromium journeys**, including direct bare-ground map targeting and EN/JA
rectangles without prior strokes. Release and production web builds pass with
zero Svelte errors/warnings. Fresh logs and hashes are recorded in [the direct-placement review](parity/terrace-direct-review.json).
Final capacity acceptance and earlier native/deferred-platform gates remain open.
