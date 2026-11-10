# P6-15 stairs and passage integration — 10 November 2026

Stations → Passages now offers Passage and Stairs in English and Japanese.
Stairs use one slim-platform cell, selected by coordinates or on the map, with
an authoritative free preview, cancellation, confirmation and removal. Concrete,
brown wood and green wood share the existing multiplayer command, role, receipt,
epoch/geometry/minute/content/quote and late-response fences. `PassageQuery.part`
is additive and defaults to `passage`; `stairs` requires identical endpoints.
Save10/protocol8 retain their current versions. The prepared catalog hash changes
because it gains three explicitly corrected render recipes. Current protocol/save
fixtures and stock bindings are refreshed; historical saves remain historical.

## Source behavior and preservation

Stable ExAV `883cf7d81cf49db25ff38cd3a25d0f140afe31a0` is the authority:
`StationPassagewayController.createStair/removeStair`, `ThinPlatform.StairPlatform`,
`RailRoadImpl.isDoubleWidth/draw/drawAfter` and `PassagewayRail.getStairSprite`.

Placement requires a slim platform and an immediately forward or backward slim
neighbor with passage outlook. Forward takes precedence and selects upward;
backward selects downward. Neighbor direction, material and bridge flag add no
predicate in the source. Width uses the left90, opposite-facing passage neighbor;
roof follows the target yard's existing roof. East/south stairs draw before cars,
north/west after cars; stairs have no floating foreground pass. Changes to adjacent
passages recalculate width and invalidate retained render/pick columns.

`STATION-D011` is preserved: preview stages a detached replacement without the
source test-mode cast/write. Commit rechecks authoritative state. Removal restores
only stair outlooks and is otherwise a no-op. Passage removal does not delete an
existing stair or reverse its stored upward choice; missing-neighbor rejection
applies when placing again. Passage placement can replace a stair as in the source.
Names, host graphs, service counters, train state, rails, roofs, bells, timers,
accounts, allocator, clock and random state are preserved. Stair edits book no
transaction, including a zero-valued transaction.

## EXAV-GAL-001 disposition

The six original material/day/night diagnostics remain unchanged. Original
`system:{material}-stair-23` still requests `(388,4,32,36)` from 336×153 sheets;
its source provenance, invalid status and P6-02 literal fixtures are retained.

The portable renderer selects new `system:{material}-stair-23-corrected` recipes
at `(288,4,32,36)`, anchor `(0,20)`. This is an explicit corrected selection from
the unchanged authored sheet, not a claim about original-runtime output. Evidence:
index22's unroofed south/down/double crop starts at x256; its visibly roofed
partner is the next 32-pixel tile at x288. The east/up/double pair likewise uses
x192/x224; the south/down/single pair uses x176/x208. x288 is within all six
sheets and contains the matching roofed descending stair. No pixels, picture
bindings, original source hashes or original recipes are repaired or replaced.
The preparation script records the correction in a separate recipe's source symbol
and generates its bounds-checked day/night masks. All 96 selector combinations
resolve to ready runtime frames. Native/human fidelity acceptance remains P6-27.

## Verification

Fresh verification on 10 November 2026:

- **1,161/1,161 relevant Release .NET tests**, with no skips. Five added stair
  cases cover each heading/all materials, pure preview, precedence, missing
  neighbors, removal, revision and authority fences. Both existing occupied-yard
  train directions now include real stair edits and exact reopen/movement.
  Shared wire round trips include three new stair fixtures.
- **374/374 web units** (372 ordinary plus two retention tests). New tests
  enumerate all 96 material/heading/up/down/roof/width selections, ready bounded
  crops, original diagnostic preservation, train stages and pure single-cell ghosts.
- **20 distinct focused Chromium scenarios** pass across passages/stairs, slim
  and wide platforms, station rendering and transport. EN desktop and JA 430×860
  touch cover stair map picking, all materials, preview/cancel/remove, peer state,
  exact HTTP reopen and checkpoint restart. Existing authority scenarios retain
  Observer, stale/late-response cancellation and world replacement coverage.
- Stair pixel comparisons cover **96 appearances × eight season/lighting variants**,
  **9,216,000 pixels and 9,216,000 alpha picks** against a separate literal crop
  table, including corrected index23. Twelve material/heading cases cover real
  train-art overlap, source before/after ordering, height cuts and 76,800 overlap
  picks. Existing passage comparisons retain 48 appearances × eight variants.
  Final EN/JA workflow captures and day/night galleries were visually inspected.
- **28 station Python tests**, **11 bundled preparation tests**, **four train
  delivery tests**, **821 independent literal fixtures**, pinned-source review,
  generated contracts/content, EN/JA locale checks, Svelte (zero errors/warnings),
  production build, project boundaries and whitespace checks pass. The existing
  Vite chunk-size advisory remains.
- The changed catalog hash is reconciled through current fixtures, generated
  profiles and stock localization. Train delivery is regenerated; the existing
  Release stock admission probe and source checker pass against the new hash.
  Historical moving-fleet/capacity evidence is not relabeled or rerun.

Early runs exposed test setup/assertion issues: target-yard indexing, immutable
array comparison, explicit height, restart access reset, preview fixture dispatch,
roof consistency and dynamically split harness imports. These were corrected.
The final sweep exposed a removed request counter in the existing passage test;
restoring it and rerunning those two workflows resolves both failures. Initial
sandbox runs could not bind localhost/build services; final server/browser runs
used approved normal local access. No unresolved failure is credited as passing.

Logs: `tmp/p6-15-dotnet-verified.log`, `tmp/p6-15-web-verified.log`,
`tmp/p6-15-build-final.log`, `tmp/p6-15-browser-verified.log`,
`tmp/p6-15-browser-passages-final.log`, `tmp/p6-15-browser-stairs-final.log`,
`tmp/p6-15-{python,preparation,train-delivery,fixtures,review,boundaries}.log`,
`tmp/p6-15-stock-admission.json` and `tmp/p6-15-stock-review.log`.
Final captures are under `tmp/p6-15-browser-verified/` and
`tmp/p6-15-browser-passages-final/`; the earlier stair gallery is under
`tmp/p6-15-browser-stairs-final/`.

```sh
dotnet test tests/FreeTrain.Online.Tests -c Release --no-restore -m:1 --filter 'FullyQualifiedName~Station|FullyQualifiedName~Platform|FullyQualifiedName~Passage|FullyQualifiedName~Mixed|FullyQualifiedName~Fleet|FullyQualifiedName~Train|FullyQualifiedName~Harbor|FullyQualifiedName~ObjectRemoval|FullyQualifiedName~NormalRailway|FullyQualifiedName~ContractTests|FullyQualifiedName~NetworkTests'
DENO_JOBS=4 deno task --cwd web test
deno task --cwd web build
deno task --cwd web test:browser:run passage.spec.ts thin-platform.spec.ts wide-platform.spec.ts station-rendering.spec.ts station-transport.spec.ts
deno task --cwd web test:browser:run passage.spec.ts --grep 'passage EN|passage JA'
python3 -m unittest discover -s scripts/tests -p 'test_station*.py'
python3 -m unittest discover -s scripts/tests -p 'test_bundled_preparation.py'
python3 -m unittest discover -s scripts/tests -p 'test_train_delivery.py'
python3 scripts/station-fixtures.py --check --source-root /Users/dmark/dev/freetrain-exav-archive
python3 scripts/review-stations.py --check --source-root /Users/dmark/dev/freetrain-exav-archive
python3 scripts/reconcile-train-delivery.py --check
dotnet run --project tools/StockAdmissionProbe -c Release --no-restore > tmp/p6-15-stock-admission.json
python3 scripts/review-stock-registration.py --runtime-report tmp/p6-15-stock-admission.json
python3 scripts/check-project-boundaries.py
git diff --check
```

Native/original-runtime/human acceptance, other browser engines, optional content,
full-suite/capacity/soak, Linux/container and deployment acceptance are not credited.
Demand, passenger service, fares, controllers and departures retain later P6 owners.
