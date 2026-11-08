# P6-10 wide platforms — 8 November 2026

The existing Stations → Platforms task now offers Wide platform, cardinal
orientation, length 1–100, an optional second lane, coordinates/automatic height,
authoritative preview and Confirm/Cancel in English and Japanese. Both styles
share `PlatformControls`; slim roof/no-roof behavior remains available. Folded
help explains the empty center and right/left track geometry. The preview frames
all center/lane cells, exposes the selected level and uses the source center and
normal-track sprites. The narrow Japanese journey uses actual touch.

`WidePlatformConstruction` uses the shared detached `PlatformConstruction`
transaction with the pinned ExAV `FatPlatform.canBeBuilt`, `canAddLane`, constructor
and removal predicates. One logical identity owns the opaque center strip;
right90 lane0 and left90 lane1 retain their own Traffic owners, lane numbers and
indexes. Lane0 is mandatory at construction. A browser adaptation optionally
includes lane1 in the same atomic transaction, using the source lane-add predicate
and free booking. Editing lanes after construction, including left-only/no-lane
states, remains P6-13; the existing canonical graph/save contracts retain these
states. A wide platform never receives a roof.

Every center cell must be vacant. Each selected lane may be empty, or contain
an unoccupied aligned straight normal rail. Curves, junctions, perpendicular and
special rails, existing platform/passage components and non-Traffic obstacles
reject the complete operation. No ground reclamation, support-pier construction
or separate track charge is invented. Automatic targeting tests the first lane's
rail axis or ground and returns the center position at that height. Explicit
levels support elevated and underground construction.

The placement charge is `500000 × length × height multiplier`, with the source
base-ground-only, underwater-zero and checked Int32 arithmetic. A second lane
adds no charge. One daily timer per platform starts at creation +1,440 minutes;
upkeep remains `180000 × length`, independently of the number of lanes. Default
name sequencing and the exact source default bell match slim platforms. New
platforms remain disconnected until P6-11 host discovery: real trains pass their
yards without station/controller, passenger, fare or departure-bell effects.

Removal accepts any center or yard cell, revalidates ID/revision and car occupancy
across both lanes, cancels the timer, disconnects existing host/child references
and removes the complete center owner. It clears the yard component and retains
the normal rails, Traffic IDs, accessories, duplicate speed-list membership,
ground flags, piers and terrain. Removal is free. Underlying rail demolition
retains `platformDependency` protection. P6-13 still owns post-construction lane
extension/removal and its complete browser lifecycle.

`WidePlatformQuery`, preview and `editWidePlatform` extend the existing
participant/FIFO/receipt boundary. Epoch, geometry, minute, content, target
revision and exact quoted cost are checked at commit; previews also validate
complete transition admission without mutating live state. The client checks
schema, echoed query, exact center/ordered-lane positions, legality, cost and
request generation. Cancel, tool/input/locale changes, peer changes, disconnect
and replacement discard obsolete quotes. Owner/Operator may edit; Observers
cannot commit. Existing Save10/protocol8 and prepared content hashes are unchanged.

Rendering uses both authored `system:fat-*` center recipes and `system:yard`.
`StationRenderPlan` handles center occupancy, source V/H/Z traversal, seasonal/
lighting masks and all visible cuts, without inventing a building-style Gray cap.
The yard chip precedes the normal rail, which precedes its car. Picks retain the
platform identity separately from the center or Traffic owner; the inspector
accepts the center's spatial identity as well as a yard's Traffic component.
Rebuilding on retained tracks invalidates the affected identity/columns. Host
properties, warning-icon presentation and real service remain P6-11/12/16–21.

The source operation/control bindings are `wide-platform`, MAN-W04/W05/A05/A08
and EXAV-10 in `doc/parity/station-review.json`. Wide/length/direction/Place/Remove
labels translate the pinned controls; the initial second-lane checkbox, preview,
confirmation, coordinates, framing and help are browser adaptations. No historical
translation is promoted to behavior authority. Original resource attribution,
qualified recipe identities and pack/distribution boundaries remain unchanged.

## Verification

- **806/806 relevant Release .NET tests** pass. These include eight independent
  constructible wide geometry fixtures, all cardinal orientations, separate center/
  lane ownership, one/two lanes, native maximum100, exact quotes and Save10 reopen,
  retained poles/flags/duplicate speed lists, center/either-lane obstacles,
  incompatible rail families, stale identities/cost/minute, roles, detached preview,
  scheduler overflow, timer cancellation and exact daily continuation. Purchased,
  deployed trains pass each lane from both approaches, block occupied demolition
  and retain exact uninterrupted/reopened successors. Existing station, slim,
  contract, mixed, spatial, removal, railway, deployment and movement families pass.
- **350/350 web units** pass (348 ordinary + two retention). New checks consume
  the literal geometry/crop fixtures, validate queries/preview geometry, resolve
  center and lane identity, check canonical ownership, exact ghosts, all eight
  seasonal/light variants, opaque center picks, cut/water behavior, first-lane
  automatic-height/axis selection, real fleet car draw order and retained-center
  identity invalidation. Both new shared protocol fixtures round-trip.
- **30/30 selected Chromium scenarios** pass without skips. Four new wide
  journeys cover EN desktop and JA430×860 actual touch, one/two lanes, all four
  headings, ground/elevated/underground previews, source-mask center identity
  picks/inspection, free removal and exact retained Traffic IDs, automatic-height
  rebuilding on restored rails, Owner/Operator/Observer, peer clock invalidation,
  late cancelled responses, replacement, exact HTTP reopen and actual backend
  checkpoint restart. Existing slim, building/name, station domain/transport,
  source-pixel gallery/local redraw, toolbar/train animation and object-removal
  regressions pass. Final English/Japanese preview and removal captures were
  inspected.
- Production build, generated contracts/content, EN/JA locale checks and Svelte
  checks pass with zero check errors/warnings. The existing Vite bundle-size
  advisory remains.
- **28/28 station Python tests**, **821 independent source fixtures**, the raw
  pinned source review, project boundaries and whitespace checks pass.

```sh
DOTNET_CLI_USE_MSBUILD_SERVER=0 dotnet test FreeTrain.Online.slnx -c Release --no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false --filter 'FullyQualifiedName~Station|FullyQualifiedName~ThinPlatform|FullyQualifiedName~WidePlatform|FullyQualifiedName~Contract|FullyQualifiedName~Mixed|FullyQualifiedName~Spatial|FullyQualifiedName~ObjectRemoval|FullyQualifiedName~FleetMovement|FullyQualifiedName~FleetDeployment|FullyQualifiedName~RailwayConstruction'
DENO_JOBS=4 deno task --cwd web test
deno task --cwd web build
deno task --cwd web test:browser:run wide-platform.spec.ts thin-platform.spec.ts station-building.spec.ts station-building-domain.spec.ts station-rendering.spec.ts station-transport.spec.ts game-toolbar.spec.ts object-removal.spec.ts
python3 -m unittest discover -s scripts/tests -p 'test_station*.py'
python3 scripts/station-fixtures.py --check --source-root /Users/dmark/dev/freetrain-exav-archive
python3 scripts/review-stations.py --check --source-root /Users/dmark/dev/freetrain-exav-archive
python3 scripts/check-project-boundaries.py
git diff --check
```

Logs: `tmp/p6-10-dotnet-verified.log`, `tmp/p6-10-web-unit-verified.log`,
`tmp/p6-10-web-build-final.log`, `tmp/p6-10-browser-verified.log`,
`tmp/p6-10-{python,fixtures,review,boundaries}.log`. Browser captures are under
`tmp/p6-10-browser-verified/`.

The earlier broad Chromium run passed28/30 and exposed an actual missing center
inspector case, fixed before the final run. Earlier unit iterations corrected
fixture map dimensions, initial mixed revision and immutable-array comparison;
the maximum/overflow test was corrected to expect an absent initial station graph.
Those intermediate counts do not replace final verification.

Native/original-runtime/human pixel fidelity, other browser engines, optional
content, full-suite/capacity/soak, Linux/container and deployment acceptance are
not credited. Host discovery, platform properties, lane editing, passenger demand,
exchange/fares and departure sounds retain their later P6 owners.
