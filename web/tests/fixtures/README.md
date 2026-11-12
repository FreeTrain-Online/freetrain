`fleet-information-v11.json` and `fleet-follow-outside-v11.json` retain the
controlled P5-25 browser witnesses previously kept only in ignored
`tmp/p5-25-color-fleet.json` and `tmp/p5-25-outside-fleet.json`. P6-07 makes the
fixtures repeatable with the current Save10 envelope; train, rail, account,
random and scheduler state is unchanged. They contain no station graph.
This fixture maintenance adds no runtime migration or support for older saves.

`passenger-service-{east,west}.json` are the P6-21 controlled-supply baselines.
They contain a legal constructed straight railway and one ordinary provider bound
through `StationCatchment.Attach` to a rail owner. The provider is explicitly
`input: "controlled"`, with `ConstantPopulation(10000)`, land value 10000 and
unit footprint scales. This is a P7 producer-interface witness, not a city
population simulation. No station, platform, train, passenger, booking origin,
stop-call count or test harbor is seeded. The browser constructs its two stations
and platforms, purchases stock and deploys it through the actual EN/JA UI.

The metadata selects the bundled single-cell station and three-car stock used by
`StationPassengerServiceTests`: normal capacity 396, packed capacity 594 and fare
400. Domain tests compare generated baseline bytes with these committed saves.
Regenerate (from repository root), then rerun the tests to check the updated files:

```sh
FREETRAIN_SERVICE_OUTPUT="$PWD/web/tests/fixtures/passenger-service" dotnet test tests/FreeTrain.Online.Tests -c Release --filter FullyQualifiedName~StationPassengerServiceTests
```

`passenger-service.spec.ts` reads these files by default; no ignored `tmp/` setup
or environment flag is required. `FREETRAIN_SERVICE_OUTPUT` can select a separate
fresh generation with the same `-east.json` / `-west.json` and metadata suffixes.

`station-controller-v11.json` is the P6-22 baseline generated and byte-checked by
`StationYardStopTests.Multiple_real_platforms_share_host_location_and_rebuilt_station_reuses_retained_TAT_setting`.
It constructs two station locations, three slim platforms, purchases/deploys a
three-car train and removes/rebuilds/reconnects the source host through domain
operations. Two platforms share that rebuilt station; the other station is
independent. The inherited TAT has location-keyed Fixed40/reverse and Pass entries.
Passengers, origins and stop-call counts are zero; no test harbor is injected.
The single special `ConstantPopulation(10000)` provider is explicitly controlled
supply bound to a real rail owner through the P7 interface. City producers remain P7.
The browser creates and edits its own additional shared TAT through the UI.
The generator checks committed bytes on every ordinary run; for deliberate fixture
regeneration update the committed save together with the domain generator.
