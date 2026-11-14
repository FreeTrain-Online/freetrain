# P7-07 fixed city construction and actual producers — 14 November 2026

Fixed city placement, construction completion and whole-owner demolition now
execute in the canonical mixed-world domain. Completion attaches the prepared
population expression to the actual owner through `StationCatchment.Attach`,
using real anchor land value, footprint area and height. Previewing a site never
registers a provider or changes the authoritative account, queue or random bank.
This delivers P7-07's domain scope. Rendering/picking and playable placement/
demolition controls remain P7-08/09; comprehensive metadata refresh remains P7-13.

## Source rules and entry points

Authority is stable ExAV 2.1.1.12, archive Git
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, subtree `trunk`.
The [source contract](web-parity-city-contract.md),
[independent fixtures](web-parity-city-fixtures.md),
[construction kernel](web-parity-city-construction.md),
[live transport](web-parity-city-transport.md) and
[heat runtime](web-parity-city-heat.md) remain inputs. Original source hashes are
retained in the P7-02 source ledger and verified against the archive revision.

| Source | Executed rule / production binding |
| --- | --- |
| `MultiSelectorController.canBeBuilt/onClick` | Catalog placement requires the entire X/Y/Z volume to be vacant, including invisible upper cells; it spends the fixed declaration price to Subsidiaries at site creation. The source predicate imposes no level-ground requirement. |
| `FixedSizeStructController.onClick`, `Commercial.canBeBuilt`, `Structure.canBeBuilt/isOnTheGround` | Legacy placement requires level ground, excludes MountainVoxel and permits only silently reclaimable occupants. It has no manifest-price spend. |
| `Structure.canBeBuilt(ControlMode.com)` | Automated placement requires occupied entities to be owned; vacant cells do not fail the source non-null voxel enumeration. Level-ground/Mountain checks still apply. Completed ownership is false and no manifest-price spend is invented. Automatic search/fit admission remains P7-25–27. |
| `ConstructionSite` | Existing per-voxel Z/X/Y constructor draws, weekday/hour/vertical gates, door/ground stages, FIFO retry handles and fresh completion identity are reused. |
| `PopulatedStructure` then `Commercial` constructor | Fixed listener attachment precedes subsidiary valuation and `time-length` draw. Both are private staging work until the entire successor validates. A late failure discards listener changes too. |
| `PThreeDimStructure.remove`, `PopulatedStructure.remove` | Whole owner removal spends the checked source Int32 product `500000*sizeX*sizeY`, clears all occupancy and both listener directions, and cancels subsidiary/site handles. Site cancellation has no removal spend/refund or new random draw. |
| `LandVoxel.remove`, `LandPropertyVoxel.remove`, Creek removal | Legacy reclamation removes the real ground/property/dummy/creek owner and its decorations and producer references without inventing a demolition fee. |

`FixedCityConstruction.Preview` returns an immutable, retained candidate;
`Commit` fences construction identity, clock minute and registration order, then
uses the existing session successor validation. `PreviewRemoval` checks exact
owner identity/revision at the picked voxel, including upper occupancy. Variable
and half definitions are rejected by this fixed-family entry point. Prepared
manual/automatic build flags remain distinct. No new network command, test-only
completion command, save version, protocol version, content identity, capacity
limit or shipped EN/JA string is introduced.

Water is the source world plane, not a separate occupied sea voxel. An otherwise
empty catalog volume can be elevated or below the water plane; the legacy caller
still requires the ground level. Implicit underground terrain and outside cells
reject safely. Mountain and protected railway/station occupants reject. Owned
railway/terrace/half reclamation requires those families' actual removal policies
and is not approximated by deleting their spatial records; automatic mixed-family
fit remains P7-27. The reviewed obsolete inverted builder predicate remains
source evidence rather than an additional executable broken entry point.

## One successor for geometry, bookings and listener lifecycle

`CityConstruction.StartCleared` receives the validated ordered reclamation list.
It stages full-owner removal, terrain removal, city/fence/producer cleanup and
callback cancellation; books nested demolition calls in source X/Y/Z discovery
order; and finally stages the new site and its catalog charge. All transactions
share one world revision. Account/queue reservations include retained railway
sites, and all allocator/order/valuation arithmetic remains checked.

Completion retains the declaration's real population binding. Fixed attachment
uses a private provisional subsidiary value only to validate the staged graph
before the actual subsidiary constructor; it cannot be saved or published.
The final market value uses actual heat-based center land value and the source
cumulative valuation formula. Provider land value is the actual owner-anchor
sample, with area and height passed separately. Station creation order, strict
attach distance, four slots and the source fifth-station asymmetry are inherited
from `StationCatchment`; load never repairs or re-advertises edges.

Whole-owner removal and automated replacement remove the subsidiary-facing
record, timers, provider and both forward/reverse station references before
validation. Repeated/stale demolition cannot charge again or resurrect an owner.
P8 market dispatch remains unavailable when its timer becomes due; P7-13 owns
updates to retained provider metadata as land value evolves.

## Verification and reproducibility

`FixedCityConstructionTests` exercises real station construction and natural city
construction, all three caller ownership/booking rules, no-effect previews,
elevated/water/ground predicates, invisible upper obstacles, exact saved
continuation, no-population completion, content flags, creek/ground/property/fence
reclamation, whole-owner upper-voxel demolition, ordered nested removal charges,
stale plans, cancellation and late subsidiary-failure rollback. A pinned bundled
residential declaration has price ¥11,000,000, ten residents and area/height 1/1.
Its real heat sample is 30; the source January 2 holiday 09:00 demand is 4,
January 4 weekday 09:00 demand is 8, and January 2 14:00 demand is 3.

The browser fixture `web/tests/fixtures/fixed-city-v11.json` is emitted by
`Natural_completion_fixture_uses_real_site_station_callbacks_and_exact_save_continuation`
after constructing a real station and advancing a real seeded site to one minute
before its natural completion. The heat buffers are explicit fixture initial
conditions, not a controlled provider or replacement land-value callback.
Companion before/snapshot/delta files come from production codecs. Web tests
apply that delta and reject partial or replayed publications. The Chromium
workflow observes zero then three outgoing passengers in the existing EN/JA
station inspector, receipt replay, Observer rejection, exact save/reopen and an
actual backend checkpoint restart. This grants no city rendering or placement UI
acceptance ahead of P7-08/09.

Fresh results: **417 selected .NET tests, 36 web tests, three Chromium workflows
and 31 city Python tests pass**. The web count combines 35 passing checks from the
selected five-file run with the corrected completion-delta test's focused rerun;
its first assertion distinguished an absent optional `trainMotion` property from
an explicitly undefined one, so comparison now uses their serialized wire shapes.
The first browser completion run needed an explicit station selection before
reading the inspector; its focused rerun passes. The other two city workflows
passed in the initial run. Browser fixture setup also ran 13 selected existing
.NET fixture tests. No complete suite was run.

Both builds pass; generated/content/locale checks and Svelte diagnostics pass
with zero errors/warnings. Vite retains the existing large-chunk advisory. All
703 independent source cases / 197 original files, P7-01 review, project boundaries
and whitespace pass. The installed .NET/Deno versions match the repository pins.
The .NET test runner and local browser servers required sandbox socket access.
Commands, results, iteration details and hashes are recorded in
`tmp/p7-07-validation.json`.

```sh
dotnet build tests/FreeTrain.Online.Tests --no-restore -m:1 -nodeReuse:false -p:UseSharedCompilation=false
FREETRAIN_FIXED_CITY_OUTPUT="$PWD/web/tests/fixtures/fixed-city-v11.json" dotnet test tests/FreeTrain.Online.Tests --no-build --no-restore -m:1 -nodeReuse:false --filter 'FullyQualifiedName~FixedCityConstructionTests|FullyQualifiedName~CityConstructionTests|FullyQualifiedName~CityPersistenceTests|FullyQualifiedName~CityHeatTests|FullyQualifiedName~CityStateTests|FullyQualifiedName~StationDemandTests|FullyQualifiedName~StationBuildingConstructionTests|FullyQualifiedName~StationaryConstructionTests|FullyQualifiedName~StationLifecycleTests'
cd web
deno test -A --no-check tests/city-state.test.ts tests/station-state.test.ts tests/mixed-state.test.ts tests/state-transfer.test.ts tests/station-information.test.ts
deno task build
FREETRAIN_ONLINE_TEST_SERVER_CONFIGURATION=Debug deno task test:browser:run tests/browser/fixed-city.spec.ts tests/browser/city-transport.spec.ts tests/browser/city-heat.spec.ts --project=canonical --workers=1
cd ..
python3 -m unittest discover -s scripts/tests -p 'test_city*.py'
python3 scripts/city-fixtures.py --check --source-root /path/to/freetrain-exav-archive
python3 scripts/review-city.py --check
python3 scripts/check-project-boundaries.py
git diff --check
```

Current-format continuation remains Save11/protocol10/City1. Historical import,
native/human fidelity, optional content, capacity/performance and P7 phase exit
retain their existing later owners.
