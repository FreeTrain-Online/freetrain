# P7-13 city population and catchment integration — 15 November 2026

Delivered at bundled browser/portable scope. Completed ordinary buildings supply
real population and refresh their land-value cap with heat changes. Passenger
land deposits/traffic consumption remain P7-14; ground construction remains
P7-16–19, automatic development P7-25–29 and stadium event supply P8. Controlled
scenario providers retain their explicit identity and receive no city credit.

## Source and production consumers

Authority is stable ExAV 2.1.1.12, Git
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, `trunk/`. The pinned
[city contract](web-parity-city-contract.md), [city fixtures](web-parity-city-fixtures.md)
and [station demand contract](web-parity-station-demand.md) bind:

- `core/world/rail/StationListenerImpl.cs`: constructor attachment, readonly
  population/location/scales, `getPopulation`, four local slots and fifth-offer
  asymmetry. `getPopulation` reads live land value at the owner's anchor.
- `core/world/structs/VarHeightBuilding.cs`, fixed Commercial/PThreeDimStructure
  and HalfDividedVoxel: completed providers, height multiplication and first-half
  binding retained through addition/partial removal.
- `core/contributions/population/*`: residents, outgoing/entering tables, ordered
  combinations, child truncation, multiplier and persistent wrappers, holiday,
  Saturday, vacation and seven-day queries.
- `core/world/development/LandValue.cs`: source Single diffusion and shifted
  public value read, already preserved under CITY-D001.

`CityGraph.ProducerPopulation` and `Provider`, `CityConstruction` and
`HalfCityConstruction` keep actual completed owner IDs and initial definitions/
heights. Of 129 ordinary definitions, 112 have population: 50 fixed, 54 variable
and eight half definitions. The remaining 17 have no provider. Variable children
truncate before height multiplication. Half additions/removals retain the initial
provider, including after removal of its original visual half. Residents,
outgoing and incoming remain distinct queries; only outgoing supplies stations.
Ground definitions retain their existing `city-ground` domain binding for their
later construction slices. The stadium special provider and controlled event
fields remain the P8 interface; no ordinary building substitutes for a stadium.

## Atomic land-value refresh and retained catchment

`CityHeat` deposit/diffusion successors call `CityPopulation.Refresh` before
publication. It samples each attached real city's owner anchor from the new heat
field, updates changed integer land values and revises matching city/spatial
owners once in that successor. An unchanged integer value leaves the owner
revision alone. Existing population expressions, geometry, controlled providers,
station revisions and both listener directions are retained. This adds no random
draw, booking, catchment offer or extra world publication. Overflow discards the
whole minute, including buffers, queue, random state, accounts and graphs.

Ordinary contribution remains
`min(outgoing / localStationCount, land * area * max(1, height / 4) + 10)`.
`StationCatchment` retains STATION-D003: building attachment tests truncated
anchor distance strictly below 10; station advertising intersects occupied
footprint voxels with its inclusive cube. The first four offers create both
edges. A closer fifth replaces the first farthest slot and removes the old reverse
edge without adding the newcomer in reverse. Ties do not replace. Demolition
cleans both directions and rebuild allocates/attaches a new owner. Geometry or
ordinary/special changes require detach/attach; metadata refresh never repairs
membership. Save/reopen restores both ordered arrays without discovery.

The new live fixture finishes an actual five-voxel apartment through scheduled
construction. At January 4, 09:00, it has 600 residents, 480 outgoing and 60
incoming. Initially its sampled heat is zero, so one station receives 10. Four
neighbor heat values of 100 diffuse the sampled cell to approximately 61.44;
source power/truncation gives land 30 and station demand 40. Both peers and exact
reopen observe this transition. The fixture's seeded heat and scheduled due time
are controlled inputs; its building, producer, population and station lifecycle
are production objects. This is not passenger-generated heat (P7-14).

## EN/JA inspection and reciprocal obligations

Selecting a completed populated city owner shows residents, current outgoing,
current incoming and current land value in `CityPopulation.svelte`, through the
existing read-only city preview route. The optional population observation in
`CityPreview` consumes authoritative game time. Client generation, epoch, minute,
revision, owner and abort checks discard late responses. It changes no world,
accounts, random state or clock. Station help now describes actual completed city
supply and keeps controlled scenario supply separate.

MAN-A06/MAN-A16/MAN-E10, DEV-A05/DEV-P16/DEV-P22/DEV-P29–31 and EXAV-06/07 bind to
`CityGraph`, `CityPopulation`, `StationCatchment`, `CitySession`,
`CityPopulation.svelte` and the existing station information controls. P7-14
retains the passenger-effect consumer portion of `providers-feedback`; P8 retains
stadium scheduling. Save11, protocol11, City1 and prepared content identities are
unchanged; the optional read-only preview field needs no save-format change.

## Focused verification

Commands, logs and hashes are recorded in `tmp/p7-13-validation.json`. The focused
C# selection passes 345 cases and the focused Deno selection passes 57 cases,
with zero skips. C# binds all 129 building definitions/112 providers, all 27
independent city provider vectors, all 42 population tables, nested/calendar
queries, source share/cap arithmetic, real construction on both sides of station
creation, horizontal/vertical cube boundaries, fifth/tie behavior, natural
removal/rebuild, exact save continuation, metadata owner revisions and overflow
rollback. Deno checks the actual C# heat-refresh delta, owner revision/geometry/
provenance rejection, inspector response validation, existing city/station/half
contracts, local shell state, fixture selection and localization.

Generated contracts, content/locales, Svelte, production build, pinned source
review, 703 city source fixtures / 197 source files, project boundaries and
whitespace are checked separately. All nine focused Chromium workflows pass with zero failures, skips or retries:
the new EN desktop/keyboard and JA phone/touch building-first workflows, existing
EN/JA natural fixed/variable/half construction workflows, and heat peer/restart
continuation. The new scenarios build a real station, observe demand 10→40,
verify unchanged listener directions on both peers, reopen exact saved bytes,
inspect 600/480/60/30 and demolish the real producer. EN and JA captures at
`tmp/p7-13-browser-final/evidence/city-population-{en,ja}.png` were visually
inspected for legible labels/values and reachable demolition controls.

Earlier browser attempts exposed test setup errors (the new fixture has a
different map origin; phone Step is in the Game menu). The final nine-case run
uses corrected ordinary map input and visible menu controls. No retry or skipped
test supplies passing credit. Full suites are not run.
Native/optional/full-city/performance and overall P7 acceptance remain open.
