using System.Collections.Immutable;
using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;
using Xunit;

namespace FreeTrain.Online.Tests;

public class RailwayTopologyTests
{
    static JsonDocument Fixtures() => JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Spatial/railway-fixtures.json")));
    static IEnumerable<JsonElement> Cases(JsonDocument f,string group) => f.RootElement.GetProperty("groups").GetProperty(group).GetProperty("cases").EnumerateArray();
    static int Int(JsonElement j,string key) => j.GetProperty(key).GetInt32();
    static int? NullableInt(JsonElement j,string key) => j.GetProperty(key).ValueKind == JsonValueKind.Null ? null : Int(j,key);
    static Location Point(JsonElement a) => new(a[0].GetInt32(),a[1].GetInt32(),a[2].GetInt32());
    static RailwayHandler Handler(int p) => p < 12 ? RailwayHandler.Normal : p < 28 ? RailwayHandler.Junction : RailwayHandler.Slope;
    static RailwayProfile Profile => RailwayProfiles.Bundled;
    static Location P => new(8,8,2);
    static DefinitionReference Ref(string id) => new(PreparedContentCatalog.BundledPackId,id,id);
    static RailComponent Rail(int pattern) => new(Ref($"system:pattern/{pattern}"),pattern);
    static TerrainWorld Terrain() => new(Profile.Terrain,"Topology",new(32,16,8),0,
        Enumerable.Repeat(new TerrainCell(2),512),new(1,[]),[]);
    static MixedWorld World(IEnumerable<(Location Position, TrafficComponents Traffic)> traffic, RailwayProfile? profile = null)
    {
        var items = traffic.Select((t,i) => (t.Position,t.Traffic,Id:(long)i+1)).ToArray();
        return new(profile ?? Profile,Terrain(),new(items.Length+1,items.Select(t => new SpatialEntity(t.Id,0,
            Profile.TrafficOwner,t.Position,new([new(new(0,0,0),true)]),true,false,0)).ToImmutableArray()),
            items.Select(t => new RailwayVoxel(t.Position,t.Id,RailwayVoxelKind.Traffic,t.Traffic)),[]);
    }
    static MixedWorld World(params (Location Position,int Pattern)[] rails) => World(rails.Select(r => (r.Position,new TrafficComponents(Rail(r.Pattern)))));

    [Fact]
    public void All_60_pattern_ports_match_the_independent_literal_table()
    {
        using var f=Fixtures();
        foreach(var row in f.RootElement.GetProperty("patterns").EnumerateArray())
            Assert.Equal(row.GetProperty("connections").EnumerateArray().Select(v=>v.GetInt32()).Order(),RailwayTopology.Ports(Int(row,"id")).Order());
        Assert.Throws<ArgumentOutOfRangeException>(()=>RailwayTopology.Ports(-1));
        Assert.Throws<ArgumentOutOfRangeException>(()=>RailwayTopology.Ports(60));
    }

    [Fact]
    public void All_576_literal_attachments_preserve_subtype_and_first_suitable_open_edge()
    {
        using var f=Fixtures();var count=0;
        foreach(var row in Cases(f,"attachments")) {
            var i=row.GetProperty("input");var e=row.GetProperty("expected");var p=Int(i,"patternId");
            var result=RailwayTopology.Attach(Handler(p),p,new(Int(i,"newDirection")),i.GetProperty("wellConnected").GetBoolean());
            Assert.Equal(e.GetProperty("allowed").GetBoolean(),result.HasValue);
            Assert.Equal(NullableInt(e,"resultId"),result);count++;
        }
        Assert.Equal(576,count);
    }

    [Fact]
    public void All_720_literal_detachments_include_16_atomic_invalid_pattern_rejections()
    {
        using var f=Fixtures();int count=0,hazards=0;
        foreach(var row in Cases(f,"detachments")) {
            var i=row.GetProperty("input");var e=row.GetProperty("expected");var p=Int(i,"patternId");var d=i.GetProperty("directions");
            int? Detach()=>RailwayTopology.Detach(Handler(p),p,new(d[0].GetInt32()),new(d[1].GetInt32()));
            if(e.GetProperty("portOutcome").GetString()=="reject-invalid-pattern") {
                Assert.Equal("invalidRailPattern",Assert.Throws<InvalidOperationException>(()=>Detach()).Message);hazards++;
            } else Assert.Equal(NullableInt(e,"patternId"),Detach());
            count++;
        }
        Assert.Equal(720,count);Assert.Equal(16,hazards);
    }

    [Fact]
    public void Literal_connectivity_requires_two_reciprocal_same_level_neighbors_on_own_ports()
    {
        using var f=Fixtures();
        foreach(var row in Cases(f,"connectivity")) {
            var input=row.GetProperty("input");var rails=new List<(Location,int)> { (P,10) };
            foreach(var n in input.GetProperty("neighbors").EnumerateArray()) {
                var edges=n.GetProperty("edges");var pos=P.Move(new(Int(n,"direction")));
                rails.Add((pos with {Z=pos.Z+Int(n,"zOffset")},NormalRailPatterns.Find(edges[0].GetInt32(),edges[1].GetInt32())));
            }
            Assert.Equal(row.GetProperty("expected").GetBoolean(),RailwayTopology.IsWellConnected(World(rails.ToArray()),P));
        }
    }

    [Fact]
    public void All_192_normal_attachment_vectors_also_pass_through_real_world_endpoint_planning()
    {
        using var f=Fixtures();var count=0;
        foreach(var row in Cases(f,"attachments").Where(r=>Int(r.GetProperty("input"),"patternId")<12)) {
            var i=row.GetProperty("input");var e=row.GetProperty("expected");var p=Int(i,"patternId");var direction=new Heading(Int(i,"newDirection"));
            var rails=new List<(Location,int)> { (P,p) };
            if(i.GetProperty("wellConnected").GetBoolean())
                foreach(var edge in f.RootElement.GetProperty("patterns")[p].GetProperty("connections").EnumerateArray()) {
                    var d=new Heading(edge.GetInt32());rails.Add((P.Move(d),NormalRailPatterns.Find(d.Index,d.Opposite.Index)));
                }
            var world=World(rails.ToArray());var plan=NormalRailway.PlanBuild(world,P,P.Move(direction));
            Assert.Equal(e.GetProperty("allowed").GetBoolean(),plan.TopologyValid);
            if(plan.TopologyValid) Assert.Equal(NullableInt(e,"resultId"),plan.Cells[0].After!.Pattern);
            else Assert.Empty(plan.Cells);
            count++;
        }
        Assert.Equal(192,count);
    }

    [Fact]
    public void Literal_routes_and_reversed_endpoints_keep_source_sign_walk_and_pattern_changes()
    {
        using var f=Fixtures();var count=0;
        foreach(var row in Cases(f,"routes")) {
            var i=row.GetProperty("input");var e=row.GetProperty("expected");
            if(e.GetProperty("outcome").GetString()!="apply")continue;
            var path=e.GetProperty("cells").EnumerateArray().Select(Point).ToArray();
            var inputs=i.GetProperty("cells").EnumerateArray().ToArray();
            var rails=inputs.Select((c,n)=>(c,n)).Where(t=>t.c.GetProperty("kind").GetString()=="rail")
                .Select(t=>(path[t.n],Int(t.c,"patternId"))).ToArray();
            var world=World(rails);var plan=NormalRailway.PlanBuild(world,Point(i.GetProperty("from")),Point(i.GetProperty("to")));
            Assert.True(plan.TopologyValid,plan.Reason);Assert.Equal(path,plan.Cells.Select(c=>c.Position));
            Assert.Equal(e.GetProperty("patterns").EnumerateArray().Select(v=>v.GetInt32()),plan.Cells.Select(c=>c.After!.Pattern));
            Assert.Equal(e.GetProperty("changed").EnumerateArray().Select(v=>v.GetInt32()),plan.Cells.Select((c,n)=>(c,n)).Where(t=>t.c.Changed).Select(t=>t.n));
            Assert.Equal(path.Length,plan.ChargedRailCells);count++;
        }
        Assert.Equal(14,count);
    }

    [Theory]
    [InlineData(0,10)][InlineData(1,1)][InlineData(2,4)][InlineData(3,7)]
    [InlineData(4,10)][InlineData(5,1)][InlineData(6,4)][InlineData(7,7)]
    public void Eight_straight_directions_keep_both_endpoints(int direction,int pattern)
    {
        var d=new Heading(direction);var end=P.Move(d).Move(d);
        var plan=NormalRailway.PlanBuild(World(),P,end);
        Assert.True(plan.TopologyValid);Assert.Equal(new[]{P,P.Move(d),end},plan.Cells.Select(c=>c.Position));
        Assert.All(plan.Cells,c=>Assert.Equal(pattern,c.After!.Pattern));Assert.Equal(3,plan.SupportColumns.Count());
    }

    [Fact]
    public void Right_angle_attachment_and_existing_middle_rail_reject_without_partial_plan()
    {
        var blocked=NormalRailway.PlanBuild(World((P,10)),P,P.Move(Heading.East));
        Assert.False(blocked.TopologyValid);Assert.Equal("cannotAttach",blocked.Reason);Assert.Empty(blocked.Cells);
        var world=World((P.Move(Heading.East),4));
        blocked=NormalRailway.PlanBuild(world,P,P.Move(Heading.East).Move(Heading.East));
        Assert.Equal("existingMiddleRail",blocked.Reason);Assert.Empty(blocked.Cells);
        Assert.Equal(4,world.Voxels[0].Traffic!.Rail!.Pattern);
    }

    [Fact]
    public void Charged_unchanged_endpoints_and_detach_noops_are_distinct_from_changed_cells()
    {
        var end=P.Move(Heading.East).Move(Heading.East);var world=World((P,4),(end,4));
        var build=NormalRailway.PlanBuild(world,P,end);
        Assert.True(build.TopologyValid);Assert.Equal(3,build.ChargedRailCells);Assert.Single(build.Cells.Where(c=>c.Changed));
        Assert.Same(build.Cells[0].Before,build.Cells[0].After);Assert.Single(build.SupportColumns);
        var remove=NormalRailway.PlanRemoval(World((P,10),(end,10)),P,end);
        Assert.True(remove.TopologyValid);Assert.Equal(2,remove.ChargedRailCells);Assert.All(remove.Cells,c=>Assert.False(c.Changed));
        Assert.Equal(12_000_000,RailwayBooking.NormalRemoval(remove.ChargedRailCells,end.Z,0).Total);
        Assert.Equal(3,remove.SupportColumns.Count());
        remove=NormalRailway.PlanRemoval(World(),P,end);
        Assert.Equal(0,remove.ChargedRailCells);Assert.Single(RailwayBooking.NormalRemoval(0,end.Z,0).Charges);
    }

    [Fact]
    public void Protected_car_skips_removal_and_charge_but_retains_unconditional_support_call()
    {
        var car=Ref("test:car");var profile=new RailwayProfile(Profile.Terrain,Profile.Bindings.Add(new(car,RailwayHandler.Car)));
        var mid=P.Move(Heading.East);var end=mid.Move(Heading.East);
        var world=World(new[] { (P,new TrafficComponents(Rail(4))),
            (mid,new TrafficComponents(Rail(4),Car:new(car,11,12))), (end,new TrafficComponents(Rail(4))) },profile);
        var build=NormalRailway.PlanBuild(world,P,end);Assert.Equal("carOccupied",build.Reason);Assert.Empty(build.Cells);
        var remove=NormalRailway.PlanRemoval(world,P,end);
        Assert.True(remove.TopologyValid);Assert.Equal(2,remove.ChargedRailCells);
        Assert.Null(remove.Cells[0].After);Assert.Same(remove.Cells[1].Before,remove.Cells[1].After);Assert.Null(remove.Cells[2].After);
        Assert.Equal(3,remove.SupportColumns.Count());
        Assert.Equal(12_000_000,RailwayBooking.NormalRemoval(remove.ChargedRailCells,end.Z,0).Total);
    }

    [Fact]
    public void Late_junction_hazard_discards_earlier_changes_and_all_prospective_charges()
    {
        var from=P.Move(Heading.NorthEast);var to=P.Move(Heading.West);
        var world=World((from,1),(P,12));var before=world.Spatial.Capture();
        var plan=NormalRailway.PlanRemoval(world,from,to);
        Assert.Equal("invalidRailPattern",plan.Reason);Assert.Empty(plan.Cells);Assert.Equal(0,plan.ChargedRailCells);Assert.Empty(plan.SupportColumns);
        Assert.Equal(before,world.Spatial.Capture());Assert.Equal(1,world.At(from)!.Traffic!.Rail!.Pattern);Assert.Equal(12,world.At(P)!.Traffic!.Rail!.Pattern);
    }

    [Theory]
    [InlineData(RailwayHandler.Bridge)][InlineData(RailwayHandler.Girder)]
    [InlineData(RailwayHandler.Tunnel)][InlineData(RailwayHandler.Garage)][InlineData(RailwayHandler.Signal)]
    public void Special_subtypes_accept_only_existing_edges_and_never_detach(RailwayHandler handler)
    {
        for(var d=0;d<8;d++) {
            Assert.Equal(d is 0 or 4 ? 10 : null,RailwayTopology.Attach(handler,10,new(d),true));
            Assert.Equal(10,RailwayTopology.Detach(handler,10,new(d),new((d+4)%8)));
        }
    }

    [Fact]
    public void Special_endpoint_retains_recipe_backup_and_controller_and_removal_stays_charged()
    {
        var rail=new RailComponent(Ref("{3E66FC7A-2B58-4182-8A8E-2D5A01BE78E5}"),10,TerrainBackup:new(1,2,3,4));
        var world=World(new[]{(P,new TrafficComponents(rail))});
        var build=NormalRailway.PlanBuild(world,P,P.Move(Heading.North));
        Assert.True(build.TopologyValid);Assert.Same(rail,build.Cells[0].After);
        var remove=NormalRailway.PlanRemoval(world,P,P.Move(Heading.North));
        Assert.True(remove.TopologyValid);Assert.Same(rail,remove.Cells[0].After);Assert.Equal(1,remove.ChargedRailCells);
        var signal=new RailComponent(Ref("{5349DE2A-DF3E-49B4-87A0-973105B7E6A4}"),10,ControllerId:42);
        world=World(new[]{(P,new TrafficComponents(signal))});
        Assert.Same(signal,NormalRailway.PlanBuild(world,P,P.Move(Heading.South)).Cells[0].After);
    }

    [Fact]
    public void Road_accessory_and_platform_slots_are_preserved_by_a_rail_only_draft()
    {
        var road=Ref("test:road");var pole=Profile.Bindings.First(b=>b.Handler==RailwayHandler.Pole).Reference;
        var platform=Ref("test:platform");
        var profile=new RailwayProfile(Profile.Terrain,Profile.Bindings.Add(new(road,RailwayHandler.Road)).Add(new(platform,RailwayHandler.Platform)));
        var components=new TrafficComponents(Rail(4),new(road,17),new(pole),Platform:platform);
        var world=World(new[]{(P,components)},profile);
        var remove=NormalRailway.PlanRemoval(world,P,P.Move(Heading.East));
        Assert.True(remove.TopologyValid);Assert.Null(remove.Cells[0].After);
        Assert.Same(components,world.At(P)!.Traffic);
        // The removal draft changes only Rail. P4-07/20 must validate dependent
        // platform/accessory cleanup before any whole-graph publication.
        Assert.Equal(1,remove.ChargedRailCells);
    }

    [Fact]
    public void Missing_or_ambiguous_exact_pattern_bindings_reject_the_whole_draft()
    {
        var missing=new RailwayProfile(Profile.Terrain,Profile.Bindings.Where(b=>b.Handler!=RailwayHandler.Normal || b.Variant!=4));
        var alias=new RailwayBinding(Ref("test:alias-4"),RailwayHandler.Normal,4);
        var ambiguous=new RailwayProfile(Profile.Terrain,Profile.Bindings.Add(alias));
        foreach(var profile in new[]{missing,ambiguous}) {
            var world=World(Array.Empty<(Location,TrafficComponents)>(),profile);
            var plan=NormalRailway.PlanBuild(world,P,P.Move(Heading.East));
            Assert.Equal("unavailableRailBinding",plan.Reason);Assert.Empty(plan.Cells);Assert.Empty(world.Voxels);
        }
    }

    [Fact]
    public void Same_point_noncoplanar_outside_and_extreme_routes_are_bounded_before_work()
    {
        var world=World();
        foreach(var removal in new[]{false,true}) {
            NormalRailwayPlan Plan(Location a,Location b)=>removal?NormalRailway.PlanRemoval(world,a,b):NormalRailway.PlanBuild(world,a,b);
            Assert.Equal("samePoint",Plan(P,P).Reason);Assert.Equal("noncoplanar",Plan(P,P with{Z=3}).Reason);
            Assert.Equal("outside",Plan(P with{Z=-1},P.Move(Heading.East) with{Z=-1}).Reason);
            Assert.Equal("outside",Plan(new(int.MinValue,0,2),new(int.MinValue+1,0,2)).Reason);
            Assert.Equal("routeWorkLimit",Plan(new(int.MinValue,0,2),new(int.MaxValue,0,2)).Reason);
        }
        Assert.Equal(NormalRailway.MaximumRouteCells,RailwayTopology.Route(new(0,0,0),new(8191,0,0)).Length);
        Assert.Throws<ArgumentException>(()=>RailwayTopology.Route(new(0,0,0),new(8192,0,0)));
        Assert.Equal(new Location(int.MaxValue,1,2),RailwayTopology.Route(new(int.MaxValue-1,0,2),new(int.MaxValue,1,2))[^1]);
    }

    [Fact]
    public void Planning_preserves_exact_saved_world_accounts_allocator_clock_random_and_mode()
    {
        var world=World((P,4));
        var request=new EmptyWorldRequest(WorldCreationProfile.Id,"Topology",new(32,16,8),0);
        var state=new MixedSessionState(new(world),TerrainProvenance.From(request));
        var before=SaveCodec.Write(state);var accounts=state.Construction.Accounts.Capture();
        Assert.True(NormalRailway.PlanBuild(world,P,P.Move(Heading.East)).TopologyValid);
        Assert.True(NormalRailway.PlanRemoval(world,P,P.Move(Heading.East)).TopologyValid);
        Assert.Equal("samePoint",NormalRailway.PlanBuild(world,P,P).Reason);
        Assert.Equal(before,SaveCodec.Write(state));Assert.Equal(accounts,state.Construction.Accounts.Capture());
        Assert.Same(world,NormalRailway.PlanBuild(world,P,P.Move(Heading.East)).Source);
    }
}
