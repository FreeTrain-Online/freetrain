# P6-13 wide-platform lanes and safe demolition — 9 November 2026

Wide platform properties now offer independent right/left track changes with a
server preview, free quote and explicit confirmation. Either, both or neither lane
can remain after editing. Slim properties expose no lane controls. This delivers
source lane extension; platform length and center geometry do not change.
Save10/protocol8 retain their current formats.

## Source and behavior

The authority remains stable ExAV `883cf7d81cf49db25ff38cd3a25d0f140afe31a0`,
`core/world/rail/FatPlatform.cs` (`canAddLane`, `addLane`, `canRemoveLane`,
`removeLane`), `YardRailRoad.cs`, `Platform.cs`, and
`core/controllers/rail/PlatformPropertyDialog.cs`. The pinned P6-01 review and
P6-02 literal fixtures remain unchanged.

Lane0 is heading.right90; lane1 is heading.left90. Adding converts aligned,
unoccupied normal straight rails or creates missing normal Traffic rails. It
rejects other platforms/passages, curves, junctions, perpendicular/special rails,
spatial obstacles and world boundaries. Removing rejects a car anywhere on the
selected lane and restores normal rails with the same Traffic owners, rail
patterns, ground/terrain flags, accessories and ordered duplicate speed lists.
Neither action books an account transaction or changes the allocator when all
rails already exist. Missing rail owners allocate atomically. Yard indices remain
base-relative on both lanes. The independent L4/train3 witnesses retain forward
index2 and opposite index0 after every edit; real trains occupy both approaches.

Lane editing increments the platform/center revision once, updates only the
selected lane's rail-owner revisions and preserves host/child order, names,
bells, service state, accounts, timers and RNG. Empty center strips remain valid,
saved and pickable. Later service/controller slices retain actual stop dispatch;
these checks establish yard geometry and train safety, not passenger stop service.

The existing demolition policies remain:

- Complete platform removal rejects any occupied yard, restores every normal
  rail, recursively disconnects children, removes only the wide center owner and
  cancels only that platform's daily timer. Removal is free.
- Station-building removal preserves platform rails and their trains, recursively
  disconnects the host tree, removes the actual building and cancels only its
  hourly/daily timers. Its checked subsidiary demolition charge remains exact.
  Connected trains are not physical occupants of the building footprint.
- Underlying rail/object removal rejects cars and platform dependencies. A user
  first removes the unoccupied lane/platform, then may remove its restored rails.
  No bypass through the generic bulldozer is introduced.

These are the existing source/portable safety choices. Every commit reconstructs
its plan from current authoritative state; frozen epoch, geometry, content,
minute, platform identity/revision and quote fences prevent stale publication.
A preview object also retains its exact source world, so direct domain commit
cannot replay it against changed occupancy.

## Browser workflow and authority

Inspect a wide platform center or yard, then choose Edit right track or Edit left
track. The control states whether the next change adds or removes that lane.
The authoritative result names the side/action, cost and affected cell count;
Confirm track change applies it, Cancel preview discards it. Illegal occupied
lanes cannot be confirmed. Apply/reset local property drafts before lane edits.
English/Japanese help explains free changes, normal-rail restoration and occupied
track protection. These controls use the existing property inspector and never
advance shared time.

Queries reuse `WidePlatformQuery` with explicit `addLane`/`removeLane` actions;
`secondLane` selects lane1 rather than describing initial construction. The
center position, direction and length must match the existing wide platform.
The browser validates exact ordered lane geometry, zero cost and identity in
responses. Operators/Owners may commit; Observers cannot. Pending edits, geometry,
minute, epoch, role/connection changes and unmount cancel quotes/requests; late
responses cannot restore them. Dirty property drafts disable lane edits.

## Verification

Fresh verification on 9 November 2026:

- **1,137/1,137 relevant Release .NET tests** pass. The final focused run passes
  **86/86 lane/contract/network tests**, including 15 new lane lifecycle cases and
  two new shared command fixture round trips. Final exact three-car head-pose
  assertions also pass **15/15** focused lane tests. No skips.
- **361/361 web units** pass: 359 ordinary and two retention tests. A separate
  refreshed shared-contract check passes **51/51**.
- **46/46 selected Chromium scenarios** pass: the station/platform/host/rendering/
  transport/draft sweep passes45 and the purchased moving-train companion passes1.
  After correcting occupied-lane feedback to show the localized train obstruction
  message, all **3/3 final lane journeys** pass. English desktop and Japanese
  430×860 actual-touch captures were inspected.
- **28/28 station Python tests**, **821 independent literal cases**, pinned-source
  review, generated contracts/content, EN/JA locales, Svelte (zero errors/warnings),
  production build, project boundaries and whitespace pass. The existing Vite
  bundle-size advisory remains.

The train tests cover both lanes and approaches, moving and serialized waiting
states, complete-platform/underlying-rail protection, opposite-lane editing,
station-parent removal, exact callback retention and production save continuation.
Browser tests also cover all four lane states, preview cancellation, free quotes,
property-draft isolation, stale peer edits, late replies, Observer controls,
replacement, real moving-train conflicts and exact two-peer HTTP/checkpoint
reopen. Invalid/missing/slim/duplicate lanes, boundaries, obstacles, incompatible
rails and direct stale candidate replay reject atomically.

Logs: `tmp/p6-13-dotnet-final.log`, `tmp/p6-13-focused-final.log`,
`tmp/p6-13-lanes-final.log`, `tmp/p6-13-web-unit-final.log`,
`tmp/p6-13-contract-web.log`, `tmp/p6-13-build-final.log`,
`tmp/p6-13-browser.log`, `tmp/p6-13-browser-occupancy.log`,
`tmp/p6-13-browser-final.log` and `tmp/p6-13-{python,fixtures,review,boundaries}.log`.
Final lane captures are in `tmp/p6-13-browser-final/`.

Early test iterations corrected immutable collection-content assertions and
avoided serializing the entire fleet profile for equality. Browser verification
also caught the occupied-lane reason falling through to generic geometry feedback;
the final test asserts the specific localized obstruction message.

Reproduction:

```sh
dotnet test tests/FreeTrain.Online.Tests -c Release --no-restore -m:1 --filter 'FullyQualifiedName~Station|FullyQualifiedName~Platform|FullyQualifiedName~Mixed|FullyQualifiedName~Fleet|FullyQualifiedName~Train|FullyQualifiedName~Harbor|FullyQualifiedName~ObjectRemoval|FullyQualifiedName~NormalRailway|FullyQualifiedName~ContractTests|FullyQualifiedName~NetworkTests'
DENO_JOBS=4 deno task --cwd web test
deno task --cwd web build
deno task --cwd web test:browser:run platform-lanes.spec.ts platform-properties.spec.ts station-hosts.spec.ts thin-platform.spec.ts wide-platform.spec.ts station-building.spec.ts station-building-domain.spec.ts station-transport.spec.ts station-rendering.spec.ts station-editing.spec.ts reference-world.spec.ts
python3 -m unittest discover -s scripts/tests -p 'test_station*.py'
python3 scripts/station-fixtures.py --check --source-root /Users/dmark/dev/freetrain-exav-archive
python3 scripts/review-stations.py --check --source-root /Users/dmark/dev/freetrain-exav-archive
python3 scripts/check-project-boundaries.py
git diff --check
```

Native/original-runtime/human fidelity, other browser engines, optional content,
full-suite/capacity/soak, Linux/container and deployment acceptance are not credited.
Passenger/demand/fare/controller service and real departure events retain their
later P6 owners. Waiting-train unit checks use a trusted wait callback to establish
a real serialized waiting state before editing; they resume through the production
movement dispatcher, without claiming completed platform service.
