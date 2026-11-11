# P6 station interfaces for P7 and P8 — 11 November 2026

This is the consuming contract accompanying [P6 acceptance](web-parity-p6-acceptance.md).
Save10/protocol9 are current. The interface is executable, but its population
inputs are **controlled**. No constructible city producer, evolving land value,
traffic simulation, stadium game or full economic report is delivered by P6.

## P7 producer lifecycle and demand

[PopulationBindings.Build(catalog)](../src/FreeTrain.Online.Content/PopulationBindings.cs)
binds all 169 inert population nodes. [Population](../src/FreeTrain.Online.Simulation/Population.cs)
exposes distinct `Residents`, `Outgoing(GameTime)` and `Incoming(GameTime)`;
station demand consumes outgoing supply. Preserve the 42 hourly tables, source
calendar branches, child-before-parent truncation and checked Int32 products.
Residents and incoming supply must not be substituted for station outgoing demand.

| P7 action | Existing consuming interface and required behavior |
| --- | --- |
| Create a provider | Construct a legal spatial owner first; call `StationCatchment.Attach(world, providerId, StationProvider)`. Identity, anchor and occupied footprint come from that owner, not a station or invented population entity. Attachment also works before stations exist. |
| Supply metadata | `StationProvider(Population, LandValue, XYSize, ZSize, Special, Input)` currently admits only `Input = "controlled"`. Land value is nonnegative; scales positive. A real producer label/state/codec requires an explicit P7 contract change; do not relabel controlled fixtures as city gameplay. |
| Change supply/land value | `StationCatchment.Update(world, id, provider)` revises affected ownership. A changed kind or geometry requires detach/attach; coordinate/footprint changes must be integrated with the producer lifecycle. |
| Remove/reclaim | `StationCatchment.Detach` and `StationBuildingLifecycle.Reclaimed` clean provider memberships. Station creation already advertises its catchment and station removal removes its links. |
| Query service | `StationCatchment.Demand(graph, stationId, time)` and `Waiting` query current time; waiting is `max(0, demand - Gone)`. No cached global population, passenger editor or automatic station supply exists. |
| Commit | Helpers return detached world candidates. Carry accounts/queue/random through `MixedSessionState.Successor`; use the authoritative single-writer session/preview/revision boundary for new gameplay commands. Never mutate a live spatial owner or publish edges separately. |

[StationCatchment](../src/FreeTrain.Online.Simulation/StationCatchment.cs) retains
STATION-D003 / SCN-D01: attaching a listener tests strict truncated 3D distance
less than 10 (ordinary) or 20 (special); station advertising tests occupied
footprint intersection with the inclusive range cube. Local station order has
four slots. A closer fifth station replaces the farthest local entry but the
source omits the newcomer reverse membership. Ties do not replace. Demand sums
reverse memberships, dividing each provider by its local station count. Ordinary
share is capped by `land * xy * max(1, z / 4) + 10`; special share is uncapped.
Persist both directions independently. Do not rebuild nearest-four links or
repair the asymmetry during load, edits or producer integration.

Independent witnesses: [StationDemandTests](../tests/FreeTrain.Online.Tests/StationDemandTests.cs)
checks 1,008 literal table cells, calendar queries, ordinary/special caps,
creation-order/fifth/tie/removal traces, real footprints and exact wire/save
restoration. [StationPassengerServiceTests](../tests/FreeTrain.Online.Tests/StationPassengerServiceTests.cs)
constructs two stations and runs real purchased trains against controlled supply.
P7 must add actual structure construction/development, producer removal/rebuild,
land-value feedback and saved continuation witnesses with source-derived outputs.
The current service fixture attaches its provider to a rail owner; that is a
legal interface witness, not a populated city building.

## P7 committed land-value and traffic consumers

[StationPassengerExchange](../src/FreeTrain.Online.Simulation/StationPassengerExchange.cs)
returns ordered `StationPassengerEffect` values: `LandValue(StationId, Location,
float Amount)`, `Transport(StationId, int Passengers)`, `TrainChanged(TrainId)`
and `StationChanged(StationId)`. Positive-demand boarding stages land value,
train change, station change; zero population stages only station change and
retains the old train passengers/origin. Unloading stages train change, land
value, nonzero transport, station change. Land value uses the source Single
cast before `min(lastWeekImportsPerDay / 24, unloaded)`.

`TrainCallbackResult.PassengerEffects` collects real-yard effects into
[MixedSessionState.PassengerEffects](../src/FreeTrain.Online.Simulation/MixedSessionState.cs).
These describe the last successful minute only. They are absent from Freeze,
saves and snapshots; failed minutes expose no new effects and retain the previous
publication. P7 must consume only a newly successful publication, exactly once,
in callback/effect order. Polling the last array or replaying it after reconnect
would apply effects twice. No durable event history or city consumer exists today.
If city effects change saved state, integrate their detached successor before the
atomic minute commit and add failure/overflow rollback and uninterrupted/reopen
oracles. Applying them later as untracked live side effects is insufficient.
Departure bells are a separate transient network/audio envelope, not a traffic log.

Independent witnesses: [StationPassengerExchangeTests](../tests/FreeTrain.Online.Tests/StationPassengerExchangeTests.cs)
checks literal boarding/fares, zero/depleted supply, effect order/Single rounding
and late-callback rollback. [StationYardStopTests](../tests/FreeTrain.Online.Tests/StationYardStopTests.cs)
checks those effects under production controller-before-yard execution. P7 owns
the independent expected land-value/traffic evolution after those notifications.

## P8 bookings, upkeep and reports

[ConstructionAccounts](../src/FreeTrain.Online.Simulation/ConstructionAccounting.cs)
provides immutable ordered `AccountTransaction` records with sequence, world
revision, absolute minute, genre, revenue, expenditure, cash and operation.
Preserve append order and source categories; `Earn`/`Book` must use the same
candidate revision and callback time. Exact money is Int64 after checked source
Int32 calculations. The finite 8,192-entry admission limit remains active; P8
must design histories/aggregation without silently dropping ordered receipts or
inventing unlimited uninterrupted use.

| Operation | Existing booking/report meaning |
| --- | --- |
| Station construction / daily upkeep | Definition price / operation cost, RailService. Construction is immediate, never generic timed ConstructionSite. |
| Station demolition | `500000 * sizeX * sizeY`, Subsidiaries; no resale refund. |
| Platform construction | `500000 * length * heightMultiplier`, using base ground only; underwater branch zero. |
| Platform upkeep | `180000 * length`, RailService, at creation +24h and every24h thereafter; station upkeep is aligned midnight. |
| Platform removal, lanes, passages, stairs | No booking for the free operation; preserve the distinction from a valid zero-cost spend receipt. |
| Passenger unload | RailService revenue `passengers * configuredFare * truncated3DDistance * 2`; distance uses current head and boarding origin. Missing origin omits receipt; valid origin with zero passengers/distance earns zero. Fare precedes same-move operating expense. |

[StationStatistics](../src/FreeTrain.Online.Simulation/StationStatistics.cs) owns
station statistics, not a full financial history. Every hour truncates Single
`Gone * 0.8f` and accumulated loaded/unloaded `* 0.9996f`. Displayed daily averages
multiply accumulated counters by Single `24 * (1 - 0.9996f)` and truncate;
they are not arithmetic daily totals. Import/export/train-call logs retain Today,
Yesterday, ThisWeek and LastWeekPerDay. Each midnight adds the fifth root of
today; Saturday computes the fifth power of the seven-day average and clears
ThisWeek. Current week divides by `DayOfWeek + 1`. Train calls include zero-supply
load/unload calls, not unique formations. Use the source new-day midnight/FIFO
order; do not replace these quantities with arithmetic means or unique-train counts.

Independent witnesses: [StationLifecycleTests](../tests/FreeTrain.Online.Tests/StationLifecycleTests.cs),
[StationStatisticsTests](../tests/FreeTrain.Online.Tests/StationStatisticsTests.cs)
and [StationContinuationTests](../tests/FreeTrain.Online.Tests/StationContinuationTests.cs)
cover literal prices, relative/aligned timers, all eight Single vectors, seven-day
logs, equal-time order and exact accounts/queue continuation. The EN/JA
[service journey](../web/tests/browser/passenger-service.spec.ts) books
`594 * 400 * 12 * 2 = 5702400` through real yards and saves/restarts exactly.
P8 retains day/month/year account reports, fiscal rollover, subsidiaries,
bank/stocks, stadium scheduling and independent economic-result acceptance.

P10 retains desktop import/timer migration and required-pack compatibility.
P11 retains P4-CAP-001/002, FLEET-CAP-001/002 and STATION-CAP-001/002/004,
full-city/long-soak/resource acceptance and final supported limits. See
[station measurements](web-parity-station-capacity.md); these misses are not waived
by handing the interfaces to later phases.
