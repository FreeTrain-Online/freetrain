# P6-24 station cross-tool and authority regression — 11 November 2026

This step adds integrated regressions to the delivered P6 station implementation.
The authority remains stable ExAV `883cf7d81cf49db25ff38cd3a25d0f140afe31a0`.
The [station contract](web-parity-station-contract.md),
[independent fixtures](web-parity-station-fixtures.md),
[platform implementation](web-parity-thin-platforms.md) and
[information evidence](web-parity-station-information.md) retain their scope.
There is no simulation, content, locale, Save10 or protocol9 format change.

## Added witnesses

`StationCrossToolTests.cs` adds eleven domain cases. Rails are actually constructed
and trains purchased/deployed through the existing domain/session operations;
the new tests do not manufacture special rail components or car occupancy.

- Normal, bridge, girder, garage, tunnel and signal scenes reject platform placement
  on a real car with `occupied`. After actual withdrawal, normal rail accepts a
  platform and the five special handlers reject with `incompatiblePlatformRail`.
  The resulting normal yard rejects rail and whole-object demolition with
  `platformDependency`. Rejected previews preserve exact canonical save bytes.
- All four legally constructed cardinal slopes reject conversion at each actual
  slope rail while retaining support ownership and saved state.
- A platform, pole, speed limit and perpendicular cosmetic passage coexist.
  Edits preserve rail identity, accounts and timer contents/order. Platform removal
  leaves the rail, pole and speed membership intact and removes its timer; the
  result round-trips exactly through the current save format.

`station-cross-tool.spec.ts` is registered in the canonical Playwright project.
Its English desktop/DPR1 and Japanese 430×860 touch/DPR2 journeys visit all
construction tasks with real stations and a deployed train. The journeys reject
terrain modification under a station and enter/exit the existing owner-requested
map-authoring extension while preserving the station graph, fleet, queue, accounts,
pause and game minute. They then allow the source pole setter on an
occupied platform rail. This peer edit invalidates a passage quote without
changing the train or its callbacks. A real peer clock step invalidates the next
quote. Confirmed cosmetic passage changes retain fleet, queue, bookings and
Traffic voxels and publish the same graph to both peers.

Each journey deliberately loses the passage command acknowledgement after server
acceptance, disconnects into the demo gate and reconnects. Keyboard input during
the gate never replays the command. Explicitly retrying the identical receipt
returns acceptance without applying again; a new request ID with the old query
rejects and preserves exact save bytes. Observer UI and direct socket commands
deny writes. Save download also returns HTTP403 for Observer; the Owner verifies
the unchanged bytes. The canvas instance survives local task switches and shared
edits. A separate cold-join case independently delays live socket state and PNG
assets and proves input remains unavailable until the first ready live draw.

## Integrated coverage

The fresh station matrix includes EN/JA building placement/removal, slim/wide
platforms, occupied lane demolition, host removal/rebuild/chains, properties and
dirty drafts, delayed previews, world replacement, clock/midnight statistics,
actual controlled-supply passenger service, TAT/group rules, local labels and
information queries. Passage/stair pixel and alpha-picking oracles include real
car overlap and height cuts. Fleet cross-tool cases verify moving cars, whole
object skips, readiness, roles, stale placement and retained decoded art/canvas.

The infrastructure companion exercises normal/slope/bridge/girder/tunnel/garage
rail, signal/pole/accessory and speed-limit construction. Terrain companions
exercise protected ground, authoring mode, peer invalidation and keyboard/narrow
confirmation. Generated yard/bell cases use fresh real callback-boundary fixtures,
including Simple waiting/boarding and forwarding departure, retry, reconnect and
rejected callback atomicity. These are browser/portable regressions; synthetic P7
road/population interfaces remain labeled in `CrossToolDependencyTests`, and
controlled populations are not evolving city producers.

## Verification

Fresh verification passes **1,565 relevant Release .NET tests**, no skips,
including the eleven new cross-tool cases. The focused fixture-generation run
passes 15 cases. All **386 client unit/retention tests**, **28 station Python
tests**, **821 independent station fixtures**, pinned-source review, generated
contracts/content, EN/JA locale checks, Svelte/TypeScript (zero errors/warnings),
production build and project boundaries pass.

**141 distinct Chromium scenarios pass**: **119 canonical** and **22 historical
prototype shell** cases. The station sweep passes 54 cases and initially skips
three fixture-dependent cases; the companion passes all 68, including those three
with freshly generated yard/bell fixtures. Three new station cases overlap the
two sweeps, giving 119 distinct canonical passes. The final three-case station
rerun includes authoring preservation and framed EN/JA captures, all passing.
The historical shell run passes 21/22 before the instrumentation correction;
the final four-case responsive rerun passes all four and resolves its sole failure.
The deduplicated case/log index is `tmp/p6-24-browser-evidence.json`. Historical
shell cases provide UI regression evidence, not canonical station gameplay credit.

Captures are retained under `tmp/p6-24-browser-stations/`,
`tmp/p6-24-browser-companion/`, `tmp/p6-24-browser-shell-accepted/` and
`tmp/p6-24-browser-final/`. The final EN/JA screenshots were visually inspected;
the station list's local Move frames the actual station/platform/train scene
without changing canonical save bytes.

Verification logs and captures use `tmp/p6-24-*`. The initial sandbox runners
could not bind local test sockets; reruns used the authorized local execution
permission. Harness corrections account for Observer download denial and compare
timer array contents rather than immutable-array record identity. The older
prototype responsive test counted Settings' independent per-mount color-preview
blob decodes as map reloads. Its instrumentation now counts the exact static map
sprite paths, retaining the same zero-redecode, camera and canvas assertions.
Warming panels alone did not resolve that false positive. No production behavior
correction was needed. The existing Vite bundle-size advisory remains.

Reproduce the added cases and generated callback companions:

```sh
FREETRAIN_YARD_FLEET_OUTPUT="$PWD/tmp/p6-24-yard.json" FREETRAIN_BELL_FLEET_OUTPUT="$PWD/tmp/p6-24-bell.json" dotnet test tests/FreeTrain.Online.Tests -c Release --no-restore -m:1 -p:UseSharedCompilation=false --filter 'FullyQualifiedName~Station_platforms_use_actual|FullyQualifiedName~Actual_slope_rails|FullyQualifiedName~Platform_passage_pole|FullyQualifiedName~Generate_real_selected_bell_departure_fixtures|FullyQualifiedName~Production_Simple_unloads_waits_boards'
FREETRAIN_YARD_FLEET_OUTPUT="$PWD/tmp/p6-24-yard.json" FREETRAIN_BELL_FLEET_OUTPUT="$PWD/tmp/p6-24-bell.json" deno task --cwd web test:browser:run station-cross-tool.spec.ts departure-bells.spec.ts station-yard.spec.ts --project=canonical --workers=2
DENO_JOBS=4 deno task --cwd web test
deno task --cwd web build
python3 -m unittest discover -s scripts/tests -p 'test_station*.py'
python3 scripts/check-project-boundaries.py
git diff --check
```

P6-25 owns systematic current-format/process/checkpoint continuation and recovery;
P6-26 owns station capacity/soak measurements. Native/original-runtime acceptance,
other browser engines, optional content, Linux/container/deployment, P7 city
producers, P8 full histories and overall P6 acceptance retain their later gates.
