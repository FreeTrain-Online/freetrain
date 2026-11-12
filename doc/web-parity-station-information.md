# P6-23 station information, list, labels and diagnosis — 10 November 2026

Game → **Station list / 駅一覧** and the picked station/platform inspector now
present live station information in English and Japanese. This delivers the
information/list slice; full histories and development reports remain P8.
Save10, protocol9 socket state, content identity and station simulation semantics
are unchanged. The new HTTP read is additive.

## Source and presentation

Authority remains stable ExAV `883cf7d81cf49db25ff38cd3a25d0f140afe31a0`:
`core/controllers/rail/StationInformationWindow.cs`, `core/world/rail/Station.cs`,
`YardRailRoad.cs`, the station controller handlers and the manifest-registered
`plugins/jp.sourceforge.freetrain-exav.plugin.tools.stationlist/src/StationList.cs`.
The actual docking contribution maps to the browser Game menu/panel registry;
the spare unregistered StationListPlugIn class is not the runtime route.
See the [source contract](web-parity-station-contract.md),
[independent fixtures](web-parity-station-fixtures.md),
[statistics](web-parity-station-statistics.md) and
[controller matrix](web-parity-station-controllers.md).

Information shows confirmed names, actual waiting separately from current demand,
today/yesterday boarding (`exports`), alighting (`imports`) and train calls, and
source daily boarded/alighted averages. A call is the source counter, not a count
of unique trains or only arrivals. Waiting is `max(0, demand − gone)`; averages
use the production float32 accumulator calculations, not a browser approximation.
The read delegates every population tree/table and asymmetric listener membership
to `StationCatchment.Demand` and `StationStatistics`.

Station List sorts localized names numerically, then equal names by numeric stable
ID. Renames retain selection, duplicate names remain distinguishable, removals
update the options and clear a removed selection without selecting another
station. World replacement clears the selection. The native list supports keyboard
selection; **Move to station** and double-click frame the selected station locally.
**Station properties** opens its actual picked inspector. Move preserves time,
save bytes and canvas identity and ends local train following. Information also
works through a platform's resolved station, including chained platform hosts;
a disconnected platform presents its diagnosis with absent metrics.

Existing View station-label visibility/font/size/color controls remain local and
persist through refresh. This slice registers those existing destinations alongside
the list actions. It introduces no shared presentation or naming side effects.
Information uses the existing panel scrolling and focus/navigation model; changing
panels still respects unapplied names and platform drafts.

The EN/JA labels adapt the source information/list semantics. Diagnostic help,
controlled-supply explanations, observation timestamps and accessible explicit
Move/properties actions are new source-derived browser wording, not claimed as
literal original XML or historical English donor strings. The original ten-minute
main-clock display remains unchanged; observation timestamps show exact minutes.

## Authoritative read and live updates

`POST /api/station/information` uses the existing authentication, compatibility,
request-size and JSON boundaries. Any authenticated role, including Observer, can
read. Default queries require the exact epoch/session revision/minute. Selected
station, platform and train IDs must exist; the platform must currently resolve
to the requested host. A disconnected platform uses station ID 0 and null demand,
waiting and averages, distinct from a connected station's actual zero supply.

The UI requests `latest:true`: an earlier requested revision/minute is a lower
bound, while epoch and host identity remain strict. The response echoes its query
and includes the actual observed revision/minute. A single outstanding request
refreshes after 250 ms; observations show their exact game timestamp. This avoids
starvation when normal Run advances multiple minutes during a network round trip.
Selection/host/epoch changes, disconnect and disposal abort old work. Strict query
callers still discard responses after revision/minute changes. Both paths preserve
dirty names. Schema parsing checks bounded counters, coherent nullable metrics,
observation bounds, waiting≤demand and eligible stop-index/controller fields.

The server evaluates under the session/world locks. It never applies the returned
controller train, commits a passenger exchange, registers a timer, books an account
or publishes a bell. Exact save comparisons cover ordinary, forwarding and TURNING
reads. Querying does not change readiness, clock speed or shared pause.

## Stop and passenger checks

Choose an actual connected platform and train. Separate messages explain:

- Missing host or platform, no selected train, and an unplaced/Outside head.
- A different rail direction, a computed stop index outside the platform, a head
  away from the platform, and a head on another yard cell. Required/current indices
  are zero-based; platform and formation lengths are shown independently.
- The current effective shared train/group controller at the resolved station
  location, an arrival Pass, a positive wait, ordinary boarding departure and
  forwarding without boarding. Actual stored rule order/masks and call count feed
  the production controller; no synthetic future timetable outcome is guessed.
- Zero current demand separately from every stop/host/controller condition. A
  station can still accept a stop and departure with zero passengers. Controlled
  population supply remains distinct from unimplemented P7 city producers.

`StationStopGeometry.Index` now holds the same arithmetic used by production
`StationYardStop` and diagnosis. Source integer division toward zero is preserved:
a formation one cell longer than a platform can still have a legal index. There
is no blanket overlength rejection. Diagnosis evaluates a call at the displayed
observation minute; it does not predict the next scheduled callback's outcome.

## Independent continuation scene

`station-information-midnight-v11.json` is generated and byte-checked by
`StationInformationTests.cs` using the existing constructed-yard domain scene.
Actual train callbacks run, board/alight and continue until 23:59; no passenger,
log or midnight-counter injection is used. At that boundary it contains boarded 2376,
alighted 300 and eight train calls today. Source hourly decay has accumulated
loaded 2362/unloaded 290. At midnight, all today fields become0 and yesterday becomes
2376/300/8, actual waiting is 9286 against demand 10000, and daily averages are 22/2.
One-step uninterrupted and reopen successors are byte-identical.

Fixture SHA-256:
`7b810fe29460f432df20ec8a45bbe3eb3c0dc55edbbbc2c954ebe41a98e25b8a`.
The two-host controller fixture supplies 5000 demand to each host from its shared
controlled 10000 provider. A further station is constructed outside that provider's
advertised cube in the browser and reports actual zero demand.

## Verification

Fresh verification passes **1,554 relevant Release .NET tests**, including 19 new
station information/continuation cases and the shared JSON round trip, with no
skips. The final focused query/contract run passes 79 cases. All **386 web
unit/retention tests** pass (384 ordinary + 2 retention), including six new
information/query/identity/timestamp cases and the shared contract fixture.

**28 distinct Chromium scenarios** pass without skips: 14 canonical station cases,
three canonical toolbar/focus/clock/narrow cases and 11 explicitly historical
prototype shell/keyboard cases. The final 14-case station matrix uses the completed
live-read implementation; the new journeys include exact midnight values and
150 ms delayed responses during Run. Prototype cases remain shell regression
evidence, not canonical station simulation evidence.

All **28 station Python tests**, **821 independent fixtures**, pinned-source
station review, generated contracts/content, EN/JA locales, Svelte/TypeScript
(zero errors/warnings), production build, project boundaries and whitespace pass.
Logs use `tmp/p6-23-*`. Reproduce:

```sh
dotnet test tests/FreeTrain.Online.Tests -c Release --no-restore -m:1 -p:UseSharedCompilation=false --filter 'FullyQualifiedName~Station|FullyQualifiedName~Platform|FullyQualifiedName~Passage|FullyQualifiedName~Mixed|FullyQualifiedName~Fleet|FullyQualifiedName~Train|FullyQualifiedName~ContractTests|FullyQualifiedName~NetworkTests'
DENO_JOBS=4 deno task --cwd web test
deno task --cwd web build
deno task --cwd web test:browser:run station-information.spec.ts station-controller.spec.ts passenger-service.spec.ts station-building.spec.ts platform-properties.spec.ts station-transport.spec.ts --project=canonical --workers=2
deno task --cwd web test:browser:run shell.spec.ts keyboard-controls.spec.ts --project=prototype-regression --workers=2
deno task --cwd web test:browser:run game-toolbar.spec.ts --project=canonical --grep 'Trains opens|shared speed confirms|speed and funds stay'
python3 -m unittest discover -s scripts/tests -p 'test_station*.py'
python3 scripts/station-fixtures.py --check --source-root /path/to/freetrain-exav-archive
python3 scripts/review-stations.py --check --source-root /path/to/freetrain-exav-archive
python3 scripts/check-project-boundaries.py
git diff --check
```

The new EN desktop and JA 430×860 touch journeys cover keyboard name/list actions,
duplicate-name retention, peer conflict and dirty-draft navigation, actual picked
information, current stop indices, Pass/forwarding, zero supply, label persistence,
read-only Observer access and stale/future/invalid queries. A response delayed 150 ms
still updates timestamped information during Run. They assert every midnight log,
exact averages/waiting, full peer state and exact reopen successors; removal clears
the list selection, and picking the surviving platform reports disconnected.
Accepted captures/state are under `tmp/p6-23-browser-accepted/`.

Initial harness corrections accounted for the rebuilt station's source identity
and shared-provider 5000 demand, and replaced a previously ambiguous prototype
Japanese Settings heading selector with an exact match. The first fixture generation
intentionally replaced a placeholder before the byte-check rerun. The final live
read correction replaced exact-minute UI starvation with timestamped latest reads;
strict reads retain their original fences. No simulation arithmetic correction was
needed. The existing Vite bundle-size advisory remains.

Native/original-runtime, other browser engines, optional content, capacity/soak,
Linux/container/deployment, evolving city producers, full accounting/development
reports and overall P6 acceptance retain their later gates. P6-24 owns the next
cross-tool integrated regression slice.
