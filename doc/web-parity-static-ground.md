# P7-16 static ground cover and farmland — 16 November 2026

The bundled browser/portable slice adds all **20 static farmland definitions and
five authored mixed-farmland selectors** to the existing Land task (`O`). English
and Japanese controls provide catalog thumbnails/provenance, two map corners or
coordinates, a fixed starting level, eligible/skipped classifications, exact
Subsidiaries cost review, confirmation and cancellation. Enter confirms the
reviewed edit; Escape cancels. General demolition and the rectangle ground-removal
action remove actual ground owners and their population listeners.

## Source bindings and intentional dispositions

Authority remains ExAV 2.1.1.12, archive Git
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, `trunk/`. The original
[P7 source decisions](web-parity-city-contract.md), especially CITY-D008/014/016,
and [independent city fixtures](web-parity-city-fixtures.md) remain binding.

| Source consumer | Delivered behavior |
| --- | --- |
| `core/contributions/land/LandBuilderContribution.cs`, `DefaultControllerImpl.cs`, `core/controllers/RectSelectorController.cs` | Inclusive normalized XY rectangle at the first ground corner's fixed Z. Every bundled static/mixed definition has grid 1×1. Wrapper books price × full selected area, including blocked, uneven and outside later cells; an entirely blocked selection can still charge. Review shows that source cost explicitly. |
| `core/world/land/LandVoxel.cs`, `core/world/World.cs` | Each placed cell must be at its own exact ground level and empty or silently reusable. Ownership does not prevent silent reuse: owned property and ground cover can be replaced. No entity purchase, land purchase or demolition charge is added to reuse. Water has no extra `LandVoxel` prohibition. Rail, buildings and mountain owners remain protected. |
| `core/contributions/land/StaticLandBuilder.cs`, `core/world/land/StaticLandVoxel.cs` | Immediate owned one-voxel ground, exact definition, variant zero, contribution price as entity value, authored sprite at all traversed height cuts. Terrain geometry and its forest/creek provenance remain in the existing P3 graph. Reclaimed decorative creeks are removed through their existing ownership graph. |
| `core/contributions/land/RandomLandBuilder.cs`, farmland manifest `lands` lists | Five ordered candidate lists resolve to the actual 20 static definitions. One saved `land-selection` draw per eligible cell in X-outer/Y-inner order; no draws for blocked cells. Resulting ground/provider identity uses the selected static definition, never the selector. Catalog mosaics use the authored order without gameplay random draws. |
| `StaticLandVoxel` population construction/removal | All 20 static definitions actually declare AgriculturalPopulation base 5. Attach that real provider immediately, including both P6 catchment directions, and detach it on replacement/removal. Literal source hourly truncation gives outgoing/incoming zero for base 5 at 5%; no positive farm economy is invented. Property ownership alone creates no provider. |
| `LandBuilderContribution.createRemover` | The native remover returns the same controller whose selection calls `contrib.create` again. The browser retains explicit real teardown through its removal task/general demolition, rather than reproducing a misleading rebuild action. This is a portable control adaptation; static owner teardown itself retains the source's zero fee. |

The five `lands` lists are immutable profile admission data. They cannot reference
selectors, nonstatic ground or absent contributions. Realized ground still admits
only static/rice/vinyl handlers; the latter two retain their later lifecycle owners.
The original reviewed P7-01 source snapshot is unchanged.

## Art and picking

All 20 static recipes render and alpha-pick through the existing `CityRenderPlan`
and terrain scene, using all four authored seasonal sets and day/night surfaces.
Catalog thumbnails and the map share the same executable frames and checked alpha
masks. Owners participate in the shared spatial graph, height cuts, water handling
and incremental invalidation. General map demolition binds the picked owner's
exact identity/revision.

**ASSET-CROP-15–18 portable qualification:** four final-row farmland definitions
request `[x,64,32,32]` from 128×80 source sheets. The derived executable frame uses
only the existing `[x,64,32,16]` pixels, retaining the original anchor. Missing
lower pixels remain transparent; no replacement art is invented. The generator
checks every seasonal/day/night sheet hash, extent, RGBA pixels and alpha mask.
Historical diagnostic frames, prepared catalog/pack identities and source hashes
are preserved. This is explicit portable rendering, not native crop acceptance.

## Atomic review, publication and continuation

The existing `/api/land/preview` and `tradeLand` authority boundary carries cover
placement/removal actions and an optional selected definition. Eligible preview
cells expose their exact realized static definition, including mixed selections,
so changed random choices invalidate a held quote. Preview/cancellation draw on a
detached bank and leave the canonical save, accounts, allocator and random state
unchanged. Epoch, role, source minute, content, affected owners, geometry and cost
revalidation retain the established quote/receipt rules.

One edit publishes one coherent world successor. Ordered provider operations
preserve source catchment order while affected stations receive one published
successor revision. Ground, property/fence reclamation, both listener directions,
Subsidiaries receipts, allocator and saved random state validate together. A late
allocator/account/transport failure abandons the entire staged edit. The selection
is bounded to 4,096 cells under the existing owner/account/transport limits.

Existing City1 ground, producers, heat and random state carry exact continuation;
**Save11/protocol11/City1 and prepared content identities remain unchanged.**
First activation stays private until commit and registers one heat timer. Saved
static and mixed farmland reopens with the exact selected definitions, listener
order, receipts and next random draw. Browser tests also restart the backend with
farmland present and observe a peer's real removal.

## Verification and retained scope

Fresh focused evidence: **125 C#,53 Deno and five unique Chromium workflows**
pass, with zero skips/retries in successful selections. Generated contracts/art,
content/locales/Svelte/build, all703 independent fixture cases/197 pinned source
files, P7-01 review, project boundaries and whitespace checks pass.

Focused commands, counts, logs and hashes are recorded in
`tmp/p7-16-validation.json`; no complete C#, Deno or browser suites run.
`StaticGroundTests` uses literal source prices, all five candidate lists and
controlled last/first random samples, rather than production-generated numeric
expectations. Exported canonical successors are transport fixtures, not independent
numeric oracles. Existing land trading, fixed construction, city state/population,
persistence, removal and mixed-terrain regressions accompany the new tests.

Deno checks all 20 definitions × eight appearances, real alpha picks and height
cuts, five deterministic mixed catalog mosaics, crop provenance rejection,
coherent C# deltas, exact charges, malformed reviews and general demolition.
Chromium covers EN desktop keyboard and JA phone touch, cancellation, all 25
catalog choices, final-row art, actual providers, peer teardown, random-preview
isolation, four committed mixed draws and exact reopening/restart. Existing
EN/JA land-trade/current-value workflows retain their regression coverage.

[Forest authoring P7-17](web-parity-forest-authoring.md) now extends the catalog to43
choices and rectangle removal to forests. Rice remains P7-18, creek authoring P7-19,
fences P7-20, development P7-25–29 and measured city capacity P7-32. Native,
optional, full-city/performance and overall P7 acceptance retain their gates.
