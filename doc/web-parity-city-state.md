# P7-03 canonical city state — 11 November 2026

This slice delivers **detached domain definitions, ownership and continuation
contracts**. It consumes the pinned [P7-01 source/decision review](web-parity-city-contract.md)
and [P7-02 independent fixtures](web-parity-city-fixtures.md). It does not enable
city construction tools, passenger supply, diffusion or automatic development.
P7-04 supplies lifecycle execution; P7-05 supplies the live authority, save and
transport boundary. Save10/protocol9, the prepared catalog identity, supported
world limits and shipped English/Japanese resources remain unchanged.

## Production bindings and source consumers

`CityProfiles.Build` consumes the existing hash-checked `PreparedContentCatalog`
and `PopulationBindings`; `RailwayProfiles.Build` installs its immutable
`CityProfile` beside the existing terrain, fleet and station profiles. Exact pack
identity/version/hash equality is required. No source XML, desktop assembly,
process singleton or mutable gameplay RNG is activated.

| Reviewed input / original source consumer | Production destination | Verification / later owner |
| --- | --- | --- |
| All 209 P7-assigned declaration IDs, source types, implementation bindings and authored build flags in `city-review.json` | `CityContentBinding`, exact contribution/recipe/pack references | Exact-set and per-record comparison in `CityStateTests`; original source review stays immutable |
| 66 commercial / 55 variable definitions; `CommercialStructureContribution`, `VarHeightBuildingContribution` | `CityStructureDefinition`: dimensions, inclusive height limits, Int64 price, source integral price/area, population expression | All 225 independent definition cases; playable fixed/variable families remain P7-07–10/12 |
| Eight `HalfVoxelContribution` definitions / `HVStructure` | Exact direction/side/color selectors, `CityHalf`, one shared spatial owner | Compatible opposite sides share one full storage voxel; incompatible axes, duplicate sides and unavailable selectors reject; P7-11 owns placement/removal/drawing |
| Three `StandardRoadContribution`/A3 definitions and `RoadPattern` | `CityRoadDefinition` style/sidewalk/lanes and existing `TrafficComponents.Road` | All 45 literal masks/direction lists; P7-21–23 own routes, crops and construction |
| 66 land/fence declarations, `LandBuilderContribution`, `StaticLandVoxel`, rice/vinyl voxels | `CityGroundDefinition`: price, source grid, population and handler; realized ground identity/variant | Static ground, rice 3×3 indices and vinyl three-part indices; random-land selections must resolve to actual land definitions; P7-16–20 own lifecycle |
| `LandPropertyVoxel`, `FenceBuilder.DummyVoxel`, voxel fence slots | `CityProperty`, `CityFenceHost`, per-voxel `CityFence` | Owned/reclaimable zero-value property/dummy owners; fences reference actual owner **and voxel**, including Traffic/structures/terrain |
| `ConstructionSite` | Per-voxel `CityConstructionVoxel` over existing `ConstructionCell`, named `ScheduledAction` handles | Full occupied volume, unowned/zero-valued site, no subsidiary/provider, one handle per unfinished voxel; P7-04 owns gates, bookings and draws |
| `PopulatedStructure`, `VarHeightBuilding`, `HVStructure`, land producers | `CityProducer` bound to the real owner ID and initial contribution/height | Existing immutable `Population` expressions reused; height multiplication stays outside child truncation; initial half snapshot survives removal of that visual half |
| `Commercial`, `VarHeightBuilding`, `HVStructure`, `Parameters`, `SubsidiaryCompany` | Structure price/area/population inputs, `CitySubsidiary`, `CityValuation` | Three literal cumulative market traces; fixed/half entity value uses current market price, variable entity value uses unit-price × height; P8 owns full market lists/history/reports/events |
| `LandValue` q/back/rho and static settings | Immutable `CityHeatState`, `CityDevelopmentSettings` | H-major/V-minor arrays include both borders, source float32 values and distinct back buffer; initial interior-only bare rho; negative deposits can survive until source diffusion clamps them; P7-06 executes heat |
| `SearchPlan`, `Plan`, `ULVFactory`, `ULV` | `CitySearchPlan`, `CitySelectedPlan`, `CityLandCache` | Durable phase, strength, budget, four work indices, scan site, selected definition/extent/value/final price and station ID; P7-25–28 implement search/settings |

The generic null parent/zero-volume generated child, menu/category registrations,
road accessory/dummy-car menus, stadium and team-name provider retain exact inert
bindings. They cannot acquire a city footprint through this profile. Stadium
lifecycle remains P8; no bundled roadAccessory/DummyCar instance is invented.
Existing P3 forest/creek occupants and their realized decoration remain in
`TerrainWorld`, with their existing spatial IDs; this slice adds no duplicate
forest/creek grid. Category controls, localization and all 215 reviewed action
records retain their P7-01 owners. This domain-only slice adds no controls or
changes to the reciprocal MAN/DEV/action obligations.

## One ownership graph

`MixedWorld.City` composes `CityGraph` into the **same** `SpatialIndex` and positive
Int64 allocator as terrain, Traffic, stations and fleet. Roads occupy the existing
Traffic component slot and may share it with rail. Traffic retains its established
eight-heading bit representation: source road masks N/E/S/W = 1/2/4/8 map to
1/4/16/64. The explicit inverse maps back to the source art selector. Existing
crossing predicates therefore keep their current semantics; no second road grid
or protocol reinterpretation is introduced.

Full/variable structures occupy their complete exclusive box, including invisible
middle/interior cells. Half buildings own one whole cell; their visual height and
two component contributions do not grant storage sharing to unrelated entities.
Property and source DummyVoxel ownership are independent from silent
reclaimability and entity value. Fences attach to a specific occupied voxel and
side; a vacant fence location needs an allocated DummyVoxel. Static ground, rice
and vinyl retain realized variants; menus/removers/random selectors cannot
masquerade as realized ground. All bounds/collisions are checked by the shared
index. `CityOwnership.Placement` checks source level-ground/player reuse legality
without allocating, booking, mutating buffers or consuming random values. As with
existing stationary owners, placement legality is separate from graph restoration;
restoring a footprint does not rebuild it against today's terrain placement rules.

Detached replacement validates profile identity, owner payload/footprint/value,
allocator monotonicity, station/fleet/terrain identity conflicts, owner revisions,
producer references, construction lifecycle, fence hosts, search station/site and
callback handle uniqueness before returning a candidate. Existing city identities
cannot move, change contribution/owner kind, rewind revisions or be resurrected
below the allocator. Changed city payloads and voxel fences require their owner's
revision. Removed owners cannot leave city producers or voxel fences dangling.
No failure changes the prior world, accounts, allocator or graph. Later lifecycle
operations must supply all affected successor graphs atomically.

## Durable data and derived indexes

`CityState` is an immutable data capture with checked graph reconstruction.
`CityCandidate` combines a `MixedConstructionState` with the existing `ClockState`
and **one** `RandomStreamsState`. The existing named construction/development/
land/subsidiary streams are retained; there is no city-specific replacement RNG
bank. Construction, subsidiary, heat and development callback descriptors retain
registration, FIFO order, due minute, handler and captured interval. The envelope
validates queue membership alongside existing railway/fleet/station callbacks,
using the canonical clock/random restorers, and checks booking time boundaries.
It binds no executing simulation delegates.

Dictionary indexes over owners/definitions, sprite lookup caches and candidate
pool indexes are rebuildable from validated state/content. **Non-null ULV samples
are durable:** the source cache deliberately stays stale. Their sampled land and
unique-entity totals survive removal/repricing of the original objects; they do
not become dangling entity references. Null ULV results are not stored, preserving
retry behavior. Both heat buffers, border values, conductivity, search work and
selected plan must survive round-trip, rather than being regenerated from a seed
or current geometry. P7-25 implements the source pool/search ordering, not this
state slice.

Settings currently have representation checks for finite numeric values, positive
integral cadence and a finite nonzero reciprocal price power. This is **not**
complete runtime settings admission. P7-28 must validate reachable powers, casts,
products and `Next` bounds against actual world/content values under CITY-D010,
without imposing balancing ranges. A saved timer interval remains separate from
the current cadence/deposit setting.

P7-03 verifies complete detached `CityState`, clock and random JSON round-trips,
including a mixed station/fleet/road world. These are in-process data-contract
checks, **not a new public save format**. `MixedSessionState`, `MixedWire.Read` and
`MixedWire.RailwayEntities` explicitly reject detached city graphs until P7-04/05
implement their lifecycle and codecs. Current Save10 code therefore cannot
silently discard city buffers/plans/listeners. Bundled city road references also
require a city graph, so omitting the graph cannot bypass this boundary.
`StationProvider.Input` remains
`controlled`: city producer descriptors cannot be published as controlled station
listeners. P7-05 extends that contract; P7-07/13 attach real supply at its source
lifecycle point, preserving both existing listener directions. No total population
is stored on a station by this slice.

## Verification

`CityStateTests` has 14 focused tests. They consume all 209 reviewed declaration
records, all 225 literal fixed/variable definition cases, all 45 road masks and
three cumulative valuation traces. Additional invalid/valid graph witnesses cover
invisible-cell collisions, half sharing and initial listener snapshots, property
reclamation, revisions/allocator reuse, actual ground/fence/Traffic owners,
construction handles, malformed heat/settings/cache/search state, station/fleet
identity conflicts, whole-state JSON round-trip, FIFO ties and isolated random
continuation. No expected value is generated by production code.

Reproduce:

```sh
dotnet test tests/FreeTrain.Online.Tests --no-restore -m:1 -nodeReuse:false -p:UseSharedCompilation=false --filter FullyQualifiedName~CityStateTests
dotnet test tests/FreeTrain.Online.Tests --no-restore -c Release -m:1 -nodeReuse:false -p:UseSharedCompilation=false
python3 scripts/city-fixtures.py --check --source-root /path/to/freetrain-exav-archive
python3 scripts/review-city.py --check
python3 -m unittest discover -s scripts/tests
python3 scripts/check-project-boundaries.py
deno task --cwd web test
deno task --cwd web check:generated
deno task --cwd web check:content
deno task --cwd web check:locales
```

Fresh commands/results/artifact hashes are recorded in `tmp/p7-03-validation.json`
and the ordered checklist. Browser execution is not claimed: no city browser
workflow is enabled in this state-only step. Construction lifecycle, live city
persistence/transport, land-value execution, real station supply, automatic
development, native/optional/capacity and P7 phase acceptance remain open.

Fresh execution: **Release 2,318/2,318 .NET tests pass** (3m55s), with zero
build warnings/errors. After the final city-road publication guard, **101/101
focused Debug tests pass**, covering all 14 city tests and the complete heightmap,
railway-state, station-state and cross-tool classes. The broad Release run used
the immediately preceding assembly; the final guard has the explicit focused
regression witness. The initial broad Debug run recorded 2,316 passes and two
heightmap per-thread allocation budget failures (16×16×16 and32×32×64); those
measurements pass in the separate complete heightmap class and Release run.
No allocation budget or existing test was weakened.

Client checks: **386 pass** (384 ordinary plus two retention checks). Python:
**259 run,258 pass,one existing historical-sample skip**. All703 city fixtures /
197 source files pass original-Git checking. P7-01 review, project boundaries,
generated contracts, prepared content/catalog identity, both locales, report links
and whitespace checks pass. Tool versions: .NET SDK10.0.402, Deno2.9.8,
Python3.15.2. Logs and SHA-256s are retained in `tmp/p7-03-validation.json`.

## P7-04 continuation handoff

[Detached generic construction](web-parity-city-construction.md) now executes
site lifecycle, caller-specific charges and completion ownership, and preserves
the caller alongside each site's original identity. `CityCandidate` also checks
account capacity including accessory completion reservations and site-delay
bounds. Public Save10/protocol9 codecs and live `MixedSessionState` still reject
city graphs pending P7-05; the historical P7-03 test counts above are unchanged.
