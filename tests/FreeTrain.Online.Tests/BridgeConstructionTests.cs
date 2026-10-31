using System.Collections.Immutable;
using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Simulation;
using Xunit;

namespace FreeTrain.Online.Tests;

public class BridgeConstructionTests
{
    static RailwayProfile Profile => RailwayProfiles.Bundled;
    static Location Start => new(14,10,2);
    static TerrainWorld Terrain(int ground=0,int water=0) => new(Profile.Terrain,"Bridge",new(40,32,8),water,
        Enumerable.Repeat(new TerrainCell(ground,TerrainSurface.Cut),1280),new(1,[]),[]);
    static MixedWorld Empty(TerrainWorld? terrain=null) { var t=terrain??Terrain();return new(Profile,t,t.Spatial.Capture(),[],[]); }
    static MixedWorld Add(MixedWorld w,Location p,RailComponent? rail=null,bool car=false,DefinitionReference? platform=null,
        RailwayVoxelKind kind=RailwayVoxelKind.Traffic,long? supported=null)
    {
        var id=w.Spatial.NextEntityId;
        var def=kind==RailwayVoxelKind.Pier ? Profile.Bindings.Single(b=>b.Reference.ContributionId=="system:pier").Reference:Profile.TrafficOwner;
        var carDef=new DefinitionReference(Profile.Terrain.Packs[0].Id,"test:car","test:car");
        var profile=car&&!w.Profile.Bindings.Any(b=>b.Handler==RailwayHandler.Car)?new RailwayProfile(w.Profile.Terrain,w.Profile.Bindings.Add(new(carDef,RailwayHandler.Car))):w.Profile;
        var voxel=kind==RailwayVoxelKind.Pier?new RailwayVoxel(p,id,kind,Definition:def,SupportedOwnerId:supported):
            new(p,id,kind,new(rail,Car:car?new(carDef,1,1):null,Platform:platform));
        return new(profile,w.Terrain,new(id+1,w.Spatial.Entities.Add(new(id,0,def,p,new([new(new(0,0,0),true)]),true,false,0))),w.Voxels.Add(voxel),w.Slopes,w.Revision);
    }
    static RailComponent Rail(int pattern=4) => new(Profile.Bindings.Single(b=>b.Handler==RailwayHandler.Normal&&b.Variant==pattern).Reference,pattern);
    static MixedWorld Apply(MixedWorld w,RailwayConstructionPlan p) => BridgeRailwayConstruction.Apply(new(w,new ConstructionAccounts()),p,new(0)).World;

    [Fact]
    public void Independent_literal_bridge_vectors_match_geometry_art_booking_and_protection()
    {
        using var f=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Spatial/railway-fixtures.json")));
        var rows=f.RootElement.GetProperty("groups").EnumerateObject().SelectMany(g=>g.Value.GetProperty("cases").EnumerateArray()).Where(c=>c.GetProperty("id").GetString()!.StartsWith("BRIDGE-"));
        var count=0;
        foreach (var row in rows)
        {
            var input=row.GetProperty("input");var expected=row.GetProperty("expected");
            var d=input.GetProperty("direction").GetInt32();
            if (d%2!=0) continue; // Controller snapping is exercised separately below.
            var z=input.GetProperty("z").GetInt32();var data=input.GetProperty("cells").EnumerateArray().ToArray();
            var start=Start with { Z=z };var (dx,dy)=new Heading(d).Offset;
            Location P(int n)=>new(start.X+n*dx,start.Y+n*dy,z);
            var terrain=Terrain(2,input.GetProperty("water").GetInt32());var cells=terrain.Cells.ToBuilder();
            for (var n=0;n<data.Length;n++) {var s=terrain.Space.ToStorage(P(n));cells[s.V*40+s.H]=new(data[n].GetProperty("ground").GetInt32(),TerrainSurface.Cut);}
            terrain=new(terrain.Profile,terrain.Name,new(40,32,8),terrain.WaterLevel,cells,new(1,[]),[]);
            var w=Empty(terrain);
            for (var n=0;n<data.Length;n++)
            {
                var cell=data[n];var kind=cell.GetProperty("kind").GetString();if(kind=="empty")continue;
                var pattern=Enumerable.Range(0,28).First(p=>RailwayTopology.Ports(p).Order().SequenceEqual(cell.GetProperty("edges").EnumerateArray().Select(e=>e.GetInt32()).Order()));
                var rail=kind=="bridge"?new RailComponent(Profile.Bindings.Single(b=>b.Handler==RailwayHandler.Bridge).Reference,pattern):Rail(pattern);
                w=Add(w,P(n),rail,cell.TryGetProperty("car",out var c)&&c.GetBoolean());
            }
            var p=BridgeRailwayConstruction.Preview(w,start,P(data.Length-1),input.GetProperty("action").GetString()=="remove");
            var legal=expected.GetProperty("outcome").GetString()=="apply";
            Assert.Equal(legal,p.Legal);
            Assert.Equal(expected.GetProperty("spend").GetInt64(),p.Booking.Total);
            if (legal)
            {
                Assert.Equal(expected.GetProperty("changed").EnumerateArray().Select(e=>e.GetInt32()),p.Topology.Cells.Select((c,n)=>(c,n)).Where(x=>x.c.Changed).Select(x=>x.n));
                foreach (var a in expected.GetProperty("art").EnumerateArray())Assert.Equal(a.GetProperty("sprite").GetInt32(),p.Topology.Cells[a.GetProperty("cell").GetInt32()].After!.Variant);
                var result=Apply(w,p);Assert.Equal(w.Terrain,result.Terrain);
                Assert.Equal(w.Revision+1,result.Revision);
            }
            count++;
        }
        Assert.Equal(14,count);
    }

    [Theory]
    [InlineData(0,3,4,5,2)][InlineData(2,2,1,0,0)][InlineData(4,5,4,3,2)][InlineData(6,0,1,2,0)]
    public void Exact_axis_art_alternating_top_body_piers_and_removal(int direction,int begin,int middle,int end,int style)
    {
        var w=Empty();var d=new Heading(direction);var (dx,dy)=d.Offset;var to=Start with{X=Start.X+4*dx,Y=Start.Y+4*dy};
        var plan=BridgeRailwayConstruction.Preview(w,Start,to);Assert.True(plan.Legal);
        Assert.Equal(new[]{begin,middle,middle,middle,end},plan.Topology.Cells.Select(c=>c.After!.Variant));
        Assert.Equal(90_000_000,plan.Booking.Total);Assert.Equal(6,plan.ElectedPiers.Length);
        Assert.All(plan.ElectedPiers,p=>Assert.Equal($"system:bridge-pier/{style+(p.Position.Z==Start.Z-1?0:1)}",p.Definition.ContributionId));
        var built=Apply(w,plan);Assert.Equal(11,built.Voxels.Length);
        var remove=BridgeRailwayConstruction.Preview(built,Start,to,true);Assert.True(remove.Legal);Assert.Equal(30_000_000,remove.Booking.Total);
        Assert.Equal(6,remove.RemovedPiers.Length);Assert.Empty(Apply(built,remove).Voxels);
    }

    [Fact]
    public void Snapping_bounds_short_spans_blocked_supports_and_existing_run_continuity()
    {
        Assert.Equal(new Location(18,10,2),BridgeRailwayConstruction.Snap(Start,new(18,14,7)));
        Assert.Equal(new Location(14,16,2),BridgeRailwayConstruction.Snap(Start,new(18,16,0)));
        var w=Empty();var to=Start with{X=Start.X+1};var shortPlan=BridgeRailwayConstruction.Preview(w,Start,to);
        Assert.Equal(new[]{2,0},shortPlan.Topology.Cells.Select(c=>c.After!.Variant));
        Assert.Equal("samePoint",BridgeRailwayConstruction.Preview(w,Start,Start).Reason);
        Assert.Equal("routeWorkLimit",BridgeRailwayConstruction.Preview(w,Start,new(int.MaxValue,int.MinValue,2)).Reason);
        Assert.Equal("outside",BridgeRailwayConstruction.Preview(w,new(-100,-100,2),Start).Reason);
        w=Add(w,Start with{Z=0},Rail()); // Full-floor occupancy suppresses pier election, without rejecting the rail.
        var p=BridgeRailwayConstruction.Preview(w,Start,to);Assert.True(p.Legal);Assert.Empty(p.ElectedPiers);
        Assert.Equal(2,p.Topology.Cells.Count(c=>c.Changed));
        w=Add(w,Start with{X=Start.X+1},Rail());
        p=BridgeRailwayConstruction.Preview(w,Start,Start with{X=Start.X+4});Assert.True(p.Legal);
        Assert.Equal(new[]{2,0,2,1,0},p.Topology.Cells.Select(c=>c.After!.Variant));
        Assert.Equal(new[]{17,17},p.ElectedPiers.Select(v=>v.Position.X)); // Alternation spans existing rails.
        var wrong=Add(Empty(),Start,Rail(10));Assert.False(BridgeRailwayConstruction.Preview(wrong,Start,to).Legal);
    }

    [Fact]
    public void Removal_tears_down_foreign_piers_only_under_matching_unoccupied_rails_and_rejects_stale_apply()
    {
        var w=Empty();var to=Start with{X=Start.X+2};w=Apply(w,BridgeRailwayConstruction.Preview(w,Start,to));
        var normal=Start with{X=Start.X+3};w=Add(w,normal,Rail());
        var higher=normal with{Z=4};w=Add(w,higher,Rail());
        var foreignOwner=w.At(higher)!.OwnerId;w=Add(w,normal with{Z=0},kind:RailwayVoxelKind.Pier,supported:foreignOwner);
        var foreignRail=Start with{X=Start.X+1,Z=4};w=Add(w,foreignRail,Rail());
        var foreignId=w.At(foreignRail)!.OwnerId;
        w=Add(w,foreignRail with{Z=0},kind:RailwayVoxelKind.Pier,supported:foreignId);
        var plan=BridgeRailwayConstruction.Preview(w,Start,normal,true);Assert.True(plan.Legal);
        Assert.Equal(5,plan.RemovedPiers.Length);Assert.Contains(plan.RemovedPiers,p=>p.SupportedOwnerId==foreignId);var after=Apply(w,plan);
        Assert.NotNull(after.At(normal with{Z=0}));Assert.NotNull(after.At(normal));
        Assert.Null(after.At(foreignRail with{Z=0}));Assert.NotNull(after.At(foreignRail));
        Assert.Throws<InvalidOperationException>(()=>BridgeRailwayConstruction.Apply(new(after,new ConstructionAccounts()),plan,new(0)));
    }
    [Theory]
    [InlineData(4,true)][InlineData(10,false)][InlineData(14,true)][InlineData(15,true)][InlineData(0,false)]
    public void Existing_ports_not_subtype_or_endpoint_status_determine_compatibility(int pattern,bool legal)
    {
        var w=Empty();var binding=Profile.Bindings.Single(b=>b.Variant==pattern&&b.Handler==(pattern<12?RailwayHandler.Normal:RailwayHandler.Junction));
        var component=new RailComponent(binding.Reference,pattern);
        var middle=Start with{X=Start.X+1};w=Add(w,middle,component);
        var plan=BridgeRailwayConstruction.Preview(w,Start,Start with{X=Start.X+2});Assert.Equal(legal,plan.Legal);
        if (legal)Assert.Equal(component,Apply(w,plan).At(middle)!.Traffic!.Rail);
    }

}
