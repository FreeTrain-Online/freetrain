# P7-08 fixed city rendering and picking — 14 November 2026

Canonical fixed city structures and their live construction cells now enter the
terrain scene through their actual prepared recipes. All **66 bundled fixed
selectors** are covered. This delivers browser presentation and picking;
placement/removal controls remain P7-09, variable/half structures P7-10/11 and
independent ground/fence families their later slices. Save11/protocol10/City1,
content hashes, source images, authors/notices and admission limits are unchanged.

## Source rules and implementation

Authority is stable ExAV 2.1.1.12, archive Git
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, subtree `trunk`.
The [city contract](web-parity-city-contract.md),
[independent city fixtures](web-parity-city-fixtures.md) and
[fixed construction domain](web-parity-fixed-city.md) remain inputs.
`PThreeDimStructure`, `Commercial`, `ConstructionSite` and `QuarterViewDrawer`
were inspected directly; the P7 source ledger retains their pinned source hashes.

[`CityRenderPlan`](../web/src/lib/city-renderer.ts) resolves qualified fixed
bindings, checks the complete X/Y/Z recipe, and emits each authored sprite at its
real member position. Invisible members keep opaque occupancy. Missing recipes,
empty frame sets, malformed cells and unavailable appearance masks block readiness
even above the cut or offscreen. Variable/half art receives no fixed-family credit.
The renderer never samples random state, changes ownership, completes a site or
creates a population provider.

`PThreeDimStructure.VoxelImpl.draw` uses `cutZ - voxelZ + 1`: normal building art
starts at `cutZ >= baseZ + height`, or full-height mode. Every base footprint member
contributes a silhouette at intersecting cuts, including invisible authored cells;
upper members contribute no cut art. `Commercial.heightCutColor` is the literal
**RGB(179,115,51), `#b37333`**, unaffected by forced night/winter. A cut below the
base hides the building. Opaque occupancy suppresses ordinary ground, water and
underground chips; full-height traversal skips submerged members. Elevated sites
use actual terrain height for construction ground detail, rather than assuming
that a structure's base is ground level.

The shared [`constructionParts`](../web/src/lib/construction-renderer.ts) retains
`ConstructionSite.VoxelImpl.draw` order: rear fences, current ground/bones or
rear walls/bones/front walls, then front fences. Saved state selects empty,
bone1/2/3 or walled art, all seven ground objects and the saved door flag;
canonical neighbors select the exact bones and exterior edges. Upper empty cells
have no art. Construction ignores the silhouette rule and draws every traversed
cell up to the cut. The existing railway stationary sites now call the same
ordering function with their existing state and geometry.

City operations share source V/H/Z traversal with terrain, trains, railway and
platforms. Alpha picking walks that same ordered scene backwards; silhouette picks
use its exact polygons. Every pick returns the stable canonical owner and actual
member position. The existing bilingual inspector exposes height and original
content provenance; selecting another member refreshes its position. Generic
progress and placement/removal workflows remain P7-09.

City footprints and recipe overhangs participate in retained column signatures,
neighbor grid masks, viewport bounds and local raster damage. Stage updates,
completion identity replacement and removal publish pixels and picks together.
Unrelated operations and decoded images remain reusable. The existing forced
night/winter, seasonal art and zoom/display paths apply to city layers.

## Focused verification

- **81 selected .NET tests pass**, covering FixedCityConstruction, CityConstruction,
  CityState, CityPersistence and StationaryConstruction. Browser setup also runs
  13 existing fixture tests. No server/domain behavior changed.
- **78 selected web tests pass**, covering city state/rendering, terrain rendering,
  station rendering and railway rendering. City cases bind all 66 fixed selectors
  to every construction stage, all eight appearances, all intersecting cuts,
  invisible occupancy, saved door/ground/connectivity, water/underground,
  fractional zoom picks, malformed art, immutable prior scenes and local damage.
- The browser oracle independently evaluates pinned XML `DefaultSpriteLoader.load3D`
  rectangles/anchors/member geometry for all 66 selectors in eight appearances:
  **63,360,000 pixels and 63,360,000 alpha picks** match. Expected geometry does not
  come from production recipes or the production renderer. Original prepared
  source sheets supply the pixels, with their real transparency and effects.
- The construction oracle uses literal source sheet rectangles, anchors, bones
  connectivity and pass order, separately from `constructionParts`: all five
  stages × seven ground values × two door modes in day/night match
  **9,240,000 pixels and the same number of alpha picks** on a 2×3×3 building.
  The walled gallery retains one bone3 cell so its construction state is legal.
- The cut gallery checks literal brown pixels and stable owner picks for **168
  base members**, including invisible members, under forced winter/night.
- Retained build/stage/completion/removal rasters and **120,000 picks** match fresh
  full scenes at 0.75× / DPR2 beside a train, station and thin platform.
- Actual saved P7-07 construction completes by the real clock in EN desktop and
  JA narrow DPR2 peers. Site/completed owner picks, forced brown cuts, content
  provenance, unchanged saves/camera and no page errors are checked. The existing
  fixed-city continuation, station and railway rendering regressions are selected.

The focused browser run contains **12 passing Chromium workflows**. Source fixture
checks retain **703 independent cases / 197 original files**; source review and
project boundaries pass. Both builds, generated contracts/content/locales and
Svelte checks pass with zero diagnostics. Vite retains its existing chunk-size
advisory. No complete suite is run.

```sh
dotnet build tests/FreeTrain.Online.Tests --no-restore -m:1 -nodeReuse:false -p:UseSharedCompilation=false
dotnet test tests/FreeTrain.Online.Tests --no-build --no-restore -m:1 -nodeReuse:false --filter 'FullyQualifiedName~FixedCityConstructionTests|FullyQualifiedName~CityConstructionTests|FullyQualifiedName~CityStateTests|FullyQualifiedName~CityPersistenceTests|FullyQualifiedName~StationaryConstructionTests'
cd web
deno test -A --no-check tests/city-renderer.test.ts tests/railway-renderer.test.ts tests/station-renderer.test.ts tests/terrain-renderer.test.ts tests/city-state.test.ts
deno task build
FREETRAIN_ONLINE_TEST_SERVER_CONFIGURATION=Debug deno task test:browser:run city-rendering.spec.ts fixed-city.spec.ts station-rendering.spec.ts railway-rendering.spec.ts --project=canonical --workers=1
cd ..
python3 scripts/city-fixtures.py --check --source-root /path/to/freetrain-exav-archive
python3 scripts/review-city.py --check
python3 scripts/check-project-boundaries.py
git diff --check
```

Logs and hashes: `tmp/p7-08-validation.json`. Browser galleries, reports and
inspected EN/JA captures: `tmp/p7-08-browser-evidence/`.
Initial test-fixture iterations corrected callback allocation/order, empty upper
stage expectations, offscreen camera positioning and single-bundle harness imports;
the domain validators were preserved. The .NET runner and browser servers require
local socket access and were run with that authorization.

This is automated prepared-art/browser evidence with inspected captures. Original
runtime human/native pixel fidelity, other engines, optional packs, capacity and
performance remain with their recorded later owners. No P7 phase-exit credit or
P7-09 placement-control credit follows from this presentation slice.
