# P6-14 cosmetic passage construction — 9 November 2026

Stations → Passages now supports concrete, brown wood and green wood, Place and
Remove, explicit coordinates/height, two map endpoints, an authoritative free
preview, framing, Confirm and Cancel in English/Japanese. Selecting Passages
closes the building catalog, and detailed help expands on demand. Passage appearance
survives exact save reopen, peer updates and checkpoint restart. Save10/protocol8
retain their current formats; the command/preview schema gains passage edits.

## Source behavior

The authority is stable ExAV `883cf7d81cf49db25ff38cd3a25d0f140afe31a0`,
`trunk/core/controllers/rail/StationPassagewayController.cs`,
`trunk/core/world/rail/ThinPlatform.cs` and `PassagewayRail.cs`, with P6-01 source
review and P6-02 independent literal fixtures unchanged.

Placement requires distinct, cardinal, same-height endpoints. Each cell must be
empty or an orthogonal slim-platform yard. Ordinary rails, parallel slim yards,
wide platforms and other spatial obstacles reject the complete placement. The
browser caps inclusive paths at 100 cells, matching the existing platform tools.
Map targeting selects a slim platform or ground first; the second endpoint keeps
its height and aligns to the dominant axis, choosing X on a tie as the source does.

Only existing slim-yard outlooks change. Source construction leaves empty path
cells empty: its floating/open-ended bridge creation branches are TODOs. The
portable implementation preserves those legal/no-effect cells and never invents
standalone rails, bridges, piers or stations. A wholly empty path, repeated bridged
placement and removal without passages preserve exact saved state, including a
terrain-only world's profile. Every passage edit costs **¥0**, without an account
transaction or timer registration/cancellation.

New outlooks use the selected material. The first endpoint stays unbridged when
the path's right90 is the platform heading; the last stays unbridged when its
left90 is the heading. Other yards are bridged. Replacing an unbridged passage
promotes it to bridged using the selected material. An already bridged passage
retains both its bridge and its existing material, exactly as the source branch
does. Removal restores only passage outlooks to plain and skips all other cells.

Width is derived rather than stored or selected: a slim yard is double width
when its left90 neighbor is an opposite-facing slim yard with passage outlook.
That neighbor's material and bridge flag do not affect width. Actual rendering
selects all 48 material/heading/bridge/width combinations from the authored crops.
North/west outlooks draw after trains, east/south before; bridged east/south also
redraw the floating foreground after trains. The same platform identity remains
pickable through every opaque outlook layer. Appearance/width changes invalidate
retained raster/pick columns even when Traffic components themselves are unchanged.
The browser ghost shows resulting outlook crops and affected neighboring yards,
with path outlines over legal empty cells. This adapts the source controller’s
floating line overlay to preview the actual result without implying empty-cell
bridge construction.

## Authority and preservation

`PassageConstruction` creates a detached plan. Preview admission and commit both
use the existing mixed-state transition validation; commit reconstructs legality
from authoritative state. The source world identity prevents direct stale-plan
replay. `PassageQuery`/`editPassage` use the accessory preview/command boundary,
FIFO, role checks, receipts and epoch/geometry/minute/content/quote fences. Costs
are always zero; malformed/nonzero command quotes cannot change state. Pending
requests use cancellation and generation checks across close, reconnect, locale
changes and world replacement. The response parser verifies the exact ordered
path, zero quote and legal/reason consistency.

Changed platform and Traffic-owner revisions advance once. Names, roofs, bells,
logical host/child graphs, listeners, passenger counters, fleet poses, rail and
accessory components, ground flags, speed lists, allocator, accounts, clock and
RNG remain intact. Existing connected hosts and actual moving trains are covered;
passage edits themselves cannot create/disconnect hosts or start passenger service.
These pieces remain separate from P4 stationary structures. Stairs and their
neighbor dependencies/EXAV-GAL-001 disposition remain P6-15.

## Verification

Fresh verification on 9 November 2026:

- **1,153/1,153 relevant Release .NET tests** pass without skips, including 11
  new passage tests, independent literal paths, all materials/headings, connected
  hosts, real occupied-yard trains in both directions and exact reopen/movement.
  The separate focused passage/shared-contract run passes **67/67**.
- **369/369 web units** pass: 367 ordinary and two retention tests. Five passage
  tests cover exact previews, the 48 art selections/orderings, neighboring-width
  invalidation, pure ghosts and cancellation/lifecycle fences. Three shared wire
  fixtures cover both commands and a preview.
- **26 distinct selected Chromium scenarios** pass across the station/platform/
  transport/rendering/draft sweep and corrected passage authority rerun. The first
  sweep passed 25 and exposed a shared-cookie test setup error; the isolated
  observer rerun passed 1. After the narrow-screen catalog/help refinement,
  **4/4 final passage scenarios** pass without skips.
  EN desktop and JA 430×860 actual-touch journeys cover all materials, two map
  endpoints, cancel, free edits, removal, peers, exact HTTP reopen/checkpoint
  restart, stale minute/late-response cancellation, Observer and world replacement.
- Independent browser comparisons pass **48 appearances × eight variants**:
  **4,608,000 pixels and 4,608,000 alpha picks**, with exact source rectangles,
  anchors and floating foregrounds. Captures were visually inspected.
- **28/28 station Python tests**, **821 independent literal fixtures**, pinned
  source review, generated contracts/content, EN/JA locale checks, Svelte
  (zero errors/warnings), production build, project boundaries and whitespace pass.
  The existing Vite bundle-size advisory remains.

Early iterations corrected the new tests' endpoint/collection assertions, concrete
recipe naming, exact selector labels and Japanese readiness. Browser authority
verification exposed shared observer credentials in the test context; separate
contexts resolved it.
The production no-op test also led to preserving a terrain-only profile on empty
path confirmation.

Logs: `tmp/p6-14-dotnet-final.log`, `tmp/p6-14-focused-final.log`,
`tmp/p6-14-web-unit-final.log`, `tmp/p6-14-build-final.log`,
`tmp/p6-14-browser-final.log`, `tmp/p6-14-browser-authority.log`,
`tmp/p6-14-browser-ui-final.log`, and
`tmp/p6-14-{python,fixtures,review,boundaries}.log`. Final screenshots are under
`tmp/p6-14-browser-ui-final/`; earlier sweep galleries are under
`tmp/p6-14-browser-final/`.

```sh
dotnet test tests/FreeTrain.Online.Tests -c Release --no-restore -m:1 --filter 'FullyQualifiedName~Station|FullyQualifiedName~Platform|FullyQualifiedName~Passage|FullyQualifiedName~Mixed|FullyQualifiedName~Fleet|FullyQualifiedName~Train|FullyQualifiedName~Harbor|FullyQualifiedName~ObjectRemoval|FullyQualifiedName~NormalRailway|FullyQualifiedName~ContractTests|FullyQualifiedName~NetworkTests'
DENO_JOBS=4 deno task --cwd web test
deno task --cwd web build
deno task --cwd web test:browser:run passage.spec.ts thin-platform.spec.ts wide-platform.spec.ts platform-lanes.spec.ts platform-properties.spec.ts station-hosts.spec.ts station-transport.spec.ts station-rendering.spec.ts station-editing.spec.ts
python3 -m unittest discover -s scripts/tests -p 'test_station*.py'
python3 scripts/station-fixtures.py --check --source-root /Users/dmark/dev/freetrain-exav-archive
python3 scripts/review-stations.py --check --source-root /Users/dmark/dev/freetrain-exav-archive
python3 scripts/check-project-boundaries.py
git diff --check
```

Native/original-runtime/human fidelity, other browser engines, optional content,
full-suite/capacity/soak, Linux/container and deployment acceptance are not credited.
Passenger/demand/fare/controller service and actual departure events retain their
later P6 owners.
