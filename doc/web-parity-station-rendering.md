# P6-07 station-building rendering — 8 November 2026

Complete at bundled browser presentation scope. All **35 station definitions**
render their immediate built state through existing P2 recipes, with their
authored orientation, exact member positions and stable Station identity.
Construction/name/removal controls remain P6-08; platforms, passengers and service
remain later P6 work. Save10/protocol8, content hashes and admission limits are
unchanged.

## Source and implementation

Authority is stable ExAV 2.1.1.12, archive Git
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, `trunk/`. The
[station source contract](web-parity-station-contract.md),
[independent station fixtures](web-parity-station-fixtures.md) and
[geometry dependencies](web-parity-geometry-dependencies.md) supply the inputs.
The pinned `PThreeDimStructure.VoxelImpl.draw`,
`DefaultSpriteLoaderContributionImpl.load3D` and `QuarterViewDrawer` were read
directly for this implementation; the existing source review verifies raw blobs.

[`StationRenderPlan`](../web/src/lib/station-renderer.ts) resolves the exact
qualified definition and preflights every recipe/member, even offscreen and above
the cut. It contributes source-normal art to the existing V/H/Z terrain scene;
stations remain separate from decorative railway-stationary accessories. Each
sprite targets its actual footprint member and stable station ID. Reverse alpha
picking and cut polygon picking use the same ordered scene as painting.

Source `heightCutDiff = cutZ - voxelZ + 1` means normal station art begins when
`cutZ >= baseZ + height`, or in full-height mode. Intersecting cuts draw Gray
(`#808080`) on **every base footprint cell**, including cells whose authored
sprite is invisible; upper members contribute no cut art. A cut below the base
has no station art. Opaque station occupancy suppresses ordinary ground, water
and underground chips at matching occupied cells. Full-height traversal hides
underwater members; explicit cuts reveal them. Source day/night/seasonal bindings
are used without inventing replacement artwork or lighting.

Full footprints enter column signatures, neighbor grid masks and localized raster
damage. Build/remove publications preserve unrelated operations, decoded images
and previous immutable picks. Large-map viewport bounds include station sprite
overhangs. Local patches replay overlapping terrain, rail and train operations;
pixels and picks publish together.

[`Station labels`](../web/src/lib/station-labels.ts) use stored names at the base
projection, Y−16, after map overlays and before train names. Like the source,
labels remain visible when a cut hides the building. White text with a black
one-pixel shadow is enabled by default. View adds independent local visibility,
font, size and color preferences in English/Japanese. Browser font families are
portable presentation choices, not proof of native font/pixel identity. Preferences
do not mutate the world or rebuild/decode its art. A picked station displays its
confirmed name and original author/plugin; selecting another member of the same
station refreshes the displayed position. Canonical stations do not enter the
historical prototype's name editor. P6-08 owns the actual edit workflow.

## Verification

- **333/333 web units** (331 ordinary + two retention), including seven new station
  cases: all 35 footprints and eight variants, literal Hakodate crops/anchors,
  source traversal, every intersecting cut, stable member/alpha picks, neighboring
  redraw dependencies, cache reuse, offscreen/bad-art rejection, underwater and
  controlled buried-state drawing, and bilingual name projection/preferences.
- **224/224 relevant Release .NET tests**, no skips: Station, Spatial and
  ObjectRemoval families. No backend behavior changed in this slice.
- **15/15 selected Chromium tests**, no skips: station presentation/domain,
  railway rendering, train information/labels and historical station drafts.
  The station gallery compares **24,640,000 pixels and the same number of alpha
  picks** across 35 definitions and eight variants against independently evaluated
  pinned XML/load3D geometry. It does not derive expected rectangles, anchors or
  member positions from the production renderer. Build/remove local rasters match
  fresh full scenes with overlapping train/terrain art, plus 30,000 matching picks.
  Real API construction is visible/pickable in EN desktop and JA narrow DPR2 peers;
  exact Gray cut pixels, same-ID member changes, local removal, unchanged saves,
  canvas/camera and decode-cache identity are checked.
- Generated contracts/content, both locales, Svelte/TypeScript and production
  build pass with zero check errors/warnings; Vite retains its chunk-size advisory.
  All 28 station Python tests,821 literal cases, raw pinned source review, project
  boundaries and whitespace checks pass.

Commands and evidence:

```sh
DENO_JOBS=4 deno task --cwd web test
deno task --cwd web build
dotnet test FreeTrain.Online.slnx -c Release --no-restore --filter 'FullyQualifiedName~Station|FullyQualifiedName~Spatial|FullyQualifiedName~ObjectRemoval'
deno task --cwd web test:browser:run station-rendering.spec.ts station-building-domain.spec.ts railway-rendering.spec.ts train-information.spec.ts station-editing.spec.ts
python3 scripts/station-fixtures.py --check
python3 scripts/review-stations.py --check --source-root /Users/dmark/dev/freetrain-exav-archive
python3 -m unittest discover -s scripts/tests -p 'test_station*.py'
python3 scripts/check-project-boundaries.py
```

Logs: `tmp/p6-07-web-unit.log`, `tmp/p6-07-web-build.log`,
`tmp/p6-07-dotnet-final.log`, `tmp/p6-07-browser-verified.log`,
`tmp/p6-07-fixtures.log`, `tmp/p6-07-source-review.log`,
`tmp/p6-07-python.log` and `tmp/p6-07-boundaries.log`.
Inspected gallery, EN full/cut/night, JA narrow and local redraw captures are in
`tmp/p6-07-browser-evidence/`, alongside the per-variant gallery report.

Screenshot review exposed an initially premature cut assertion; the final test
waits for the requested cut and completed presentation and checks exact Gray
pixels. Broader regression execution also found four old train-information setup
failures caused by Save9 inputs in ignored `tmp/`. Those controlled scenarios now
live in tracked current-format test fixtures; train/rail/account/random/queue
state is unchanged. No older-save compatibility was added. The .NET test runner's
initial sandbox IPC failure was resolved using authorized local execution; its
successful result is retained separately from the failed sandbox output.

This is automated browser/prepared-art evidence and inspected captures, not
original-runtime human/native pixel-fidelity acceptance. Full backend/browser
suites, other engines, optional content, capacity/soak and Linux/deployment were
not rerun. P6-08 controls and later platform/passenger/service gates remain open.
