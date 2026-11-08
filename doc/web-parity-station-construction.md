# P6-06 station-building construction domain — 8 November 2026

Complete at construction/authority scope. All 35 bundled station contributions
construct immediately as real Station identities with their exact authored
orientation, full footprint and manifest price. Placement does not create a
ConstructionSite, railway-stationary substitute or completion callback.
[P6-07 rendering](web-parity-station-rendering.md) is delivered separately;
P6-08 building controls remain open. This API slice grants no playable station
construction UI or passenger service.

## Domain and lifecycle

[`StationBuildingConstruction`](../src/FreeTrain.Online.Simulation/StationBuildingConstruction.cs)
produces immutable plans against the actual mixed world. Every footprint member
must be inside the H/V/Z world, with each horizontal column's ground at the base
height and every occupied member silently reclaimable. Sloped terrain, elevated/
underground bases, non-reclaimable terrain, rails, cars and other structures reject
through those predicates. The source supplies separate contribution identities
for orientations; the domain does not rotate a generic rectangle independently
of its selected art/definition. There is no additional water prohibition or
water-based station-price discount.

A legal plan reports every exact occupied position and reclaimed terrain owner.
Commit rechecks the plan's source world, removes reclaimed terrain and its
listener memberships, allocates one owned/non-reclaimable/value-zero station and
books the definition's price to RAIL_SERVICE. Automatic source names start at
`ST 1`; the world-local counter and identity allocator advance only on publication
and never rewind after removal. Construction RNG is untouched.

`StationBuildingLifecycle.Created`, `.Reclaimed` and `.Removed` are pure graph
hooks called before publication. They register the station graph member, preserve
ordered listener edges, remove reclaimed provider edges, and recursively disconnect
host descendants on removal. Changed provider, station and wide-center spatial
owners receive matching revisions. These hooks are the construction integration
points for P6-11 discovery and P6-16 provider advertising; no automatic range/
catchment/demand discovery is implemented here.

Removing any actual footprint member requires the exact station ID/revision,
removes the complete owner and books `500000 * sizeX * sizeY` to SUBSIDIARIES with
no refund. Descendants retain their identities, rails and day timers but lose
hosts/children. Other stations and their timers remain unchanged.

A bounded portable deletion rule follows P6-05's no-dangling-reference contract:
remove a deleted station ID from every provider-local list, including a source
fifth-listener edge with missing reverse registration. This does **not** repair
that source asymmetry while the station is live or recompute nearest stations.
Provider deletion likewise removes its reverse memberships without inventing
replacement demand links. Source creation-order/demand behavior remains P6-16.

[`StationLifecycle`](../src/FreeTrain.Online.Simulation/StationLifecycle.cs)
registers the first hour at the next aligned hour and the first day at the next
midnight, preserving FIFO order when they coincide. Removal cancels only that
station's registrations. Geometry, accounts, graph, allocator and clock publish
as one validated successor; late booking/allocator/queue/transport rejection
preserves the original state. The default due-station dispatcher still rejects
at the first callback until P6-17 supplies hourly/daily passenger/log behavior.
Save/reopen preserves that explicit boundary and all pending timers exactly.

## Authoritative API

[`StationBuildingSession`](../src/FreeTrain.Online.Session/StationBuildingSession.cs)
adds read-only `POST /api/station/building/preview` and FIFO
`editStationBuilding { query, expectedCost }`. Both use:

- World epoch, geometry revision, absolute minute and content-version fences.
- Exact qualified definition for placement; actual station ID/revision for removal.
- Pure full-transition validation in preview, exact quoted cost at commit, and
  full validation before authoritative publication.
- Existing authentication, compatibility/pack checks, bounded request bodies,
  Owner/Operator mutation, Observer read-only access and network receipt replay.

Terrain promotion stays detached until successful commit. The shared schema,
C# contracts, generated TypeScript and literal command/preview fixtures agree.
These are additive command/query contracts on protocol8; no persisted or existing
snapshot shape changed. [Save10/protocol8](web-parity-station-transport.md), pack
hashes, content versions and runtime limits remain unchanged. No migration or
historical import was added. User-facing station controls and EN/JA wording ship
with P6-08; this slice adds no product controls or new localized text.

## Verification

Fresh final runs:

- **358/358 relevant Release .NET tests**: station definitions/state/lifecycle/
  persistence/construction/session, shared contracts, mixed state/continuation,
  spatial occupancy, state transfer and object removal. Includes independent
  production construction/removal cases for every bundled building, exact prices/
  demolition genres, full footprints, upper-member targeting, legal terrain
  reclamation, stale/occupied/illegal rejection, midnight FIFO, host descendants,
  asymmetric listener cleanup, retained timers and provider reclamation. Account
  capacity, cash overflow and timer allocator failure leave exact saves unchanged.
- **323/323 client tests**: 321 ordinary plus two retention tests. New shared
  fixtures and numeric/definition corruption checks accompany existing detached
  station decoder, transport and shell regressions.
- **5/5 selected Chromium tests**, no skips: new real construction/removal API
  journey through an EN desktop owner and JA narrow observer, duplicate receipt,
  rejected cost/stale commands and occupied previews, full peer publications, save replacement,
  checkpoint process restart, upper-footprint removal and exact timer cancellation;
  P6-05 station transport and historical station draft/conflict/disconnect checks.
  This is API evidence, not station-building UI or pixel-fidelity acceptance.
- Generated contracts/content, EN/JA locale checks, Svelte/TypeScript (zero errors/
  warnings) and production build pass. Vite retains its existing chunk advisory.
  Project boundaries, whitespace, 821 independent station fixtures, source review
  and all **28 station Python evidence tests** pass.

Commands and logs:

```sh
dotnet test FreeTrain.Online.slnx -c Release --no-restore --filter 'FullyQualifiedName~Station|FullyQualifiedName~Contract|FullyQualifiedName~Mixed|FullyQualifiedName~Spatial|FullyQualifiedName~StateTransfer|FullyQualifiedName~ObjectRemoval'
DENO_JOBS=4 deno task --cwd web test
deno task --cwd web build
deno task --cwd web test:browser:run station-building-domain.spec.ts station-transport.spec.ts station-editing.spec.ts
python3 scripts/station-fixtures.py --check
python3 scripts/review-stations.py --check
python3 -m unittest discover -s scripts/tests -p 'test_station*.py'
python3 scripts/check-project-boundaries.py
```

Evidence is in `tmp/p6-06-dotnet-final.log`, `tmp/p6-06-web-unit.log`,
`tmp/p6-06-web-build.log`, `tmp/p6-06-browser.log`, `tmp/p6-06-python.log`,
`tmp/p6-06-fixtures.log`, `tmp/p6-06-review.log` and `tmp/p6-06-boundaries.log`.
The final .NET/browser runs use authorized local IPC/server execution outside
the restrictive sandbox. Initial failures exposed an invalid test terrain setup,
a constructor argument, and the new typed preview fixture's missing decoder
branch; these were corrected before final verification.

The source checker also exposed P6-05's stale runtime-policy document hash.
Its reviewed dependency and the downstream fixture digest were refreshed after
reconciling the existing Save10/protocol8 policy and this construction handoff.
No original ExAV hashes, source decisions, fixture inputs or expected outputs
changed. Full backend/Python suites, native/optional/capacity and other browser
engines were not run or credited. Host discovery, source listener/demand gameplay,
station-service callbacks and passenger/fare acceptance retain their later owners.
