# P0-04 expanded ExAV girder/platform gallery

**Source visual review complete — 20 October 2026.** This closes the remaining
P0-04 specification/reconciliation work against stable ExAV **2.1.1.12**, archive
Git `883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, `trunk/`. It establishes offline
source expectations; it does not establish implemented game or runtime fidelity.

The [gallery artifact](parity/exav-geometry-gallery.json) records source hashes,
166 crop recipes, every draw layer/voxel/pass, camera center, scene parameters and
PNG hashes. The [review record](parity/exav-geometry-gallery-review.json) pins the
artifact and all **28 inspected contact sheets**. The generator's `--check` rejects
a stale review or changed contact hashes. [Reviewed source inputs](parity/exav-geometry-rules.json)
now include nine gallery source/manifest bindings; combined with the previous
geometry/dependency reviews, **98 distinct files** have reviewed dispositions.
The discovery ledger is retained without relabeling its lexical inventory as
runtime verification.

Generated images and HTML are local under
[the expanded gallery](../tmp/parity-p0/exav-rail-gallery/expanded/index.html).
The original [152 rail/car studies](../tmp/parity-p0/exav-rail-gallery/compositions.html)
and [60 pattern/16 support gallery](../tmp/parity-p0/exav-rail-gallery/index.html)
remain separate denominators, with their ExAV provenance retained.

## Coverage and source bindings

There are **332 raw day/night records**: 326 source crops and six explicit invalid
crop diagnostics. The **1,176 expanded composition records** comprise 1,164
source-formula studies and 12 diagnostic compositions. The count includes
intentional empty below-cut cases and equal full/at-cut cases; it is not a feature
completion percentage or a count of runtime scenarios.

Each parameter combination is rendered in day/night at cut 0, cut 1 and full
height. Representative rails/platforms occupy z=1; girder supports occupy z=0.
The camera stays fixed across cuts. The full-height contact sheets exhaust every
parameter combination; extra girder sheets show supports below the rail cut.

| Family | Parameter combinations before light/cut expansion | Records | Source bindings |
| --- | --- | ---: | --- |
| Girder | Eight directions; modulo-four direction maps to crop 3/1/0/2 | 48 | `StealSupportedRailContributionImpl.onInitComplete / RailImpl.drawBefore`, changed ExAV girder sheet; `BridgePierVoxel` |
| Plain thin platform | Four cardinal directions × roof on/off | 48 | `ThinPlatform.getSprite / PlainPlatform / RailRoadImpl.drawBefore/drawAfter` |
| Wide platform | Four directions × required first lane / both lanes | 48 | `FatPlatform.getSprite / FatPlatformVoxel.draw / RailRoadImpl.drawBefore`, YardChip then inherited rail, H/V voxel order |
| Passage outlook | Three styles × four directions × single/double width × bridge on/off | 288 | `PassagewayRail.getSprite/getFloatingSprite`, `ThinPlatform.PassagewayPlatform` |
| Stair outlook | Three styles × four directions × single/double width × up/down × roof on/off | 576 | Every one of the 32 stair entries per style, including aliases, source offsets and invalid index 23 |
| Standalone passage dispatch | Three styles × four directions | 72 | Ordinary two-argument rail-after dispatch omits the three-argument `PassagewayRail.drawAfter`; these studies deliberately show no floating passage |
| Roof run | Four directions × lengths 4/8 × roof on/off | 96 | `ThinPlatform` constructor's middle-quarter roof rule; projected voxel traversal |

Passage outlook does not take a roof parameter. Stairs and plain platforms do.
Double-width is a parameter study with its source context recorded: the left
neighbor must be an opposite-facing thin platform with a passage outlook. The
neighbor/host network is not simulated by these isolated studies. Roof runs repeat
representative 485 head art to expose roof coverage; they do not claim a simulated
head/body/tail consist. The existing 152-study gallery retains the distinct train
composition/reversal evidence.

## Verified draw and cut expectations

Thin platform outlooks draw before the car for E/S and after it for N/W. Bridged
E/S passage outlooks add the selected floating crop after the car. Girder art has
only a before-car pass. Wide platforms retain a separate center voxel and one or
two yard-rail voxels, sorted in the drawer's V/H/Z order. Warning badges, fences,
other traffic, terrain/water and station host simulation are outside these
representative connected-voxel scenes.

Cutting at the occupied voxel keeps its whole drawing, including tall roof/stair
pixels above the cut plane: TrafficVoxel/platform drawing does not slice the
sprite by heightCutDiff. Cutting below the voxel removes it; only lower girder
supports remain. Full and at-cut images match for these cases. This is distinct
from the mountain/structure cut handlers reviewed in
[the dependency report](web-parity-geometry-dependencies.md).

Thin-platform and all three passage/stair styles resolve their exact authored
night sheets from pinned `plugins/system/plugin.xml`. Other recipes use the
reviewed RGB888 quarter-color/light-marker formula. Native 555/565/24/32-bit pixel
format equivalence and original-game captures remain unverified. The gallery
never silently substitutes an original-FreeTrain image or optional plugin asset.

## EXAV-GAL-001 — invalid authored stair selection

Pinned `PassagewayRail` stair index **23** selects rectangle **(388,4,32,36)** in
all three day/night sheets, each **336×153**. This is the south-facing, downward,
double-width, roofed case. It is entirely out of the sheet. The extractor keeps
388 and records the invalid crop; it does not infer a corrected coordinate.

Six raw records and 12 included-voxel compositions show a red diagnostic cross.
Those pixels identify an invalid source request, **not** a prediction of what the
native API displays. Below-cut cases exclude the affected voxel normally. All
other source crops pass bounds checks; any additional out-of-bounds crop fails
generation instead of being silently replaced.

**Disposition:** source discrepancy specified and handed to P2/P6 for an explicit
port/correction decision and runtime case. No source correction is approved or
applied here. P0-04 source review can close because the source selection, defect,
visual-evidence limit and implementing owners are explicit.

## Reproduction and phase boundary

```sh
python3 scripts/review-exav-geometry.py --source-root /path/to/freetrain-exav-archive --gallery tmp/parity-p0/exav-rail-gallery
python3 scripts/review-exav-geometry.py --source-root /path/to/freetrain-exav-archive --check
python3 scripts/parity-rail-gallery.py --check
python3 -m unittest discover -s scripts/tests
git diff --check
```

`--check` regenerates all expanded PNGs in a temporary directory and compares the
recorded hashes/metadata and visual-review record. It needs an explicit archive
repository and reads pinned Git objects; ordinary application builds do not gain
a sibling-checkout dependency. PNGs/HTML stay under ignored `tmp/`; the committed
metadata/review record preserves the review when those local images are absent.

**P0-04 is closed at source-specification level.** P0-06–09 and overall P0 remain
open. P1–P6/P9 implementation, independent P0-06 fixtures, source-defect decisions,
full-world/native/browser rendering comparison and P10 save continuation retain
their existing gates. This batch changes no gameplay, protocol, save format,
archive source or optional-content distribution.

Closure validation on 20 October 2026: **88 Python tests pass**, including 11
ExAV geometry/crop/dispatch/cut/review checks. Fresh expanded regeneration,
ExAV `--check` (artifact and contact hashes), historical geometry `--check` and
`git diff --check` pass. No runtime/native/browser or save-continuation checks
were rerun for this source-specification closure.
