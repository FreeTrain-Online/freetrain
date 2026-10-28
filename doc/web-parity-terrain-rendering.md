# P3-05 terrain rendering and picking — 28 October 2026

P3-05 connects the validated terrain read model to the existing browser canvas.
Save 6 terrain worlds can now be loaded and reopened for viewing, inspection,
clock controls and read-only location checks. **Next: P3-06 empty-world creation.**
World creation, terrain editing, generators and size presets remain disabled;
P3 and the previously recorded P2/native/platform acceptance gates remain open.

[TerrainScene](../web/src/lib/terrain-renderer.ts) builds an ordered scene from
actual cells, entities and realized vegetation. [MapRenderer](../web/src/lib/map-renderer.ts)
dispatches the exact terrain profile separately from the existing reference and
historical prototype renderers. No flat substitute, random tree placement or
regeneration is used when loading a save.

## Source behavior and prepared media

The pinned bundled source under `content/bundled-v2/source/trunk/` supplies
`core/views/QuarterViewDrawer.cs`, `core/world/terrain/MountainVoxel.cs`,
`core/framework/graphics/ColorMap.cs`, `core/res/mountainPalette.xml` and the
Terrace/Creek implementations. This is the same stable ExAV revision as the
[P3-01 contract](web-parity-terrain-contract.md); no original-FreeTrain runtime
rules are introduced.

- Traverse stored columns in source V/H order, then lower-to-higher occupants.
  Include all stored border cells. Project with the canonical H/V origin;
  convert sprite coordinates explicitly to the P2 recipe origin.
- Ground uses `system:ground/spring`, `winter`, `water` and `underground` recipes.
  Full-height viewing starts at global water level, hiding submerged occupants
  and drawing the opaque water plane in source RGB `(0,114,188)`. A lower height
  cut reveals underwater ground and occupants, omits the sea surface, and draws
  underground chips where the cut is below ground. Equality with water remains dry.
- Mountain NE/SE/SW/NW vertices are `(16,-4*NE)`, `(32,8-4*SE)`,
  `(16,16-4*SW)`, `(0,8-4*NW)` relative to the voxel top-left. The palette/light
  calculation, seasonal colors, underwater colors and night quarter-channel
  mapping follow `MountainVoxel`/`ColorMap`. Its cut face uses palette entry 3,
  retaining the source's direct, non-night-mapped color. Flat Mountains reveal
  the ground beneath; saturated Mountains retain their stored identities.
- Cliff recipes select source side and the two saved quarter offsets. West/South
  neighbor checks retain same-z Mountain suppression and ground comparisons;
  outside ground is zero. Terraces use their exact prepared sprite below the
  surface ground, with the surface and its occupant painted afterward.
- Forest base/`-m`/`-g` recipes retain their own ground overlays, shown only on
  flat Mountains. Trees use the saved sprite and array order, source integer
  slope offset and prepared anchors. Rendering consumes no random draws.
  Creek connection bits select the source mask, with single-ended N/S and E/W
  normalized only for sprite selection; the stored masks remain unchanged.

The [palette transcription](../web/src/lib/terrain-palettes.json) retains the
source path and raw SHA-256. An offline client test compares every HSV entry and
hash directly to the checked-in bundled source. Sprites use the P2 verified
catalog hash, seasonal/day/night surfaces and alpha masks through
[RecipeImageCache and ReadyScene](../web/src/lib/content-recipes.ts).
Terrain supplies explicit ordering when mixing those sprites with geometry;
the gallery's ordinary traversal is unchanged.

Canvas polygons implement the quarter vertices and source shading. Native GDI
edge rasterization has not been captured or established as pixel-identical.
Existing P2 invalid-source-crop diagnostics remain unchanged and are not silently
repaired. Full cross-system cuts/occlusion with future rails, buildings and other
occupants remain with P4/P7/P9; this profile still admits no railway entities.

## Picking, reveal and lifecycle

Picking reverses the actual paint order. Prepared layers use their variant's
opaque-pixel masks; slopes and water use their projected polygons. Ground cells
return stable `cell:H,V` targets and the displayed level. Occupants return their
persisted entity ID, anchor and surface kind. Transparent sprite corners do not
capture an entire bounding rectangle, and no outside cell is fabricated.
Selection and location previews use the existing canvas overlay.

The browser advertises `terrain-content-1`. A validated replacement selecting a
different supported profile re-bootstraps the authoritative exact pack identity
before reveal. Unknown identities still fail validation. New epochs reset cuts,
interaction and readiness; pending old scene results cannot publish after a newer
geometry/epoch/season/night/cut request. A view change clears the previous canvas
and hit map while retaining mounted panels. Initial reveal waits for the catalog
checksum and all required decoded images; failures enter the existing renderer
failure gate. Readiness does not enable gameplay handlers or creation presets.

The canvas, camera and image-cache owner survive panel and view changes. Terrain
scenes are cached for the current geometry/epoch/variant/cut rather than rebuilt
for every clock minute. Catalog media promises share decodes and retain retry
behavior. Geometry and sprite bounds are culled during painting; whole-scene
preparation and image readiness still enumerate required layers. These small
fixtures establish no large-world memory/frame-time capacity claim. The
[P3-04 dense transfer limits](web-parity-terrain-transport.md#bounds-and-measured-limits)
remain unresolved for preset admission.

EN/JA resources label terrain, ground, slope, water, underground cut, forest,
creek, cliff and terrace, and explain current viewing/editing availability.
These inspector labels and availability prose are browser presentation adaptations
of the source terrain classes and draw paths, not newly exposed source tool
commands. Existing tool labels, source-time formatting, pack translations, roles,
save confirmation and spatial-query contracts are retained.

## Verification

[Eight client renderer cases](../web/tests/terrain-renderer.test.ts) bind literal
quarter vertices/tree arithmetic/colors, independent slope/ground/water/underwater/
cut hit points, asymmetric stored boundaries, all eight seasonal/light combinations,
all 18 forest treatments, every Creek mask, terrace occlusion, cliff recipes,
cache reuse, input preservation, missing-recipe rejection, palette transcription
and preserved crop diagnostics. These supplement the shared P3-04 wire tests;
source fixtures and runtime rendering tests remain distinct evidence.

[Chromium terrain scenarios](../web/tests/browser/terrain.spec.ts) load the real
server Save 6 fixture, reconnect across the profile change, pick independent
water/tree pixels, inspect the entity, change cut/light/season, preserve canvas
and cache across panels, download/reopen without image input, exercise Japanese
narrow DPR2 presentation, block reveal on failed image decoding and reject late
night-image completion after a newer day view. Screenshots are retained under
the run's `tmp/browser-evidence-*/terrain/` directory.

Reproduce:

```sh
cd web
deno task build
deno task test
node node_modules/@playwright/test/cli.js test
```

Chromium and the existing .NET/server regressions need local socket/subprocess
access. The initial sandboxed browser launch did not make progress; the completed
runs use that access. Logs are `tmp/p3-05-{web-build,client,browser,dotnet,python}.log`
and `tmp/p3-05-browser-terrain.log`. This work does not claim deployment/container,
original desktop capture, native IME/device or deferred browser-engine acceptance.

Validation on 28 October: production build, generated contracts/content, locale/type
checks and **116 client / 414 .NET / 141 Python tests pass**. Offline and explicit
archive P3-01 checks, project boundaries, changed documentation links and whitespace
checks pass. The full Chromium run passed **73 of 74** cases, with one existing
multiplayer train-picking assertion failing. Rerunning all three multiplayer cases
and all three final terrain cases passes **6/6**, including the added delayed-image
case. Together these runs cover **75 distinct canonical/terrain/historical cases**;
the full run is not reported as a clean single-run pass. Optional source-directory
and prepared-directory configurations each pass **3/3**. The final focused log is
`tmp/p3-05-browser-final.log`; optional logs are
`tmp/p3-05-browser-optional-{source,prepared}.log`. No native/device or additional
browser engine was tested.
