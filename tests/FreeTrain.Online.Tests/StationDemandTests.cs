using System.Collections.Immutable;
using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;
using Xunit;
using static FreeTrain.Online.Tests.StationStateTests;

namespace FreeTrain.Online.Tests;

public class StationDemandTests
{
    static JsonElement[] Rows(string group)
    {
        using var doc = Fixtures();
        return Cases(doc, group).Select(r => r.Clone()).ToArray();
    }
    public static IEnumerable<object[]> Tables() => Rows("tables").Select(r => new object[] { r });
    public static IEnumerable<object[]> Providers() => Rows("providers").Select(r => new object[] { r });
    public static IEnumerable<object[]> Listeners() => Rows("listeners").Select(r => new object[] { r });

    [Theory, MemberData(nameof(Tables))]
    public void All_42_source_tables_match_1008_independent_cells_and_truncation(JsonElement row)
    {
        var input = row.GetProperty("input");
        var kind = Path.GetFileNameWithoutExtension(input.GetProperty("source").GetString()!);
        var table = PopulationTables.All[$"{kind}.{input.GetProperty("table").GetString()}"];
        Assert.Equal(24, table.Length);
        var expected = row.GetProperty("expected");
        Assert.Equal(expected.GetProperty("percent").EnumerateArray().Select(v => v.GetInt32()), table);
        Assert.Equal(expected.GetProperty("scaled").EnumerateArray().Select(v => v.GetInt32()), table.Select(p => Population.Scale(input.GetProperty("base").GetInt32(), p)));
        Assert.Equal(42, PopulationTables.All.Count);
    }

    [Theory, MemberData(nameof(Providers))]
    public void Source_calendar_branches_and_residents_use_real_portable_time(JsonElement row)
    {
        var i = row.GetProperty("input"); var expected = row.GetProperty("expected");
        var time = Enumerable.Range(0, 365 * 7).Select(d => new GameTime(d * GameTime.Day + i.GetProperty("hour").GetInt32() * 60))
            .First(t => t.DayOfWeek == i.GetProperty("dayOfWeek").GetInt32() && t.IsWeekend == i.GetProperty("weekend").GetBoolean() &&
                t.IsHoliday == i.GetProperty("holiday").GetBoolean() && t.IsVacation == i.GetProperty("vacation").GetBoolean());
        var p = new Population(i.GetProperty("provider").GetString()!, i.GetProperty("base").GetInt32()); p.Validate();
        Assert.Equal(expected.GetProperty("residents").GetInt32(), p.Residents);
        Assert.Equal(expected.GetProperty("value").GetInt32(), i.GetProperty("entering").GetBoolean() ? p.Incoming(time) : p.Outgoing(time));
    }

    static Population Nested(JsonElement tree)
    {
        var kind = tree.GetProperty("kind").GetString();
        return kind switch {
            "constant" => new("ConstantPopulation", tree.GetProperty("value").GetInt32()),
            "hourly" => new("HourlyPopulation", tree.GetProperty("base").GetInt32(), Tables: Enumerable.Repeat(Enumerable.Repeat(tree.GetProperty("percent").GetInt32(), 24).ToImmutableArray(), 4).ToImmutableArray()),
            "multiplier" => new("MultiplierPopulation", Factor: tree.GetProperty("factor").GetInt32(), Children: [Nested(tree.GetProperty("child"))]),
            "persistent" => new("PersistentPopulation", Children: [Nested(tree.GetProperty("child"))]),
            "sum" => new("CombinationPopulation", Children: tree.GetProperty("children").EnumerateArray().Select(Nested).ToImmutableArray()),
            _ => throw new ArgumentException(kind)
        };
    }
    [Fact]
    public void Nested_truncation_persistent_binding_and_checked_products_match_independent_vectors()
    {
        foreach (var row in Rows("nested"))
        {
            var p = Nested(row.GetProperty("input").GetProperty("tree")); p.Validate();
            var expected = row.GetProperty("expected");
            if (expected.TryGetProperty("rejected", out _)) Assert.Throws<OverflowException>(() => p.Outgoing(new(0)));
            else Assert.Equal(expected.GetProperty("value").GetInt32(), p.Outgoing(new(0)));
        }
        var tables = Enumerable.Range(0, 14).Select(i => Enumerable.Repeat(i, 24).ToImmutableArray()).ToImmutableArray();
        var weekly = new Population("HourlyWeeklyPopulation", 100, Tables: tables); weekly.Validate();
        for (var day = 0; day < 7; day++)
        {
            var t = new GameTime((7 + day) * GameTime.Day);
            Assert.Equal(t.IsHoliday ? 0 : day, weekly.Outgoing(t));
            Assert.Equal(7 + (t.IsHoliday ? 0 : day), weekly.Incoming(t));
        }
        var stadium = new Population("StadiumPopulation", 1234, Entering: 876);
        Assert.Equal(1, stadium.Residents); Assert.Equal(1234, stadium.Outgoing(new(0))); Assert.Equal(876, stadium.Incoming(new(0)));
        var aboveBase = new Population("HourlyPopulation", 100,
            Tables: Enumerable.Repeat(Enumerable.Repeat(125, 24).ToImmutableArray(), 4).ToImmutableArray());
        aboveBase.Validate(); Assert.Equal(125, aboveBase.Outgoing(new(0)));
        var bar = new Population("BarPopulation", 125);
        var friday = new GameTime(12 * GameTime.Day + 22 * 60); // January 13: nonholiday Friday.
        Assert.Equal(125, bar.Outgoing(friday)); Assert.Equal(112, bar.Incoming(friday));
    }

    [Fact]
    public void Every_bundled_population_node_binds_from_inert_declarations_without_city_activation()
    {
        var bindings = PopulationBindings.Build(PreparedContentCatalog.Bundled);
        var nodes = 0;
        void Count(Population p) { nodes++; foreach (var c in p.Children.IsDefault ? [] : p.Children) Count(c); }
        foreach (var p in bindings.Values) { p.Validate(); Count(p); Assert.True(p.Residents >= 0); Assert.True(p.Outgoing(new(GameTime.StartMinute)) >= 0); }
        Assert.Equal(169, nodes);
        Assert.Contains(bindings.Values, p => p.Kind == "CombinationPopulation");
        Assert.Single(bindings.Values.Where(p => p.Kind == "StadiumPopulation"));
    }

    static readonly Location Origin = new(30, 30, 2);
    static SpatialIndex Spatial(Footprint? footprint = null) => new(new WorldSpace(96, 96, 32), 101,
        [new(100, 0, RailwayProfiles.Bundled.TrafficOwner, Origin, footprint ?? new([new(new(0, 0, 0), true)]), true, false, 0)]);
    static StationProvider Supply(bool special = false) => new(new("ConstantPopulation", 1000), 3, Special: special);
    static string[] Names(IEnumerable<long> ids, Dictionary<long, string> names) => ids.Select(id => names[id]).ToArray();

    [Theory, MemberData(nameof(Listeners))]
    public void Creation_order_sphere_cube_fifth_tie_and_removal_match_literal_traces(JsonElement row)
    {
        var i = row.GetProperty("input"); var expected = row.GetProperty("expected");
        var data = i.GetProperty("stations").EnumerateArray().ToArray();
        var names = data.Select((s, n) => (Id: (long)n + 1, Name: s.GetProperty("id").GetString()!)).ToDictionary(p => p.Id, p => p.Name);
        var buildings = data.Select((s, n) => { var o = s.GetProperty("offset"); return Building(n + 1) with {
            Location = new(Origin.X + o[0].GetInt32(), Origin.Y + o[1].GetInt32(), Origin.Z + o[2].GetInt32()) }; }).ToArray();
        var graph = Graph(); var spatial = Spatial(); var provider = Supply(i.GetProperty("special").GetBoolean());
        var listenerFirst = i.GetProperty("order").GetString() == "station-after";
        if (listenerFirst) graph = StationCatchment.Attach(graph, 100, provider, spatial);
        var traces = expected.GetProperty("trace").EnumerateArray().ToArray();
        for (var n = 0; n < buildings.Length; n++)
        {
            if (listenerFirst) graph = StationCatchment.Advertise(StationBuildingLifecycle.Created(graph, buildings[n]), buildings[n].Id, spatial, new(96, 96, 32));
            else graph = StationCatchment.Attach(Graph(buildings.Take(n + 1)), 100, provider, spatial);
            Assert.Equal(traces[n].GetProperty("local").EnumerateArray().Select(v => v.GetString()), Names(graph.Listeners.Single().Stations, names));
            Assert.Equal(traces[n].GetProperty("reverse").EnumerateArray().Select(v => v.GetString()), Names(graph.Buildings.Where(b => graph.ListenerMemberships.Any(m => m.StationId == b.Id && m.Providers.Contains(100))).Select(b => b.Id), names));
            // Restoring the wire must retain asymmetric forward/reverse edges.
            graph = StationWire.Restore(StationWire.Read(graph), Profile)!;
        }
        var removed = i.GetProperty("remove").EnumerateArray().Select(s => s.GetString()).ToHashSet();
        foreach (var name in removed) graph = StationBuildingLifecycle.Removed(graph, names.Single(p => p.Value == name).Key);
        Assert.Equal(expected.GetProperty("local").EnumerateArray().Select(v => v.GetString()).Where(s => !removed.Contains(s)), Names(graph.Listeners.Single().Stations, names));
        Assert.Equal(expected.GetProperty("reverse").EnumerateArray().Select(v => v.GetString()), Names(graph.Buildings.Where(b => graph.ListenerMemberships.Any(m => m.StationId == b.Id && m.Providers.Contains(100))).Select(b => b.Id), names));
    }

    [Fact]
    public void Ordinary_sharing_height_land_caps_and_special_supply_keep_source_integer_order()
    {
        foreach (var row in Rows("listenerDemand"))
        {
            var i = row.GetProperty("input"); var e = row.GetProperty("expected");
            int Query(bool special) => StationProvider.Contribution(i.GetProperty("raw").GetInt32(), i.GetProperty("stations").GetInt32(),
                i.GetProperty("land").GetInt32(), i.GetProperty("xy").GetInt32(), i.GetProperty("z").GetInt32(), special);
            if (e.TryGetProperty("rejected", out _)) Assert.Throws<OverflowException>(() => Query(false));
            else { Assert.Equal(e.GetProperty("ordinary").GetInt32(), Query(false)); Assert.Equal(e.GetProperty("special").GetInt32(), Query(true)); }
        }
    }

    [Fact]
    public void Actual_footprint_intersection_advertises_once_and_z_bounds_are_clipped()
    {
        var spatial = Spatial(new([new(new(0, 0, 0), true), new(new(-10, 0, 0), true)]));
        var g = StationCatchment.Attach(Graph(), 100, Supply(), spatial);
        var b = Building() with { Location = new(10, 30, 2) };
        g = StationCatchment.Advertise(StationBuildingLifecycle.Created(g, b), b.Id, spatial, new(96, 96, 32));
        Assert.Equal(new long[] { 1 }, g.Listeners.Single().Stations);
        Assert.Equal(new long[] { 100 }, g.ListenerMemberships.Single().Providers);
        Assert.Equal(0, g.Building(1).Revision);
        var again = StationCatchment.Advertise(g, 1, spatial, new(96, 96, 32));
        Assert.Equal(g.Listeners.Single().Stations, again.Listeners.Single().Stations);
        Assert.Equal(g.Building(1).Revision, again.Building(1).Revision);
    }

    static MixedSessionState Open(byte[] bytes) => SaveCodec.ReadWorld(bytes, TerrainProfiles.Resolve, RailwayProfiles.Resolve).Mixed!;
    [Fact]
    public void Bound_inputs_real_station_construction_removal_and_reopen_retain_exact_demand_and_revisions()
    {
        var initial = World(Graph(passages: [new(100, new(12, 12, 2), Heading.East, PassageMaterial.Concrete)]));
        // The first city provider can precede both a station and a station graph.
        var w = new MixedWorld(initial.Profile, initial.Terrain, initial.Spatial.Capture(), initial.Voxels, initial.Slopes);
        var source = StationBuildingConstructionTests.Empty(world: w);
        w = StationCatchment.Attach(w, 100, new(new("ResidentialPopulation", 125), 3));
        var session = source.Successor(source.Revision, new(w, source.Construction.Accounts), source.ConstructionMode);
        var original = SaveCodec.Write(session);
        var preview = StationBuildingConstruction.Preview(w, new(20, 12, 2), Definition.Reference);
        Assert.True(preview.Legal, preview.Reason); Assert.Equal(original, SaveCodec.Write(session));
        session = StationBuildingConstruction.Commit(session, preview);
        var b = session.World.Stations!.Buildings.Single();
        Assert.Equal(0, b.Revision); Assert.Equal(new long[] { b.Id }, session.World.Stations.Listeners.Single().Stations);
        Assert.Equal(2, session.World.Spatial.Entities.Single(e => e.Id == 100).Revision);
        var demand = StationCatchment.Demand(session.World.Stations, b.Id, session.Clock.Time);
        Assert.InRange(demand, 1, 13);
        Assert.Equal(demand, StationCatchment.Waiting(session.World.Stations, b.Id, session.Clock.Time));
        var bytes = SaveCodec.Write(session); var reopened = Open(bytes);
        Assert.Equal(bytes, SaveCodec.Write(reopened));
        Assert.Equal(demand, StationCatchment.Demand(reopened.World.Stations!, b.Id, reopened.Clock.Time));
        var provider = reopened.World.Stations!.Listeners.Single().Provider!;
        var oldGraph = reopened.World.Stations!;
        var stale = new StationGraph(oldGraph.Profile, oldGraph.Buildings, oldGraph.Platforms, oldGraph.Passages,
            oldGraph.NextStationName, oldGraph.NextPlatformName, oldGraph.Listeners.Select(l => l with { Provider = provider with { LandValue = 4 } }), oldGraph.ListenerMemberships);
        Assert.Throws<ArgumentException>(() => reopened.World.Replace(reopened.World.Revision, reopened.World.Terrain,
            reopened.World.Spatial.Capture(), reopened.World.Voxels, reopened.World.Slopes, stations: stale));
        var updated = StationCatchment.Update(reopened.World, 100, provider with { Population = new("StadiumPopulation", 500, Entering: 100) });
        Assert.Equal(3, updated.Spatial.Entities.Single(e => e.Id == 100).Revision);
        Assert.Equal(13, StationCatchment.Demand(updated.Stations!, b.Id, reopened.Clock.Time));
        var detached = StationCatchment.Detach(updated, 100);
        Assert.Empty(detached.Stations!.Listeners); Assert.Equal(0, StationCatchment.Demand(detached.Stations, b.Id, reopened.Clock.Time));
        Assert.Equal(b.Revision + 1, detached.Stations.Building(b.Id).Revision);
        var remove = StationBuildingConstruction.Preview(reopened.World, b.Location, null, true, b.Id, b.Revision);
        Assert.True(remove.Legal, remove.Reason);
        var removed = StationBuildingConstruction.Commit(reopened, remove);
        Assert.Empty(removed.World.Stations!.Listeners.Single().Stations);
        Assert.Equal(3, removed.World.Spatial.Entities.Single(e => e.Id == 100).Revision);
        Assert.Equal(SaveCodec.Write(removed), SaveCodec.Write(Open(SaveCodec.Write(removed))));
        Assert.Throws<ArgumentException>(() => StationCatchment.Attach(w, 999, Supply()));
        var invalid = provider with { Population = new("MultiplierPopulation") };
        Assert.Throws<ArgumentException>(() => StationCatchment.Update(reopened.World, 100, invalid));
        Assert.Equal(bytes, SaveCodec.Write(reopened));
    }

    [Fact]
    public void Special_and_ordinary_totals_share_by_local_links_but_only_reverse_members_supply_demand()
    {
        var s = StationPersistenceTests.Scene(); var g = s.World.Stations!;
        g = new(g.Profile, g.Buildings, g.Platforms, g.Passages, g.NextStationName, g.NextPlatformName,
            g.Listeners.Select(l => l with { Provider = new(new("ConstantPopulation", 1000), 0, Special: l.ProviderId == 140) }), g.ListenerMemberships);
        var w = new MixedWorld(s.World.Profile, s.World.Terrain, s.World.Spatial.Capture(), s.World.Voxels, s.World.Slopes,
            s.World.Revision, s.World.SpeedLimits, s.World.Fleet, g);
        var bound = StationBuildingConstructionTests.Empty(world: w, clock: s.Clock.Capture());
        Assert.Equal(0, StationCatchment.Demand(g, 5, s.Clock.Time)); // fifth newcomer has no reverse edge
        Assert.Equal(260, StationCatchment.Demand(g, 2, s.Clock.Time));
        Assert.Equal(10, StationCatchment.Demand(g, 1, s.Clock.Time));
        Assert.Equal(3, StationCatchment.Waiting(g, 1, s.Clock.Time));
        var saved = SaveCodec.Write(bound); var restored = Open(saved);
        Assert.Equal(saved, SaveCodec.Write(restored));
        Assert.Equal(260, StationCatchment.Demand(restored.World.Stations!, 2, restored.Clock.Time));
        for (var n = 0; n < 5; n++) { bound.AdvanceOneMinute(); restored.AdvanceOneMinute(); }
        Assert.Equal(SaveCodec.Write(bound), SaveCodec.Write(restored));
    }
}
