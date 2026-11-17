# P7-17 forest authoring — 17 November 2026

The Land task (`O`) now offers all **18 bundled forest definitions** alongside
P7-16's 25 farmland choices: six species, each with existing ground, seasonal
mountain ground or grass ground. English/Japanese controls provide source-art
thumbnails and provenance, two map corners or coordinates, a fixed anchor level,
eligible/skipped cells, full-area Subsidiaries cost review, Confirm/Cancel and
free removal. Enter confirms a reviewed edit; Escape cancels it. The existing
terrain initialization tool remains the explicit unowned authoring path.

## Source bindings and lifecycle

Authority is ExAV 2.1.1.12, archive Git
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, `trunk/`.
[CITY-D014 and the P7 source contract](web-parity-city-contract.md),
[independent city fixtures](web-parity-city-fixtures.md) and the
[P3 forest scatter/generation specification](web-parity-forests.md) remain binding.

| Pinned source | Delivered behavior |
| --- | --- |
| `plugins/org.kohsuke.freetrain.land.forest/src/ForestBuilder.cs`, `plugin.xml` | All 18 definitions resolve to the existing reviewed terrain profile. Plant in X-outer/Y-inner order at each cell's exact ground height. Empty or silently reusable cells, including owned property/ground, are eligible; Mountain terrain remains eligible even when owned. Rail, buildings and other protected owners are skipped. Water adds no restriction. |
| `ForestBuilder.createRandomTrees` | Exactly `2*density` binary count draws, then x, y and sprite draws for each tree. Reuse P3's exact screen-coordinate transform and position-only selection sort. A zero-tree outcome preserves the old identity, ownership, decoration and population listeners. It still consumes the count draws; there is no reroll. |
| `core/contributions/land/LandBuilderContribution.cs` | Inclusive normalized rectangle, fixed anchor Z, price 100 × full selected area, including skipped cells and zero-tree results. One Subsidiaries spend after staged creation. No extra land purchase, entity-value payment or per-tree charge. Existing 4,096-cell admission bound applies. |
| `core/world/terrain/MountainVoxel.cs`, `setTrees` | Store actual tree triples and selected art on Mountain, retain its existing identity/corners, increment its revision, set owned=true and entity value=100. New flat Mountains have transparent occupancy. Replacement of non-Mountain reusable ground removes its property/ground/fences and detaches actual providers in both station-listener directions. |
| Registered builder versus `ForestVoxel.cs` | The active builder does not instantiate `ForestVoxel`. Neither active Mountain decoration nor that alternative defines aging, growth, production or a forest timer. No population provider or economic growth is invented. Existing authored season/night pictures follow the game clock without resampling. |
| `MountainVoxel.removeTrees` | Clear tree art/patterns/value and ownership, retaining nonflat terrain geometry and identity. The portable remover drops an empty flat decoration owner, as P3 already did. General demolition and rectangle ground removal share those semantics. |

Native `MountainVoxel.remove()` is an unimplemented geometry-removal stub, and
the inherited land remover selects the builder again. The browser's explicit
free teardown continues P3/P7-16's reviewed portable control adaptation using
`removeTrees` semantics. The previous portable general remover incorrectly
retained ownership on nonflat cleared forest; P7-17 corrects that mismatch.
Native remover fidelity is not claimed.

## Generation, authority and persistence

Planted and generated forests use the same canonical Mountain decoration,
realized triples, source art, picking, height cuts, value and removal lifecycle.
Generated forests remain unowned until a nonzero paid planting replaces them.
WORLDGEN-FOREST-1's mask/clustering, reserved starting area and cell-local RNG
remain separate modern generation policy. Editing preserves the original request,
seed/profile and generated-world provenance; it never regenerates other cells.

Paid planting uses the saved `forest` stream on a detached bank. Catalog art,
review, cancellation, seasons, rendering, join and load consume no shared draws.
An accepted zero-tree result advances only the forest stream and books the source
area cost. Exact saves preserve the realized forest and next draw.

`/api/land/preview` and `tradeLand` admit the existing `cover` action with a reviewed
forest definition; `removeCover` removes static ground and forests. Existing role,
epoch, content, geometry, quote and receipt fences apply. New or revised terrain
owners, reclaimed city ground, station listeners, heat notifications, allocator,
booking and random bank publish together. Detached allocator or validation failure
leaves the live state unchanged. No new wire fields or saved state are needed:
**Save11/protocol11/City1 and content identities remain unchanged.**

## Focused verification

Only relevant test selections ran. **179 unique C# tests**, **79 Deno tests** and
**seven unique Chromium workflows** pass across the recorded scoped runs, with no
skips or retry-dependent passes in those successful selections. Build checks also
verify generated contracts/art, content, locales, browser source policy and Svelte.
Commands, logs, failure corrections and artifact hashes are recorded in
`tmp/p7-17-validation.json`.

C# checks all 18 definitions with literal price/density/scatter inputs, private
preview, skipped/protected/water/bounds cases, zero outcomes preserving real farm
providers, nonzero replacement detaching both directions, owned slope replanting,
forest removal clearing ownership, actual generated forest replant/removal,
isolated draws and exact save continuation, late allocator rejection, Observer
rejection, wrong cost, stale quote and network receipt replay. Five 24-hour traces
include midnight and every seasonal transition without forest aging, new timers,
resampling or upkeep. Shared construction/heat and prior farmland, land-trading,
authoring, generation and demolition consumers have focused regression coverage.

Deno reconstructs actual C# placement/removal deltas, checks accounts/provenance,
and renders/alpha-picks all 18 definitions in all eight season/light combinations,
including cuts, catalog previews and malformed quote rejection. Existing terrain,
removal, localization, land/farm and browser-fixture checks pass.

Chromium covers EN desktop keyboard and JA phone touch planting, cancellation,
source-art thumbnails, exact ¥400 review, owned replanting with retained IDs,
real picks, peers, free removal, exact reopen and actual backend checkpoint restart.
The existing five land/farm browser workflows pass separately. The first forest
run exposed an incorrect test assumption that a station without any provider has
a reverse-membership row; the corrected focused rerun passes both workflows.
Inspected captures are in `tmp/p7-17-browser-forest/evidence/forest-{en,ja}-{review,placed}.png`.

The independent 27-case P3 forest oracle and 703-case/197-file city specification
remain verified. Exported C# snapshots/deltas are transport witnesses, separate
from independent source expectations. Native/human full-gallery, optional packs,
full-city capacity/performance and overall P7 acceptance retain their gates.
Ricefields, creeks and fences remain P7-18/19/20; P7-17 adds no growth subsystem.
