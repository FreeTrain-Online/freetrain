# P5-09 moving stock art — 3 November 2026

Deployed fleet cars now render in the live mixed world through their exact
prepared recipes. [Train pose selection](../web/src/lib/train-renderer.ts) and
[Traffic composition](../web/src/lib/railway-renderer.ts) replace the former
unsupported-car guard. Purchase/deployment browser commands remain P5-10/11;
advanced movement, controller workflows and fleet capacity keep their numbered
owners. Save9/protocol7, content hashes, assets, schemas and resource limits are
unchanged. No new package or asset dependency is introduced.

## Source contract and presentation

Stable ExAV 2.1.1.12, Git `883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, is the
authority. The [P5 source contract](web-parity-train-contract.md),
[source review](parity/train-review.json), [independent pose literals](parity/train-fixtures.json)
and [P4 rendering contract](web-parity-railway-rendering.md) remain the inputs.

| Pinned source | Delivered behavior |
| --- | --- |
| `core/world/rail/Train.cs`, `TrainCar.draw` | Initial Y−9; sixteen planar angles; curve X±3/Y±2; four ramp height offsets 0/−4/+8/+4; formation reversal changes orientation independently of recipe wrappers |
| `SingleRailRoad.cs`, `JunctionRailRoad.cs`, `SpecialPurposeRailRoad.cs` | Query the current rail; ordinary curves prefer straight, then left/right; selectable junction followers query their predecessor's saved location; the simple head chooses straight; special rails retain heading |
| `SymTrainCarImpl.cs`, `AsymTrainCarImpl.cs`, `ReverseTrainCarImpl.cs`, `ColoredTrainCarImpl.cs` | Exact symmetric 8+4 and asymmetric 16+8 selectors, precompiled reverse-tail direction/climbing changes and authored color surfaces |
| `core/world/Traffic.cs`, `draw` | Accessory-before → rail-before → car → rail-after → accessory-after; future roads retain their earlier before pass when P7 supplies their consumer |
| `TunnelRail.cs`, `invalidateVoxel` | An adjacent S/W tunnel hides an interior car; source foregrounds and V/H/Z traversal preserve that occlusion |

All six bundled train formations resolve their existing eight car recipes,
including both mixed head/body/reversed-tail compositions and the color-test
train. Exact masks and seasonal/day/night surfaces come from the unchanged
verified catalog. Every occupied car is preflighted, including offscreen and
height-cut cells. Missing pose, recipe, selector, variant or image fails readiness
instead of receiving substitute art. Outside and unplaced cars have no Traffic
slot and produce no sprite.

The current simple-junction movement boundary remains explicit. A selectable
junction head using an unimplemented controller decision is unavailable, matching
P5-08's dispatcher boundary. Later controller slices must supply their pure draw
decision alongside movement; painting never advances a controller or randomness.

## Retained rendering, occlusion and picking

Car operations participate in the existing terrain/rail source traversal, cuts,
foregrounds and per-column operation cache. Signatures include the resolved car
frame, anchor and stable target, so curve/predecessor and formation changes cannot
reuse an obsolete pose. Unchanged columns reuse their existing operations.

Large mixed worlds now share the existing two-buffer local redraw path.
Changed old/new car columns contribute their complete old/new bounds. The clipped
redraw restores every overlapping unchanged terrain, rail and accessory operation
in source order. Large damage or a global/presentation change keeps the existing
full progressive redraw. Assets and the front canvas remain retained; the image
cache shares decodes across movement and world reopen. Completed pixels and picks
publish together. `data-render-revision` identifies the completed visible revision
for browser verification, including publications whose geometry is unchanged.

The native tunnel invalidation shortcut suppresses an interior update when
another tunnel lies S/W. This browser implementation conservatively repaints
those changed columns so their retained state/pick operations also stay current.
It preserves the source pixels and hidden-car picking result; the focused test
proves identical before/after tunnel images in all eight presentation variants.
This is extra damage work, not a different tunnel/car visibility rule. Resource
optimization remains P5-28/P11.

Alpha picks return stable **TrainId + CarId**, exact definition and visible
location. The existing inspector can select another car in the same train,
updates its position by ID as it moves, and shows localized car count, position,
car identity and provenance. The highlight also resolves the current saved car.
The added portable label is English **Car** / Japanese **車両**; existing source
metadata/credits remain intact. Full train information, tracking and name/color
settings remain P5-25. World replacement retains the existing selection reset.

## Focused verification and inspected captures

Only selected tests were run, following the owner's request.

- **65/65 client tests**: train-renderer, train-fixtures, railway-renderer,
  terrain-renderer and fleet-state. Includes the **448 independent P5-02 poses**,
  every bundled formation in both directions/eight presentation variants,
  foreground ordering, predecessor routing, cuts, decode reuse, stable IDs,
  missing/offscreen asset rejection and retained viewport regressions.
- **15/15 .NET movement tests**, freshly emitting
  `tmp/p5-09-moving-fleet.json` from actual construction, purchase and deployment.
  Existing independently expected movement, ramp, rollback and save continuation
  assertions pass. No production backend behavior changes in P5-09.
- **Six selected Chromium cases**: the two fleet cases, three railway-rendering
  cases and one exhaustive moving-art case. Two real peers render the purchased/
  deployed consist, select it before/after movement and curves, retain the canvas
  and decoded art, and continue after HTTP reopen/reload. Infrastructure
  regressions include Japanese DPR2 and missing-media readiness.
- The moving-art browser case uses the production renderer with actual decoded
  PNGs, served by a test-only route bundled through the pinned Vite API. It is
  absent from `web/dist`. All **eight car recipes ×112 pose cases ×8 variants =
  7,168 poses** match independently expected crops/anchors and actual PNG pixels.
  The 448 generic source cases are applied to their equivalent registered car
  families; this is **16,515,072 exact pixel and alpha-pick comparisons**.
- **115 infrastructure compositions per variant /920 contacts** exercise every
  bridge/girder/tunnel/garage/signal recipe selector, without/with all four bundled
  pole types on the appropriate axis. These are explicit rendering fixtures;
  their screenshots grant no additional construction/controller gameplay credit.
  The source S/W tunnel case adds **345,600 hidden-pick checks**. A legal graph
  rendering fixture on a **200×100×6** map shifts three cars across four existing
  rail columns: its local redraw exactly matches a fresh full render and
  **24,000 pixel-location picks**. This is rendering evidence, not fleet capacity.
- Clean web build, generated contracts, content and EN/JA locale checks pass,
  with zero Svelte errors/warnings and the existing bundle-size advisory.
  Pinned source review, all **744** independent train fixtures and whitespace
  checks pass. No full web/browser suite was run.

Logs are local under `tmp/p5-09-*`; captures and the machine-readable art report
are under `tmp/p5-09-browser/`. Inspected captures include the actual moving
straight/curve consist, full car pose contacts, infrastructure contacts, hidden
tunnel and local redraw. The four seasonal car/infrastructure contacts are
byte-identical within each lighting choice; their day and night images were
both inspected. The renderer uses the real seasonal keys independently in every
run. The prepared assets have no further seasonal car differences.

These are prepared-art pixel/alpha assertions and inspected browser captures.
They do not establish native desktop pixel identity or close inherited P2 human
full-gallery, native display/accessibility, optional-pack or cross-browser gates.
Full runtime reversal/Outside/controller integration, recovery, capacities and
complete EN/JA purchase/deploy journeys retain their later P5 gates.

Reproduce from the repository root:

```sh
FREETRAIN_MOVING_FLEET_OUTPUT="$PWD/tmp/p5-09-moving-fleet.json" dotnet test tests/FreeTrain.Online.Tests/FreeTrain.Online.Tests.csproj --no-restore --filter 'FullyQualifiedName~FleetMovementTests' -m:1 -nodeReuse:false -p:UseSharedCompilation=false
python3 scripts/review-trains.py --source-root ../freetrain-exav-archive --check
python3 scripts/train-fixtures.py --source-root ../freetrain-exav-archive --check
```

From `web/`:

```sh
deno task build
deno test -A --no-check tests/train-renderer.test.ts tests/train-fixtures.test.ts tests/railway-renderer.test.ts tests/terrain-renderer.test.ts tests/fleet-state.test.ts
FREETRAIN_MOVING_FLEET_OUTPUT="$PWD/../tmp/p5-09-moving-fleet.json" FREETRAIN_ONLINE_TEST_SERVER_CONFIGURATION=Debug FREETRAIN_ONLINE_BROWSER_EVIDENCE="$PWD/../tmp/p5-09-browser" deno task test:browser:run fleet-persistence.spec.ts train-rendering.spec.ts railway-rendering.spec.ts --project=canonical --workers=1
```
