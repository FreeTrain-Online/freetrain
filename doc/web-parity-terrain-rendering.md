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

[Client renderer cases](../web/tests/terrain-renderer.test.ts) bind literal
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

## Synthetic connection-screen landscape

The [local demo](../web/src/lib/demo-scene.ts) uses the terrain model and renderer
instead of the historical prototype snapshot. A fixed 48×96×16 coastal landscape
has continuous shared quarter samples, six forest species with realized tree
positions and an inland Terrace garden. One global water plane
covers submerged ground. The lowest dry beach level is tree-free. No decorative
Creek occupants remain in this backdrop; ordinary terrain Creek rendering stays
supported for loaded worlds.

A [fixed coastal alignment](../web/src/lib/demo-railway-path.ts) circles the whole
island, including the northern peninsula, with **136 rail cells and 18 bends**.
Straight cardinal/diagonal runs are at least four cells long, with stretches up
to twelve cells. The route sits slightly inland on the lowest beach level; tests
check dry ground, a nearby shoreline, complete island coverage, closure, unique
cells and reciprocal source normal rail ports. A narrow, clear level bed is
blended into a shared corner field. Its touching neighbours are repaired until
every cell fits quarter offsets 0…4, preserving continuous slopes throughout the
island. The backdrop has no artificial cut cells or removed hillside occupants;
only the intentional small Terrace garden retains its separate platform geometry.

[DemoRailway](../web/src/lib/demo-railway.ts) uses prepared `system:pattern/*`
recipes and the bundled `{3983B298-ADB1-4905-94E5-03B7AAE5A221}` three-car
composition. Head/body/tail frames retain the prepared anchors plus the source
Train.Car.draw nine-pixel vertical offset. Each car samples its own continuous
path position and 16-angle tangent, so followers turn independently and the train
wraps around the loop without jumping. Terrain and rails are cached in one
viewport/DPR bitmap; only the train is drawn again during local animation capped at 30 fps. The bitmap is released when the reference renderer takes over.
Animation stops while hidden or under `prefers-reduced-motion: reduce` and does
not advance from a game clock, consume gameplay RNG or send commands.

Rails and cars are presentation layers outside the frozen terrain snapshot's
`tracks`, `trains` and spatial entities. They have no gameplay/picking/authority
registration, session publication or save path. This implements neither a
playable railway nor the P3-06–12 creation/generation work; the terrain profile
continues to require zero gameplay rails/trains. The demo read retains its local
epoch. Fixed landscape samples and presentation alignment are not generation or
train-simulation parity evidence.

Desktop framing shows the complete island/circuit; portrait uses a closer view.
The local background extends the surrounding sea beyond stored map edges. The
same mounted canvas changes to the synchronized live read on connection and
removes decorative train metadata. Demo asset failure leaves session controls
usable over the plain background. Sea diamonds seal shared edges at fractional
zoom to avoid antialiased gaps through the opaque water plane.

Client cases validate the synthetic graph, natural corner continuity, request
hash, immutable input, zero gameplay rails/trains, beach tree exclusion, absence
of creeks, source rail ports, long straight runs, continuous car spacing/wrapping
and prepared recipe readiness/rejection. Chromium holds bootstrap to inspect
1280×900 and 360×800 backdrops, checks actual canvas animation, hidden-page and
reduced-motion pausing, image-cache reuse and a continuous sea patch, then joins
the real world on the same canvas. Existing terrain and session-gate/lifecycle
regressions remain part of the focused verification.

Logs: `tmp/demo-railway-{build,client,browser}.log`. Browser checks use isolated
5180/5181 and 4273/4274 ports because another local service occupies port 5080.
Screenshots are in the run's
`tmp/browser-evidence-*/terrain/connection-landscape-{desktop,portrait}.png`.

Final coastal-loop verification: production build, generated/content/locale/type
checks and **119 client cases** pass; all **14 Chromium terrain/session-gate/
lifecycle cases** pass. Desktop and portrait screenshots were visually reviewed.
No backend, native/device or additional browser-engine testing is claimed for
this presentation-only change.

Terrain seam repair: removed the deliberate northeast ledges and isolated rail-bed
cuts. Shared corner samples now blend the level bed into neighbouring hills;
bounded integer samples only decrease during normalization until every cell is
representable. A client regression restores the underlying garden surface and
validates the entire island as natural terrain, with no cut/terrace exemptions to
hide unmatched corners. Chromium checks the former gap area for ocean-coloured
pixels and exports `terrain/island-terrain.png` without the connection dialog.
The production build, **120 client cases** and all **14 Chromium terrain/session
cases** pass. Logs: `tmp/demo-terrain-mend-{build,client,browser}.log`.


## Localized terrain redraw — 1 November 2026

An owner-reported delay after general terrain edits exposed a browser identity
issue and unnecessary viewport rebuilding. The server already validates a sparse
draft's changed columns and all touching corner neighbors. An isolated Debug
profile of a 400×600×24 existing terrain world measured 6.41 ms planning,
17.49 ms draft completion/validation, 3.26 ms admission, 0.26 ms replacement,
1.88 ms snapshot publication and 2.09 ms delta construction (about 32 ms total).
It did not need another configurable validation radius.

The shell now receives replacement model states through Svelte's `$state.raw`.
Its immutable snapshots retain the exact identity certified by transport
validation, avoiding a proxy-induced full validation when preparing the scene.
Validated terrain deltas attach weak predecessor metadata containing changed
columns and their eight-neighbor ring. Clock-only reads share that certificate;
a bounded traversal can combine edits received before a render without retaining
world history.

The viewport replaces only those columns' drawing/picking operations. Damage
includes both old and new polygon/sprite bounds, covering removed trees, shifted
canopies and exposed cliffs. All overlapping operations repaint in source V/H/Z
order into the spare buffer, and matching pixels/picks publish together. Large
changes (over 2,048 affected columns or one-third of the raster), missing
certificates, world/variant/cut changes, global water changes and mixed railway
scenes retain the established full redraw. No legality, source selection,
geometry limits, save format or receipt timeouts change.

The isolated Chromium Debug empty-world comparison improved click-to-visible
feedback from 302.1 ms to 80.8 ms at 400×600×24. A separate authored-forest
regression measured 81.5 / 51.3 / 50.5 / 66.5 ms through two raises and two lowers;
the post-arrival redraw portions were 16.4–19.3 ms. Each local result is pixel
identical to an independently forced full day render. The test also verifies
removed artwork clears, an overlapping forest outside the validated ring keeps
identical saved bytes, and the canvas/camera survive. These are local measurements without injected
RTT, not a replacement for the recorded full-size capacity budgets or accepted
dense-edit latency miss.

Validation: **991/991 .NET tests**, **198/198 client tests** and production /
generated/content/locales/type checks pass. The focused Debug browser case passes. The configured browser
sweep passed 173/175 cases; an existing authoring test still targeted “Cancel
preview” instead of the mode confirmation’s “Cancel.” Its timeout skipped world
restoration and caused the startup-world failure. Correcting that scoped test
selector yields **5/5** authoring/startup cases on a fresh service, resolving both
failures without further product changes. The other 173 cases remain unchanged.
Logs: `tmp/terrain-local-{validation-before,render-before,render-after,forest-browser,client,render-build,dotnet,browser-regression,authoring-recheck}.log`.
