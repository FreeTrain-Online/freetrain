using System.Collections.Immutable;
using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Simulation;
using Xunit;

namespace FreeTrain.Online.Tests;

public class GirderConstructionTests
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
    static MixedWorld Apply(MixedWorld w,RailwayConstructionPlan p) => GirderRailwayConstruction.Apply(new(w,new ConstructionAccounts()),p,new(0)).World;

    [Fact]
    public void Independent_literal_girder_vectors_match_geometry_art_booking_and_protection()
    {
        using var f=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Spatial/railway-fixtures.json")));
        var rows=f.RootElement.GetProperty("groups").EnumerateObject().SelectMany(g=>g.Value.GetProperty("cases").EnumerateArray()).Where(c=>c.GetProperty("id").GetString()!.StartsWith("GIRDER-"));
        var count=0;
        foreach (var row in rows)
        {
            var input=row.GetProperty("input");var expected=row.GetProperty("expected");
            var d=input.GetProperty("direction").GetInt32();
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
                var rail=kind=="girder"?new RailComponent(Profile.Bindings.Single(b=>b.Handler==RailwayHandler.Girder).Reference,pattern):Rail(pattern);
                w=Add(w,P(n),rail,cell.TryGetProperty("car",out var c)&&c.GetBoolean());
            }
            var p=GirderRailwayConstruction.Preview(w,start,P(data.Length-1),input.GetProperty("action").GetString()=="remove");
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
        Assert.Equal(13,count);
    }

    [Theory]
    [InlineData(0,10,3)][InlineData(1,1,1)][InlineData(2,4,0)][InlineData(3,7,2)]
    [InlineData(4,10,3)][InlineData(5,1,1)][InlineData(6,4,0)][InlineData(7,7,2)]
    public void All_directions_have_default_piers_every_new_cell_and_exact_removal(int direction,int pattern,int sprite)
    {
        var w=Empty();var (dx,dy)=new Heading(direction).Offset;var to=new Location(Start.X+4*dx,Start.Y+4*dy,2);
        var p=GirderRailwayConstruction.Preview(w,Start,to);Assert.True(p.Legal);
        Assert.All(p.Topology.Cells,c=>{Assert.Equal(pattern,c.After!.Pattern);Assert.Equal(sprite,c.After.Variant);});
        Assert.Equal(90_000_000,p.Booking.Total);Assert.Equal(10,p.ElectedPiers.Length);
        Assert.All(p.ElectedPiers,v=>{Assert.Equal("system:pier",v.Definition.ContributionId);Assert.Equal(0,v.Sprite);});
        w=Apply(w,p);var remove=GirderRailwayConstruction.Preview(w,Start,to,true);
        Assert.Equal(30_000_000,remove.Booking.Total);Assert.Equal(10,remove.RemovedPiers.Length);Assert.Empty(Apply(w,remove).Voxels);
        Assert.Throws<InvalidOperationException>(()=>GirderRailwayConstruction.Apply(new(w,new ConstructionAccounts()),p,new(0)));
    }
    [Fact]
    public void Source_integer_snapping_thresholds_bounds_and_coincident_guards()
    {
        var a=new Location(0,0,2);
        Assert.Equal(new Location(2,2,2),GirderRailwayConstruction.Snap(a,new(2,5,7)));
        Assert.Equal(new Location(0,6,2),GirderRailwayConstruction.Snap(a,new(2,6,7)));
        Assert.Equal(new Location(5,5,2),GirderRailwayConstruction.Snap(a,new(5,2,7)));
        Assert.Equal(new Location(6,0,2),GirderRailwayConstruction.Snap(a,new(6,2,7)));
        Assert.Equal(a,GirderRailwayConstruction.Snap(a,new(0,1,7)));
        Assert.Equal("samePoint",GirderRailwayConstruction.Preview(Empty(),Start,Start).Reason);
        Assert.Equal("routeWorkLimit",GirderRailwayConstruction.Preview(Empty(),a,new(int.MaxValue,0,2)).Reason);
        Assert.Equal("outside",GirderRailwayConstruction.Preview(Empty(),new(-100,0,2),new(-98,0,2)).Reason);
        Assert.Equal("constructionOverflow",GirderRailwayConstruction.Preview(Empty(),new(int.MinValue,0,2),new(int.MaxValue,int.MaxValue,2)).Reason);
    }
    [Theory]
    [InlineData(4,true)][InlineData(10,false)][InlineData(14,true)][InlineData(15,true)][InlineData(0,false)][InlineData(17,false)]
    public void Existing_compatible_straights_and_junctions_survive_but_curves_and_wrong_ports_reject(int pattern,bool legal)
    {
        var binding=Profile.Bindings.Single(b=>b.Variant==pattern&&b.Handler==(pattern<12?RailwayHandler.Normal:RailwayHandler.Junction));
        var component=new RailComponent(binding.Reference,pattern);var w=Add(Empty(),Start,component);
        var p=GirderRailwayConstruction.Preview(w,Start,Start with{X=Start.X+2});Assert.Equal(legal,p.Legal);
        if(legal) {Assert.Equal(component,Apply(w,p).At(Start)!.Traffic!.Rail);Assert.Equal(4,p.ElectedPiers.Length);}
    }
    [Fact]
    public void Every_ground_cell_is_checked_including_existing_rails_and_blocked_supports_do_not_reject()
    {
        var w=Empty();var end=Start with{X=Start.X+2};var cells=w.Terrain.Cells.ToBuilder();var storage=w.Terrain.Space.ToStorage(end);
        cells[storage.V*40+storage.H]=new(2,TerrainSurface.Cut);
        var terrain=new TerrainWorld(Profile.Terrain,"Ground",new(40,32,8),0,cells,new(1,[]),[]);
        Assert.Equal("girderAboveGround",GirderRailwayConstruction.Preview(Empty(terrain),Start,end).Reason);
        Assert.Equal("girderAboveGround",GirderRailwayConstruction.Preview(Add(Empty(terrain),end,Rail()),Start,end).Reason);
        w=Add(w,Start with{Z=0},Rail());var p=GirderRailwayConstruction.Preview(w,Start,end);
        Assert.True(p.Legal);Assert.Equal(4,p.ElectedPiers.Length);Assert.DoesNotContain(p.ElectedPiers,v=>v.Position.X==Start.X);
        Assert.Equal("carOccupied",GirderRailwayConstruction.Preview(Add(Empty(),Start,Rail(),car:true),Start,end).Reason);
    }
    [Fact]
    public void Only_removed_matching_rails_tear_down_foreign_piers_and_platforms_reject_atomically()
    {
        var to=Start with{X=Start.X+2};var w=Empty();w=Apply(w,GirderRailwayConstruction.Preview(w,Start,to));
        var high=Start with{Z=4,X=Start.X+1};w=Add(w,high,Rail());var id=w.At(high)!.OwnerId;
        // Replace an elected pier with a pier belonging to the higher foreign rail.
        var old=w.At(high with{Z=0})!;
        w=new(w.Profile,w.Terrain,w.Spatial.Capture(),w.Voxels.Select(v=>v==old?v with{SupportedOwnerId=id}:v),w.Slopes,w.Revision);
        var p=GirderRailwayConstruction.Preview(w,Start,to,true);Assert.Contains(p.RemovedPiers,v=>v.SupportedOwnerId==id);
        var after=Apply(w,p);Assert.NotNull(after.At(high));Assert.Null(after.At(high with{Z=0}));
        var platformDef=Profile.TrafficOwner with {ContributionId="test:platform"};
        var empty=Empty();empty=new(new RailwayProfile(Profile.Terrain,Profile.Bindings.Add(new(platformDef,RailwayHandler.Platform))),empty.Terrain,empty.Spatial.Capture(),[],[]);
        var platform=Add(empty,Start,new RailComponent(Profile.Bindings.Single(b=>b.Handler==RailwayHandler.Girder).Reference,4),platform:platformDef);
        Assert.Equal("platformDependency",GirderRailwayConstruction.Preview(platform,Start,to,true).Reason);
    }
}
