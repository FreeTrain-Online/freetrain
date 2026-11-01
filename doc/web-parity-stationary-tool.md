# P4-18 railway accessories — 1 November 2026

**Railway accessories / 鉄道アクセサリ (A)** delivers all **54 bundled
railStationary contributions**, with source art previews, XY/map selection,
authoritative preview/confirmation, construction progress, cancellation and
whole-structure removal. [The contribution ledger](parity/railway-accessories.json)
reconciles all **64** P4 definitions, including the four special rails, two signals
and four poles delivered by earlier slices. It retains exact source IDs and raw
manifest hashes; malformed-looking braces are intentional identities.

| English / Japanese family | Variants | Footprint / height |
| --- | ---: | --- |
| Brick pier / レンガ橋脚 | 10 | 1×1×1 |
| Concrete pier / コン橋脚 | 8 | 1×1×1 |
| Brick pier with side path / レンガ橋脚(側道付) | 8 | 1×1×1 |
| Concrete pier with side path / コン橋脚(側道付) | 8 | 1×1×1 |
| Composite concrete pier / コン橋脚(複合) | 18 | six 1×1×1; twelve 1×1×2 |
| Signal box / 信号所 | 2 | 1×1×1 |

Each contribution has its own baked orientation; there is no invented rotation
or inferred rail connection. Decorative piers do not elect/support track, and
signal boxes do not control trains. P6 station/passage/stair contributions and
P7 generic structures/roads remain distinct. The roadAccessory extension group
has no bundled declarations. Automatic crossings retain their trusted P4 boundary
fixtures and P4-20/P7 ownership. Optional packs and invalid source resources keep
their existing gates; this step registers no optional handlers.

## Source and domain

Authority is stable ExAV 2.1.1.12, archive
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, `trunk/`.
The [P4-01 pinned source review](parity/railway-review.json) supplies
RailStationaryContribution, RailStationaryStructure, PThreeDimStructure,
StationaryStructPlacementController, the active FixedSizeStructController and
ConstructionSite. The inactive inverted placement predicate is not used.
[StationaryConstruction](../src/FreeTrain.Online.Simulation/StationaryConstruction.cs)
implements those consumers; `review-railway-accessories.py --check` reconciles the
implementation ledger with source scope and generated browser metadata.

Placement requires the entire exclusive XYZ footprint inside stored world bounds,
equal ground at the base level, and genuinely reclaimable occupancy. Reclamation
removes whole entities and appears in the loss preview; transparency grants no
reclamation permission. Every height member occupies space, even if its art is
invisible. Sites are unowned/nonreclaimable/value zero with opaque footprints;
completed bundled 1×1 structures are owned/nonreclaimable/value zero with source
transparent voxels. Source cleanup replaces the site owner with a fresh completed
owner ID. No terrain, rail component or unrelated owner is repurposed.

Confirmation creates a site and books **nothing**. Per-cell state stores the
source empty/bone1/bone2/bone3/walled stage, seven ground-object variants and door
flag. Initial creation follows Z/X/Y order; the south-west corner suppresses the
door draw. One-shot delays are **40–119 source minutes**, including retries.
Construction shares the P1 world-local `construction` random stream and FIFO
scheduler. Work progresses on weekdays at **09:00–17:59**, with the source
below-floor/above-floor ordering constraints; holidays are not an extra exclusion.
The last walled cell clears the site and books the contribution's price to
**RAIL_SERVICE**: 100,000 for piers, 3,000,000 or 1,000,000 for the signal boxes.

Removal resolves the actual stationary owner from any occupied member, independent
of the selected catalog variant. Completed demolition clears the whole footprint
and books **500,000 × sizeX × sizeY to SUBSIDIARIES**, independent of height.
Site cancellation costs zero, drops its callbacks and never charges completion.
The dedicated tool also exposes site cancellation; the existing object bulldozer
can cancel a site. Completed structures require the dedicated priced tool, so
object demolition cannot bypass their expenditure. Pending completions reserve
journal capacity against later rail/slope expenditure.

## Persistence, rendering and presentation

Save8/protocol6 retain their identities: the bounded `stationary` voxel kind and
optional construction-cell payload extend the existing mixed graph. Saves without
sites require no new fields. Active cells must have exactly one bound future
one-shot action within the source retry horizon; completed cells have none.
Unknown, duplicated, missing and malformed state/schedules reject before world
replacement. Cancellation prunes only the removed site's registrations. Exact
clock order, RNG, owner/allocator, progress and completion expenditure survive
reopen. Clock callbacks execute on a detached boundary before publication.

The mixed renderer uses exact qualified fixed-3D recipe cells and the existing
**67 construction-art recipes** (eight fences, seven ground objects, 48 bones,
four walls). Fence/background/bones/wall/foreground order follows ConstructionSite.
Completed height cuts use the source grey base diamond; normal art, alpha picks,
upper-cell targeting and owner/recipe inspection retain full occupancy semantics.
The toolbar, Help, diagnostics, progress rules, future completion price and actual
removal target are available in English and Japanese. MAN-T05/M03/A12 and DEV-P12/
P26/P28 bind to the tool, shared lifecycle, prepared sprites and stored footprints.

Source RAIL-TEXT-021 uses shared illegal-placement diagnostics. The source menu
and contribution groups become the typed task/variant selector; the six Japanese
groups above have reviewed English metadata, source-manifest provenance and
stable per-group variant numbers. Native window placement and silent no-target
removal adapt to the existing tool card and explicit diagnostics. Construction
sound/spatial audio dispatch remains with P9; this step claims no new audio
playback or native/human pixel-fidelity acceptance.

## Verification

- Release .NET **1,025/1,025**: all 54 source-ledger dimensions/prices, sites and
  completion/demolition; actual-type/upper-cell removal; cancellation/bulldozer,
  independent hours/floor constraints, copied-stream preview, world isolation,
  creek losses, reserved journal capacity, malformed schedules/payloads,
  role/stale/point fences and identical session save/continuation.
- Client **210/210**: all 54 variants in eight seasonal/light combinations,
  exact crops/anchors/cells, source construction pass order, cuts, qualified
  picking, malformed owners/progress, complete cleanup and preview identity.
- Python **162 checks, one skip**; pinned P4-01 source review, **1,484** independent
  railway fixtures, the new 64-entry implementation ledger, project boundaries,
  generated contracts/content/locales, Svelte and production build pass.
- Focused Chromium **3/3**: actual sites and progress in EN/JA, peer publication,
  exact HTTP save/reopen, cancellation, A/Escape, Observer and 430×850 controls.
  The completed canonical gallery contains all 54 variants; each has visible
  opaque samples matching the prepared source atlas. This is runtime rendering
  evidence; construction/booking for every variant is independently covered by
  the domain suite. Initial captures were inspected in
  `tmp/browser-evidence-iw04bY/stationary/`.

The shared railway Chromium sweep passed **56/57** (`tmp/p4-18-browser-final.log`).
The existing Japanese narrow forest-removal check exposed toolbar overflow after
adding a twelfth tool: scrolling the page to Remove moved its map click to X −22.
The toolbar now owns its scroll range; keyboard navigation reveals the focused
button without moving the page or camera. Fresh object-removal/accessory
Chromium **11/11** passes (`tmp/p4-18-browser-final3.log`), resolving that failure
and verifying the final three accessory cases. The strengthened ground-click
check explicitly restores automatic height after upper-cell removal.
Final gallery, EN/JA site and 430×850 actual-target captures in
`tmp/browser-evidence-za6EJ1/stationary/`, plus `remove/remove-ja-narrow.png`,
were visually inspected. The gallery matches prepared atlas samples; original
runtime/human pixel identity is not inferred from that comparison.

Final logs: `tmp/p4-18-tests-final2.log`, `tmp/p4-18-client-final3.log`,
`tmp/p4-18-python.log`, `tmp/p4-18-build-final3.log`,
`tmp/p4-18-source-pinned.log`, `tmp/p4-18-ledger-final.log` and
`tmp/p4-18-boundaries-final.log`. Whitespace checks pass. The existing production
chunk-size advisory remains. P4-19 speed authoring, P4-20–24 integration/recovery/
capacity/acceptance, P5/P6/P7 gameplay, optional-suite and native/human-fidelity
gates remain open.
