# P4-13 girder elevated-track workflow — 1 November 2026

The **Girder viaduct / ガード高架** task (**V**, immediately after Slope) delivers bundled contribution
`{7FD493FF-D6A7-4a13-A471-FB737641D8A0}` through the persistent map,
authoritative special-rail preview/confirmation and canonical mixed-world saves.
Stable ExAV 2.1.1.12, archive `883cf7d81cf49db25ff38cd3a25d0f140afe31a0`,
`trunk/core/world/rail/StealSupportedRailContributionImpl.cs`, is the authority.
The misspelled source type/resource names and contribution identity are preserved.

## Source geometry, occupancy and art

[GirderRailwayConstruction](../src/FreeTrain.Online.Simulation/GirderRailwayConstruction.cs)
preserves `Location.align8To` after taking the first point's Z. Let X and Y be
absolute endpoint differences: integer `Y/2 > X` selects the Y axis; otherwise
`X/2 > Y` selects the X axis; otherwise the diagonal keeps the requested X and
uses `anchorY + X × sign(deltaY)`. The comparisons are strict and use integer
division: differences (2,5) snap to (2,2), while (2,6) snap to (0,6). This is
source snapping, not a nearest-angle heuristic. Snapped coincident points reject
under RAIL-D001; checked coordinates and the existing 8,192-cell work bound apply.
The preview echoes the original query and supplies the actual snapped route,
ghost and endpoint markers.

Every inclusive build cell must be strictly above its own actual ground,
including cells retaining existing rails. Every occupied target must contain
rail with both directional ports along the span. Compatible existing rails keep
their subtype, identity, artwork and components. Incompatible curves, junctions,
perpendicular rail and nonrail occupancy reject atomically. **Compatible junctions
remain legal**: the checklist's shorthand “junction rejection” is qualified by
the actual source two-port predicate, also used by Bridge. No new junction is
created by this tool. The approved RAIL-D005 car fence rejects the whole build;
there is no reclamation or curve routing.

Neither endpoint needs an existing railway or slope. Disconnected elevated spans
may be built first and connected later. Piers are created when possible and are
not a prerequisite: blocked support columns may remain unsupported. Girder Lay
now automatically chooses one level above the starting column's actual ground,
or a higher visible existing rail, for map and keyboard starts. Both automatic
map picks identify visible terrain/rail columns; the end retains the chosen
rail level, avoiding an XY shift from intersecting the raised construction plane.
The choice stays within world levels; ground at the ceiling still receives an
authoritative rejection. Later higher ground can still require a higher manual
level. Removal and other railway tools retain their existing picking/level rules.
This is an authored browser adaptation, not a relaxation of the source predicate.
ExAV checks that predicate before calling build; it has no exception here for
an intended future structure.

New cells receive the source RailImpl orientation and exactly one prepared
32×32 crop at anchor (0,16), stage 10, before the reserved P5 car pass:

| Heading | Straight pattern | Sprite index / source X |
| --- | --- | --- |
| N/S (0/4) | 10 | 3 / 96 |
| NE/SW (1/5) | 1 | 1 / 32 |
| E/W (2/6) | 4 | 0 / 0 |
| SE/NW (3/7) | 7 | 2 / 64 |

There is no after-car girder frame. Every newly created rail elects default
`system:pier` support, from actual ground through railZ−1. Existing rails elect
nothing. The source scan starts at Z0; any obstruction, including below ground,
suppresses that entire pier column without rejecting the railway. Bridge retains
its distinct alternating top/body supports and Begin/Middle/End art. The shared
[ElevatedRailwayConstruction](../src/FreeTrain.Online.Simulation/ElevatedRailwayConstruction.cs)
engine handles their common occupancy, booking and atomic graph mutation while
each public contribution boundary supplies its own snapping and semantics.

## Removal, accounting and durability

Removal visits both endpoints and clears only matching girder rails aligned to
the selected direction, skipping cars, other subtypes, wrong directions and gaps.
Platform dependencies reject rather than orphaning a future object. Road/pole
components survive; rail-dependent crossing accessories follow shared teardown.
Only actually removed matching rails trigger support teardown. All lower piers
are losses regardless of owner under GEO-004/RAIL-D006, with both owner and
supported-owner identities in the preview. Higher foreign rails survive.

Actual source booking charges every inclusive cell, including unchanged builds
and removal skips: build `6,000,000 × M(z−ground)`, removal
`2,000,000 × M(z−ground)`, where `M(h)=−2h` below ground and `h+1` otherwise.
If the starting column has `ground <= z <= water` and `ground < water`, the whole
booking is zero. Zero bookings still record an expenditure transaction. Ground
validation applies only to build. Shared cash/rescue, account and voxel budgets
remain enforced; the inherited native road estimate is not used.

The additive protocol6 `RailwayQuery.infrastructure = "girder"` passes the HTTP,
WebSocket, session and browser schema boundaries. FIFO confirmation revalidates
participant, role, epoch, geometry, content, legality and actual booking before
one publication. Preview/cancel preserves save bytes. Terrain-to-mixed promotion
retains terrain, provenance, clock/random, mode, epoch and live canvas/camera.
Duplicate transport receipts book once; stale host replays reject. Save8,
protocol6, catalog bytes and pack identities are unchanged. Existing exact art,
alpha-mask inspection/picking, cuts and mixed deltas serve the new subtype.

## Workflow and translation reconciliation

The shared two-point panel offers Lay/Remove, automatic/manual level, keyboard
XY or map picks, authoritative route/cost/support/loss preview and Confirm/Cancel.
Bend-order controls remain specific to normal rail. Manually selecting ground
height produces localized guidance to choose a higher level. For an existing
girder preview, changing height or toggling automatic height cancels the old quote
and requests a fresh authoritative preview; Confirm is enabled only when the new
quote is legal. This fixes the silent idle/disabled state after changing the
height of an already picked span. Explicit coordinate edits retain their Preview
route action. EN/JA help describes the automatic clearance and preview refresh.
V, its tooltip and accessible
shortcut metadata select the dedicated girder task. Escape, tool/panel changes,
late previews, reconnect, role/input and uncertain-outcome guards retain the
existing task lifecycle. Cost/confirmation stay together; detailed rules fold.

This implements the girder portions of MAN-M03/W02/A04/E12 and DEV-P14/P24 in the
existing browser shell. Moving trains and platforms retain P5/P6 ownership.
The immutable [source-text review](parity/railway-localization-review.json) keeps
its hashes; P4-22 retains overall reconciliation:

| Source | Delivered EN/JA | Disposition |
| --- | --- | --- |
| RAIL-TEXT-024, ガード高架 | `girderTask`: Girder viaduct / ガード高架 | Historical donor meaning retained, sentence case |
| RAIL-TEXT-025, スチールで強化された高架 | `girderDescription`: Steel-reinforced elevated railway / スチールで強化された高架 | Source-bound English translation |
| 敷設 / 撤去 / 費用： | Shared action and cost resources | Actual contribution booking |
| Snapping, ground/occupancy/support rules, confirmation, removal, V shortcut | Dedicated `girder*`, `confirmGirder` and shared railway resources | Authored EN/JA browser adaptations |

## Verification

The initial P4-13 .NET suite passes **961/961**; its client suite passes **191/191**.
Girder domain/session coverage includes all 13 independent GIRDER fixture rows,
all eight directions, literal patterns/crops, default support per new cell,
source integer snap thresholds, bounds/overflow/coincident guards, every-cell
including retained-rail ground checks, compatible junction preservation,
incompatible curves/junctions, car build protection and charged removal skips,
platform dependency, blocked supports, owner-agnostic foreign-pier losses,
preview byte immutability, exact save/reopen/remove, water-zero and unchanged
booking, stale/FIFO/role/disconnect/content/replay fences. The source coincident
removal no-op is represented by the existing invalid/no-mutation live guard.
Car/platform fixtures use explicit trusted test bindings; their browser gameplay
remains P5/P6. The renderer verifies every girder heading and source crop,
before-car-only dispatch, alpha picks, cuts and immutability in all eight
season/light variants. Request/schema tests distinguish girder from other tasks.

**161 Python tests, one existing skip**, pass. Production build has zero Svelte
errors/warnings (Vite retains its bundle-size notice); generated contracts/content/
locales, default and explicit pinned-source review/1,484 fixtures, project
boundaries and whitespace checks pass.

The first browser run exposed a missing WebSocket infrastructure allowlist entry:
girder preview worked, but its command was refused before session dispatch. The
entry is corrected and actual command/receipt workflows are verified. A later
compatibility test setup incorrectly tried starting ordinary rail above empty
ground; it now builds legal ground track and loads an explicitly authored raised
Save8 fixture with matching entity/voxel anchors. This setup is not claimed as a
normal-rail gameplay capability.

The initial P4-13 Chromium run passes **30/30** Girder/Bridge/Track/Slope/renderer cases,
including all five dedicated girder workflows. Browser coverage includes all
eight directions with actual prepared rail/pier pixels, cut views, exact source
booking and loss counts, cancellation, map/keyboard picks, strict ground and
nonrail occupancy rejection, compatible straight/junction identity preservation,
incompatible curve/junction rejection, blocked supports, charged unchanged and
wrong-subtype removal, two peers, canvas retention, EN desktop and JA narrow DPR2,
V shortcut, stale previews, Observer rejection, duplicate receipts and exact
save/reopen/remove. Representative EN build, diagonal pier-cut and JA narrow
captures were visually inspected.

Logs: `tmp/p4-13-*`. Final captures:
`tmp/browser-evidence-Z3gDwm/girder/`.

Original-runtime/native/human fidelity, other browser engines,
optional-suite acceptance, moving trains and measured P4 capacity retain their
separate gates. Mixed-world terrain editing/cross-tool closure remains P4-20.


## Height/confirmation UI follow-up — 1 November

The owner reported disabled Confirm with no visible error while choosing an
above-ground span. Changing a height after picking/previewing a complete route
called the generic edit reset, discarding the quote and returning the task to
idle; it did not request a new quote. The first added browser reproduction also
failed because default automatic map picks used bare ground height, making an
isolated viaduct invalid by default. The browser now uses the girder clearance
policy above and refreshes an existing preview after height/mode changes. Initial
coordinate entry retains its explicit Preview action, and old/in-flight responses
still cannot replace an edited draft. The toolbar remains immediately after
Slope. Backend legality, pricing, Save8/protocol6 and catalog bytes are unchanged.

**192/192 client tests**, production build (zero Svelte errors/warnings), generated/
content/locales and whitespace checks pass. **28/28 Chromium** Girder/Bridge/Track/
Slope cases pass, followed by **6/6 final girder workflows** after restricting the
refresh trigger to existing previews. The new UI regression verifies automatic
bare-ground map picks, automatic coordinate height, immutable previews, a rejected
manual ground level corrected to a legal elevated level without another Preview
click, enabled Confirm and an actual five-cell isolated build with zero slopes.
It also captures the enabled button before confirmation; the screenshot was
visually inspected. Existing all-eight-direction art/pier/save/removal, EN/JA narrow
DPR2, two-peer and stale/receipt tests continue to pass. Unit coverage independently
checks clearance, existing elevated attachment, cuts, world ceiling and unchanged
normal-rail height selection.

Logs: `tmp/p4-13-girder-height-*`. Final captures:
`tmp/browser-evidence-iQJVKz/girder/`, including
`girder-isolated-ready-to-confirm.png` and `girder-isolated-auto-height.png`.
