using System.Collections.Immutable;
using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;
using Xunit;
using static FreeTrain.Online.Tests.StationStateTests;

namespace FreeTrain.Online.Tests;

public class StationStatisticsTests
{
    static int Int(JsonElement e, string key) => e.GetProperty(key).GetInt32();
    public static IEnumerable<object[]> Rows(string family)
    {
        using var doc = Fixtures();
        return Cases(doc, family).Select(r => new object[] { r.Clone() }).ToArray();
    }
    public static IEnumerable<object[]> Decay() => Rows("decay");
    public static IEnumerable<object[]> Logs() => Rows("logs");
    public static IEnumerable<object[]> Fifo() => Rows("fifo");
    static StationGraph Copy(StationGraph g, IEnumerable<StationBuilding> buildings) =>
        new(g.Profile, buildings, g.Platforms, g.Passages, g.NextStationName, g.NextPlatformName, g.Listeners, g.ListenerMemberships);
    static MixedSessionState Scene(long minute, StationBuilding? building = null, bool platform = false, ConstructionAccounts? accounts = null)
    {
        var schedule = StationLifecycle.Reconcile(null, Graph([building ?? Building()], platform ? [Platform()] : []), new(minute, 0, []));
        var world = World(schedule.Graph);
        return new(new(world, accounts), TerrainProvenance.From(new EmptyWorldRequest(WorldCreationProfile.Id,
            world.Terrain.Name, new(64, 24, 8), 0)), clock: schedule.Clock);
    }
    static MixedSessionState Reopen(MixedSessionState s) => SaveCodec.ReadWorld(SaveCodec.Write(s), TerrainProfiles.Resolve, RailwayProfiles.Resolve).Mixed!;
    static void Until(MixedSessionState s, long minute) { while (s.Clock.Time.AbsoluteMinute < minute) s.AdvanceOneMinute(); }
    static void Near(double expected, double actual) => Assert.True(Math.Abs(expected - actual) <= Math.Max(1e-12, Math.Abs(expected) * 1e-12), $"{expected:R} != {actual:R}");

    [Theory, MemberData(nameof(Decay))]
    public void Actual_aligned_hour_consumes_all_eight_independent_float32_vectors(JsonElement row)
    {
        var i = row.GetProperty("input"); var e = row.GetProperty("expected");
        var passengers = new StationPassengers(Int(i, "gone"), Int(i, "loaded"), Int(i, "unloaded"));
        Assert.True(e.GetProperty("factor").GetDouble() == StationStatistics.PerDayFactor);
        Assert.Equal(Int(e, "averageLoaded"), StationStatistics.AverageLoaded(passengers));
        Assert.Equal(Int(e, "averageUnloaded"), StationStatistics.AverageUnloaded(passengers));
        var s = Scene(517, Building() with { Passengers = passengers, Imports = new(17, 19, 23, 29) });
        var registration = s.World.Stations!.Building(1).HourTimer!.Registration;
        Until(s, 539); Assert.Equal(passengers, s.World.Stations.Building(1).Passengers);
        s.AdvanceOneMinute(); var b = s.World.Stations.Building(1);
        Assert.Equal(new StationPassengers(Int(e, "gone"), Int(e, "loaded"), Int(e, "unloaded")), b.Passengers);
        Assert.Equal(new StationTransportLog(17, 19, 23, 29), b.Imports);
        Assert.Empty(s.Construction.Accounts.Transactions);
        Assert.Equal(1, b.Revision); Assert.Equal(b.Revision, s.World.Spatial.Entities.Single().Revision);
        Assert.Equal(registration, b.HourTimer!.Registration); Assert.Equal(600, b.HourTimer.DueMinute);
    }

    [Theory, MemberData(nameof(Logs))]
    public void Actual_midnights_rotate_all_three_logs_using_source_day_and_nonlinear_week(JsonElement row)
    {
        var totals = row.GetProperty("input").GetProperty("totals").EnumerateArray().Select(v => v.GetInt32()).ToArray();
        var expected = row.GetProperty("expected").GetProperty("trace").EnumerateArray().ToArray();
        var log = new StationTransportLog();
        for (var day = 0; day < 7; day++)
        {
            // Midnight on Sunday through Saturday, rather than the day just ended.
            var due = (7 + day) * GameTime.Day;
            log = log with { Today = totals[day] };
            var s = Scene(due - 1, Building() with { Imports = log, Exports = log, Trains = log });
            s.AdvanceOneMinute(); var b = s.World.Stations!.Building(1); log = b.Imports;
            var e = expected[day];
            Assert.Equal(Int(e, "today"), log.Today); Assert.Equal(Int(e, "yesterday"), log.Yesterday);
            Near(e.GetProperty("thisWeek").GetDouble(), log.ThisWeek); Near(e.GetProperty("lastWeek").GetDouble(), log.LastWeekPerDay);
            Assert.Equal(log, b.Exports); Assert.Equal(log, b.Trains);
            var charge = Assert.Single(s.Construction.Accounts.Transactions);
            Assert.Equal(Definition.OperationCost, charge.Expenditure); Assert.Equal(due, charge.AbsoluteMinute);
            Assert.Equal(AccountGenre.RailService, charge.Genre);
            Near(Math.Pow(log.ThisWeek / (day + 1), 5), StationStatistics.ThisWeekPerDay(log, s.Clock.Time));
            Assert.Equal(SaveCodec.Write(s), SaveCodec.Write(Reopen(s)));
        }
    }

    [Fact]
    public void Hourly_demand_refresh_below_gone_clamps_waiting_without_clamping_or_resetting_gone()
    {
        var s = Scene(517, Building() with { Passengers = new(800, 319, 39) }, platform: true);
        // Bind a real Traffic owner as controlled special supply, using the P6-16 boundary.
        var table = Enumerable.Range(0, 24).Select(h => h == 8 ? 100 : h == 9 ? 10 : 200).ToImmutableArray();
        var p = new StationProvider(new("HourlyPopulation", 1000, Tables: [table, table, table, table]), 0, Special: true);
        var world = StationCatchment.Attach(s.World, 10, p);
        s = s.Successor(s.Revision, new(world, s.Construction.Accounts), false);
        Assert.Equal(1000, StationCatchment.Demand(s.World.Stations!, 1, s.Clock.Time));
        Assert.Equal(200, StationCatchment.Waiting(s.World.Stations!, 1, s.Clock.Time));
        Until(s, 540);
        Assert.Equal(100, StationCatchment.Demand(s.World.Stations!, 1, s.Clock.Time));
        Assert.Equal(640, s.World.Stations!.Building(1).Passengers.Gone);
        Assert.Equal(0, StationCatchment.Waiting(s.World.Stations, 1, s.Clock.Time));
        s = Reopen(s); Until(s, 600);
        Assert.Equal(2000, StationCatchment.Demand(s.World.Stations!, 1, s.Clock.Time));
        Assert.Equal(1488, StationCatchment.Waiting(s.World.Stations!, 1, s.Clock.Time));
        Assert.Equal(512, s.World.Stations!.Building(1).Passengers.Gone);
    }

    [Fact]
    public void Uninterrupted_and_repeated_checkpoint_continuation_match_counters_accounts_and_every_queue_field()
    {
        var direct = Scene(517, Building() with { Passengers = new(1234567, 16777217, int.MaxValue),
            Imports = new(17, 19, 23.125, 29.5), Exports = new(31, 37, 41.75, 43.25), Trains = new(47, 53, 59.5, 61.125) }, platform: true);
        var resumed = Reopen(direct);
        var cookies = direct.Clock.Capture().Pending.ToDictionary(t => t.HandlerId, t => t.Registration);
        foreach (var boundary in new long[] { 539, 540, 1439, 1440, 1956, 1957, 8639, 8640, 10080 })
        {
            Until(direct, boundary); Until(resumed, boundary);
            Assert.Equal(SaveCodec.Write(direct), SaveCodec.Write(resumed));
            resumed = Reopen(resumed);
            var pending = resumed.Clock.Capture().Pending;
            Assert.Equal(3, pending.Length); Assert.Equal(3, pending.Select(t => t.HandlerId).Distinct().Count());
            Assert.All(pending, t => Assert.Equal(cookies[t.HandlerId], t.Registration));
        }
        var charges = direct.Construction.Accounts.Transactions;
        Assert.Equal(7, charges.Count(t => t.Operation == "station/1/day"));
        Assert.Equal(6, charges.Count(t => t.Operation == "platform/2/day"));
        Assert.All(charges.Where(t => t.Operation == "platform/2/day"), t => {
            Assert.Equal(720000, t.Expenditure); Assert.Equal(517, t.AbsoluteMinute % 1440);
        });
    }

    [Theory, MemberData(nameof(Fifo))]
    public void Six_literal_equal_time_traces_use_real_station_dispatch_and_controlled_train_callbacks(JsonElement row)
    {
        var i = row.GetProperty("input"); var entries = i.GetProperty("entries").EnumerateArray().ToArray();
        var expected = row.GetProperty("expected").GetProperty("trace").EnumerateArray().ToArray();
        var due = entries[0].GetProperty("due").GetInt64();
        var s = Scene(due - 1, Building() with { Passengers = new(Int(i, "gone")), Exports = new(Int(i, "today"), Int(i, "yesterday")) }, platform: true);
        // Bind the fixture's explicit 600,000 source operating-cost input. No bundled definition is substituted.
        var profile = RailwayProfiles.Bundled;
        var stations = new StationProfile(Profile.Packs, Profile.Definitions.Select(d => d == Definition ? d with { OperationCost = Int(i, "stationCost") } : d), Profile.Art, Profile.Bells);
        var railway = new RailwayProfile(profile.Terrain, profile.Bindings, profile.FleetProfile, stations);
        var stock = RollingStockCatalog.Bundled.Trains.First();
        for (var n = 0; n < 2; n++) s = FleetTrading.Commit(s, FleetTrading.QuotePurchase(s, stock.Binding, 1, 1));
        var f = s.Fleet!;
        var trains = f.Trains.Select(t => t with { Revision = t.Revision + 1, State = TrainOperationalState.Moving,
            Cars = t.Cars.Select(c => c with { Revision = c.Revision + 1, State = new FleetCarState.Outside(new(0, 0, 2), Heading.East, 100) }).ToImmutableArray() }).ToArray();
        f = new(f.Profile, f.RootGroupId, trains, f.Groups, f.Controllers);
        var labels = new Dictionary<string, string> { [MixedSessionState.TrainHandler(trains[0].Id)] = "depart", [MixedSessionState.TrainHandler(trains[1].Id)] = "wait",
            ["station/1/hour"] = "hour", ["station/1/day"] = "day", ["platform/2/day"] = "platform" };
        var pending = new List<ScheduledAction>();
        long next = 10;
        ScheduledAction Timer(ScheduledAction timer)
        {
            var match = entries.SingleOrDefault(e => e.GetProperty("handler").GetString() == labels[timer.HandlerId]);
            var order = match.ValueKind == JsonValueKind.Undefined ? next++ : Int(match, "order");
            var changed = timer with { Registration = order, Order = order };
            pending.Add(changed); return changed;
        }
        var g = s.World.Stations!;
        g = new(stations, g.Buildings.Select(b => b with { HourTimer = Timer(b.HourTimer!), DayTimer = Timer(b.DayTimer!) }),
            g.Platforms.Select(p => p with { DayTimer = Timer(p.DayTimer!) }));
        // Platform entries in the midnight fixtures are already at their relative due time.
        if (entries.Any(e => e.GetProperty("handler").GetString() == "platform"))
        {
            var timer = g.Platform(2).DayTimer!; var changed = timer with { DueMinute = due };
            pending[pending.IndexOf(timer)] = changed;
            g = new(stations, g.Buildings, [g.Platform(2) with { DayTimer = changed }]);
        }
        foreach (var t in trains)
        {
            var handler = MixedSessionState.TrainHandler(t.Id);
            var entry = entries.SingleOrDefault(e => e.GetProperty("handler").GetString() == labels[handler]);
            var order = entry.ValueKind == JsonValueKind.Undefined ? next++ : Int(entry, "order");
            pending.Add(new(order, order, entry.ValueKind == JsonValueKind.Undefined ? due + 10000 : due, handler, 0));
        }
        var world = new MixedWorld(railway, s.World.Terrain, s.World.Spatial.Capture(), s.World.Voxels, [], s.World.Revision, fleet: f, stations: g);
        var trace = new List<(string Handler, int Gone, int Today, int Yesterday, long Cash)>();
        void Record(string label, MixedConstructionState state)
        {
            var b = state.World.Stations!.Building(1);
            trace.Add((label, b.Passengers.Gone, b.Exports.Today, b.Exports.Yesterday, state.Accounts.Cash));
        }
        TrainCallbackResult ControlledTrain(MixedSessionState session, long id)
        {
            var label = labels[MixedSessionState.TrainHandler(id)]; var state = session.Construction;
            if (label == "depart")
            {
                // P6-19 owns real yard exchange. This controlled effect isolates FIFO visibility.
                var graph = session.World.Stations!; var b = graph.Building(1);
                var amount = Math.Min(Int(i, "packed"), (int)(Math.Max(0, Int(i, "population") - b.Passengers.Gone) * Int(i, "amenity") * 0.01f * 0.3f));
                b = b with { Revision = b.Revision + 1, Passengers = b.Passengers with { Gone = b.Passengers.Gone + amount }, Exports = b.Exports with { Today = b.Exports.Today + amount } };
                var spatial = session.World.Spatial.Capture() with { Entities = session.World.Spatial.Entities.Select(e => e.Id == b.Id ? e with { Revision = b.Revision } : e).ToImmutableArray() };
                state = new(session.World.Replace(session.World.Revision, session.World.Terrain, spatial, session.World.Voxels, [], stations: Copy(graph, [b])), state.Accounts);
            }
            Record(label, state); return new(state, 10000);
        }
        s = new(new(world, new(Int(i, "cash"))), s.Provenance, clock: new(due - 1, next, pending.ToImmutableArray()), revision: world.Revision,
            trainCallback: ControlledTrain, stationCallback: (session, handler) => { var after = StationStatistics.Tick(session, handler); Record(labels[handler], after); return after; });
        s.AdvanceOneMinute();
        Assert.Equal(expected.Select(e => (e.GetProperty("handler").GetString()!, Int(e, "gone"), Int(e, "today"), Int(e, "yesterday"), e.GetProperty("cash").GetInt64())), trace);
    }

    [Fact]
    public void Late_nonfinite_weekly_result_discards_prior_midnight_charges_and_log_resets()
    {
        var s = Scene(6 * GameTime.Day - 1, Building() with { Imports = new(17), Exports = new(31, ThisWeek: double.MaxValue) }, platform: true);
        var before = SaveCodec.Write(s);
        Assert.Throws<OverflowException>(() => s.AdvanceOneMinute());
        Assert.Equal(before, SaveCodec.Write(s));
    }
}
