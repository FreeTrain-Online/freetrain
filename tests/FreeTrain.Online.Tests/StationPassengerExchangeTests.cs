using System.Collections.Immutable;
using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;
using Xunit;
using static FreeTrain.Online.Tests.StationStateTests;

namespace FreeTrain.Online.Tests;

public class StationPassengerExchangeTests
{
    static int Int(JsonElement e, string key) => e.GetProperty(key).GetInt32();
    static Location Point(JsonElement e) => new(e[0].GetInt32(), e[1].GetInt32(), e[2].GetInt32());
    public static IEnumerable<object[]> Rows(string family)
    {
        using var doc = Fixtures();
        return Cases(doc, family).Select(r => new object[] { r.Clone() }).ToArray();
    }
    public static IEnumerable<object[]> Boarding() => Rows("boarding");
    public static IEnumerable<object[]> Fares() => Rows("fare");
    public static IEnumerable<object[]> Capacities() => Rows("capacity");
    static StockTrain Stock => RollingStockCatalog.Bundled.Trains.First(t => t.PermittedLengths.Contains(1) && t.Create(1).PackedCapacity > t.Create(1).NormalCapacity);
    static FleetTrain Train(Location head, int passengers = 0, Location? origin = null) => new(100, 0, "Train", Stock.Binding, 101, null,
        [new(102, 0, 0, Stock.Create(1).Cars[0].Reference, new FleetCarState.Inside(head, Heading.East))],
        State: TrainOperationalState.StoppingAtStation, Passengers: passengers,
        BoardingOrigin: origin is {} p ? new FleetCarState.Inside(p, Heading.North) : null);

    [Theory, MemberData(nameof(Boarding))]
    public void All_independent_boarding_traces_use_station_state_and_exact_source_width(JsonElement row)
    {
        var i = row.GetProperty("input"); var e = row.GetProperty("expected");
        var b = Building() with { Passengers = new(Int(i, "gone"), Int(i, "loaded")),
            Exports = new(Int(i, "exported")), Trains = new(Int(i, "trains")) };
        var t = Train(Point(i.GetProperty("head").GetProperty("location")));
        StationExchange Depart() => StationPassengerExchange.Board(b, t, Int(i, "population"), Int(i, "packed"), Int(i, "amenity"));
        if (e.TryGetProperty("rejected", out _))
        {
            var before = b; var train = t;
            Assert.Throws<OverflowException>(() => Depart()); Assert.Same(before, b); Assert.Same(train, t); return;
        }
        var expected = e.GetProperty("trace"); var n = 0;
        foreach (var action in i.GetProperty("actions").EnumerateArray())
        {
            var next = expected[n++];
            if (action.GetString() == "hour") b = b with { Passengers = StationStatistics.Hour(b.Passengers) };
            else
            {
                var exchange = Depart(); b = exchange.Station; t = exchange.Train;
                Assert.Null(exchange.Revenue);
                Assert.Equal(Int(next, "boarded"), t.Passengers);
                Assert.Equal(next.GetProperty("loaderCalled").GetBoolean(), exchange.Effects.OfType<StationPassengerEffect.TrainChanged>().Any());
                if (next.GetProperty("origin").ValueKind == JsonValueKind.Null) Assert.Null(t.BoardingOrigin);
                else Assert.Equal(t.Head.State, t.BoardingOrigin);
                Assert.IsType<StationPassengerEffect.StationChanged>(exchange.Effects[^1]);
                if (exchange.Effects.Length > 1)
                    Assert.Equal(new StationPassengerEffect.LandValue(b.Id, b.Location, t.Passengers), exchange.Effects[0]);
            }
            Assert.Equal(Int(next, "gone"), b.Passengers.Gone); Assert.Equal(Int(next, "loaded"), b.Passengers.AccumulatedLoaded);
            Assert.Equal(Int(next, "waiting"), Math.Max(0, Int(i, "population") - b.Passengers.Gone));
        }
        Assert.Equal(Int(e, "exported"), b.Exports.Today); Assert.Equal(Int(e, "trains"), b.Trains.Today);
    }

    [Theory, MemberData(nameof(Fares))]
    public void All_independent_fares_clear_origin_and_stage_ordered_station_effects(JsonElement row)
    {
        var i = row.GetProperty("input"); var e = row.GetProperty("expected");
        var source = i.GetProperty("source");
        var train = Train(Point(i.GetProperty("destination")), Int(i, "passengers"), source.ValueKind == JsonValueKind.Null ? null : Point(source));
        var station = Building() with { Imports = new(7, 11, 13, 2400), Passengers = new(3, 5, 17) };
        StationExchange Unload() => StationPassengerExchange.Unload(station, train, Int(i, "fare"));
        if (e.TryGetProperty("rejected", out _)) { Assert.Throws<OverflowException>(() => Unload()); Assert.Equal(Int(i, "passengers"), train.Passengers); return; }
        var result = Unload();
        Assert.Equal(Int(e, "revenue"), result.Revenue ?? 0); Assert.Equal(0, result.Train.Passengers); Assert.Null(result.Train.BoardingOrigin);
        Assert.Equal(checked(7 + train.Passengers), result.Station.Imports.Today);
        Assert.Equal(checked(17 + train.Passengers), result.Station.Passengers.AccumulatedUnloaded);
        Assert.Equal(1, result.Station.Trains.Today); Assert.Equal(station.Exports, result.Station.Exports);
        Assert.Equal(3, result.Station.Passengers.Gone); Assert.Equal(5, result.Station.Passengers.AccumulatedLoaded);
        Assert.IsType<StationPassengerEffect.TrainChanged>(result.Effects[0]);
        Assert.Equal(new StationPassengerEffect.LandValue(station.Id, station.Location, Math.Min(100f, train.Passengers)), result.Effects[1]);
        Assert.Equal(train.Passengers != 0, result.Effects.OfType<StationPassengerEffect.Transport>().Any());
        Assert.IsType<StationPassengerEffect.StationChanged>(result.Effects[^1]);
        var second = StationPassengerExchange.Unload(result.Station, result.Train, Int(i, "fare"));
        Assert.Null(second.Revenue); Assert.Empty(second.Effects.OfType<StationPassengerEffect.Transport>());
        Assert.Equal(result.Station.Imports, second.Station.Imports); Assert.Equal(2, second.Station.Trains.Today);
    }

    [Theory, MemberData(nameof(Capacities))]
    public void Independent_normal_seated_packed_and_Outside_capacity_vectors_use_stock_admission(JsonElement row)
    {
        var cars = row.GetProperty("input").GetProperty("cars").EnumerateArray().Select(c =>
            new StockCar(Stock.Create(1).Cars[0].Reference, StockCarHandler.Symmetric, Int(c, "normal"), Int(c, "seated"))).ToImmutableArray();
        var e = row.GetProperty("expected");
        StockTrain Create() => new(Stock.Binding, Stock.Text, 0, 1, 3, 100, 0, null, null, null, [cars]);
        if (e.TryGetProperty("rejected", out _)) { Assert.Throws<ArgumentException>(() => Create()); return; }
        var formation = Create().Create(cars.Length);
        Assert.Equal(Int(e, "normal"), formation.NormalCapacity); Assert.Equal(Int(e, "seated"), formation.SeatedCapacity);
        Assert.Equal(Int(e, "packed"), formation.PackedCapacity); Assert.Equal(Int(e, "outside"), Math.Min(100, formation.NormalCapacity));
    }

    static MixedSessionState Scene(StationBuilding? building = null, int population = 1000, ConstructionAccounts? accounts = null, StockTrain? stock = null)
    {
        var scheduled = StationLifecycle.Reconcile(null, Graph([building ?? Building()], [Platform(length: 1, direction: 2)]), new(517, 0, []));
        var world = World(scheduled.Graph);
        if (stock != null)
        {
            var profile = RollingStockCatalog.Bundled.FleetProfile;
            world = world.EnableFleet(new(profile.Packs, profile.Trains.Select(t => t.Binding == stock.Binding ? stock : t), profile.Providers, profile.ColorTemplates));
        }
        var session = new MixedSessionState(new(world, accounts), TerrainProvenance.From(new EmptyWorldRequest(WorldCreationProfile.Id,
            world.Terrain.Name, new(64, 24, 8), 0)), clock: scheduled.Clock, revision: world.Revision);
        session = FleetTrading.Commit(session, FleetTrading.QuotePurchase(session, Stock.Binding, 1, 1));
        session = FleetDeployment.Commit(session, FleetDeployment.PreviewPlacement(session, session.Fleet!.Trains[0].Id, new(20, 20, 2)));
        world = StationCatchment.Attach(session.World, 10, new(new("ConstantPopulation", population), 0, Special: true));
        return session.Successor(session.Revision, new(world, session.Construction.Accounts), false);
    }
    static MixedSessionState Apply(MixedSessionState session, StationExchangeCandidate exchange) =>
        session.Successor(session.Revision, exchange.Candidate, session.ConstructionMode);
    static MixedSessionState Reopen(MixedSessionState s) => SaveCodec.ReadWorld(SaveCodec.Write(s), TerrainProfiles.Resolve, RailwayProfiles.Resolve).Mixed!;

    static MixedSessionState WithPassengers(MixedSessionState session, int passengers, Location origin)
    {
        var fleet = session.Fleet!;
        var train = fleet.Trains[0] with { Revision = fleet.Trains[0].Revision + 1, Passengers = passengers,
            BoardingOrigin = new FleetCarState.Inside(origin, Heading.North) };
        var graph = new FleetGraph(fleet.Profile, fleet.RootGroupId, [train], fleet.Groups, fleet.Controllers, fleet.ColorTest);
        var w = session.World;
        return session.Successor(session.Revision, new(w.Replace(w.Revision, w.Terrain, w.Spatial.Capture(), w.Voxels, w.Slopes, fleet: graph), session.Construction.Accounts), false);
    }

    [Fact]
    public void Controlled_39_times_3_times_5_times_2_books_1170_at_current_head_in_one_atomic_successor()
    {
        var stock = new StockTrain(Stock.Binding, Stock.Text, Stock.PricePerCar, Stock.MinutesPerVoxel, 3, 100, Stock.TripRange,
            null, null, null, [[Stock.Create(1).Cars[0] with { NormalCapacity = 100, SeatedCapacity = 40 }]]);
        var s = WithPassengers(Scene(stock: stock), 39, new(18, 17, 6)); var id = s.Fleet!.Trains[0].Id;
        var before = SaveCodec.Write(s); var exchange = StationPassengerExchange.Unload(s, 1, id);
        Assert.Equal(before, SaveCodec.Write(s));
        var after = Apply(s, exchange); var receipt = after.Construction.Accounts.Transactions[^1];
        Assert.Equal(1170, receipt.Revenue); Assert.Equal(s.Construction.Accounts.Cash + 1170, after.Construction.Accounts.Cash);
        Assert.Equal(after.World.Revision, receipt.WorldRevision); Assert.Equal(s.Clock.Time.AbsoluteMinute, receipt.AbsoluteMinute);
        Assert.Equal(39, after.World.Stations!.Building(1).Imports.Today);
        Assert.Equal(39, Assert.Single(exchange.Effects.OfType<StationPassengerEffect.Transport>()).Passengers);
        Assert.Equal<RailwayVoxel>(s.World.Voxels, after.World.Voxels); Assert.Equal<ScheduledAction>(s.Clock.Capture().Pending, after.Clock.Capture().Pending);
        Assert.Null(after.Fleet!.Train(id).BoardingOrigin); Assert.Equal(0, after.Fleet.Train(id).Passengers);
        Assert.Equal(s.World.Revision + 1, after.World.Revision);
    }

    [Fact]
    public void Fare_cash_overflow_discards_station_train_accounts_and_notifications()
    {
        var s = WithPassengers(Scene(), 39, new(18, 17, 6));
        s = new(new(s.World, new ConstructionAccounts(long.MaxValue)), s.Provenance, clock: s.Clock.Capture(), revision: s.Revision);
        var before = SaveCodec.Write(s);
        Assert.Throws<OverflowException>(() => StationPassengerExchange.Unload(s, 1, s.Fleet!.Trains[0].Id));
        Assert.Equal(before, SaveCodec.Write(s));
    }

    [Fact]
    public void A_later_failed_callback_discards_exchange_from_the_same_detached_minute()
    {
        var s = Scene(); var id = s.Fleet!.Trains[0].Id;
        var pending = s.Clock.Capture().Pending;
        // Due train first, followed by a deliberately rejected station callback.
        var due = 540L;
        var trainTimer = pending.Single(t => t.HandlerId == MixedSessionState.TrainHandler(id)) with { DueMinute = due, Order = 0, Registration = 0 };
        var b = s.World.Stations!.Building(1);
        var hour = b.HourTimer! with { Order = 1, Registration = 1 };
        var day = b.DayTimer! with { Order = 2, Registration = 2 };
        var platform = s.World.Stations.Platforms[0] with { DayTimer = s.World.Stations.Platforms[0].DayTimer! with { Order = 3, Registration = 3 } };
        var stations = new StationGraph(Profile, [b with { HourTimer = hour, DayTimer = day }], [platform],
            listeners: s.World.Stations.Listeners, listenerMemberships: s.World.Stations.ListenerMemberships);
        var world = new MixedWorld(s.World.Profile, s.World.Terrain, s.World.Spatial.Capture(), s.World.Voxels, [], s.World.Revision,
            fleet: s.Fleet, stations: stations);
        var called = false;
        s = new(new(world, s.Construction.Accounts), s.Provenance, clock: new(539, 4, [trainTimer, hour, day, platform.DayTimer!]), revision: s.Revision,
            trainCallback: (session, train) => { called = true; return new(StationPassengerExchange.Board(session, 1, train).Candidate, 10); },
            stationCallback: (_, _) => throw new OverflowException("late station failure"));
        var before = SaveCodec.Write(s);
        Assert.Throws<OverflowException>(() => s.AdvanceOneMinute()); Assert.True(called); Assert.Equal(before, SaveCodec.Write(s));
    }

    [Fact]
    public void Real_demand_boarding_publishes_packed_train_station_and_spatial_revision_and_preserves_queue_and_reopen()
    {
        var s = Scene(population: 10000); var before = SaveCodec.Write(s); var t = s.Fleet!.Trains[0];
        var exchange = StationPassengerExchange.Board(s, 1, t.Id);
        Assert.Equal(before, SaveCodec.Write(s));
        var after = Apply(s, exchange); var train = after.Fleet!.Train(t.Id); var station = after.World.Stations!.Building(1);
        Assert.Equal(Stock.Create(1).PackedCapacity, train.Passengers); Assert.True(train.Passengers > Stock.Create(1).NormalCapacity);
        Assert.Equal(t.Head.State, train.BoardingOrigin); Assert.Equal(t.Revision + 1, train.Revision);
        Assert.Equal(station.Revision, after.World.Spatial.Entities.Single(e => e.Id == station.Id).Revision);
        Assert.Equal(train.Passengers, station.Exports.Today); Assert.Equal(train.Passengers, station.Passengers.Gone);
        Assert.Equal<ScheduledAction>(s.Clock.Capture().Pending, after.Clock.Capture().Pending); Assert.Equal(s.Clock.NextOrder, after.Clock.NextOrder);
        Assert.Equal<RailwayVoxel>(s.World.Voxels, after.World.Voxels); Assert.Equal(s.Construction.Accounts.Cash, after.Construction.Accounts.Cash);
        Assert.Equal(SaveCodec.Write(after), SaveCodec.Write(Reopen(after)));
        var unloaded = Apply(after, StationPassengerExchange.Unload(after, 1, t.Id));
        var receipt = unloaded.Construction.Accounts.Transactions[^1]; Assert.Equal(0, receipt.Revenue); // same placed head
        Assert.Equal(AccountGenre.RailService, receipt.Genre); Assert.Equal(0, unloaded.Fleet!.Train(t.Id).Passengers);
        Assert.Null(unloaded.Fleet.Train(t.Id).BoardingOrigin); Assert.Equal(train.Passengers, unloaded.World.Stations!.Building(1).Imports.Today);
        Assert.Equal(SaveCodec.Write(unloaded), SaveCodec.Write(Reopen(unloaded)));
        var second = Apply(unloaded, StationPassengerExchange.Unload(unloaded, 1, t.Id));
        Assert.Equal(unloaded.Construction.Accounts.Transactions, second.Construction.Accounts.Transactions);
    }

    [Fact]
    public void Zero_demand_keeps_loaded_train_and_origin_but_demand_drop_invokes_loader_with_zero()
    {
        var s = Scene(population: 10000); var id = s.Fleet!.Trains[0].Id;
        s = Apply(s, StationPassengerExchange.Board(s, 1, id)); var before = s.Fleet!.Train(id);
        var provider = s.World.Stations!.Listeners.Single().Provider!;
        var zero = StationCatchment.Update(s.World, 10, provider with { Population = provider.Population with { Base = 0 } });
        s = s.Successor(s.Revision, new(zero, s.Construction.Accounts), false);
        var exchange = StationPassengerExchange.Board(s, 1, id);
        Assert.Equal(before, exchange.Candidate.World.Fleet!.Train(id)); Assert.Single(exchange.Effects);
        Assert.Equal(2, exchange.Candidate.World.Stations!.Building(1).Trains.Today);
        var dropped = StationCatchment.Update(s.World, 10, provider with { Population = provider.Population with { Base = 1 } });
        s = s.Successor(s.Revision, new(dropped, s.Construction.Accounts), false);
        exchange = StationPassengerExchange.Board(s, 1, id);
        Assert.Equal(0, exchange.Candidate.World.Fleet!.Train(id).Passengers);
        Assert.Equal(before.Head.State, exchange.Candidate.World.Fleet.Train(id).BoardingOrigin);
        Assert.Equal(before.Passengers, exchange.Candidate.World.Stations!.Building(1).Passengers.Gone);
        Assert.Equal(3, exchange.Effects.Length);
    }

    [Theory]
    [InlineData("product")]
    [InlineData("exports")]
    [InlineData("loaded")]
    [InlineData("trains")]
    public void Failed_real_boarding_leaves_entire_session_unchanged(string field)
    {
        var b = Building() with { Exports = new(field == "exports" ? int.MaxValue : 0),
            Passengers = new(0, field == "loaded" ? int.MaxValue : 0), Trains = new(field == "trains" ? int.MaxValue : 0) };
        var s = Scene(b, field == "product" ? int.MaxValue : 1000); var before = SaveCodec.Write(s);
        Assert.Throws<OverflowException>(() => StationPassengerExchange.Board(s, 1, s.Fleet!.Trains[0].Id));
        Assert.Equal(before, SaveCodec.Write(s));
    }

    [Theory]
    [InlineData("imports")]
    [InlineData("unloaded")]
    [InlineData("trains")]
    public void Failed_unload_after_fare_calculation_leaves_train_origin_accounts_and_queue_unchanged(string field)
    {
        var b = Building() with { Imports = new(field == "imports" ? int.MaxValue : 0),
            Passengers = new(0, 0, field == "unloaded" ? int.MaxValue : 0), Trains = new(field == "trains" ? int.MaxValue - 1 : 0) };
        var s = Scene(b); var id = s.Fleet!.Trains[0].Id; s = Apply(s, StationPassengerExchange.Board(s, 1, id));
        var before = SaveCodec.Write(s);
        Assert.Throws<OverflowException>(() => StationPassengerExchange.Unload(s, 1, id)); Assert.Equal(before, SaveCodec.Write(s));
    }

    [Fact]
    public void Float32_landQ_rounding_and_zero_population_without_placed_head_are_preserved()
    {
        var b = Building() with { Imports = new(LastWeekPerDay: 24 * 1.00000006) };
        var result = StationPassengerExchange.Unload(b, Train(new(1, 2, 3), 2), 3);
        Assert.Equal((float)1.00000006, Assert.IsType<StationPassengerEffect.LandValue>(result.Effects[1]).Amount);
        var unplaced = Train(new(1, 2, 3), 39, new(0, 0, 0));
        unplaced = unplaced with { Cars = [unplaced.Head with { State = new FleetCarState.Unplaced() }] };
        result = StationPassengerExchange.Board(b, unplaced, 0, 160, 100);
        Assert.Same(unplaced, result.Train); Assert.Single(result.Effects); Assert.Equal(1, result.Station.Trains.Today);
    }
}
