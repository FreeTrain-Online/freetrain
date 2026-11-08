# P6-09 slim platforms — 8 November 2026

The Stations task now has a Platforms tab with roofed and unroofed slim
platforms, four cardinal orientations, length 1–100, Place/Remove, coordinates,
automatic rail/ground targeting, authoritative preview and Confirm/Cancel in
English and Japanese. The authored sprite ghost uses the actual middle roof run;
Show platform preview frames the selected cells beside/above the task and sets
the height cut to their level, including underground platforms. Folded help,
separate field rows and a pinned quote/confirmation area retain narrow usability.

`ThinPlatformConstruction` ports the pinned ExAV `ThinPlatform.canBeBuilt`,
constructor/removal, `Platform` registration and `PlatformController` booking.
An empty voxel is legal even without an existing rail; an occupied voxel must
contain an unoccupied straight `SingleRailRoad` on the same axis. Curves,
junctions, slope/bridge/girder/tunnel/garage/signal rails, existing platform yards,
standalone passages and non-Traffic occupants reject atomically. The source's
native length maximum is 100. No terrain reclamation, supporting piers or extra
normal-track construction charge is invented for empty cells.

Each yard retains a real normal rail in its existing Traffic owner and adds the
exact `system:thin-{direction + roof}` component. Only indexes
`length/4 <= i < length-length/4` receive a roof, using integer division. New
Traffic owners and the nonspatial platform identity share the existing allocator.
The default canonical name retains source `ホーム{0,2:d}` and the exact default
bell `{9B087AEA-6E9C-48cd-A1F3-1B774500752E}`. Host discovery remains P6-11: these
new platforms are disconnected, and their trains pass without controller,
passenger, fare or bell effects.

Removal from any yard revalidates platform ID/revision and current car occupancy,
cancels the daily timer and clears the platform component. Normal rails,
Traffic identities, accessories, speed-list entries (including duplicates),
ground flags, piers and terrain remain. Existing host/child references are detached
consistently for loaded graphs; discovery/reparenting UI and the full wide/lane
lifecycle remain P6-10–13. Underlying rail demolition keeps its existing
`platformDependency` protection.

Placement uses the source base-ground-only quote: ¥500,000 × length × height
multiplier, with the source underwater zero branch and checked Int32 arithmetic.
Removal is free. The existing station lifecycle registers platform upkeep at
creation + 1,440 minutes, books ¥180,000 × length daily and preserves exact FIFO
continuation after save/reopen. The default runtime now dispatches platform daily
upkeep; station hour/day passenger/log work remains explicitly pending P6-17.

The server adds `ThinPlatformQuery`, preview and `editThinPlatform` through the
existing FIFO/participant/receipt infrastructure. Preview is detached and also
checks complete transition admission before quoting. Epoch, geometry, minute,
content, target revision and quoted cost are revalidated at commit. The browser
checks schema, echoed query, exact placement positions, cost/legality and request
generation. Cancel, edited inputs, tab/tool/locale changes, disconnect, replacement
and peer clock/geometry changes discard obsolete quotes. Owner/Operator may edit;
Observers may inspect. Save10/protocol8 and content pack hashes are unchanged.

Rendering uses the actual platform recipe in the railway scene, after normal
rail art and before the car for east/south, after the car for north/west. It
retains source V/H/Z traversal, cuts, seasonal/light variants, prepared alpha-mask
picks and cache readiness. Picks identify the Traffic owner, platform ID and exact
recipe; replacing a platform on retained rails invalidates its picked identity.
The inspector shows its name/ID/revision and disconnected status. Full platform
properties, source warning-icon presentation and logical connection controls
remain P6-12; passage/stair outlook dispatch belongs to P6-14/15. No missing recipe
fallback grants rendering credit to those later outlooks.

The source bindings and translations remain those of P6-01's `thin-platform`
operation: MAN-T04/W04/A05/D02/E06, EXAV-10 and the pinned controls in
`doc/parity/station-review.json`. `ホーム`, `向き`, `長さ`, slim roof/no-roof and
Place/Remove labels receive direct EN/JA UI translations; preview, confirmation,
coordinates, disconnected-service explanation and framing are browser adaptations.
The Japanese canonical default name is saved state, independently of UI locale.

## Verification

- **787/787 relevant Release .NET tests** pass, including the new platform tests,
  32 independent literal thin geometries, native maximum, ground/elevated/underground
  quotes, exact default name/bell, compatible rail attachment, curves/junctions/
  special-rail rejection, retained poles/speed lists, occupied/stale demolition,
  pure preview, role/clock/cost fences, overflow, exact Save10 round trips, daily
  upkeep and real purchased/deployed trains crossing yards from both approaches.
  Existing station, contract, mixed, spatial, removal, railway construction,
  deployment and movement families are included.
- **343/343 web units** pass (341 ordinary + two retention), including two new
  shared protocol fixtures, query/identity/native bounds, malformed preview
  rejection, exact roof-run ghosts, all eight seasonal/light variants, source
  rectangles/anchors, cut/alpha picks, actual fleet car pass ordering and retained
  Traffic platform-identity invalidation.
- **26 distinct selected Chromium scenarios verified without skips**: the final
  broad run passed **25/26**, followed by a fresh **12/12** platform/removal run
  after synchronizing the existing removal helper with completed raster/camera
  frames. This resolves the one Japanese narrow forest-pick timing failure.
  All four platform journeys pass: EN desktop and JA 430×860 actual touch,
  ground/elevated roof/no-roof, literal middle roof flags, prepared ghosts,
  source-mask sprite/identity picks, real automatic-height map attachment on
  restored elevated rails, free removal, Owner/Operator/Observer, peer state,
  stale clock, late cancelled response, world replacement, exact HTTP reopen
  and actual backend checkpoint restart. The separate underground case verifies
  the explicit level, ¥4,000,000 quote, visible cut preview and exact reopen.
  Existing station domain/transport/pixel-gallery/local redraw, building/name,
  toolbar and removal regressions are included.
- Production build, generated contracts/content, EN/JA locales and Svelte checks
  pass with zero check errors/warnings. The existing Vite bundle-size advisory
  remains.
- **28/28 station Python tests**, **821 independent source fixtures**, raw pinned
  source review, project boundaries and whitespace checks pass.

Commands:

```sh
dotnet test FreeTrain.Online.slnx -c Release --no-restore --filter 'FullyQualifiedName~Station|FullyQualifiedName~ThinPlatform|FullyQualifiedName~Contract|FullyQualifiedName~Mixed|FullyQualifiedName~Spatial|FullyQualifiedName~ObjectRemoval|FullyQualifiedName~FleetMovement|FullyQualifiedName~FleetDeployment|FullyQualifiedName~RailwayConstruction'
DENO_JOBS=4 deno task --cwd web test
deno task --cwd web build
deno task --cwd web test:browser:run thin-platform.spec.ts station-building.spec.ts station-building-domain.spec.ts station-rendering.spec.ts station-transport.spec.ts game-toolbar.spec.ts object-removal.spec.ts
python3 -m unittest discover -s scripts/tests -p 'test_station*.py'
python3 scripts/station-fixtures.py --check --source-root /Users/dmark/dev/freetrain-exav-archive
python3 scripts/review-stations.py --check --source-root /Users/dmark/dev/freetrain-exav-archive
python3 scripts/check-project-boundaries.py
git diff --check
```

Logs: `tmp/p6-09-dotnet-verified.log`, `tmp/p6-09-web-unit-final.log`,
`tmp/p6-09-web-build.log`, `tmp/p6-09-browser-acceptance.log`,
`tmp/p6-09-browser-confirmed.log`,
`tmp/p6-09-{python,fixtures,review,boundaries}.log`. Captures are under
`tmp/p6-09-browser-acceptance/` and `tmp/p6-09-browser-confirmed/`;
EN/JA and underground captures were inspected. Earlier inspected platform captures
are under `tmp/p6-09-browser-touch/`.

Earlier runs exposed a real tab-container layout error (fixed), test-world border
and fixture-dispatch mistakes, and missing test reauthentication after backend
restart. The broad earlier browser run passed 23/25; corrected platform journeys
then passed 3/3, including actual touch and map attachment. A broader .NET run
passed 847/848 before the speed-list assertion was corrected; the final relevant
787/787 run includes that assertion and the new preview fixture dispatcher. These
intermediate runs do not replace final evidence or grant performance acceptance.

Native/original-runtime/human pixel fidelity, other browser engines, optional
content, full-suite/capacity/soak, Linux/container and deployment acceptance are
not credited. Wide platforms, hosting, properties, passengers, fares, demand and
real departure sounds retain their later P6 owners.
