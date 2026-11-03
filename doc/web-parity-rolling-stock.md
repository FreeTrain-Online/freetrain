# P5-03 rolling-stock registration — 3 November 2026

P5-03 is complete at registration and detached-formation admission scope.
[`RollingStockCatalog`](../src/FreeTrain.Online.Content/RollingStockCatalog.cs)
registers all **six bundled trains, eight cars and seven color templates**, with
no unresolved stock diagnostics. Server startup registers this typed catalog
separately from prepared authoring metadata. The
[source-bound registration record](parity/train-stock-registration.json) accounts
for every stock declaration in the P0/P5 ledgers and both generated reverse
selection references. There are no generated stock declarations. The current
bundled catalog remains byte-identical:
`9e85cf889fd8bd49c24c6173c971bf7fe935a5d3a36438f4e242e84bee3c35b9`.
Save8/protocol6, railway bindings and existing world limits are unchanged.

This supplies executable content bindings for subsequent fleet work. Fleet
identity/occupancy/callbacks, save/transport, purchase/sale mutations, deployment,
movement and browser purchase controls remain P5-04 onward. P4 trusted test cars
cannot resolve as rolling stock; no bundled Car state handler is enabled here.
Prepared contribution statuses still describe inert authoring metadata,
independently of the explicitly registered stock handlers.

## Exact factory and numeric contracts

The [P5-01 contract](web-parity-train-contract.md) and pinned ExAV
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0` remain the authority. Bindings retain
original IDs, company/type/name/author/description, price, native minutes/voxel,
fare, clamped amenity and trip range. Source text remains available for the
source-bound EN/JA purchase presentation in P5-10; this step introduces no new
browser text or purchase panel. Unavailable-definition diagnostics carry English
and Japanese messages plus a stable code, identity and technical detail.

Parameterized creation fills the body, replaces the head, then replaces the
tail: **the tail wins at length one**. Explicit patterns ignore whitespace,
resolve exact character/car references, preserve mixed order and allow only
declared lengths. Duplicate lengths/characters, missing body/characters/cars,
cycles, unsupported implementations and oversized patterns are rejected or
diagnosed before admission. Formations are immutable.

Reverse cars inherit both raw capacities and retain their exact recipe/base
identity. Per-car seated values outside 0…normal substitute normal capacity;
packed capacity is `seated + 2*(normal-seated)` after the source aggregate guard.
Price and capacity arithmetic is checked at its original Int32 width. Quotes
multiply safe formation price by quantity in checked Int64. Negative price,
fare or normal capacity and nonpositive/malformed/out-of-Int32 native speed are
unavailable content. Positive numeric speed, including 5 and Int32.MaxValue,
is retained without clamping. Future due-time validation remains with the clock.

The source form's decimal purchase quote can represent a longer expensive
formation than the source `price(length)` Int32 method can. Under P5-01's checked
arithmetic policy, this port rejects those lengths **before fleet admission**
instead of allowing overflow later during resale. This is a portable restriction,
not a claim that the source factory refuses those lengths.

| Bundled family | Price/car | Minutes/voxel | Fare | Amenity | Safe admitted lengths |
| --- | ---: | ---: | ---: | ---: | --- |
| FT1 | 30,000,000 | 2 | 400 | 100 | 1–71 |
| FT2 | 28,000,000 | 3 | 400 | 100 | 1–76 |
| FT3 | 25,000,000 | 4 | 400 | 100 | 1–85 |
| 485 雷鳥 (Raicho) | 50,000,000 | 2 | 750 | 120 | 1–42 |
| E3 こまち (Komachi) | 70,000,000 | 1 | 1,000 | 140 | 1–30 |
| Color-test train | 100,000,000 | 3 | 400 | 100 | 1–21 |

Five-car normal/seated/packed capacities are **660/330/990** for each FT family,
**340/340/340** for Raicho, **304/304/304** for Komachi and **0/0/0** for color-test.
Color-test is authored content: first declared template and red/blue/green/yellow;
an unavailable first template is diagnosed without falling through to another.
All seven templates require prepared pictures. Cars require complete ready 8+4 or
16+8 selectors; reverse base, capacity, template and palette must agree with art.

Pure resale valuation accepts nullable absolute purchase minutes: null returns
zero; completed 365-day years give `price(length)/(years+2)` with integer
truncation. Tests consume P5-02's five anniversary/null literals. Inventory,
withdrawal, callbacks and accounting remain P5-06.

## Source UI and portable admission

Pinned `TrainTradingDialog.cs` SHA256
`7124579b0f78952904039471605c424a2a47af41668f0db2d904b6b0c00b32c6`
sets length Minimum=1 at lines 184–188 and no Maximum on either numeric control.
The [Microsoft .NET Framework API reference](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.numericupdown.maximum?view=netframework-4.8.1)
documents inherited Maximum=100. The manual's 100-car claim therefore agrees with
the source purchase UI; neither factory establishes a universal 100-car ceiling.
This resolves TRAIN-F001's control question without claiming native UI execution.
Quantity zero remains a source UI empty action, not a purchase request.

[`StockAdmission`](../src/FreeTrain.Online.Simulation/RollingStock.cs) enforces:

- Formation maximum **100 cars**, further restricted by declared patterns and
  checked price/capacity arithmetic.
- Quantity maximum **100**, request maximum **1,024 cars**.
- Fleet maximum **128 trains / 1,024 cars**, including unplaced inventory.
  Quotes validate cumulative owned counts with widened admission arithmetic.

Formation/quantity caps preserve the source purchase range. Request/fleet caps
are explicit portable resource policies. P5-04/06 must enforce them against actual
fleet state at authoritative execution; browser-supplied counts are not authority.
P5-28 must validate/revise them against legal moving fleets. Registration
measurements establish no movement/network/browser/save/passenger/full-city
capacity; P4-CAP-001/002 remain deferred to P11.

The reproducible [Release probe](../tools/StockAdmissionProbe/Program.cs) uses
real bundled definitions, distinct formation arrays and checked admission for
each addition. Registration budgets: cold catalog+registry<1s; warm registration
p95<100ms and allocation<2MiB; request/fleet formation construction p95<10ms and
allocation<256KiB per operation.

Hardware: Apple M1 Max, 64 GiB RAM, 10 logical processors; macOS 26.7.1 Arm64;
.NET SDK 10.0.402/runtime 10.0.13, Release. No RTT/browser involved. Cold catalog+
registration **120.39ms**. Warm runs after 20 warmups:

| Operation | Samples | Median / p95 / max (ms) | Allocated bytes/operation |
| --- | ---: | --- | ---: |
| Registry construction | 200 | 2.062 / 2.388 / 2.743 | 559,805 |
| 100 trains ×10 cars request | 1,000 | 0.0064 / 0.0073 / 0.6038 | 16,400 |
| 64 trains ×16 cars maximum request | 1,000 | 0.0055 / 0.0058 / 0.1650 | 13,760 |
| 128 trains ×8 cars detached fleet | 1,000 | 0.0129 / 0.0135 / 0.1686 | 40,984 |

All registration budgets pass. Allocations count operation-local managed bytes,
not resident memory or future per-instance fleet/callback state.

## Optional separation and verification

`OptionalPluginRegistry.RollingStock` includes explicitly selected, hash- and
dependency-verified packs. `RollingStockCatalog.Bundled` remains base-only.
Train resolution requires exact pack/version/catalog hash/contribution;
car resolution requires exact pack/contribution/recipe. `Matches` checks the
entire pack set for later world/fleet validation. Unknown stock, incompatible
identities, missing art and broken dependencies cannot acquire fallback bindings.
Startup registers verified stock and logs unresolved diagnostics.

The optional preparer now explicitly supports source `PatternTrainImpl` and
`ColoredTrainCarImpl`, including native palette reduction, without loading DLLs.
An original synthetic source pack was prepared outside the repository and loaded
through the verified optional registry/probe: **7 trains / 9 cars / 7 templates**,
no diagnostics; declared lengths 1/4 yield mixed capacities **464/182/746**,
speed 5, amenity 200, trip-range 1 and exact authored colors. The bundled registry
remains **6/8/7** before/after. No non-free/sibling-set assets enter this repo.
Full optional suites and P2 human/native qualifications remain P5-29;
TRAIN-F003's TAT resource diagnostic is unchanged.

Fresh selected verification; logs under local `tmp/p5-03-*`:

- **105/105 .NET**: `RollingStockTests`, `PreparedContentTests`,
  `TrainFixtureBoundaryTests`, `OptionalPluginTests`, `MixedWorldTests`,
  `MixedPersistenceTests`. Covers all 21 records, all 448 P5-02 car-crop recipe
  identities, literal economics/capacities, malformed graphs/packs/numerics,
  null dates and admission boundaries.
- **23/23 Python**: stock-registration 2, optional-preparation 11, content-recipes 10.
  Actual source preparation covers patterns/colors and rejected duplicate/unknown/
  oversized patterns. Bundled preparation `--check` passes.
- **14/14 selected web unit tests**: content, content-recipes, train-fixtures,
  optional-content. **One Chromium representative gallery workflow passes**
  (Deno 2.9.7, Playwright 1.64.1; fresh Debug backend, unchanged existing web build).
  No full web/browser sweep, following the owner's request.
- Default/raw-pinned-source P5-01 review, P5-02 literals, P5-03 registration review,
  generated contracts, locales, project boundaries and `git diff --check` pass.
  No new managed package dependency or catalog regeneration.

Reproduction from the repository root:

```sh
dotnet test tests/FreeTrain.Online.Tests --no-restore -m:1 -p:UseSharedCompilation=false --filter 'FullyQualifiedName~RollingStockTests|FullyQualifiedName~PreparedContentTests|FullyQualifiedName~TrainFixtureBoundaryTests|FullyQualifiedName~OptionalPluginTests|FullyQualifiedName~MixedWorldTests|FullyQualifiedName~MixedPersistenceTests'
dotnet run --project tools/StockAdmissionProbe -c Release -p:UseSharedCompilation=false -p:BuildInParallel=false > tmp/p5-03-stock-admission-final.json
python3 scripts/review-stock-registration.py --runtime-report tmp/p5-03-stock-admission-final.json
python3 -m unittest discover -s scripts/tests -p 'test_stock_registration.py' -v
python3 scripts/prepare-bundled-content.py --check
```

From `web/`:

```sh
deno test -A --no-check tests/content.test.ts tests/content-recipes.test.ts tests/train-fixtures.test.ts tests/optional-content.test.ts
FREETRAIN_ONLINE_TEST_SERVER_CONFIGURATION=Debug deno task test:browser:run reference-world.spec.ts --project=canonical --grep 'bundled recipe gallery representative' --workers=1
```

P5-04 starts from these bindings/limits. TRAIN-F001's moving-resource qualification
remains P5-28; TRAIN-F003's resource/gallery/native/optional qualifications retain
their recorded scopes.
