# P4-08 live railway rendering and picking — 31 October 2026

P4-08 displays persisted mixed-world rails and piers using bundled executable
recipes in the existing terrain canvas. It delivers read-only railway inspection
in English and Japanese. **Next: P4-09 normal-rail construction controls and
session commands.** New-world generation still selects the terrain profile;
Save8/protocol6 and the catalog identity are unchanged. The asset-free P1
reference loop remains a separate diagnostic profile.

Authority is stable ExAV 2.1.1.12, archive
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, `trunk/`:
`QuarterViewDrawer`, `Traffic`, `RailRoad`, `RailPattern`, `BridgePierVoxel`,
`Disambiguators`, `SlopeRailRoad` and the signal drawing methods. The
[railway contract](web-parity-railway-contract.md),
[literal source fixtures](web-parity-railway-fixtures.md) and
[P4-07 construction handoff](web-parity-railway-construction.md) retain the
reviewed source quirks. These checks are source-derived and browser-executed;
they are not original-runtime/native pixel-identity evidence.

## Rendering and visibility

[RailwayRenderPlan](../web/src/lib/railway-renderer.ts) resolves exact
pack/contribution/recipe bindings and saved selectors. Normal and junction
patterns use their literal rail-sheet crops and anchors. Piers retain their
saved style; paired slope fillers remain invisible while still controlling
holes. Existing slope/support, bridge, girder, tunnel, garage, signal and pole
bindings have explicit rendering dispatch. This gives their saved art a consumer;
it does not implement their later construction or movement workflows.

[TerrainScene](../web/src/lib/terrain-renderer.ts) integrates those operations
within each stored V/H column, ordered by Z. Ground precedes the voxel at its
level. Traffic orders accessory-before, rail-before, the reserved P5 car slot,
rail-after and accessory-after; P7 retains the road consumer. The current mixed
profile has no executable car/platform/road bindings. Such consumers must supply
their own exact state/poses and dispatch before they can be revealed; this step
does not invent a train pose from an identity alone.

GEO-007 uses the source signal calls: `needsToDrawAfter == true` draws the signal
before the track, and false draws it after the car slot. The live adapter reverses
the descriptive P2 signal stages to preserve those calls; gallery/catalog bytes
are unchanged. Signal track pixels retain the signal's inspector/controller
identity even though their art comes from a normal pattern recipe.

Cuts include Z equal to the cut and exclude greater Z. A full view starts at the
water plane; a cut view traverses from Z0 and exposes underwater terrain and
rails. The source one-level-lower start for a hole at the water plane is retained.
Ground/underground chips consult the current voxel's stored `drawGroundBelow`
and the voxel below's `drawGroundAbove`, including source floor/ceiling exceptions.
Flags are not regenerated from current terrain. The drawer's visible pier cut
difference is at least one, so piers remain opaque at the cut; no global offset
makes the source's unreachable alpha-at-zero branch execute.

Every saved railway binding is preflighted, including offscreen and cut voxels.
Missing recipes, missing active-variant masks, invalid crops and image/hash/decode
failures block readiness. There is no mixed-world line-drawing fallback.

## Picking, inspector and cache lifecycle

Visible inspector picks traverse the actual operations in reverse, using the same
variant alpha mask and source anchor as drawing. Targets carry the stable owner
ID, exact XYZ, component, qualified definition and optional controller ID. The
inspector resolves current component definitions after updates and shows the
supported rail's owner for a pier. Transparent pixels expose the lower operation;
invisible slope occupancy creates no visible inspector target.

`pickRailwayLocation` separately implements the source controller disambiguators:
scan from the cut down for rail-or-ground or actual ground, or constrain the
second point to the first point's stored level. Stored H/V/Z bounds apply; an
unselectable/outside ray returns no target. P4-09 must use this geometric policy
for construction, rather than interpreting a sprite/inspector hit as a legal
endpoint. `MapRenderer.pickLocation` carries this policy through the completed
scene/viewport, including old geometry during refinement. Domain construction
still revalidates actual occupancy and water.

[MapRenderer](../web/src/lib/map-renderer.ts) keys scenes by world epoch, pack
identity, presentation, terrain revision and **mixed geometry revision**. Calendar
updates alone do not rebuild geometry. Mixed validation shares the terrain's
validated spatial lookup with its combined read, preserving exact terrain state.
A changed-column cache reuses unchanged operation objects; its dependency key
includes local terrain definitions, neighboring cliff geometry, water and all
saved railway state in that column. It is bounded at 65,536 cells.

Image decodes remain shared across geometry and view changes. Catalog-hash changes
replace the catalog/image cache. Epoch/content/presentation changes clear old
presentation; late asynchronous completions cannot replace newer scenes. Large
mixed worlds use the existing bounded two-buffer TerrainViewport: old visible
pixels retain their old matching picks until a complete replacement is published.
Small worlds replace their completed scene as one unit. The canvas and camera
survive panels and ordinary geometry updates.

For review before P4-09, load
[`mixed-v8.json`](../prototype/protocol/saves/mixed-v8.json) through Save and restore.
This small synthetic saved world exposes elevated rail/pier and signal art in its
full view, with the tunnel visible in the lower cut views. It is a renderer fixture,
not a playable city or construction demonstration.

## Verification

The focused client suite covers all 28 normal/junction patterns in four seasons
and both light modes, checked against P4-02's literal rectangles/anchors. It also
checks asymmetric V/H/Z ordering, buried/elevated/underwater cuts, independent
controller targets, source signal dispatch, missing/invalid offscreen recipes,
cache reuse/invalidation, exact snapshot immutability, and all 15 legal literal
ramp fixtures at every cut. A large mixed viewport fixture verifies old/new pick
publication and reuse of the two backing buffers.

Chromium scenarios check prepared-image pixel equality, rail/pier IDs, exact cut
inclusion, English/Japanese inspectors (including narrow DPR2), two-peer save
replacement, save/reopen, variant/panel decode reuse and failed-media readiness.
A test-injected **validated literal mixed delta** on a real Step publication
verifies railway removal with no terrain revision change. This is transport/render
coverage, not a claim that a live railway construction command exists yet.

Reproduce from the repository root:

```sh
deno task --cwd web test
deno task --cwd web build
python3 scripts/railway-fixtures.py --check
python3 scripts/review-railway.py --check
python3 -m unittest discover -s scripts/tests
python3 scripts/check-project-boundaries.py
dotnet build FreeTrain.Online.slnx -c Release --no-restore -m:1 -p:UseSharedCompilation=false
dotnet test FreeTrain.Online.slnx -c Release --no-build --no-restore -m:1
cd web
deno run -A npm:@playwright/test@1.64.1 test
```

Fresh verification passes **168/168 client tests**, including **11/11 focused
railway rendering tests**, **865/865 .NET tests**, and **161 Python tests with
one existing skip**. Production build, generated contracts/content/locales,
source fixture/review, project-boundary and whitespace checks pass. Chromium
passes **126/126 broad scenarios** plus **3/3 focused scenarios on the final
production build** after the completed-scene controller-picking API was added.
The broad run includes all 6,496 recipe gallery season/light variants.

Local logs: `tmp/p4-08-client-final.log`, `tmp/p4-08-targeted-final.log`,
`tmp/p4-08-build-final.log`, `tmp/p4-08-dotnet.log`, `tmp/p4-08-python.log`,
`tmp/p4-08-browser-broad.log` and `tmp/p4-08-browser-final.log`. EN and Japanese
DPR2 screenshots are under `tmp/browser-evidence-QXHixN/railway/`; representative
inspection confirms readable inspectors, exact fixture placement and retained
canvas layout. Sandboxed browser startup could not start the local services;
successful browser/.NET runs use the allowed loopback-socket context.
Native/human fidelity, deferred browser engines, optional-suite acceptance and
P4 capacity/full-city gates remain open. This step makes no measured gameplay
performance or native display/accessibility claim.
