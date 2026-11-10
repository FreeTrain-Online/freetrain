# P6-16 demand providers, catchment and listeners — 10 November 2026

Complete at the station-facing controlled-provider boundary. Population queries
and catchment relationships consume actual spatial owners. Constructible city
sources, population/land-value evolution and stadium game scheduling remain
P7/P8. [P6-17](web-parity-station-statistics.md) now activates hourly decay and
daily log rotation through the live upkeep dispatcher. Passenger exchange,
fares and station information remain P6-18 onward.

Authority is stable ExAV 2.1.1.12, Git
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, `trunk/`. The
[source contract](web-parity-station-contract.md) and
[independent fixtures](web-parity-station-fixtures.md) retain their hashes and
original expectations. Production table literals come directly from pinned Git
blobs, separately from the independent fixture arrays. No legacy runtime or CLR
objects execute.

## Population and controlled supply

[Population](../src/FreeTrain.Online.Simulation/Population.cs) implements ten
concrete hourly providers, all 42 distribution/entering tables (1,008 cells),
generic hourly/seven-day hourly providers, constants, ordered combinations,
multipliers and persistent wrappers. Queries use the existing `GameTime`,
including holiday/Sunday Bar mapping, School vacations, Leisure vacation
division by 125 and BusinessInn's shared weekday tables. Int32 products/sums are
checked; children truncate before parent addition/multiplication. Residents stay
distinct: School/Stadium 1, Leisure 10, other ordinary concrete providers' base.

[PopulationBindings](../src/FreeTrain.Online.Content/PopulationBindings.cs)
consumes the inert prepared catalog and binds all **169 population nodes**,
including nested combinations and the reachable stadium. Stadium starts with
source outgoing/entering 10 and has independent controlled outgoing/entering fields,
with residents 1. Persistent wrappers serialize immutable child bindings instead
of desktop object resolvers. No city structure or stadium game is constructed.

`StationProvider` carries population, land value, XY footprint/height scales,
ordinary/special classification and `input: "controlled"`. Identity, anchor and
occupied footprint come from the actual spatial owner at `ProviderId`. There is
no gameplay population editor or default passenger supply added to stations.
Existing unbound listener transport witnesses contribute zero demand.

Ordinary contribution is
`min(raw / localStationCount, landValue * XYSize * max(1, ZSize / 4) + 10)`;
special contribution is `raw / localStationCount`, without the land cap.
Stations sum only reverse memberships, using each provider's retained local count.
`Demand` and `Waiting` are separate queries; waiting subtracts `Gone` and clamps
at zero. Incoming demand and residents remain separate producer queries.

## Catchment and source anomaly

[StationCatchment](../src/FreeTrain.Online.Simulation/StationCatchment.cs) attaches
providers to existing stations in retained creation order, with truncated 3D
distance `<10` ordinary / `<20` special. Actual station construction advertises
to existing providers whose occupied voxels intersect its inclusive ±10/20 cube,
clipped to valid world voxels, without a spherical recheck. Intersection away
from an owner's anchor qualifies; multi-voxel owners receive one offer. Provider
iteration uses retained listener order as the portable deterministic choice;
source entity/set traversal is not claimed as an ordering guarantee.

The first four offers register both local and reverse edges. A strictly closer
fifth replaces the first farthest local slot, removes the old reverse membership,
and **does not register the newcomer in reverse**, preserving STATION-D003.
Equal distances do not replace. Reload preserves both arrays and their order;
it never recomputes nearest-four or repairs reciprocity.

**Removal disposition:** removing an unregistered fifth clears its local ID even
though the source cannot notify that listener. This retains P6-06's portable
removal policy and prohibits dangling saved references. It does not register the
missing reverse edge or seek a replacement station. Fixture `listener-13` remains
unchanged with its source-only dangling diagnostic; production expectations
explicitly exclude removed IDs. Other creation/tie/removal traces match literals.

Attach/update/detach return immutable world candidates and support providers
before the first station/graph. Provider changes revise their actual spatial
owners; reverse membership changes revise existing stations/owners once per
publication. A newly advertised station starts at revision 0. Previews stay pure
and commit uses the existing authority, booking, scheduler and stale-world gates.
Provider reclamation cleans reverse edges; station removal cleans local links
and disconnects platforms. Invalid expressions/queries or checked overflow publish
no partial state.

## Save/transport and P7 handoff

Save10/protocol8 gain optional `listeners[].provider` data in both schemas, .NET
DTOs and generated browser contracts. Absence means unbound; writers omit absent
data, retaining existing literal payloads exactly. This additive optional field
changes no format/protocol number, content hash or earlier-save support.
Expressions are data only, bounded to depth 16 / 256 nodes with valid child/table
shapes. Server/browser reject bound reverse edges without corresponding local
links. Metadata changes require the spatial owner's revision in both decoders.

| P7 producer responsibility | Implemented boundary |
| --- | --- |
| Construct a legal city owner with stable identity, anchor and footprint. | `StationCatchment.Attach(world, id, provider)`, including before the first station. |
| Bind the owner's content definition/population. | `PopulationBindings.Build(catalog)[contributionId]`; separate `Residents`, `Outgoing(time)`, `Incoming(time)`. |
| Supply land value at the anchor, XY size and height. | Explicit provider fields with source grouping/caps; no guessed global population or evolving land values. |
| Change population, land value or stadium outgoing/entering amounts with producer state. | `StationCatchment.Update` returns revised ownership; listener kind/geometry changes require detach/attach. Inputs remain labeled `controlled` until P7 supplies its producer contract. |
| Remove/reclaim producers. | `Detach` / `StationBuildingLifecycle.Reclaimed` clean memberships. Actual station construction already advertises to bound inputs. |
| Publish through the single-writer session boundary. | Candidate world/accounts/queue use existing `MixedSessionState.Successor`; helpers do not bypass authority or mutate live sessions. |
| Evolve land value and traffic after passenger movement. | Demand queries exist here; boarding/unload notifications remain P6-18, producer evolution remains P7. |

## Verification

- **1,409 relevant Release .NET tests** pass across stations/platforms/passages,
  mixed worlds, fleet/trains, persistence, contracts and networking. This includes
  **262 focused demand tests**, also rerun against the final generic percentage
  boundary, covering all 1,008 cells, 200 calendar queries, nested
  truncation/overflow, generic weekly/Friday Bar behavior, all 169 manifest nodes,
  14 listener traces, five sharing/cap vectors, footprint/z boundaries, actual
  construction/removal, owner revisions, waiting/demand, special/ordinary totals
  and exact reopen/continuation.
- **375 web unit/retention tests** pass (373 ordinary plus two retention).
  New client checks reject malformed/deep expressions, invalid input labels,
  missing local references and stale provider revisions.
- **8 focused Chromium scenarios** pass: actual construction/removal, EN desktop
  / JA narrow touch station tools/hosts, stale/draft/Observer fences and nested
  controlled providers retained through two-peer publication, corrupt upload
  rejection, reopen and independent checkpoint process restart.
- **28 station Python tests**, **821 independent fixtures**, raw-Git source
  review, project boundaries, generated contracts/content, EN/JA locale checks,
  Svelte and production web build pass. Existing Vite chunk-size advisory remains.

Logs: `tmp/p6-16-dotnet-final.log`, `tmp/p6-16-demand-final.log`,
`tmp/p6-16-client-focused.log`, `tmp/p6-16-web-final.log`,
`tmp/p6-16-build-web-final.log`, `tmp/p6-16-browser.log`,
`tmp/p6-16-browser-final.log`, `tmp/p6-16-{python,fixtures,review,boundaries}.log`.
The final two browser cases were rerun against the completed backend/client.
Captures: `tmp/p6-16-browser/` and `tmp/p6-16-browser-final/`.

```sh
dotnet test tests/FreeTrain.Online.Tests -c Release --no-restore -m:1 --filter FullyQualifiedName~StationDemandTests
dotnet test tests/FreeTrain.Online.Tests -c Release --no-build --no-restore -m:1 --filter 'FullyQualifiedName~Station|FullyQualifiedName~Platform|FullyQualifiedName~Passage|FullyQualifiedName~Mixed|FullyQualifiedName~Fleet|FullyQualifiedName~Train|FullyQualifiedName~ContractTests|FullyQualifiedName~NetworkTests'
DENO_JOBS=4 deno task --cwd web test
deno task --cwd web build
deno task --cwd web test:browser:run station-transport.spec.ts station-building.spec.ts station-building-domain.spec.ts station-hosts.spec.ts
python3 -m unittest discover -s scripts/tests -p 'test_station*.py'
python3 scripts/station-fixtures.py --check --source-root /Users/dmark/dev/freetrain-exav-archive
python3 scripts/review-stations.py --check --source-root /Users/dmark/dev/freetrain-exav-archive
python3 scripts/check-project-boundaries.py
git diff --check
```

Initial runs corrected synthetic-definition filtering, a TypeScript tuple-union
check and test setup/revision expectations. Final focused tests pass.
Native/original-runtime, other browser engines, optional content, capacity/soak,
Linux/container and deployment acceptance are not credited. No evolving-city or
passenger-service gameplay is claimed.
