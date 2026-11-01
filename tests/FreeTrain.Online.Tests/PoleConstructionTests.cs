using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;
using Xunit;
namespace FreeTrain.Online.Tests;
public class PoleConstructionTests
{
    static Location P=>GarageConstructionTests.Start;
    static readonly string[] Types=[PoleRailwayConstruction.Double1,PoleRailwayConstruction.Double2,PoleRailwayConstruction.Single1,PoleRailwayConstruction.Single2];
    static MixedWorld World()=>GarageConstructionTests.World();
    static MixedWorld Add(MixedWorld w,int pattern=4,RailwayHandler handler=RailwayHandler.Normal,bool components=false)
    {
        var bindings=w.Profile.Bindings;
        var car=new DefinitionReference(w.Profile.Terrain.Packs[0].Id,"test:car","test:car");
        var road=car with{ContributionId="test:road",RecipeId="test:road"};
        if(components)bindings=bindings.Add(new(car,RailwayHandler.Car)).Add(new(road,RailwayHandler.Road));
        var profile=new RailwayProfile(w.Profile.Terrain,bindings);
        var rail=bindings.First(b=>b.Handler==handler&&(handler is not (RailwayHandler.Normal or RailwayHandler.Junction)||b.Variant==pattern));
        var id=w.Spatial.NextEntityId;
        return new(profile,w.Terrain,new(id+1,w.Spatial.Entities.Add(new(id,0,profile.TrafficOwner,P,new([new(new(0,0,0),true)]),true,false,0))),
            [new(P,id,RailwayVoxelKind.Traffic,new(new(rail.Reference,pattern,Direction:handler==RailwayHandler.Signal&&pattern==4?2:0,TerrainBackup:handler==RailwayHandler.Tunnel?new QuarterCorners(0,0,0,0):null),Road:components?new(road,17):null,Car:components?new(car,1,1):null))],[],w.Revision);
    }
    static MixedConstructionState Apply(MixedWorld w,RailwayConstructionPlan p)=>PoleRailwayConstruction.Apply(new(w),p,new(0));
    [Fact]
    public void Every_type_axis_and_special_subtype_preserves_track_and_owner_without_booking()
    {
        foreach(var handler in new[]{RailwayHandler.Normal,RailwayHandler.Bridge,RailwayHandler.Girder,RailwayHandler.Tunnel,RailwayHandler.Garage,RailwayHandler.Signal})
        foreach(var pattern in new[]{4,10})foreach(var type in Types){
            var w=Add(World(),pattern,handler);var before=w.At(P)!;
            var plan=PoleRailwayConstruction.Preview(w,P,type);Assert.True(plan.Legal,plan.Reason);Assert.Single(plan.Topology.Cells);Assert.Empty(plan.Booking.Charges);
            var built=Apply(w,plan);Assert.Same(w.Terrain,built.World.Terrain);Assert.Equal(w.Spatial.NextEntityId,built.World.Spatial.NextEntityId);
            Assert.Equal(before.OwnerId,built.World.At(P)!.OwnerId);Assert.Equal(before.Traffic!.Rail,built.World.At(P)!.Traffic!.Rail);
            Assert.Equal(type,built.World.At(P)!.Traffic!.Accessory!.Definition.ContributionId);Assert.Equal(pattern==10?1:0,built.World.At(P)!.Traffic!.Accessory!.Direction);
            var remove=PoleRailwayConstruction.Preview(built.World,P,Types.First(t=>t!=type),true);Assert.True(remove.Legal);Assert.Single(remove.ComponentLosses);
            var removed=Apply(built.World,remove);Assert.Equal(before,removed.World.At(P));Assert.Empty(removed.Accounts.Transactions);
        }
    }
    [Fact]
    public void Independent_literal_eligibility_fixtures_cover_all_patterns()
    {
        using var f=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Spatial/railway-fixtures.json")));
        var count=0;
        foreach(var row in f.RootElement.GetProperty("groups").EnumerateObject().SelectMany(g=>g.Value.GetProperty("cases").EnumerateArray()).Where(c=>c.GetProperty("input").TryGetProperty("kind",out var k)&&k.GetString()=="pole")){
            var edges=row.GetProperty("input").GetProperty("edges").EnumerateArray().Select(e=>e.GetInt32()).Order().ToArray();
            var pattern=Enumerable.Range(0,28).FirstOrDefault(p=>RailwayTopology.Ports(p).Order().SequenceEqual(edges),-1);if(pattern<0)continue;
            var w=Add(World(),pattern,pattern<12?RailwayHandler.Normal:RailwayHandler.Junction);
            var quote=PoleRailwayConstruction.Preview(w,P,Types[0]);Assert.Equal(row.GetProperty("expected").GetProperty("allowed").GetBoolean(),quote.Legal);count++;
        }
        Assert.True(count>0);
        foreach(var p in Enumerable.Range(0,28))Assert.Equal(p is 4 or 10,PoleRailwayConstruction.Preview(Add(World(),p,p<12?RailwayHandler.Normal:RailwayHandler.Junction),P,Types[0]).Legal);
    }
    [Fact]
    public void Replacement_with_car_and_road_keeps_components_and_preview_lists_lost_accessory()
    {
        var w=Add(World(),components:true);
        var crossing=new RailwayBinding(new(w.Profile.Terrain.Packs[0].Id,"test:crossing","test:crossing"),RailwayHandler.Crossing);
        w=new(new(w.Profile.Terrain,w.Profile.Bindings.Add(crossing)),w.Terrain,w.Spatial.Capture(),w.Voxels,w.Slopes,w.Revision);
        var original=w.At(P)!.Traffic!;
        foreach(var type in Types){
            var plan=PoleRailwayConstruction.Preview(w,P,type);Assert.True(plan.Legal);Assert.Equal(w.At(P)!.Traffic!.Accessory==null?0:1,plan.ComponentLosses.Length);
            w=Apply(w,plan).World;Assert.Equal(original.Rail,w.At(P)!.Traffic!.Rail);Assert.Equal(original.Car,w.At(P)!.Traffic!.Car);Assert.Equal(original.Road,w.At(P)!.Traffic!.Road);
        }
        w=Apply(w,PoleRailwayConstruction.Preview(w,P,Types[0],true)).World;Assert.Equal(original with{Accessory=new(crossing.Reference)},w.At(P)!.Traffic);
    }
    [Fact]
    public void Crossing_replacement_and_orphan_cleanup_follow_accessory_setter()
    {
        var w=Add(World(),components:true);var voxel=w.At(P)!;
        var crossing=new RailwayBinding(new(w.Profile.Terrain.Packs[0].Id,"test:crossing","test:crossing"),RailwayHandler.Crossing);
        w=new(new(w.Profile.Terrain,w.Profile.Bindings.Add(crossing)),w.Terrain,w.Spatial.Capture(),w.Voxels,w.Slopes,w.Revision);
        w=new(w.Profile,w.Terrain,w.Spatial.Capture(),[voxel with{Traffic=voxel.Traffic! with{Accessory=new(crossing.Reference)}}],[],w.Revision);
        var quote=PoleRailwayConstruction.Preview(w,P,Types[0]);Assert.True(quote.Legal);Assert.Equal(crossing.Reference,Assert.Single(quote.ComponentLosses).Accessory.Definition);
        w=Apply(w,quote).World;voxel=w.At(P)!;
        w=new(w.Profile,w.Terrain,w.Spatial.Capture(),[voxel with{Traffic=new(Accessory:voxel.Traffic!.Accessory)}],[],w.Revision);
        w=Apply(w,PoleRailwayConstruction.Preview(w,P,Types[1],true)).World;Assert.Null(w.At(P));Assert.Empty(w.Spatial.Entities.Where(e=>e.Definition==w.Profile.TrafficOwner));
    }
    [Fact]
    public void Track_detachment_keeps_the_pole_until_explicit_removal_and_slopes_reject()
    {
        var w=Add(World());w=Apply(w,PoleRailwayConstruction.Preview(w,P,Types[0])).World;
        var owner=w.At(P)!.OwnerId;var removal=NormalRailwayConstruction.PreviewRemoval(w,P,P with{X=P.X+2});Assert.True(removal.Legal,removal.Reason);
        w=NormalRailwayConstruction.Apply(new(w),removal,new(0)).World;Assert.Null(w.At(P)!.Traffic!.Rail);Assert.NotNull(w.At(P)!.Traffic!.Accessory);
        Assert.Equal(owner,w.At(P)!.OwnerId);w=Apply(w,PoleRailwayConstruction.Preview(w,P,Types[3],true)).World;Assert.Null(w.At(P));
        Assert.False(PoleRailwayConstruction.Preview(Add(World(),1,RailwayHandler.Girder),P,Types[0]).Legal);
        var slope=SlopeRailwayConstruction.PreviewBuild(World(),P,new(2));Assert.True(slope.Legal,slope.Reason);
        w=SlopeRailwayConstruction.Apply(new(slope.Source),slope,new(0)).World;
        foreach(var v in w.Voxels)Assert.False(PoleRailwayConstruction.Preview(w,v.Position,Types[0]).Legal);
    }
    [Fact]
    public void Rejection_stale_plan_zero_journal_and_exact_save_continuation()
    {
        Assert.Equal("poleTarget",PoleRailwayConstruction.Preview(World(),P,Types[0]).Reason);
        Assert.Equal("poleNotFound",PoleRailwayConstruction.Preview(World(),P,Types[0],true).Reason);
        Assert.Equal("poleTarget",PoleRailwayConstruction.Preview(TunnelConstructionTests.World(),P,Types[0]).Reason);
        Assert.Equal("outside",PoleRailwayConstruction.Preview(World(),new(-100,0,0),Types[0]).Reason);
        Assert.Equal("invalidInput",PoleRailwayConstruction.Preview(World(),P,"bad").Reason);
        var w=Add(World());var quote=PoleRailwayConstruction.Preview(w,P,Types[0]);var built=Apply(w,quote);
        var voxel=built.World.At(P)!;Assert.Throws<ArgumentException>(()=>new MixedWorld(built.World.Profile,built.World.Terrain,built.World.Spatial.Capture(),[voxel with{Traffic=voxel.Traffic! with{Accessory=voxel.Traffic!.Accessory! with{Direction=2}}}],[],built.World.Revision));
        Assert.Throws<InvalidOperationException>(()=>PoleRailwayConstruction.Apply(built,quote,new(0)));
        Assert.Throws<InvalidOperationException>(()=>Apply(w,SignalRailwayConstruction.Preview(w,P,SignalRailwayConstruction.ThreeAspect,0)));
        var state=new MixedSessionState(built,TerrainProvenance.From(new EmptyWorldRequest(WorldCreationProfile.Id,w.Terrain.Name,new(40,32,8),0)),revision:built.World.Revision);
        var bytes=SaveCodec.Write(state);var restored=SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;Assert.Equal(bytes,SaveCodec.Write(restored));
        Assert.Equal(SaveCodec.Write(state.Successor(state.Revision,Apply(state.World,PoleRailwayConstruction.Preview(state.World,P,Types[3])),false)),SaveCodec.Write(restored.Successor(restored.Revision,Apply(restored.World,PoleRailwayConstruction.Preview(restored.World,P,Types[3])),false)));
        w=new(w.Profile,w.Terrain,w.Spatial.Capture(),w.Voxels,w.Slopes,1);quote=PoleRailwayConstruction.Preview(w,P,Types[0]);
        var accounts=new ConstructionAccounts().Book(new ConstructionBooking(Enumerable.Repeat(new AccountCharge(AccountGenre.RailService,0),8192)),1,new(0),"fixture");
        Assert.Equal(8192,PoleRailwayConstruction.Apply(new(w,accounts),quote,new(0)).Accounts.Transactions.Length);
    }
}
