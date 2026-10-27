# P3-01 terrain contract and independent fixtures — 27 October 2026

**P3-01 is specified and checked.** This closes its source-contract, decision,
workflow/text mapping and independent-fixture deliverables. It implements no
production terrain or generator, changes no save/protocol identity, and closes no
P3 runtime, browser, original-runtime or native acceptance gate. **Next: P3-02.**

The owner confirmed retaining the simple water heuristics for this checklist:
**one global water level**, with no flowing-water, elevated-lake or fluid-simulation
deliverable. The requested generation controls remain in scope using that model.
Existing P2 crop, native/audio and optional-suite repair deferrals are unchanged.

[The semantic review](parity/terrain-review.json) binds **40 raw source files** to
ExAV 2.1.1.12/V2, archive `883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, `trunk/`.
Files were read from pinned Git blobs without executing the desktop application.
[77 literal fixtures](parity/terrain-fixtures.json) have inputs, independent
expectations, derivations, classifications and source references. A small
[read-only checker](../scripts/review-terrain.py) checks arithmetic and evidence
integrity; it is neither a production generator nor a complete original-world
simulator. Existing [geometry](web-parity-geometry.md),
[actions](web-parity-actions.md), [random](web-parity-random.md) and
[persistence](web-parity-persistence.md) reviews retain their wider authority.

## Creation, bounds and coordinates

The empty-world UI's defaults are 30/30/6. It constructs stored dimensions
`H=x, V=y+2*z, Z=z`, water `z/4` using integer division, and initializes every ground
level to water. Thus its default is **30×42×6, water/ground 1**. The image loader
and fixed-map provider already pass their supplied H/V/Z dimensions directly.
The source validates positive dimensions and image-provider `0 <= water < Z`,
but does not establish a measured supported size ceiling. `groundLevels` and
`waterLevel` are bytes; unchecked casts/wraparound are source hazards, not useful
large-world behavior.

**P3-TERRAIN-001:** browser fields mean explicit stored H/V/Z, with no hidden
padding. An explicit source-default choice expands the old values and displays
the resulting stored dimensions. Retain nonempty names verbatim; an empty name
uses localized “Untitled world.” World/pixel/voxel limits and generated presets
are P3-02 deliverables, not inferred from anecdotes or proposed city sizes.

For nonnegative storage h/v, the source maps:

```text
x = h - floor(v/2) + floor((V-1)/2)
y = h + ceil(v/2)
h = (x - floor((V-1)/2) + y) >> 1
v = y - (x - floor((V-1)/2))
```

`isInsideWorld` checks horizontal H/V only; the sparse index checks Z separately.
`isBorderOfWorld` is a different visible/train-border predicate:
`h<0 || H<=h || v<2*z || V-2*(Z-z)<=v`. Do not shrink the terrain storage grid
using this train-border predicate. Carry P1 checked canonical XYZ bounds into
all terrain reads/writes; reject invalid heights before allocating/publishing.

Canonical N/E/S/W are `(0,-1)/(1,0)/(0,1)/(-1,0)`. With unchanged elevation their
isometric screen deltas are `(-16,-8)/(16,-8)/(16,8)/(-16,8)` respectively.
H increases southeast in XY, V southwest; storage sides do not have cardinal
names. For example, in H=5,V=8, storage `(0,0)/(4,0)/(0,7)/(4,7)` maps to
XY `(3,0)/(7,4)/(0,4)/(4,8)`.

**WORLDGEN-D001**, a new generation UX rule, partitions boundary storage cells
by normalized canonical XY displacement from the footprint bounding-box center.
For bounds `xmin/ymin/xmax/ymax`, set `qx=2*x-xmin-xmax`,
`qy=2*y-ymin-ymax`. Compare `abs(qy)*spanX >= abs(qx)*spanY`; true selects N/S
by qy's sign, false selects W/E by qx's sign. Ties favor Y. Require nonzero spans
and a nonempty sector for each requested direction. This gives disjoint,
reproducible coastline sectors even on the staggered grid. Draw the compass
through the same projection; a north-up top-down view and the isometric view
have different screen directions. This policy does not become ExAV parity credit.

## Ground, corners and the water plane

Ground level `g` makes voxels with `z<g` underground; `z==g` is the ground cell
and `z>g` is elevated. Independently, `z<water` is underwater. The renderer uses
the one global plane, water/ground colors and cut mode, without a flood-fill or
per-body fluid state. At equal ground/water level there is no below-plane gap.
Decorative creek tiles do not alter ground, water level or water connectivity.
**P3-TERRAIN-002** retains these semantics and the owner's simple-water scope.

`MountainVoxel` corners are **NE, SE, SW, NW**, each in `0..4` quarter units;
their packed nibbles are bits 12/8/4/0. Fine height is `4*g+corner`. Absence of a
mountain at ground implies offsets zero. All-zero mountain geometry is flat;
all-four geometry is saturated. Editor operations normalize it to ground+1
without a slope, but importer texturing and cut restoration can create all-four
geometry at the existing ground. Preserve that distinction; a global rewrite
would change source ground queries and subsequent placement behavior.
Flat mountains can still own tree/ground decoration; do not erase them merely
because their geometry is zero. Saturation/removal in the source can erase that
decoration and ownership; the preview must describe that loss.

`isCornerMatched` compares the four touching corners in fine units, ignoring
outside neighbors represented by -1. Natural imported/generated surfaces must
agree; deliberate **cut and terrace states can be discontinuous**. A universal
shared-corner-equality invariant would reject source-supported terrain.
**P3-TERRAIN-003/011:** validate geometry by terrain kind and preserve explicit
discontinuities, dimensions, occupancy and original terrace backing offsets.

## Heightmap pipeline

The required order is **displace → interpolate → smooth → texture**:

1. `GetBrightness()` is HSL lightness `(min(R,G,B)+max(R,G,B))/510`, evaluated as
   float32. Pure red, green and blue each give 0.5; a luminance converter differs.
   Sample `x=(h+(v%2)*0.5)*imageWidth/H`, `y=v*imageHeight/V`, bilinearly, with
   floor/ceil indices clamped to the last pixel. Whole ground truncates
   `min(Z-1,Z*brightness)`.
2. Each interpolation pass traverses `v=-1..V-1`, then `h=-1..H-1`, converts
   to XYZ and considers `(x,y),(x+1,y),(x,y+1),(x+1,y+1)`. Ignore outside cells;
   lower every valid member above `minimum+1`. Repeat until unchanged. It never
   raises valleys and can remove narrow ridges or water barriers.
3. Smoothing uses a flat valid 2×2 ground neighborhood and a surrounding 4×4 with
   at least one higher ground level. Resample at `h+0.5,v`; quarter offset is
   `int(min(4*Z-1,4*Z*depth)) % 4`. Nonqualifying sites retain zero offset.
4. For each shared corner, if adjacent valid ground min/max differ, fine height
   is `max*4`; otherwise add the sampled smoothing offset. An out-of-range
   smoothing lookup falls back to `max*4`. `texture` maps those shared values to
   local NE/SE/SW/NW offsets and creates a mountain unless all match `4*g`.

**P3-TERRAIN-005/007:** do this on a detached candidate. The desktop calls
`MainWindow.setWorld` before `texture` completes; its `World` constructor also
registers development/listeners and creates three test trains. None of those
early publication/global side effects is part of portable map generation.
No incidental test trains precede P5. Bound convergence/work and reject invalid
corner ranges or byte overflow. Image decode/alpha/animation handling and exact
resource budgets ship with P3-02/07. The fixed-map factory preserves image/size/
height/water/author/name/description metadata and prepared resource identity;
it is not an additional declared base new-game provider.

## Corner and rectangle editing

The ordinary controller chooses a shared northeast corner using the selected
diamond and its slope heights. Raise modifies that corner in the touching
2×2 region `(x..x+1,y-1..y)` by one quarter. It requires matched corners, equal
ground, selection at ground, empty or Mountain occupants, corner<4, and forbids
corner 3→4 when ground is Z-1. Outside neighbors are skipped when raising.
Saturation raises ground and removes the old mountain.

Lowering requires matched corners. An existing base mountain must have its NE
corner >0. Otherwise source base ground must equal selected z or z-1; z=0 is
rejected and the working level drops one. Each touching cell must be a mountain
or have no occupant at the working level **and** one above. A missing slope
lowers ground and creates all-four corners before decrement; all-zero result
removes the slope. Outside sentinels block lowering, unlike raising. The broader
source selection acceptance must never produce invalid canonical ground/corner
state; validate the candidate, not just the original controller's guard.

Mountain2 has distinct behavior:

- RectSelector normalizes X/Y endpoints at the anchor z. Raise/lower process
  **exclusive shared-corner sites** `x<maxX,y<maxY`, x-major then y-minor; either
  span<1 is a no-op. Repair visits its selected cells inclusively. Land fill is
  also inclusive, so these extents must not share one generic count formula.
- Its SE shared-corner lookup is `(0,0,SE),(0,1,NE),(1,0,SW),(1,1,NW)`.
  Upper skips mismatched/outside/obstructed/unequal-ground/saturated sites and
  rejects a selection at Z-1. Default target offset is **4**, not a +1 increment.
- With Shift, fewer than two base corners >=2 targets **2**; otherwise **4**.
  East/south low ground, obstacles or the selection's last x/y site force **2**
  even if the >=2 count would select 4. This is not the ordinary corner-repeat
  modifier. There is no reachable “keep unchanged” case from the final two flags.
- Lower rejects z=0, skips a site whose base is already Mountain, checks the
  lower level and upper obstruction, then sets the touching corners to **0**.
  It does not repeatedly decrement existing base slopes.
- Ctrl chooses repair, not reversed raising. For each unmatched corner, the
  target must be empty/Mountain and its present neighbor fine heights agree.
  Source modulo/range handling can fail to make the corner actually match.

**P3-TERRAIN-004/010/012:** evaluate these ordered operations on a candidate,
preserving source local skips. Return changed/skipped sites and publish the
entire result atomically; invalid authority/revision/input/final state leaves
everything unchanged. This differs from exposing desktop incremental writes.
Repair requires at least one agreeing neighbor and an exact representable offset
`referenceFine-4*g` in 0..4. Reject all-absent/inconsistent/unrepresentable cases
and validate every selected target before accessing it. For example reference16
at g=2 cannot be repaired by source's fallback offset4 (which totals12).
Provide explicit actions in addition to modifiers; stop strokes on release,
blur, disconnect and epoch change. Shift repeats on movement, never a timer.

## Forests, creeks, booking and ownership

The forest manifest has **18 actual land declarations**: six species × three
ground treatments. Base has no ground overlay; `-m` references the forest's
seasonal ground picture; `-g` references the farmland grass picture at origin
64,0. These are not different ecological species. All have price100 and
`computerCannotBuild`; species density/sprite counts are cedar3/9, pine2/6,
camphor1/9, maple2/3, ginkgo2/5, beech3/6. Exact IDs and ground dependencies are
in the review. The commented seaside pine/BulldozerEx are not registrations.

Source forest eligibility is ground equality and either reusable or Mountain
occupancy. **There is no underwater exclusion.** Empty/reclaimable ground is
replaced by a flat Mountain; an existing Mountain receives new tree/ground data,
price and selected ownership without changing its corners. `2*density` calls to
`Next(2)` count zeros, then each tree consumes `Next(16),Next(16),Next(spriteCount)`.
Positions are `X=x+y`, `Y=((-x+y)>>1)+8`. The sort swaps only X/Y positions,
leaving sprite indices in their original array slots. Zero trees means no new
placement or decoration change. Do not consume authoritative forest draws while
rendering a hover preview.

Creek route seed is unchecked Int32 `((x1*128+y1)^(x2*128+y2))`, already supplied
by P1 `RandomStreams.ForCreek`. The fixed-z route steps cardinally toward its end,
choosing left/right by `Next(2)` for diagonal intent. N/E/S/W bits are 8/4/2/1.
Endpoints get one direction (or zero for coincident points); single-ended N/S
uses straight N-S art and E/W uses E-W art. Existing creek bits are ORed only
when its cell is reusable and at ground. Obstacles are skipped with no rerouting,
so the placed creek may be disconnected. No same-geometry-under-reversal promise
follows from symmetric endpoint seeding.

**P3-TERRAIN-008:** manual eligibility remains source behavior. Generated
vegetation filters underwater cells as a new generator rule; generation uses its
own deterministic stage draws, while committed manual forest uses the P1 forest
stream and creek uses its local seed. Normalization may erase narrow water
features, so generated topology validation follows it.

**P3-TERRAIN-009:** source corner/Mountain2/terrace/cut methods do not book money.
The `LandBuilderContribution.create(Location,Location,owned)` wrapper normalizes
grid bounds, invokes its builder, then charges SUBSIDIARIES for the **entire
inclusive selected rectangle when owned**, regardless of skipped/zero-tree cells.
For 2×3 at price100 the charge is600 whether six, one or zero cells were placed.
Unowned initialization costs zero. A forest Mountain's entity value stores one
contribution price, not tree-count×price; creek value is zero. Keep these separate
from future land acquisition/removal accounting rather than inventing one terrain
cost multiplier or cost exemption from the authoring flag.

## Terraces, cuts and construction mode

Terrace placement requires Mountain. It backs up four offsets only, removes the
Mountain, raises ground one and installs a non-reclaimable/unowned Terrace at the
old z. Removal requires Terrace with no occupant above; it removes the Terrace,
lowers ground one and recreates Mountain from the saved offsets. Trees, ground
overlay and original ownership are **not** backed up; the recreated Mountain is
unowned and undecorated. Preserve these source transitions, with loss shown in
the preview and top-capacity/obstruction checks applied before publication.

Cut placement requires Mountain and rejects if any of its four diagonal neighbor
ground levels is higher than the selected ground. It removes Mountain without
changing ground, creating an intentional discontinuity. Cut removal requires an
empty ground cell. For each corner, it takes maximum fine height over the four
touching samples at the selected z (Mountain offset at that same z or plain
`4*z`), subtracts selected ground×4, rejects any offset>4, does nothing if all0,
otherwise creates Mountain. This is same-z reconstruction, not an arbitrary
terrain healing solver. The source's 2009 guard rejects invalid byte offsets;
the port checks signed values before narrowing. Terrain classification must
permit legitimate cut/terrace discontinuities without treating malformed natural
corner data as a cut implicitly.

Stable core/plugins contain only the construction flag getter, field and
assignment in `Core.init`; no source economic/placement/timing consumer was found.
**P3-TERRAIN-006:** use explicit world-owned mode, default off, Owner-controlled
entry/exit, persisted with the world. It grants no legality/money/role bypass,
does not alter shared pause, and does not disable ordinary Operator-authorized
terrain tools. Its workflow ships P3-16; excluded launcher/debug/video tools stay
excluded. Pending map edits remain subject to existing epoch/revision/draft rules.

## Workflow and text handoff

The review has reciprocal source/workflow/slice mappings; the maintained
[action](web-parity-actions.md#p3-01-terrain-workflow-bindings) and
[manual](web-parity-manual.md#p3-01-terrain-source-reconciliation) overlays retain
those destinations. Historical developer pages remain donors/context, not rules.

| Family | Manual and developer anchors | Source destination and runtime owner |
| --- | --- | --- |
| New/heightmap | MAN-M01/O02/E02/A02/A19; DEV-P09/P17 | Empty dialog, LoadDialog, TerrainOnlyNewGameContribution; P3-06/07 |
| Corner | MAN-T09/M05/O06/A14/A18; DEV-P28 | MountainController and shared-corner geometry; P3-05/13 |
| Rectangle/repair | MAN-T09/O06/A18; DEV-P15/P28; EXAV-13 | Mountain2 and atomic source-local edits; P3-14 |
| Terraces/cuts | MAN-M05/A14; DEV-P15/P28; EXAV-13 | Four terrace strategies and backed-up offsets; P3-15 |
| Forest/creek | MAN-M05/A16; DEV-P07/P10 | Land wrapper, ForestBuilder/CreekBuilder and recipes; P3-10/11 |
| Cuts/picking | MAN-M02/W11/D02/A14; DEV-P28 | Ground/Mountain disambiguators and renderer; P3-05/13; full cross-system cut acceptance continues P4/P7/P9 |

[59 source-bound text records](parity/terrain-localization-review.json) retain
exact Japanese, UTF-8 text hash, source file/line/expression, proposed English,
exact-carryover/meaning-adapted/new-translation classification and historical
English donor file/line/hash where available. Forest names/groups remain tied to
their contribution identities. Commented repair diagnostics are separate, not
reachable strings. New browser repair/repeat/partial-result wording is explicitly
an adaptation, not a Japanese desktop label. Existing P2 pack metadata/credits
translations remain authoritative for plugin titles/authors/descriptions; provider
supplied fixed-map metadata is content, not hardcoded new UI text.

These are reviewed **wording candidates, not shipped resources**. Owning playable
slices must add/review EN/JA resources, interpolation, errors, help and layouts
through the existing locale registry; P3-01 adds no product control. Numeric
defaults, separators, contributor IDs and author placeholders are data, not new
translation branches. Native dialogs' file chooser/filter adapts to a browser
image upload with the source formats accounted for by P3-07.

## Verification and next step

Reproduce the read-only specification check without an external checkout:

```sh
python3 scripts/review-terrain.py --check
python3 -m unittest discover -s scripts/tests
git diff --check
```

For optional raw-blob/symbol/text verification, explicitly name the archive:

```sh
python3 scripts/review-terrain.py --check --source-root /Users/dmark/dev/freetrain-exav-archive
```

The 77 vectors cover dimensions/coordinates/compass, water versus ground,
brightness/sampling/displacement/interpolation/smoothing, corner transitions,
Mountain2 sites/Shift/repair, forest scatter/eligibility, creek seed/route/skips,
booking and terrace/cut restoration. Controlled random draws isolate source
consumption/geometry; P1 remains the frozen RNG/seed authority. These small
vectors do not exercise whole-world import or validate generated topology.
Those runtime fixtures remain with P3-03–17. Negative checker tests reject
source/fixture/decision/mapping/text drift without needing the archive.

Verification on 27 October: the explicit-archive checker passes for all 40
hashes/symbol bindings, 77 vectors, forest manifest bindings and 59 source text
records; the offline check also passes. **All 141 Python tests pass**, including
eight new terrain-review guards. Markdown links, JSON references and whitespace
are checked. No .NET/client/browser suite was run for this specification-only
change; those results would not establish production terrain that is not yet
implemented.

P3-02 must now define immutable requests and candidate/job lifecycle, exact
generation defaults/ranges and seed contract, content capabilities and measured
memory/pixel/voxel/work/transfer budgets. P3-01 deliberately sets no new supported
world limit or save/protocol number. Deferred P2/native/cross-browser work retains
its actual status; no application/backend/browser execution is claimed here.
